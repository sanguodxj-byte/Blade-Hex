// DebugConsole.cs
// 全局调试控制台 (Autoload) — 跨场景 overlay
//
// 功能:
//   • 由外部注册 section_provider(name, Callable) 提供分区数据，避免耦合到特定场景
//   • 节流自动刷新 (默认 0.5s) + 可关闭 + F5 手动刷新
//   • 命令输入 (LineEdit)：内置 help / clear / copy / close / refresh，场景可注册业务命令
//   • 日志环形缓冲 (log_info/log_warn/log_err) + 级别着色
//   • 一键复制全部内容到剪贴板
//   • 仅在 debug 构建或 "dev" feature 激活；Release 下完全惰性，不创建 UI
//   • 默认按 ` (反引号) 切换；F1 作为兼容热键
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Diagnostics;

namespace BladeHex.Debug;

/// <summary>
/// [Autoload Singleton] 全局调试控制台。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>DebugConsole</c>。</para>
/// <para>生命周期：应用全局；Release 构建中惰性，不创建 UI。</para>
/// <para>访问方式：建议通过 <see cref="BladeHex.Data.Globals.DebugConsole"/>（容错版本，可能为 null）。</para>
/// <para>职责：分区数据显示、命令输入、日志环形缓冲、剪贴板复制。</para>
/// </summary>
[GlobalClass]
public partial class DebugConsole : Node
{
    // ========================================================================
    // Singleton
    // ========================================================================

    public static DebugConsole? Instance { get; private set; }

    // ========================================================================
    // 常量 / 配置
    // ========================================================================

    private const Key HotkeyTogglePrimary = Key.Quoteleft;
    private const Key HotkeyToggleSecondary = Key.F1;
    private const Key HotkeyRefresh = Key.F5;
    private const Key HotkeyFocusCmd = Key.Slash;

    private const int LogBufferMax = 200;
    private const float DefaultRefreshInterval = 0.5f;

    private const int PanelLeft = 8;
    private const int PanelTop = 60;
    private const int PanelRight = 620;
    private const int PanelBottom = 780;

    // ========================================================================
    // 内部类型
    // ========================================================================

    private sealed class SectionEntry
    {
        public GodotObject? Owner;
        public Func<Variant> Provider = null!;
    }

    private sealed class CommandEntry
    {
        public GodotObject? Owner;
        public Func<string[], string?> Handler = null!;
        public string Help = "";
    }

    // ========================================================================
    // 运行时状态
    // ========================================================================

    private bool _enabled;
    private bool _visible = false; // 默认关闭，按 ` 打开
    private bool _autoRefresh = true;
    private float _refreshInterval = DefaultRefreshInterval;
    private float _refreshAccum;

    private readonly Dictionary<string, SectionEntry> _sectionProviders = new();
    private readonly List<string> _sectionOrder = new();

    private readonly Dictionary<string, CommandEntry> _commands = new();

    /// <summary>日志环形缓冲</summary>
    private readonly List<(string Level, string Text)> _logBuffer = new();

    // ========================================================================
    // UI 节点
    // ========================================================================

    private CanvasLayer? _canvasLayer;
    private PanelContainer? _panel;
    private RichTextLabel? _contentLabel; // 合并的内容区域（sections + log）
    private ScrollContainer? _contentScroll;
    private LineEdit? _cmdInput;
    private CheckBox? _autoCheckbox;

    // ========================================================================
    // 生命周期
    // ========================================================================

    public override void _Ready()
    {
        Instance = this;
        DiagnosticLog.Event("Game", "startup", new Dictionary<string, object?>
        {
            ["log"] = DiagnosticLog.CurrentLogPath,
        });
        GD.Print($"[DiagnosticLog] Writing to: {DiagnosticLog.CurrentLogPath}");

        _enabled = OS.IsDebugBuild() || OS.HasFeature("dev") || OS.HasFeature("debug_console");
        if (!_enabled)
        {
            SetProcess(false);
            SetProcessUnhandledInput(false);
            return;
        }

        ProcessMode = ProcessModeEnum.Always;

        BuildUi();
        RegisterBuiltinCommands();
        BladeHex.Debug.LuaDebugCommands.Register(this);
        LogInfo("[Debug] 控制台已就绪。按 ` 切换，F5 刷新，/ 聚焦命令。输入 help 查看命令。");
    }

    public override void _Process(double delta)
    {
        if (!_enabled || !_visible || !_autoRefresh) return;

        _refreshAccum += (float)delta;
        if (_refreshAccum >= _refreshInterval)
        {
            _refreshAccum = 0.0f;
            Refresh();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_enabled || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        Key code = keyEvent.Keycode;

        if (code == HotkeyTogglePrimary || code == HotkeyToggleSecondary)
        {
            ToggleVisible();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!_visible) return;

        if (code == HotkeyRefresh)
        {
            Refresh();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (code == HotkeyFocusCmd && _cmdInput != null)
        {
            _cmdInput.GrabFocus();
            _cmdInput.Clear();
            GetViewport().SetInputAsHandled();
        }
    }

    // ========================================================================
    // 对外 API — 分区
    // ========================================================================

    /// <summary>注册一个分区内容提供器。name 是唯一键；重复注册会覆盖。</summary>
    public void RegisterSection(string name, Func<Variant> provider, GodotObject? owner = null)
    {
        if (!_enabled) return;
        if (!_sectionProviders.ContainsKey(name))
            _sectionOrder.Add(name);
        _sectionProviders[name] = new SectionEntry { Owner = owner, Provider = provider };
    }

    /// <summary>注册一个分区（Callable 版本，兼容 调用）</summary>
    public void RegisterSection(string name, Callable callable)
    {
        if (!_enabled) return;
        if (!_sectionProviders.ContainsKey(name))
            _sectionOrder.Add(name);
        _sectionProviders[name] = new SectionEntry { Owner = null, Provider = () => callable.Call() };
    }

    public void UnregisterSection(string name)
    {
        if (!_enabled) return;
        _sectionProviders.Remove(name);
        _sectionOrder.Remove(name);
    }

    /// <summary>批量移除某个对象持有的所有分区</summary>
    public void UnregisterSectionsOf(GodotObject? owner)
    {
        if (!_enabled || owner == null) return;

        var toRemove = _sectionProviders
            .Where(kvp => kvp.Value.Owner == owner)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string k in toRemove)
            UnregisterSection(k);
    }

    // ========================================================================
    // 对外 API — 命令
    // ========================================================================

    /// <summary>注册命令。handler 签名: func(args: string[]) -> string? (返回值写入日志)</summary>
    public void RegisterCommand(string name, Func<string[], string?> handler, string help = "", GodotObject? owner = null)
    {
        if (!_enabled) return;
        _commands[name] = new CommandEntry { Owner = owner, Handler = handler, Help = help };
    }

    public void UnregisterCommand(string name)
    {
        if (!_enabled) return;
        _commands.Remove(name);
    }

    /// <summary>批量移除某个对象持有的所有命令</summary>
    public void UnregisterCommandsOf(GodotObject? owner)
    {
        if (!_enabled || owner == null) return;

        var toRemove = _commands
            .Where(kvp => kvp.Value.Owner == owner)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string k in toRemove)
            UnregisterCommand(k);
    }

    // ========================================================================
    // 对外 API — 日志
    // ========================================================================

    public void LogInfo(string msg) => AppendLog("info", msg);

    public void LogWarn(string msg)
    {
        AppendLog("warn", msg);
        GD.PushWarning(msg);
    }

    public void LogErr(string msg)
    {
        AppendLog("err", msg);
        GD.PushError(msg);
    }

    // ========================================================================
    // 对外 API — 控制
    // ========================================================================

    public void Refresh()
    {
        if (!_enabled) return;
        RefreshSections();
    }

    public void ToggleVisible()
    {
        if (!_enabled) return;
        SetPanelVisible(!_visible);
    }

    public void SetPanelVisible(bool v)
    {
        if (!_enabled || _panel == null) return;
        _visible = v;
        _panel.Visible = v;
        if (v) Refresh();
    }

    public bool IsPanelVisible() => _enabled && _visible;

    public void SetAutoRefresh(bool on)
    {
        _autoRefresh = on;
        if (_autoCheckbox != null && _autoCheckbox.ButtonPressed != on)
            _autoCheckbox.ButtonPressed = on;
    }

    public void SetRefreshInterval(float sec)
    {
        _refreshInterval = Mathf.Max(0.1f, sec);
    }

    public void CopyAllToClipboard()
    {
        if (!_enabled || _contentLabel == null) return;

        string content = _contentLabel.GetParsedText();
        DisplayServer.ClipboardSet(content);
        LogInfo("[Debug] 已复制到剪贴板");
    }

    public void ClearLog()
    {
        _logBuffer.Clear();
        _refreshAccum = _refreshInterval; // 触发刷新
    }

    // ========================================================================
    // 内部 — UI 构建
    // ========================================================================

    private void BuildUi()
    {
        _canvasLayer = new CanvasLayer { Name = "DebugConsoleLayer", Layer = 1000 };
        AddChild(_canvasLayer);

        _panel = new PanelContainer { Name = "DebugConsolePanel" };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _panel.OffsetLeft = PanelLeft;
        _panel.OffsetTop = PanelTop;
        _panel.OffsetRight = PanelRight;
        _panel.OffsetBottom = PanelBottom;
        _panel.ZIndex = 4096;
        _panel.Visible = _visible; // 默认隐藏

        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.88f),
            BorderColor = new Color(1.0f, 0.85f, 0.2f, 0.9f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        bg.SetBorderWidthAll(1);
        _panel.AddThemeStyleboxOverride("panel", bg);
        _canvasLayer.AddChild(_panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 2);
        _panel.AddChild(root);

        // 紧凑工具栏：标题 + 按钮一行
        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 4);
        root.AddChild(toolbar);

        var title = new Label { Text = "Debug ` F5" };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.3f));
        toolbar.AddChild(title);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        toolbar.AddChild(spacer);

        _autoCheckbox = new CheckBox { Text = "Auto", ButtonPressed = _autoRefresh };
        _autoCheckbox.AddThemeFontSizeOverride("font_size", 11);
        _autoCheckbox.Toggled += (pressed) => _autoRefresh = pressed;
        toolbar.AddChild(_autoCheckbox);

        toolbar.AddChild(MakeButton("↻", Refresh));
        toolbar.AddChild(MakeButton("Copy", CopyAllToClipboard));
        toolbar.AddChild(MakeButton("×", () => SetPanelVisible(false)));

        // 合并内容区域（sections + log 统一显示）
        _contentScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(_contentScroll);

        _contentLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _contentLabel.AddThemeFontSizeOverride("normal_font_size", 12);
        _contentLabel.AddThemeColorOverride("default_color", new Color(0.85f, 1.0f, 0.85f));
        _contentScroll.AddChild(_contentLabel);

        // 命令输入行
        var cmdRow = new HBoxContainer();
        cmdRow.AddThemeConstantOverride("separation", 2);
        root.AddChild(cmdRow);

        var prompt = new Label { Text = ">" };
        prompt.AddThemeFontSizeOverride("font_size", 12);
        prompt.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.3f));
        cmdRow.AddChild(prompt);

        _cmdInput = new LineEdit
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            PlaceholderText = "命令 (help)"
        };
        _cmdInput.AddThemeFontSizeOverride("font_size", 12);
        _cmdInput.TextSubmitted += OnCmdSubmitted;
        cmdRow.AddChild(_cmdInput);

        // 快捷折叠控制按钮
        var toggleBtn = new Button
        {
            Text = "[-] 快捷秘籍面板 (点击折叠)",
            Flat = true,
            Alignment = HorizontalAlignment.Left
        };
        toggleBtn.AddThemeFontSizeOverride("font_size", 11);
        toggleBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.2f));
        root.AddChild(toggleBtn);

        // 快捷按钮面板（分页分类）
        var quickPanel = new ScrollContainer();
        quickPanel.CustomMinimumSize = new Vector2(0, 132);
        quickPanel.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        quickPanel.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        root.AddChild(quickPanel);

        toggleBtn.Pressed += () =>
        {
            quickPanel.Visible = !quickPanel.Visible;
            toggleBtn.Text = quickPanel.Visible ? "[-] 快捷秘籍面板 (点击折叠)" : "[+] 快捷秘籍面板 (点击展开)";
        };

        var quickInner = new VBoxContainer();
        quickInner.AddThemeConstantOverride("separation", 2);
        quickPanel.AddChild(quickInner);

        // ---- 标签栏 ----
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", 2);
        quickInner.AddChild(tabBar);

        // ---- 分类数据 ----
        var categories = new (string TabName, (string Label, string Cmd)[] Items)[]
        {
            ("🌤 天气", new[]
            {
                ("晴天", "weather clear"),
                ("轻雨", "weather rain light"),
                ("暴雨", "weather rain heavy"),
                ("轻雪", "weather snow light"),
                ("暴雪", "weather snow heavy"),
                ("轻度沙暴", "weather sand light"),
                ("强沙尘暴", "weather sand heavy"),
            }),
            ("⏰ 时间", new[]
            {
                ("+1天", "day 1"),
                ("+7天", "day 7"),
                ("+30天", "day 30"),
                ("6时(晨)", "time 6"),
                ("12时(午)", "time 12"),
                ("18时(昏)", "time 18"),
                ("x10速", "speed 10"),
                ("x1速", "speed 1"),
            }),
            ("💰 资源", new[]
            {
                ("金币满", "gold 99999"),
                ("食物满", "food 100"),
                ("全队治愈", "heal"),
                ("升级Lv10", "levelup 10"),
                ("升级Lv30", "levelup 30"),
            }),
            ("🗺 地图", new[]
            {
                ("开全图", "reveal_all"),
                ("开/关迷雾", "toggle_fog"),
                ("清空敌人", "kill_all"),
            }),
            ("👾 生成", new[]
            {
                ("生成冒险者", "spawn adventurer"),
                ("生成商队", "spawn caravan"),
                ("生成山贼", "spawn bandit"),
                ("生成劫匪", "spawn robber"),
                ("生成海寇", "spawn pirate"),
                ("生成掠夺者", "spawn raiding"),
                ("生成领主军", "spawn lord"),
                ("生成巨龙", "spawn dragon"),
                ("生成魔像", "spawn golem"),
            }),
            ("⚔ 外交", new[]
            {
                ("外交-宣战A", "war_declare nation_0 nation_1"),
                ("外交-议和A", "war_end nation_0 nation_1"),
                ("帝国宣战", "war_declare kingdom empire"),
                ("帝国停战", "war_end kingdom empire"),
                ("主势力影+50", "influence kingdom 50"),
                ("势力0影+100", "influence nation_0 100"),
                ("势力1影+100", "influence nation_1 100"),
                ("领主状态", "lord_status"),
            }),
        };

        var grids = new List<GridContainer>();
        var tabBtns = new List<Button>();

        for (int ci = 0; ci < categories.Length; ci++)
        {
            int capturedIdx = ci; // capture for closure
            var (tabName, items) = categories[ci];

            // 标签按钮
            var tabBtn = new Button
            {
                Text = tabName,
                Flat = true,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            tabBtn.AddThemeFontSizeOverride("font_size", 10);
            tabBtn.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            tabBar.AddChild(tabBtn);
            tabBtns.Add(tabBtn);

            // 分类 Grid
            var grid = new GridContainer();
            grid.Columns = 5;
            grid.AddThemeConstantOverride("h_separation", 3);
            grid.AddThemeConstantOverride("v_separation", 3);
            grid.Visible = ci == 0; // 默认显示第一个
            quickInner.AddChild(grid);
            grids.Add(grid);

            foreach (var (label, cmd) in items)
                AddQuickBtn(grid, label, cmd);

            // 标签点击 → 切换
            tabBtn.Pressed += () =>
            {
                for (int i = 0; i < grids.Count; i++)
                {
                    bool active = i == capturedIdx;
                    grids[i].Visible = active;
                    tabBtns[i].AddThemeColorOverride("font_color",
                        active ? new Color(1.0f, 0.9f, 0.3f) : new Color(0.8f, 0.8f, 0.8f));
                }
            };
        }

        // 激活默认标签（第一个）高亮
        if (tabBtns.Count > 0)
            tabBtns[0].AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.3f));
    }

    private void AddQuickBtn(Control parent, string label, string command)
    {
        var btn = new Button();
        btn.Text = label;
        btn.CustomMinimumSize = new Vector2(72, 28);
        btn.AddThemeFontSizeOverride("font_size", 10);
        btn.Pressed += () => OnCmdSubmitted(command);
        parent.AddChild(btn);
    }

    private static Button MakeButton(string txt, Action callback)
    {
        var b = new Button { Text = txt };
        b.Pressed += callback;
        return b;
    }

    // ========================================================================
    // 内部 — 刷新
    // ========================================================================

    private void RefreshSections()
    {
        if (_contentLabel == null) return;

        var lines = new List<string>();
        string sceneName = GetTree().CurrentScene?.Name ?? "?";
        lines.Add($"[color=#888]FPS: {Engine.GetFramesPerSecond()} | 场景: {sceneName}[/color]");

        var invalid = new List<string>();
        foreach (string name in _sectionOrder.ToList())
        {
            if (!_sectionProviders.TryGetValue(name, out var entry))
            {
                invalid.Add(name);
                continue;
            }

            // Check if owner is still valid
            if (entry.Owner != null && !GodotObject.IsInstanceValid(entry.Owner))
            {
                invalid.Add(name);
                continue;
            }

            try
            {
                var result = entry.Provider();
                lines.AddRange(FormatSection(name, result));
            }
            catch
            {
                invalid.Add(name);
            }
        }

        foreach (string n in invalid)
        {
            _sectionProviders.Remove(n);
            _sectionOrder.Remove(n);
        }

        // 追加日志（最近若干条）
        if (_logBuffer.Count > 0)
        {
            lines.Add("[color=#556]─── log ───[/color]");
            int start = Math.Max(0, _logBuffer.Count - 30); // 只显示最近 30 条
            for (int i = start; i < _logBuffer.Count; i++)
            {
                lines.Add(FormatLogLine(_logBuffer[i].Level, _logBuffer[i].Text));
            }
        }

        _contentLabel.Text = string.Join("\n", lines);

        // 自动滚动到底部（显示最新日志）
        CallDeferred(nameof(DoScrollToBottom));
    }

    private void DoScrollToBottom()
    {
        // ScrollContainer 自动滚动到底部
        if (GetParent() is ScrollContainer scroll)
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
    }

    private static List<string> FormatSection(string fallbackName, Variant result)
    {
        string title = fallbackName;
        var bodyLines = new List<string>();

        if (result.VariantType == Variant.Type.Dictionary)
        {
            var dict = result.AsGodotDictionary();
            if (dict.ContainsKey("title"))
                title = dict["title"].AsString();
            if (dict.ContainsKey("lines"))
            {
                var raw = dict["lines"];
                if (raw.VariantType == Variant.Type.Array)
                {
                    foreach (var item in raw.AsGodotArray())
                        bodyLines.Add(item.AsString());
                }
                else
                {
                    bodyLines.Add(raw.AsString());
                }
            }
        }
        else if (result.VariantType == Variant.Type.Array)
        {
            foreach (var item in result.AsGodotArray())
                bodyLines.Add(item.AsString());
        }
        else if (result.VariantType == Variant.Type.PackedStringArray)
        {
            foreach (string s in result.AsStringArray())
                bodyLines.Add(s);
        }
        else
        {
            bodyLines.Add(result.AsString());
        }

        var output = new List<string> { $"[color=cyan][b]--- {title} ---[/b][/color]" };
        output.AddRange(bodyLines);
        return output;
    }

    // ========================================================================
    // 内部 — 日志
    // ========================================================================

    private void AppendLog(string level, string msg)
    {
        if (!_enabled) return;

        _logBuffer.Add((level, msg));
        if (_logBuffer.Count > LogBufferMax)
            _logBuffer.RemoveAt(0);

        // 日志追加后触发下次刷新时显示（不再单独写入 _logLabel）
        _refreshAccum = _refreshInterval;
    }

    private static string FormatLogLine(string level, string msg)
    {
        return level switch
        {
            "warn" => $"[color=#ffcc55]! {msg}[/color]",
            "err" => $"[color=#ff6666]x {msg}[/color]",
            _ => $"[color=#b0ffb0]· {msg}[/color]"
        };
    }

    // ========================================================================
    // 内部 — 命令
    // ========================================================================

    private void RegisterBuiltinCommands()
    {
        RegisterCommand("help", CmdHelp, "列出所有命令");
        RegisterCommand("?", CmdHelp, "别名: help");
        RegisterCommand("clear", (_) => { ClearLog(); return null; }, "清空日志");
        RegisterCommand("copy", (_) => { CopyAllToClipboard(); return null; }, "复制内容到剪贴板");
        RegisterCommand("close", (_) => { SetPanelVisible(false); return null; }, "隐藏面板");
        RegisterCommand("refresh", (_) => { Refresh(); return null; }, "立即刷新");
        RegisterCommand("auto", CmdAuto, "auto on|off 切换自动刷新");
        RegisterCommand("interval", CmdInterval, "interval <秒> 设置刷新间隔");
        RegisterCommand("war_declare", CmdWarDeclare, "war_declare <A> <B> 强制两势力宣战");
        RegisterCommand("war_end", CmdWarEnd, "war_end <A> <B> 强制两势力媾和");
        RegisterCommand("influence", CmdInfluence, "influence <阵营> <数值> 变更势力影响力");
        RegisterCommand("capture", CmdCapture, "capture <聚落名> <新势力> 强制聚落易手");
        RegisterCommand("lord_status", CmdLordStatus, "获取所有NPC领主行动与围城状态");
    }

    private BladeHex.Scenes.Overworld2d.OverworldScene2D? GetOverworldScene()
    {
        return GetTree().CurrentScene as BladeHex.Scenes.Overworld2d.OverworldScene2D;
    }

    private string? CmdWarDeclare(string[] args)
    {
        if (args.Length < 2) return "用法: war_declare <阵营A> <阵营B>";
        var scene = GetOverworldScene();
        if (scene?.EntityMgr?.WorldEngine == null) return "大地图场景未就绪";
        
        string a = args[0];
        string b = args[1];
        scene.EntityMgr.WorldEngine.SetRelation(a, b, -80);
        var result = KingdomDecisionService.TryDeclareWar(a, b, scene.EntityMgr.WorldEngine);
        return $"宣战结果: {result}";
    }

    private string? CmdWarEnd(string[] args)
    {
        if (args.Length < 2) return "用法: war_end <阵营A> <阵营B>";
        var scene = GetOverworldScene();
        if (scene?.EntityMgr?.WorldEngine == null) return "大地图场景未就绪";
        
        string a = args[0];
        string b = args[1];
        var result = KingdomDecisionService.TryMakePeace(a, b, scene.EntityMgr.WorldEngine);
        return $"停战结果: {result}";
    }

    private string? CmdInfluence(string[] args)
    {
        if (args.Length < 2) return "用法: influence <阵营Id> <数值>";
        var scene = GetOverworldScene();
        if (scene?.EntityMgr?.WorldEngine == null) return "大地图场景未就绪";
        
        string nation = args[0];
        if (int.TryParse(args[1], out int amt))
        {
            scene.EntityMgr.WorldEngine.Influence.Add(nation, amt, "调试秘籍加减影响力");
            return $"已将 {nation} 的影响力变更 {amt}，当前: {scene.EntityMgr.WorldEngine.Influence.Get(nation)}";
        }
        return "数值解析失败";
    }

    private string? CmdCapture(string[] args)
    {
        if (args.Length < 2) return "用法: capture <聚落名称> <新归属势力>";
        var scene = GetOverworldScene();
        if (scene?.EntityMgr?.WorldEngine == null) return "大地图场景未就绪";
        
        string poiName = args[0];
        string newFaction = args[1];
        
        var poi = scene.EntityMgr.Pois.FirstOrDefault(p => p.PoiName == poiName);
        if (poi == null) return $"未找到聚落: {poiName}";
        
        int currentDay = scene.CurrentDay;
        bool playerNearby = scene.PlayerParty != null
            && scene.PlayerParty.Position.DistanceTo(poi.Position) <= 600.0f;
        PoiTransferService.Apply(poi, newFaction, null, currentDay, scene.EntityMgr.WorldEngine, playerNearby);
        return $"已成功强制将聚落 {poiName} 易手归属为 {newFaction}！";
    }

    private string? CmdLordStatus(string[] args)
    {
        var scene = GetOverworldScene();
        if (scene?.EntityMgr == null) return "大地图场景未就绪";
        
        var lines = new List<string> { "=== 领主军团当前状态 ===" };
        foreach (var ent in scene.EntityMgr.Entities)
        {
            if (ent.EntityTypeEnum == OverworldEntity.EntityType.LordArmy)
            {
                string target = ent.SiegeTarget != null ? ent.SiegeTarget.PoiName : (string.IsNullOrEmpty(ent.AssignedWarTargetPoiName) ? "无" : ent.AssignedWarTargetPoiName);
                lines.Add($"· {ent.EntityName} ({ent.Faction}): AIState={ent.CurrentAIState} | 目标={target} | 兵力={ent.GarrisonSize} | 是否存活={ent.IsAlive}");
            }
        }
        return string.Join("\n", lines);
    }

    private void OnCmdSubmitted(string text)
    {
        string line = text.Trim();
        _cmdInput?.Clear();
        if (string.IsNullOrEmpty(line)) return;

        LogInfo("> " + line);

        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string name = parts[0].ToLower();
        string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (!_commands.TryGetValue(name, out var entry))
        {
            LogWarn($"未知命令: {name} (输入 help)");
            return;
        }

        // Check if owner is still valid
        if (entry.Owner != null && !GodotObject.IsInstanceValid(entry.Owner))
        {
            LogWarn($"命令 {name} 的目标已失效");
            _commands.Remove(name);
            return;
        }

        string? result = entry.Handler(args);
        if (!string.IsNullOrEmpty(result))
            LogInfo(result);

        _refreshAccum = _refreshInterval; // 命令后立刻刷新一次
    }

    private string? CmdHelp(string[] args)
    {
        var lines = new List<string> { "可用命令:" };
        foreach (var key in _commands.Keys.OrderBy(k => k))
        {
            string h = _commands[key].Help;
            lines.Add($"  {key} - {h}");
        }
        return string.Join("\n", lines);
    }

    private string? CmdAuto(string[] args)
    {
        if (args.Length == 0)
            return $"auto = {_autoRefresh}";
        string v = args[0].ToLower();
        SetAutoRefresh(v == "on" || v == "1" || v == "true");
        return $"auto = {_autoRefresh}";
    }

    private string? CmdInterval(string[] args)
    {
        if (args.Length == 0)
            return $"interval = {_refreshInterval:F2}s";
        if (float.TryParse(args[0], out float val))
            SetRefreshInterval(val);
        return $"interval = {_refreshInterval:F2}s";
    }
}
