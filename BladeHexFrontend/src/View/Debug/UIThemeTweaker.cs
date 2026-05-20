// UIThemeTweaker.cs
// 运行时 UI 可视化调试工具 — 点选模式
// Ctrl+F12 呼出面板，Ctrl+左键点选屏幕上的 UI 控件
// 选中后显示该控件的实际属性（位置/大小/颜色/字号/margin等），拖滑条实时调整
// 支持节点树浏览（仅当前场景可见 UI）、高亮选中控件、导出修改代码
// 仅在 Debug 构建中可用
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BladeHex.Debug;

/// <summary>
/// [Autoload Singleton] UI 可视化调试面板 — 点选模式。
/// <para>快捷键：Ctrl+F12 切换面板；面板打开时 Ctrl+左键拾取 UI 控件。</para>
/// </summary>
[GlobalClass]
public partial class UIThemeTweaker : Node
{
    public static UIThemeTweaker? Instance { get; private set; }

    // ========================================================================
    // 配置
    // ========================================================================
    private const Key HotkeyToggle = Key.F12;
    private const int PanelWidth = 400;
    private const int PanelHeight = 680;

    // ========================================================================
    // 状态
    // ========================================================================
    private bool _enabled;
    private bool _visible;
    private bool _pickMode; // Ctrl+左键拾取模式
    private Control? _selectedControl;

    // 拖拽移动状态
    private bool _dragging;
    private Vector2 _dragStartMousePos;
    private Vector2 _dragStartCtrlPos;

    // 高亮框
    private ColorRect? _highlightRect;

    // ========================================================================
    // UI 节点
    // ========================================================================
    private CanvasLayer? _canvasLayer;
    private PanelContainer? _panel;
    private VBoxContainer? _root;
    private Label? _statusLabel;
    private Label? _nodePathLabel;
    private VBoxContainer? _propsContainer;
    private ScrollContainer? _propsScroll;
    private Tree? _nodeTree;
    private TabContainer? _tabs;

    // ========================================================================
    // 变更记录
    // ========================================================================
    private sealed class ChangeRecord
    {
        public string NodePath = "";
        public string NodeType = "";
        public string NodeName = "";
        public string Property = "";
        public string NewValue = "";
        public string CodeSnippet = "";
    }

    private readonly List<ChangeRecord> _changes = new();

    // ========================================================================
    // 生命周期
    // ========================================================================

    public override void _Ready()
    {
        Instance = this;
        _enabled = OS.IsDebugBuild() || OS.HasFeature("dev");
        if (!_enabled) { SetProcess(false); return; }

        ProcessMode = ProcessModeEnum.Always;
        BuildUI();
        RegisterDebugCommands();
    }

    public override void _Input(InputEvent @event)
    {
        if (!_enabled) return;

        // Ctrl+F12 切换面板
        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == HotkeyToggle && key.CtrlPressed)
        {
            ToggleVisible();
            GetViewport().SetInputAsHandled();
            return;
        }

        // 面板打开时，Ctrl+左键拾取
        if (_visible && _pickMode && @event is InputEventMouseButton mb
            && mb.Pressed && mb.ButtonIndex == MouseButton.Left && mb.CtrlPressed)
        {
            // 延迟一帧拾取，确保不被其他节点的 SetInputAsHandled 干扰
            _pendingPickPos = mb.Position;
            _pendingPick = true;
            GetViewport().SetInputAsHandled();
            return;
        }

        // === 拖拽移动：Alt+左键拖拽选中控件 ===
        if (_visible && _selectedControl != null && GodotObject.IsInstanceValid(_selectedControl))
        {
            if (@event is InputEventMouseButton dragMb && dragMb.ButtonIndex == MouseButton.Left && dragMb.AltPressed)
            {
                if (dragMb.Pressed)
                {
                    _dragging = true;
                    _dragStartMousePos = dragMb.Position;
                    _dragStartCtrlPos = _selectedControl.Position;
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    if (_dragging)
                    {
                        _dragging = false;
                        RecordChange(_selectedControl, "Position", $"{_selectedControl.Position.X}f, {_selectedControl.Position.Y}f");
                        RefreshSelectedProps();
                        GetViewport().SetInputAsHandled();
                    }
                }
                return;
            }

            if (_dragging && @event is InputEventMouseMotion dragMotion)
            {
                var delta = dragMotion.Position - _dragStartMousePos;
                _selectedControl.Position = _dragStartCtrlPos + delta;
                GetViewport().SetInputAsHandled();
                return;
            }

            // === 滚轮缩放：Alt+滚轮调整选中控件大小 ===
            if (@event is InputEventMouseButton scrollMb && scrollMb.AltPressed && scrollMb.Pressed)
            {
                if (scrollMb.ButtonIndex == MouseButton.WheelUp || scrollMb.ButtonIndex == MouseButton.WheelDown)
                {
                    float step = scrollMb.ShiftPressed ? 1f : 4f; // Shift 精细调整
                    float direction = scrollMb.ButtonIndex == MouseButton.WheelUp ? 1f : -1f;
                    float sizeDelta = step * direction;

                    var newMin = _selectedControl.CustomMinimumSize + new Vector2(sizeDelta, sizeDelta);
                    newMin = new Vector2(Mathf.Max(10, newMin.X), Mathf.Max(10, newMin.Y));
                    _selectedControl.CustomMinimumSize = newMin;

                    RecordChange(_selectedControl, "CustomMinimumSize", $"{newMin.X}f, {newMin.Y}f");
                    SetStatus($"尺寸: {newMin.X}×{newMin.Y}");
                    RefreshSelectedProps();
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
        }

        // 松开拖拽（安全兜底）
        if (_dragging && @event is InputEventMouseButton releaseMb
            && !releaseMb.Pressed && releaseMb.ButtonIndex == MouseButton.Left)
        {
            _dragging = false;
        }
    }

    private bool _pendingPick;
    private Vector2 _pendingPickPos;

    public override void _Process(double delta)
    {
        if (!_enabled || !_visible) return;
        UpdateHighlight();

        if (_pendingPick)
        {
            _pendingPick = false;
            PickControlAt(_pendingPickPos);
        }
    }

    // ========================================================================
    // 公开 API
    // ========================================================================

    public void ToggleVisible()
    {
        _visible = !_visible;
        if (_panel != null) _panel.Visible = _visible;
        if (_visible)
        {
            _pickMode = true;
            RefreshNodeTree();
            SetStatus("Ctrl+左键点选 UI 控件，或从树中选择");
        }
        else
        {
            _pickMode = false;
            ClearHighlight();
        }
    }

    // ========================================================================
    // UI 构建
    // ========================================================================

    private void BuildUI()
    {
        _canvasLayer = new CanvasLayer { Name = "UIThemeTweakerLayer", Layer = 998 };
        AddChild(_canvasLayer);

        // 高亮框（用于标记选中控件）
        _highlightRect = new ColorRect();
        _highlightRect.Color = new Color(0.2f, 0.8f, 1.0f, 0.25f);
        _highlightRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _highlightRect.Visible = false;
        _canvasLayer.AddChild(_highlightRect);

        // 主面板
        _panel = new PanelContainer { Name = "UITweakerPanel" };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.OffsetLeft = -PanelWidth - 10;
        _panel.OffsetTop = 10;
        _panel.OffsetRight = -10;
        _panel.OffsetBottom = PanelHeight + 10;
        _panel.CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
        _panel.Visible = false;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop; // 面板本身不被穿透

        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.07f, 0.96f),
            BorderColor = new Color(0.3f, 0.7f, 1.0f, 0.9f),
        };
        bg.SetBorderWidthAll(2);
        bg.SetCornerRadiusAll(6);
        bg.SetContentMarginAll(6);
        _panel.AddThemeStyleboxOverride("panel", bg);
        _canvasLayer.AddChild(_panel);

        _root = new VBoxContainer();
        _root.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(_root);

        // 标题行
        var titleRow = new HBoxContainer();
        _root.AddChild(titleRow);

        var title = new Label { Text = "🔍 UI Inspector (Ctrl+F12)" };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", new Color(0.3f, 0.85f, 1.0f));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);

        var pickBtn = new CheckButton { Text = "拾取", ButtonPressed = true };
        pickBtn.AddThemeFontSizeOverride("font_size", 11);
        pickBtn.Toggled += (on) => _pickMode = on;
        titleRow.AddChild(pickBtn);

        var closeBtn = new Button { Text = "×" };
        closeBtn.Pressed += () => ToggleVisible();
        titleRow.AddChild(closeBtn);

        // 选中节点路径
        _nodePathLabel = new Label { Text = "未选中" };
        _nodePathLabel.AddThemeFontSizeOverride("font_size", 11);
        _nodePathLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.4f));
        _nodePathLabel.ClipText = true;
        _root.AddChild(_nodePathLabel);

        // Tab 容器：属性 | 节点树 | 修改记录
        _tabs = new TabContainer();
        _tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _tabs.AddThemeFontSizeOverride("font_size", 11);
        _root.AddChild(_tabs);

        // Tab 1: 属性面板
        _propsScroll = new ScrollContainer { Name = "属性" };
        _propsScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _tabs.AddChild(_propsScroll);

        _propsContainer = new VBoxContainer();
        _propsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _propsContainer.AddThemeConstantOverride("separation", 2);
        _propsScroll.AddChild(_propsContainer);

        // Tab 2: 节点树
        var treeScroll = new ScrollContainer { Name = "节点树" };
        _tabs.AddChild(treeScroll);

        _nodeTree = new Tree();
        _nodeTree.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _nodeTree.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _nodeTree.HideRoot = true;
        _nodeTree.AddThemeFontSizeOverride("font_size", 11);
        _nodeTree.ItemSelected += OnTreeItemSelected;
        treeScroll.AddChild(_nodeTree);

        // Tab 3: 修改记录
        var changesScroll = new ScrollContainer { Name = "修改记录" };
        _tabs.AddChild(changesScroll);

        var changesBox = new VBoxContainer { Name = "ChangesBox" };
        changesBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        changesScroll.AddChild(changesBox);

        // 操作按钮行
        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 4);
        _root.AddChild(actionRow);

        var exportBtn = new Button { Text = "导出全部" };
        exportBtn.AddThemeFontSizeOverride("font_size", 11);
        exportBtn.Pressed += ExportChanges;
        actionRow.AddChild(exportBtn);

        var exportCurBtn = new Button { Text = "导出当前" };
        exportCurBtn.AddThemeFontSizeOverride("font_size", 11);
        exportCurBtn.Pressed += ExportCurrentControl;
        actionRow.AddChild(exportCurBtn);

        var resetBtn = new Button { Text = "重置当前" };
        resetBtn.AddThemeFontSizeOverride("font_size", 11);
        resetBtn.Pressed += ResetCurrentControl;
        actionRow.AddChild(resetBtn);

        var refreshBtn = new Button { Text = "刷新树" };
        refreshBtn.AddThemeFontSizeOverride("font_size", 11);
        refreshBtn.Pressed += RefreshNodeTree;
        actionRow.AddChild(refreshBtn);

        var clearBtn = new Button { Text = "清除记录" };
        clearBtn.AddThemeFontSizeOverride("font_size", 11);
        clearBtn.Pressed += () => { _changes.Clear(); SetStatus("已清除修改记录"); };
        actionRow.AddChild(clearBtn);

        // 状态栏
        _statusLabel = new Label { Text = "就绪" };
        _statusLabel.AddThemeFontSizeOverride("font_size", 10);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        _root.AddChild(_statusLabel);
    }

    // ========================================================================
    // 拾取逻辑
    // ========================================================================

    private void PickControlAt(Vector2 screenPos)
    {
        // 遍历整个场景树（包括所有 CanvasLayer），找到最上层命中的 Control
        var root = GetTree().Root;
        if (root == null) return;

        Control? best = null;
        int bestDepth = -1;

        FindControlAt(root, screenPos, 0, ref best, ref bestDepth);

        if (best != null && best != _panel && !IsOurUI(best))
        {
            SelectControl(best);
        }
        else
        {
            SetStatus("该位置没有找到 UI 控件（或点到了调试面板自身）");
        }
    }

    private void FindControlAt(Node node, Vector2 pos, int depth, ref Control? best, ref int bestDepth)
    {
        if (node is Control ctrl && ctrl.Visible && ctrl.GetGlobalRect().HasPoint(pos))
        {
            // 跳过我们自己的 UI
            if (!IsOurUI(ctrl))
            {
                if (depth > bestDepth)
                {
                    best = ctrl;
                    bestDepth = depth;
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            if (child is Node n)
                FindControlAt(n, pos, depth + 1, ref best, ref bestDepth);
        }
    }

    private bool IsOurUI(Control ctrl)
    {
        // 检查是否属于我们的 CanvasLayer
        Node? parent = ctrl;
        while (parent != null)
        {
            if (parent == _canvasLayer) return true;
            parent = parent.GetParent();
        }
        return false;
    }

    private void SelectControl(Control ctrl)
    {
        _selectedControl = ctrl;
        string path = ctrl.GetPath().ToString();
        string typeName = ctrl.GetType().Name;

        if (_nodePathLabel != null)
            _nodePathLabel.Text = $"[{typeName}] {ctrl.Name}";

        SetStatus($"已选中: {path}");
        RefreshSelectedProps();
    }

    // ========================================================================
    // 高亮选中控件
    // ========================================================================

    private void UpdateHighlight()
    {
        if (_highlightRect == null) return;

        if (_selectedControl != null && GodotObject.IsInstanceValid(_selectedControl) && _selectedControl.IsVisibleInTree())
        {
            var rect = _selectedControl.GetGlobalRect();
            _highlightRect.Position = rect.Position;
            _highlightRect.Size = rect.Size;
            _highlightRect.Visible = true;
        }
        else
        {
            _highlightRect.Visible = false;
        }
    }

    private void ClearHighlight()
    {
        if (_highlightRect != null) _highlightRect.Visible = false;
        _selectedControl = null;
    }

    // ========================================================================
    // 属性面板 — 显示选中控件的可调属性
    // ========================================================================

    /// <summary>属性名中文翻译表</summary>
    private static readonly Dictionary<string, string> PropTranslations = new()
    {
        // 布局
        ["Position"] = "位置",
        ["Size"] = "尺寸",
        ["MinSize"] = "最小尺寸",
        ["OffsetLeft"] = "左偏移",
        ["OffsetTop"] = "上偏移",
        ["OffsetRight"] = "右偏移",
        ["OffsetBottom"] = "下偏移",
        // 显示
        ["Visible"] = "可见",
        ["Modulate.A"] = "透明度",
        // 文本
        ["Text"] = "文本",
        ["FontSize"] = "字号",
        ["FontColor"] = "字色",
        // 面板
        ["BgColor"] = "背景色",
        ["BorderColor"] = "边框色",
        ["CornerRadius"] = "圆角",
        ["ContentMargin"] = "内边距",
        // 进度条
        ["Value"] = "当前值",
        ["FillColor"] = "填充色",
        // 颜色
        ["Color"] = "颜色",
        // 容器
        ["Separation"] = "子项间距",
    };

    /// <summary>获取属性的显示名（中文 + 原名）</summary>
    private static string Tr(string propName)
    {
        if (PropTranslations.TryGetValue(propName, out var cn))
            return $"{cn} ({propName})";
        return propName;
    }

    private void RefreshSelectedProps()
    {
        if (_propsContainer == null) return;

        foreach (var child in _propsContainer.GetChildren())
            child.QueueFree();

        if (_selectedControl == null || !GodotObject.IsInstanceValid(_selectedControl))
        {
            AddInfoLabel("未选中控件");
            return;
        }

        var ctrl = _selectedControl;
        string typeName = ctrl.GetType().Name;
        AddSectionHeader($"📦 {typeName}: {ctrl.Name}");

        // 布局属性
        AddSectionHeader("── 布局 ──");
        AddVector2Row(Tr("Position"), ctrl.Position, (v) => { ctrl.Position = v; RecordChange(ctrl, "Position", $"{v.X}f, {v.Y}f"); });
        AddVector2Row(Tr("Size"), ctrl.Size, (v) => { ctrl.Size = v; RecordChange(ctrl, "Size", $"{v.X}f, {v.Y}f"); });
        AddVector2Row(Tr("MinSize"), ctrl.CustomMinimumSize, (v) => { ctrl.CustomMinimumSize = v; RecordChange(ctrl, "CustomMinimumSize", $"{v.X}f, {v.Y}f"); });

        // Margin（如果有 anchor 设置）
        AddFloatRow(Tr("OffsetLeft"), ctrl.OffsetLeft, -500, 2000, (v) => { ctrl.OffsetLeft = v; RecordChange(ctrl, "OffsetLeft", $"{v}"); });
        AddFloatRow(Tr("OffsetTop"), ctrl.OffsetTop, -500, 2000, (v) => { ctrl.OffsetTop = v; RecordChange(ctrl, "OffsetTop", $"{v}"); });
        AddFloatRow(Tr("OffsetRight"), ctrl.OffsetRight, -500, 2000, (v) => { ctrl.OffsetRight = v; RecordChange(ctrl, "OffsetRight", $"{v}"); });
        AddFloatRow(Tr("OffsetBottom"), ctrl.OffsetBottom, -500, 2000, (v) => { ctrl.OffsetBottom = v; RecordChange(ctrl, "OffsetBottom", $"{v}"); });

        // 可见性
        AddSectionHeader("── 显示 ──");
        AddBoolRow(Tr("Visible"), ctrl.Visible, (v) => ctrl.Visible = v);
        AddFloatRow(Tr("Modulate.A"), ctrl.Modulate.A, 0, 1, (v) =>
        {
            var c = ctrl.Modulate;
            ctrl.Modulate = new Color(c.R, c.G, c.B, v);
        });

        // 类型特定属性
        if (ctrl is Label lbl)
        {
            AddSectionHeader("── 标签 Label ──");
            AddStringRow(Tr("Text"), lbl.Text, (v) => lbl.Text = v);
            AddIntRow(Tr("FontSize"), GetFontSizeOverride(lbl), 6, 48, (v) =>
            {
                lbl.AddThemeFontSizeOverride("font_size", v);
                RecordChange(ctrl, "FontSize", v.ToString());
            });
            AddColorRow(Tr("FontColor"), GetFontColorOverride(lbl), (c) =>
            {
                lbl.AddThemeColorOverride("font_color", c);
                RecordChange(ctrl, "FontColor", $"#{c.ToHtml()}");
            });
        }
        else if (ctrl is Button btn)
        {
            AddSectionHeader("── 按钮 Button ──");
            AddStringRow(Tr("Text"), btn.Text, (v) => btn.Text = v);
            AddIntRow(Tr("FontSize"), GetFontSizeOverride(btn), 6, 48, (v) =>
            {
                btn.AddThemeFontSizeOverride("font_size", v);
                RecordChange(ctrl, "FontSize", v.ToString());
            });
            AddColorRow(Tr("FontColor"), GetFontColorOverride(btn), (c) =>
            {
                btn.AddThemeColorOverride("font_color", c);
                RecordChange(ctrl, "FontColor", $"#{c.ToHtml()}");
            });
        }
        else if (ctrl is PanelContainer pc)
        {
            AddSectionHeader("── 面板 Panel ──");
            var style = pc.GetThemeStylebox("panel") as StyleBoxFlat;
            if (style != null)
            {
                AddColorRow(Tr("BgColor"), style.BgColor, (c) =>
                {
                    style.BgColor = c;
                    RecordChange(ctrl, "Panel.BgColor", $"#{c.ToHtml()}");
                });
                AddColorRow(Tr("BorderColor"), style.BorderColor, (c) =>
                {
                    style.BorderColor = c;
                    RecordChange(ctrl, "Panel.BorderColor", $"#{c.ToHtml()}");
                });
                AddIntRow(Tr("CornerRadius"), style.CornerRadiusTopLeft, 0, 32, (v) =>
                {
                    style.SetCornerRadiusAll(v);
                    RecordChange(ctrl, "Panel.CornerRadius", v.ToString());
                });
                AddIntRow(Tr("ContentMargin"), (int)style.ContentMarginLeft, 0, 32, (v) =>
                {
                    style.SetContentMarginAll(v);
                    RecordChange(ctrl, "Panel.ContentMargin", v.ToString());
                });
            }
        }
        else if (ctrl is ProgressBar bar)
        {
            AddSectionHeader("── 进度条 ProgressBar ──");
            AddFloatRow(Tr("Value"), (float)bar.Value, 0, 100, (v) => bar.Value = v);
            var fill = bar.GetThemeStylebox("fill") as StyleBoxFlat;
            if (fill != null)
            {
                AddColorRow(Tr("FillColor"), fill.BgColor, (c) =>
                {
                    fill.BgColor = c;
                    RecordChange(ctrl, "Fill.BgColor", $"#{c.ToHtml()}");
                });
            }
        }
        else if (ctrl is RichTextLabel rtl)
        {
            AddSectionHeader("── 富文本 RichTextLabel ──");
            AddIntRow(Tr("FontSize"), GetRtlFontSize(rtl), 6, 48, (v) =>
            {
                rtl.AddThemeFontSizeOverride("normal_font_size", v);
                RecordChange(ctrl, "FontSize", v.ToString());
            });
        }
        else if (ctrl is ColorRect cr)
        {
            AddSectionHeader("── 色块 ColorRect ──");
            AddColorRow(Tr("Color"), cr.Color, (c) =>
            {
                cr.Color = c;
                RecordChange(ctrl, "Color", $"#{c.ToHtml()}");
            });
        }

        // 容器间距
        if (ctrl is BoxContainer box)
        {
            AddSectionHeader("── 容器 Container ──");
            AddIntRow(Tr("Separation"), box.GetThemeConstant("separation"), 0, 32, (v) =>
            {
                box.AddThemeConstantOverride("separation", v);
                RecordChange(ctrl, "Separation", v.ToString());
            });
        }

        // 切换到属性 tab
        if (_tabs != null) _tabs.CurrentTab = 0;
    }

    // ========================================================================
    // 属性行构建辅助
    // ========================================================================

    private void AddSectionHeader(string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 1.0f));
        _propsContainer?.AddChild(lbl);
    }

    private void AddInfoLabel(string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        _propsContainer?.AddChild(lbl);
    }

    private void AddVector2Row(string name, Vector2 value, Action<Vector2> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(nameLabel);

        var xSpin = new SpinBox { MinValue = -2000, MaxValue = 4000, Step = 1 };
        xSpin.CustomMinimumSize = new Vector2(70, 0);
        xSpin.AddThemeFontSizeOverride("font_size", 10);
        xSpin.Prefix = "X:";
        row.AddChild(xSpin);

        var ySpin = new SpinBox { MinValue = -2000, MaxValue = 4000, Step = 1 };
        ySpin.CustomMinimumSize = new Vector2(70, 0);
        ySpin.AddThemeFontSizeOverride("font_size", 10);
        ySpin.Prefix = "Y:";
        row.AddChild(ySpin);

        // 先设置初始值
        xSpin.SetValueNoSignal(value.X);
        ySpin.SetValueNoSignal(value.Y);

        // 再连接事件
        xSpin.ValueChanged += (_) => setter(new Vector2((float)xSpin.Value, (float)ySpin.Value));
        ySpin.ValueChanged += (_) => setter(new Vector2((float)xSpin.Value, (float)ySpin.Value));

        _propsContainer?.AddChild(row);
    }

    private void AddFloatRow(string name, float value, float min, float max, Action<float> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(nameLabel);

        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1 };
        spin.CustomMinimumSize = new Vector2(70, 0);
        spin.AddThemeFontSizeOverride("font_size", 10);
        row.AddChild(spin);

        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 1 };
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(slider);

        // 先设置初始值（不触发回调）
        spin.SetValueNoSignal(value);
        slider.SetValueNoSignal(value);

        // 再连接事件
        bool _updating = false;
        spin.ValueChanged += (v) => { if (_updating) return; _updating = true; slider.SetValueNoSignal(v); setter((float)v); _updating = false; };
        slider.ValueChanged += (v) => { if (_updating) return; _updating = true; spin.SetValueNoSignal(v); setter((float)v); _updating = false; };

        _propsContainer?.AddChild(row);
    }

    private void AddIntRow(string name, int value, int min, int max, Action<int> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(nameLabel);

        var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1 };
        spin.CustomMinimumSize = new Vector2(70, 0);
        spin.AddThemeFontSizeOverride("font_size", 10);
        row.AddChild(spin);

        var slider = new HSlider { MinValue = min, MaxValue = max, Step = 1 };
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(slider);

        // 先设置初始值（不触发回调）
        spin.SetValueNoSignal(value);
        slider.SetValueNoSignal(value);

        // 再连接事件
        bool _updating = false;
        spin.ValueChanged += (v) => { if (_updating) return; _updating = true; slider.SetValueNoSignal(v); setter((int)v); _updating = false; };
        slider.ValueChanged += (v) => { if (_updating) return; _updating = true; spin.SetValueNoSignal(v); setter((int)v); _updating = false; };

        _propsContainer?.AddChild(row);
    }

    private void AddBoolRow(string name, bool value, Action<bool> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(nameLabel);

        var check = new CheckButton { ButtonPressed = value };
        check.Toggled += (on) => setter(on);
        row.AddChild(check);

        _propsContainer?.AddChild(row);
    }

    private void AddStringRow(string name, string value, Action<string> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(nameLabel);

        var edit = new LineEdit { Text = value };
        edit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        edit.AddThemeFontSizeOverride("font_size", 10);
        edit.TextSubmitted += (t) => setter(t);
        row.AddChild(edit);

        _propsContainer?.AddChild(row);
    }

    private void AddColorRow(string name, Color value, Action<Color> setter)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(nameLabel);

        var colorBtn = new ColorPickerButton();
        colorBtn.Color = value;
        colorBtn.CustomMinimumSize = new Vector2(100, 24);
        colorBtn.EditAlpha = true;
        colorBtn.ColorChanged += (c) => setter(c);
        row.AddChild(colorBtn);

        var hexLabel = new Label { Text = $"#{value.ToHtml()}" };
        hexLabel.AddThemeFontSizeOverride("font_size", 9);
        hexLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        row.AddChild(hexLabel);

        colorBtn.ColorChanged += (c) => hexLabel.Text = $"#{c.ToHtml()}";

        _propsContainer?.AddChild(row);
    }

    // ========================================================================
    // 节点树
    // ========================================================================

    private void RefreshNodeTree()
    {
        if (_nodeTree == null) return;
        _nodeTree.Clear();

        var treeRoot = _nodeTree.CreateItem();
        var root = GetTree().Root;
        if (root == null) return;

        // 遍历整个场景树，但跳过我们自己的 CanvasLayer
        BuildTreeRecursive(root, treeRoot, 0);
    }

    private void BuildTreeRecursive(Node node, TreeItem parent, int depth)
    {
        if (depth > 15) return; // 防止过深
        if (node is CanvasLayer cl && cl == _canvasLayer) return; // 跳过自己

        if (node is Control ctrl && ctrl.Visible)
        {
            var item = _nodeTree!.CreateItem(parent);
            string typeName = ctrl.GetType().Name;
            item.SetText(0, $"[{typeName}] {ctrl.Name}");
            item.SetMetadata(0, ctrl.GetPath());

            // 颜色标记类型
            Color textColor = typeName switch
            {
                "Label" or "RichTextLabel" => new Color(0.8f, 0.9f, 0.6f),
                "Button" or "CheckButton" or "OptionButton" => new Color(0.6f, 0.8f, 1.0f),
                "PanelContainer" or "Panel" => new Color(0.9f, 0.7f, 0.5f),
                "HBoxContainer" or "VBoxContainer" or "GridContainer" => new Color(0.6f, 0.6f, 0.7f),
                "ProgressBar" or "HSlider" => new Color(0.5f, 0.9f, 0.5f),
                _ => new Color(0.7f, 0.7f, 0.7f),
            };
            item.SetCustomColor(0, textColor);

            // 递归子节点
            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    BuildTreeRecursive(n, item, depth + 1);
            }
        }
        else
        {
            // 非 Control 节点但可能有 Control 子节点
            foreach (var child in node.GetChildren())
            {
                if (child is Node n)
                    BuildTreeRecursive(n, parent, depth + 1);
            }
        }
    }

    private void OnTreeItemSelected()
    {
        if (_nodeTree == null) return;
        var selected = _nodeTree.GetSelected();
        if (selected == null) return;

        var path = selected.GetMetadata(0).AsString();
        if (string.IsNullOrEmpty(path)) return;

        var node = GetNode(path);
        if (node is Control ctrl)
        {
            SelectControl(ctrl);
        }
    }

    // ========================================================================
    // 变更记录 & 导出
    // ========================================================================

    private void RecordChange(Control ctrl, string property, string newValue)
    {
        string nodeName = ctrl.Name;
        string codeSnippet = property switch
        {
            "FontSize" => $"{nodeName}.AddThemeFontSizeOverride(\"font_size\", {newValue});",
            "FontColor" => $"{nodeName}.AddThemeColorOverride(\"font_color\", new Color(\"{newValue}\"));",
            "Separation" => $"{nodeName}.AddThemeConstantOverride(\"separation\", {newValue});",
            "Panel.BgColor" => $"// {nodeName} panel style: BgColor = new Color(\"{newValue}\");",
            "Panel.BorderColor" => $"// {nodeName} panel style: BorderColor = new Color(\"{newValue}\");",
            "Panel.CornerRadius" => $"// {nodeName} panel style: SetCornerRadiusAll({newValue});",
            "Panel.ContentMargin" => $"// {nodeName} panel style: SetContentMarginAll({newValue});",
            "Fill.BgColor" => $"// {nodeName} fill style: BgColor = new Color(\"{newValue}\");",
            "Color" => $"{nodeName}.Color = new Color(\"{newValue}\");",
            "Position" => $"{nodeName}.Position = new Vector2({newValue});",
            "Size" => $"{nodeName}.Size = new Vector2({newValue});",
            "CustomMinimumSize" => $"{nodeName}.CustomMinimumSize = new Vector2({newValue});",
            "OffsetLeft" => $"{nodeName}.OffsetLeft = {newValue}f;",
            "OffsetTop" => $"{nodeName}.OffsetTop = {newValue}f;",
            "OffsetRight" => $"{nodeName}.OffsetRight = {newValue}f;",
            "OffsetBottom" => $"{nodeName}.OffsetBottom = {newValue}f;",
            _ => $"// {nodeName}.{property} = {newValue};",
        };

        _changes.Add(new ChangeRecord
        {
            NodePath = ctrl.GetPath().ToString(),
            NodeType = ctrl.GetType().Name,
            NodeName = nodeName,
            Property = property,
            NewValue = newValue,
            CodeSnippet = codeSnippet,
        });

        GD.Print($"[UITweaker] RecordChange: {nodeName}.{property} = {newValue} (total: {_changes.Count})");
    }

    private void ExportChanges()
    {
        if (_changes.Count == 0)
        {
            SetStatus("没有修改记录");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// === UI Inspector 修改导出 ===");
        sb.AppendLine($"// 共 {_changes.Count} 条修改");
        sb.AppendLine();

        // 按节点分组，每个属性只取最后一次修改
        var grouped = _changes
            .GroupBy(c => c.NodePath)
            .Select(g => new
            {
                Path = g.Key,
                First = g.First(),
                // 每个属性只保留最后一条
                Props = g.GroupBy(c => c.Property).Select(pg => pg.Last()).ToList()
            });

        foreach (var node in grouped)
        {
            sb.AppendLine($"// [{node.First.NodeType}] {node.First.NodeName}");
            sb.AppendLine($"// Path: {node.Path}");
            foreach (var change in node.Props)
            {
                sb.AppendLine($"{change.CodeSnippet}");
            }
            sb.AppendLine();
        }

        DisplayServer.ClipboardSet(sb.ToString());
        SetStatus($"已导出修改到剪贴板");
        GD.Print(sb.ToString());
    }

    private void ExportCurrentControl()
    {
        if (_selectedControl == null || !GodotObject.IsInstanceValid(_selectedControl))
        {
            SetStatus("未选中控件");
            return;
        }

        string path = _selectedControl.GetPath().ToString();
        // 每个属性只取最后一条
        var currentChanges = _changes
            .Where(c => c.NodePath == path)
            .GroupBy(c => c.Property)
            .Select(g => g.Last())
            .ToList();

        if (currentChanges.Count == 0)
        {
            SetStatus("当前控件没有修改记录");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"// === [{_selectedControl.GetType().Name}] {_selectedControl.Name} ===");
        sb.AppendLine($"// Path: {path}");
        sb.AppendLine();

        foreach (var change in currentChanges)
        {
            sb.AppendLine($"{change.CodeSnippet}");
        }

        DisplayServer.ClipboardSet(sb.ToString());
        SetStatus($"已导出当前控件 {currentChanges.Count} 处修改到剪贴板");
        GD.Print(sb.ToString());
    }

    private void ResetCurrentControl()
    {
        if (_selectedControl == null || !GodotObject.IsInstanceValid(_selectedControl))
        {
            SetStatus("未选中控件");
            return;
        }

        var ctrl = _selectedControl;

        // 移除所有 theme override
        ctrl.RemoveThemeFontSizeOverride("font_size");
        ctrl.RemoveThemeColorOverride("font_color");
        ctrl.RemoveThemeConstantOverride("separation");

        if (ctrl is RichTextLabel)
            ctrl.RemoveThemeFontSizeOverride("normal_font_size");

        // 移除该控件的修改记录
        string path = ctrl.GetPath().ToString();
        _changes.RemoveAll(c => c.NodePath == path);

        // 刷新属性面板
        RefreshSelectedProps();
        SetStatus($"已重置: {ctrl.Name}");
    }

    // ========================================================================
    // 辅助方法
    // ========================================================================

    private static int GetFontSizeOverride(Control ctrl)
    {
        if (ctrl.HasThemeFontSizeOverride("font_size"))
            return ctrl.GetThemeFontSize("font_size");
        return 14; // 默认
    }

    private static Color GetFontColorOverride(Control ctrl)
    {
        if (ctrl.HasThemeColorOverride("font_color"))
            return ctrl.GetThemeColor("font_color");
        return new Color(0.9f, 0.9f, 0.9f);
    }

    private static int GetRtlFontSize(RichTextLabel rtl)
    {
        if (rtl.HasThemeFontSizeOverride("normal_font_size"))
            return rtl.GetThemeFontSize("normal_font_size");
        return 14;
    }

    private void SetStatus(string msg)
    {
        if (_statusLabel != null) _statusLabel.Text = msg;
    }

    // ========================================================================
    // DebugConsole 集成
    // ========================================================================

    private void RegisterDebugCommands()
    {
        var console = DebugConsole.Instance;
        if (console == null) return;

        console.RegisterCommand("inspector", (args) =>
        {
            if (args.Length == 0) { ToggleVisible(); return "UI Inspector 已切换"; }
            return args[0].ToLower() switch
            {
                "on" or "show" => DoAction(() => { _visible = false; ToggleVisible(); }),
                "off" or "hide" => DoAction(() => { _visible = true; ToggleVisible(); }),
                "export" => DoAction(ExportChanges),
                _ => "用法: inspector [on|off|export]"
            };
        }, "UI可视化调试面板 (inspector on|off|export)", this);
    }

    private static string DoAction(Action action) { action(); return "OK"; }
}
