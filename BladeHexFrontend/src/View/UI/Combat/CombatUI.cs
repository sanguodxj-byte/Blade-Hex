// CombatUI.cs
// 战术战斗主用户界面
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;

namespace BladeHex.UI.Combat;

/// <summary>
/// 战术战斗主 UI — 悬浮 CanvasLayer，管理回合条、日志、敌方信息、
/// 底部信息面板、技能/法术选择及各类弹出面板。
/// 子面板使用 class_name 类型（通过 GD.Load 桥接），
/// </summary>
[GlobalClass]
public partial class CombatUI : CanvasLayer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal] public delegate void ActionSelectedEventHandler(string actionName);
    [Signal] public delegate void SpellSelectedEventHandler(SpellData spell);
    [Signal] public delegate void ActionHoveredEventHandler(string actionName);
    [Signal] public delegate void EnemyHoveredInPanelEventHandler(Unit unit);
    [Signal] public delegate void UnitSelectedInListEventHandler(Unit unit);

    // ============================================================================
    // 子面板引用 (class_name → base Godot type)
    // ============================================================================
    private TurnOrderBar _turnOrderBar = null!;
    private EnemyInfoPanel _enemyInfoPanel = null!;
    private HitPreviewTooltip _hitPreviewTooltip = null!;
    private TerrainTooltip _terrainTooltip = null!;
    private BattleLogPanel _battleLog = null!;
    private Node _spellSelect = null!;
    private CanvasLayer? _battleResult;
    private RadialMenu _radialMenu = null!;
    private UnitInspectPanel _unitInspect = null!;
    private CombatPowerBar _powerBar = null!;

    // ============================================================================
    // 底部面板控件
    // ============================================================================
    private PanelContainer? _bottomPanel;
    private HBoxContainer? _bottomHBox;
    private BladeHex.View.Unit.CharacterAvatarControl? _avatarControl;
    private Label? _weaponPrimaryLabel;
    private Label? _weaponSecondaryLabel;
    private Label? _topInfoLabel;
    private Label? _phaseLabel;
    private PanelContainer? _escMenu;
    private Button? _speedBtn;
    private BladeHex.View.UI.Inventory.ItemPopup? _itemPopup;
    /// <summary>当前底部面板显示的单位 — 武器悬浮信息要从它取武器</summary>
    private Unit? _currentDisplayedUnit;

    // 底部面板条件条
    private ProgressBar? _drBar;
    private ProgressBar? _apBar;

    // 顶部回合提示
    private Label? _turnPhaseLabel;

    // 快捷技能槽
    private GridContainer? _quickSlotContainer;
    private Button[]? _quickSlots;
    private string?[]? _quickSlotSkills;
    private string?[]? _quickSlotDescriptions;
    private PanelContainer? _skillTooltip;

    // ============================================================================
    // 过渡动画用引用
    // ============================================================================
    /// <summary>底部角色面板（过渡动画用）</summary>
    public Control? BottomPanel => _bottomPanel;
    /// <summary>回合顺序栏（过渡动画用）</summary>
    public Control? TurnOrderBarControl => _turnOrderBar;

    // ============================================================================
    // 索引字典
    // ============================================================================
    private readonly Dictionary<string, Control> _statLabels = new();

    // ============================================================================
    // 主题
    // ============================================================================
    private UITheme Theme => UITheme.Instance!;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        _SetupUI();
    }

    // ============================================================================
    // UI 树构建
    // ============================================================================

    private void _SetupUI()
    {
        // --- Root: 全屏 MarginContainer ---
        var root = new MarginContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddThemeConstantOverride("margin_left", Theme.SpacingMd);
        root.AddThemeConstantOverride("margin_right", Theme.SpacingMd);
        root.AddThemeConstantOverride("margin_top", Theme.SpacingMd);
        root.AddThemeConstantOverride("margin_bottom", 50);
        AddChild(root);

        var mainVBox = new VBoxContainer();
        mainVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(mainVBox);

        // --- 1. 顶部：战力对比 + 回合提示 (居中) ---
        var topCenterVBox = new VBoxContainer();
        topCenterVBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        topCenterVBox.AddThemeConstantOverride("separation", 2);
        topCenterVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        mainVBox.AddChild(topCenterVBox);

        _powerBar = new CombatPowerBar();
        topCenterVBox.AddChild(_powerBar);

        // 回合提示文字（"▶ 冒险者_2 的回合"）
        _turnPhaseLabel = new Label();
        _turnPhaseLabel.Text = "";
        _turnPhaseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _turnPhaseLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
        _turnPhaseLabel.AddThemeColorOverride("font_color", Theme.TextAccent);
        topCenterVBox.AddChild(_turnPhaseLabel);

        // --- 2. 中部战术区 (弹性占位 — 占满上方空间) ---
        var middleSpacer = new Control();
        middleSpacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        middleSpacer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mainVBox.AddChild(middleSpacer);

        // --- 3. 回合顺序栏 + 快进/结束回合 (同一行，居中) ---
        var turnBarRow = new HBoxContainer();
        turnBarRow.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        turnBarRow.AddThemeConstantOverride("separation", Theme.SpacingMd);
        turnBarRow.MouseFilter = Control.MouseFilterEnum.Ignore;
        mainVBox.AddChild(turnBarRow);

        _turnOrderBar = new TurnOrderBar();
        _turnOrderBar.CustomMinimumSize = new Vector2(600, 60);
        turnBarRow.AddChild(_turnOrderBar);

        _speedBtn = new Button();
        _speedBtn.Text = $"快进 {BladeHex.View.Combat.CombatSpeed.Multiplier:0}×";
        _speedBtn.CustomMinimumSize = new Vector2(80, 38);
        _speedBtn.TooltipText = "切换战斗动画播放速度(1× / 2× / 4×)";
        _speedBtn.Pressed += () =>
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_click");
            BladeHex.View.Combat.CombatSpeed.CycleNext();
        };
        _speedBtn.MouseEntered += () => BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_hover", -6.0f);
        var speedStyle = Theme.MakePanelStyle(Theme.BgSecondary, Theme.BorderDefault, 1, Theme.RadiusMd);
        _speedBtn.AddThemeStyleboxOverride("normal", speedStyle);
        turnBarRow.AddChild(_speedBtn);

        BladeHex.View.Combat.CombatSpeed.MultiplierChanged += _OnSpeedChanged;

        var endTurnBtn = new Button();
        endTurnBtn.Text = "结束回合";
        endTurnBtn.CustomMinimumSize = new Vector2(100, 38);
        endTurnBtn.Pressed += () =>
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_click");
            EmitSignal(SignalName.ActionSelected, "end_turn");
        };
        endTurnBtn.MouseEntered += () => BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_hover", -6.0f);
        var btnStyle = Theme.MakePanelStyle(Theme.BgSecondary, Theme.BorderFriendly, 1, Theme.RadiusMd);
        endTurnBtn.AddThemeStyleboxOverride("normal", btnStyle);
        turnBarRow.AddChild(endTurnBtn);

        // --- 4. 底部信息面板 (居中，紧贴回合顺序栏下方) ---
        _bottomPanel = _CreatePanel(Vector2.Zero, Theme.BgPrimary, Theme.BorderDefault);
        _bottomPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        mainVBox.AddChild(_bottomPanel);

        var bottomMargin = _CreateMargin(12, 12, 10, 10);
        _bottomPanel.AddChild(bottomMargin);

        var bottomHBox = new HBoxContainer();
        bottomHBox.AddThemeConstantOverride("separation", Theme.SpacingLg);
        bottomMargin.AddChild(bottomHBox);
        _bottomHBox = bottomHBox;

        // 5a. 头像
        var avatarBg = _CreateCard(new Vector2(80, 80), false);
        bottomHBox.AddChild(avatarBg);

        _avatarControl = new BladeHex.View.Unit.CharacterAvatarControl
        {
            Mode = BladeHex.View.Unit.CharacterAvatarControl.DisplayMode.Bust,
        };
        _avatarControl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        avatarBg.AddChild(_avatarControl);

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5b. 角色信息列 — 名称居中 + 条件资源条(HP/DR/MP/AP)
        var infoCol = new VBoxContainer();
        infoCol.AddThemeConstantOverride("separation", 3);
        infoCol.CustomMinimumSize = new Vector2(150, 0);
        bottomHBox.AddChild(infoCol);

        var charName = _CreateBodyLabel("未选择", Theme.TextAccent);
        charName.SetMeta("stat_key", "char_name");
        charName.HorizontalAlignment = HorizontalAlignment.Center;
        charName.AddThemeFontSizeOverride("font_size", Theme.FontSizeLg);
        infoCol.AddChild(charName);
        _statLabels["char_name"] = charName;

        // HP 条
        var hpBar = _CreateHpBar(150, 10);
        hpBar.SetMeta("stat_key", "hp_bar");
        infoCol.AddChild(hpBar);
        _statLabels["hp_bar"] = hpBar;

        // DR(装甲)条 — 无装甲角色隐藏
        _drBar = new ProgressBar();
        _drBar.CustomMinimumSize = new Vector2(150, 7);
        _drBar.ShowPercentage = false;
        Theme.ApplyBarTheme(_drBar, new Color(0.45f, 0.55f, 0.75f), new Color(0.15f, 0.15f, 0.2f, 0.6f));
        _drBar.Visible = false;
        infoCol.AddChild(_drBar);
        _statLabels["dr_bar"] = _drBar;

        // MP(法力)条 — 非施法角色隐藏
        var mpBar = _CreateManaBar(150, 7);
        mpBar.SetMeta("stat_key", "mp_bar");
        mpBar.Visible = false;
        infoCol.AddChild(mpBar);
        _statLabels["mp_bar"] = mpBar;

        // AP(行动力)条
        _apBar = new ProgressBar();
        _apBar.CustomMinimumSize = new Vector2(150, 7);
        _apBar.ShowPercentage = false;
        Theme.ApplyBarTheme(_apBar, new Color(0.85f, 0.75f, 0.3f), new Color(0.15f, 0.12f, 0.05f, 0.6f));
        infoCol.AddChild(_apBar);
        _statLabels["ap_bar"] = _apBar;

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5c. 武器槽
        var weaponVBox = new VBoxContainer();
        weaponVBox.Alignment = BoxContainer.AlignmentMode.Center;
        weaponVBox.AddThemeConstantOverride("separation", 4);
        bottomHBox.AddChild(weaponVBox);

        var mainHand = _CreateCard(new Vector2(50, 80), true);
        mainHand.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                    EmitSignal(SignalName.ActionSelected, "switch_to_primary");
                else if (mb.ButtonIndex == MouseButton.Right)
                    _ShowWeaponInfoPopup(_currentDisplayedUnit?.Data?.PrimaryMainHand, mb.GlobalPosition);
            }
        };
        mainHand.MouseExited += () => _itemPopup?.Hide();
        weaponVBox.AddChild(mainHand);
        _weaponPrimaryLabel = _CreateMutedLabel("主手");
        _weaponPrimaryLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        mainHand.AddChild(_weaponPrimaryLabel);

        var offHand = _CreateCard(new Vector2(50, 80), true);
        offHand.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                    EmitSignal(SignalName.ActionSelected, "switch_to_secondary");
                else if (mb.ButtonIndex == MouseButton.Right)
                    _ShowWeaponInfoPopup(_currentDisplayedUnit?.Data?.SecondaryMainHand, mb.GlobalPosition);
            }
        };
        offHand.MouseExited += () => _itemPopup?.Hide();
        weaponVBox.AddChild(offHand);
        _weaponSecondaryLabel = _CreateMutedLabel("副手");
        _weaponSecondaryLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        offHand.AddChild(_weaponSecondaryLabel);

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5d. 快捷技能槽(1-0 快捷键,共 10 个) — 5 列 2 行网格，每槽 68×68 填满空间
        _quickSlotContainer = new GridContainer();
        _quickSlotContainer.Name = "QuickActions";
        _quickSlotContainer.Columns = 5;
        _quickSlotContainer.AddThemeConstantOverride("h_separation", 5);
        _quickSlotContainer.AddThemeConstantOverride("v_separation", 5);
        _quickSlotContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bottomHBox.AddChild(_quickSlotContainer);

        _quickSlots = new Button[10];
        _quickSlotSkills = new string?[10];
        _quickSlotDescriptions = new string?[10];
        for (int i = 0; i < 10; i++)
        {
            var slot = new Button();
            slot.CustomMinimumSize = new Vector2(68, 68);
            slot.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            slot.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            slot.Text = i < 9 ? $"{i + 1}" : "0";
            slot.TooltipText = "空槽位";
            var slotStyle = Theme.MakePanelStyle(Theme.BgCard, Theme.BorderDefault, 1, Theme.RadiusMd);
            slot.AddThemeStyleboxOverride("normal", slotStyle);
            slot.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
            var mod = slot.Modulate;
            mod.A = 0.4f;
            slot.Modulate = mod;

            int slotIdx = i;
            slot.Pressed += () => _OnQuickSlotPressed(slotIdx);
            slot.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
                    _ShowSkillTooltip(slotIdx, mb.GlobalPosition);
            };
            slot.MouseExited += () => _skillTooltip?.Hide();
            _quickSlotContainer.AddChild(slot);
            _quickSlots[i] = slot;
        }

        // --- 5. 隐藏的功能组件(保留 API 兼容，不在主布局中显示) ---

        // 战斗日志 — 隐藏但保留功能(LogMessage 仍可调用)
        _battleLog = new BattleLogPanel();
        _battleLog.CustomMinimumSize = new Vector2(300, 140);
        _battleLog.Visible = false;
        AddChild(_battleLog);

        // 敌方信息面板 — 隐藏但保留注册/更新功能
        _enemyInfoPanel = new EnemyInfoPanel();
        _enemyInfoPanel.CustomMinimumSize = new Vector2(280, 0);
        _enemyInfoPanel.Visible = false;
        AddChild(_enemyInfoPanel);

        // --- 6. 悬浮/弹出组件 ---
        _hitPreviewTooltip = new HitPreviewTooltip();
        _hitPreviewTooltip.Visible = false;
        AddChild(_hitPreviewTooltip);

        _terrainTooltip = new TerrainTooltip();
        _terrainTooltip.Visible = false;
        AddChild(_terrainTooltip);

        _radialMenu = new RadialMenu();
        _radialMenu.Visible = false;
        _radialMenu.ActionSelected += _OnRadialMenuActionSelected;
        _radialMenu.ActionHovered += _OnRadialMenuActionHovered;
        AddChild(_radialMenu);

        _unitInspect = new UnitInspectPanel();
        AddChild(_unitInspect);

        _SetupEscMenu();

        // 武器信息悬浮窗
        _itemPopup = new BladeHex.View.UI.Inventory.ItemPopup();
        AddChild(_itemPopup);
    }

    /// <summary>右键武器图标弹出装备信息(用 ItemPopup 复用大背包侧的样式)</summary>
    private void _ShowWeaponInfoPopup(BladeHex.Data.ItemData? item, Vector2 globalMousePos)
    {
        if (item == null || _itemPopup == null) return;
        _itemPopup.ShowFor(item, globalMousePos);
    }

    /// <summary>ESC 菜单 — 全屏遮罩 + 返回按钮</summary>
    private void _SetupEscMenu()
    {
        _escMenu = new PanelContainer();
        _escMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _escMenu.Visible = false;
        // 显式设置半透明背景（仅在 Visible=true 时可见）
        var bg = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.5f) };
        bg.SetBorderWidthAll(0);
        _escMenu.AddThemeStyleboxOverride("panel", bg);
        _escMenu.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_escMenu);

        var center = new CenterContainer();
        _escMenu.AddChild(center);

        var resume = new Button();
        resume.Text = "返回战斗";
        resume.Pressed += () => { _escMenu.Visible = false; };
        center.AddChild(resume);
    }

    // ============================================================================
    // 内联工厂辅助方法 (替代 UIFactory)
    // ============================================================================

    private PanelContainer _CreatePanel(Vector2 minSize, Color bg, Color border)
    {
        var panel = new PanelContainer();
        if (minSize != Vector2.Zero)
            panel.CustomMinimumSize = minSize;
        panel.AddThemeStyleboxOverride("panel",
            Theme.MakePanelStyle(bg, border, 1, Theme.RadiusMd, Theme.SpacingMd));
        return panel;
    }

    private PanelContainer _CreateCard(Vector2 minSize, bool hoverable)
    {
        var card = new PanelContainer();
        if (minSize != Vector2.Zero)
            card.CustomMinimumSize = minSize;
        card.AddThemeStyleboxOverride("panel",
            Theme.MakePanelStyle(Theme.BgCard, Theme.BorderDefault, 1, Theme.RadiusMd, Theme.SpacingSm));
        if (hoverable)
            card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        return card;
    }

    private MarginContainer _CreateMargin(int left, int right, int top, int bottom)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_bottom", bottom);
        return m;
    }

    private Label _CreateBodyLabel(string text, Color? color = null)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", Theme.FontSizeMd);
        lbl.AddThemeColorOverride("font_color", color ?? Theme.TextPrimary);
        return lbl;
    }

    private Label _CreateMutedLabel(string text)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", Theme.FontSizeSm);
        lbl.AddThemeColorOverride("font_color", Theme.TextMuted);
        return lbl;
    }

    private ProgressBar _CreateHpBar(float width, int height)
    {
        var bar = new ProgressBar();
        int h = height > 0 ? height : Theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.ShowPercentage = false;
        Theme.ApplyBarTheme(bar, Theme.HpHigh, Theme.HpBarBg);
        return bar;
    }

    private ProgressBar _CreateManaBar(float width, int height)
    {
        var bar = new ProgressBar();
        int h = height > 0 ? height : Theme.BarHeightMd;
        bar.CustomMinimumSize = new Vector2(width, h);
        bar.ShowPercentage = false;
        Theme.ApplyBarTheme(bar, Theme.ManaFill, Theme.ManaBg);
        return bar;
    }

    private VSeparator _CreateSeparatorV()
    {
        var sep = new VSeparator();
        var style = new StyleBoxFlat();
        style.BgColor = Theme.BorderDefault;
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    // ============================================================================
    // 列表查找
    // ============================================================================

    private HBoxContainer? _FindAllyList()
    {
        return FindChild("AllyList", true, false) as HBoxContainer;
    }

    private HBoxContainer? _FindEnemyList()
    {
        return FindChild("EnemyList", true, false) as HBoxContainer;
    }

    // ============================================================================
    // 缩略图条目创建
    // ============================================================================

    private PanelContainer _CreateThumbnailEntry(Unit unit, bool isEnemy)
    {
        var entry = new PanelContainer();
        entry.SetMeta("unit_ref", unit);
        entry.CustomMinimumSize = new Vector2(60, 70);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        style.SetBorderWidthAll(2);
        style.BorderColor = isEnemy ? Theme.BorderEnemy : Theme.BorderFriendly;
        entry.AddThemeStyleboxOverride("panel", style);

        var v = new VBoxContainer();
        entry.AddChild(v);

        var name = new Label();
        var unitName = unit?.Data?.UnitName ?? "";
        name.Text = unitName.Length > 0
            ? unitName.Substring(0, Mathf.Min(3, unitName.Length))
            : "???";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(name);

        var hp = new ProgressBar();
        hp.CustomMinimumSize = new Vector2(50, 5);
        hp.MaxValue = unit?.GetMaxHp() ?? 10;
        hp.Value = unit?.CurrentHp ?? 10;
        v.AddChild(hp);

        return entry;
    }

    // ============================================================================
    // 公共 API — 由 CombatManager (C#) 及 战斗场景调用
    // ============================================================================

    /// <summary>用当前选中单位刷新底部信息面板</summary>
    public void UpdateUnitInfo(Unit? unit)
    {
        if (unit == null || !GodotObject.IsInstanceValid(unit) || unit.Data == null)
            return;

        _currentDisplayedUnit = unit;

        var data = unit.Data;

        // 头像 — 通过统一渲染组件展示当前选中单位
        _avatarControl?.SetUnit(data, !unit.UsingPrimaryWeapon);

        if (_statLabels.TryGetValue("char_name", out var ctrl) && ctrl is Label charLabel)
            charLabel.Text = data.UnitName;

        // HP 条
        if (_statLabels.TryGetValue("hp_bar", out ctrl) && ctrl is ProgressBar hpBar)
        {
            hpBar.MaxValue = unit.GetMaxHp();
            hpBar.Value = unit.CurrentHp;
        }

        // DR(装甲)条 — 有装甲时显示，无装甲隐藏
        if (_drBar != null)
        {
            int dr = unit.GetDr();
            int maxDr = unit.GetMaxDr();
            if (maxDr > 0)
            {
                _drBar.Visible = true;
                _drBar.MaxValue = maxDr;
                _drBar.Value = dr;
            }
            else
            {
                _drBar.Visible = false;
            }
        }

        // MP(法力)条 — 有法力(持有法系武器或已知法术)时显示
        if (_statLabels.TryGetValue("mp_bar", out ctrl) && ctrl is ProgressBar mpBar)
        {
            bool hasMana = (data.KnownSpells != null && data.KnownSpells.Count > 0)
                || (data.PrimaryMainHand is WeaponData pw && pw.IsCatalyst)
                || (data.SecondaryMainHand is WeaponData sw && sw.IsCatalyst)
                || data.CurrentMana > 0;
            if (hasMana)
            {
                mpBar.Visible = true;
                mpBar.MaxValue = Mathf.Max(data.CurrentMana, 1);
                mpBar.Value = data.CurrentMana;
            }
            else
            {
                mpBar.Visible = false;
            }
        }

        // AP(行动力)条
        if (_apBar != null)
        {
            _apBar.MaxValue = unit.Model.GetMaxAp();
            _apBar.Value = unit.CurrentAp;
        }

        if (_weaponPrimaryLabel != null)
            _weaponPrimaryLabel.Text = data.PrimaryMainHand?.ItemName ?? "徒手";

        if (_weaponSecondaryLabel != null)
            _weaponSecondaryLabel.Text = data.SecondaryMainHand?.ItemName ?? "无";

        // 刷新快捷技能槽
        RefreshQuickSlots(unit);
    }

    /// <summary>写入战斗日志</summary>
    public void LogMessage(string msg)
    {
        _battleLog?.AddEntry(msg);
    }

    /// <summary>设置回合阶段文字（显示在顶部战力条下方）</summary>
    public void SetTurnText(string text, Color? color = null)
    {
        if (_turnPhaseLabel != null)
        {
            _turnPhaseLabel.Text = text;
            _turnPhaseLabel.AddThemeColorOverride("font_color", color ?? Theme.TextAccent);
        }
    }

    /// <summary>显示 / 隐藏底部操作面板的交互元素（技能槽、武器切换）</summary>
    public void SetActionBarVisible(bool visible)
    {
        // 底部面板始终可见（显示当前行动单位信息）
        // 仅控制技能槽的可交互状态
        if (_quickSlotContainer != null)
        {
            float alpha = visible ? 1.0f : 0.4f;
            _quickSlotContainer.Modulate = new Color(1, 1, 1, alpha);
            _quickSlotContainer.MouseFilter = visible
                ? Control.MouseFilterEnum.Stop
                : Control.MouseFilterEnum.Ignore;
        }
    }

    /// <summary>显示命中预览提示</summary>
    public void ShowHitPreview(Vector2 mousePos, Unit attacker, Unit target)
    {
        if (_hitPreviewTooltip != null && attacker != null && target != null)
            _hitPreviewTooltip.ShowPreview(attacker, target);
    }

    /// <summary>显示超出射程预览（命中率 0%）</summary>
    public void ShowOutOfRangePreview(Vector2 mousePos, Unit target, int distance, int maxRange)
    {
        if (_hitPreviewTooltip != null && target != null)
            _hitPreviewTooltip.ShowOutOfRange(target, distance, maxRange);
    }

    /// <summary>隐藏命中预览</summary>
    public void HideHitPreview()
    {
        if (_hitPreviewTooltip != null && _hitPreviewTooltip.Visible)
            _hitPreviewTooltip.Visible = false;
    }

    /// <summary>打开法术选择面板</summary>
    public void OpenSpellPanel(Unit unit, SpellManager spellManager)
    {
        if (_spellSelect == null)
        {
            var panel = new SpellSelectionPanel();
            panel.SpellChosen += (spell) => EmitSignal(SignalName.SpellSelected, spell);
            AddChild(panel);
            _spellSelect = panel;
        }

        if (_spellSelect is SpellSelectionPanel sp)
        {
            sp.ShowForUnit(unit, spellManager);
        }
    }

    /// <summary>关闭法术选择面板</summary>
    public void CloseSpellPanel()
    {
        if (_spellSelect != null && _spellSelect is CanvasItem ci)
            ci.Visible = false;
    }

    /// <summary>显示地形信息提示</summary>
    public void ShowTerrainInfo(string text)
    {
        _terrainTooltip?.ShowRichText(text);
        SetMeta("terrain_info_open", true);
    }

    /// <summary>隐藏地形信息提示</summary>
    public void HideTerrainInfo()
    {
        _terrainTooltip?.HideTooltip();
        SetMeta("terrain_info_open", false);
    }

    /// <summary>注册一个友方单位到左侧列表</summary>
    public void RegisterAlly(Unit unit)
    {
        var list = _FindAllyList();
        if (list != null)
            list.AddChild(_CreateThumbnailEntry(unit, false));
    }

    /// <summary>注册一个敌方单位到右侧列表和信息面板</summary>
    public void RegisterEnemy(Unit unit)
    {
        var list = _FindEnemyList();
        if (list != null)
            list.AddChild(_CreateThumbnailEntry(unit, true));

        _enemyInfoPanel?.AddEnemy(unit);
    }

    /// <summary>更新敌方信息面板中的指定单位</summary>
    public void UpdateEnemyInfo(Unit unit)
    {
        _enemyInfoPanel?.UpdateEnemy(unit);
    }

    /// <summary>从敌方信息面板移除指定单位</summary>
    public void RemoveEnemy(Unit unit)
    {
        _enemyInfoPanel?.RemoveEnemy(unit);
    }

    /// <summary>打开径向菜单（防御 / 等待 / 取消）</summary>
    public void OpenRadialMenu(Vector2 pos, Unit unit, Node? sm = null, Node? tu = null)
    {
        var opts = new Godot.Collections.Dictionary
        {
            ["防御"] = "defend",
            ["等待"] = "wait",
            ["取消"] = "none"
        };
        _radialMenu?.Setup(opts);
        _radialMenu?.ShowMenu(pos);
    }

    /// <summary>打开自定义选项的径向菜单</summary>
    public void OpenRadialMenuCustom(Vector2 pos, Godot.Collections.Dictionary options)
    {
        _radialMenu?.Setup(options);
        _radialMenu?.ShowMenu(pos);
    }

    /// <summary>更新回合顺序栏</summary>
    public void UpdateTurnOrder(Godot.Collections.Array units, Unit? active, int turn)
    {
        _turnOrderBar?.SetTurnNumber(turn);
        if (units != null)
        {
            var unitList = new System.Collections.Generic.List<Unit>();
            foreach (var v in units)
            {
                var u = v.As<Unit>();
                if (u != null) unitList.Add(u);
            }
            _turnOrderBar?.SetUnitOrder(unitList, active);
        }
    }

    /// <summary>
    /// 把小地图嵌入底部面板最右侧。
    /// </summary>
    public void EmbedMinimap(Control minimap)
    {
        if (_bottomHBox == null || minimap == null) return;

        var existingParent = minimap.GetParent();
        existingParent?.RemoveChild(minimap);

        minimap.SizeFlagsVertical = Control.SizeFlags.Fill;
        minimap.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        minimap.MouseFilter = Control.MouseFilterEnum.Pass;

        _bottomHBox.AddChild(minimap);
    }

    /// <summary>在指定位置显示单位检视面板</summary>
    public void ShowUnitInspect(Unit unit, Vector2 screenPos)
    {
        _unitInspect?.ShowForUnit(unit, screenPos);
    }

    /// <summary>隐藏单位检视面板</summary>
    public void HideUnitInspect()
    {
        _unitInspect?.HidePanel();
    }

    /// <summary>初始化战力对比面板（战斗开始时调用）</summary>
    public void InitializePowerBar(System.Collections.Generic.IEnumerable<Unit> playerUnits, System.Collections.Generic.IEnumerable<Unit> enemyUnits)
    {
        _powerBar?.Initialize(playerUnits, enemyUnits);
    }

    /// <summary>刷新战力对比面板（单位死亡/脱离时调用）</summary>
    public void RefreshPowerBar(System.Collections.Generic.IEnumerable<Unit> playerUnits, System.Collections.Generic.IEnumerable<Unit> enemyUnits)
    {
        _powerBar?.Refresh(playerUnits, enemyUnits);
    }

    // ============================================================================
    // 输入处理 — 左键关闭地形信息面板
    // ============================================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (HasMeta("terrain_info_open") && (bool)GetMeta("terrain_info_open"))
            {
                HideTerrainInfo();
            }
        }
    }

    // ============================================================================
    // 信号转发
    // ============================================================================

    private void _OnRadialMenuActionSelected(string action)
    {
        EmitSignal(SignalName.ActionSelected, action);
    }

    private void _OnRadialMenuActionHovered(string action)
    {
        EmitSignal(SignalName.ActionHovered, action);
    }

    /// <summary>战斗倍率变化时刷新按钮显示</summary>
    private void _OnSpeedChanged(float multiplier)
    {
        if (_speedBtn != null && GodotObject.IsInstanceValid(_speedBtn))
            _speedBtn.Text = $"快进 {multiplier:0}×";
    }

    // ============================================================================
    // 快捷技能槽
    // ============================================================================

    /// <summary>当选中单位变化时,把该单位的主动技能绑到快捷槽</summary>
    public void RefreshQuickSlots(Unit? unit)
    {
        if (_quickSlots == null || _quickSlotSkills == null || _quickSlotDescriptions == null) return;

        // 清空
        for (int i = 0; i < 10; i++)
        {
            _quickSlotSkills[i] = null;
            _quickSlotDescriptions[i] = null;
            _quickSlots[i].Text = i < 9 ? $"{i + 1}" : "0";
            _quickSlots[i].TooltipText = "空槽位";
            var mod = _quickSlots[i].Modulate;
            mod.A = 0.4f;
            _quickSlots[i].Modulate = mod;
        }

        if (unit?.SkillTree == null) return;

        var activeSkills = unit.SkillTree.GetActiveSkills();
        int slotIdx = 0;
        foreach (var skillNode in activeSkills)
        {
            if (slotIdx >= 10) break;
            if (string.IsNullOrEmpty(skillNode.SkillEffect)) continue;

            _quickSlotSkills[slotIdx] = $"skill_{skillNode.SkillEffect}";
            _quickSlotDescriptions[slotIdx] = GetSkillDescription(skillNode.SkillEffect, skillNode.NodeName);
            _quickSlots[slotIdx].Text = skillNode.NodeName.Length > 3
                ? skillNode.NodeName[..3]
                : skillNode.NodeName;
            _quickSlots[slotIdx].TooltipText = skillNode.NodeName;
            var mod = _quickSlots[slotIdx].Modulate;
            mod.A = 1.0f;
            _quickSlots[slotIdx].Modulate = mod;
            slotIdx++;
        }

        // 职业技能
        var careerSkill = unit.GetCareerSkill();
        if (careerSkill != null && slotIdx < 10)
        {
            _quickSlotSkills[slotIdx] = "career_skill";
            _quickSlotDescriptions[slotIdx] = $"[b]{careerSkill.DisplayName}[/b]\n职业技能\nAP消耗: {careerSkill.ApCost}";
            _quickSlots[slotIdx].Text = careerSkill.DisplayName.Length > 3
                ? careerSkill.DisplayName[..3]
                : careerSkill.DisplayName;
            _quickSlots[slotIdx].TooltipText = careerSkill.DisplayName;
            var mod = _quickSlots[slotIdx].Modulate;
            mod.A = 1.0f;
            _quickSlots[slotIdx].Modulate = mod;
        }
    }

    /// <summary>按快捷键触发对应槽位</summary>
    public void TriggerQuickSlot(int slotIndex)
    {
        if (_quickSlotSkills == null || slotIndex < 0 || slotIndex >= 10) return;
        var action = _quickSlotSkills[slotIndex];
        if (string.IsNullOrEmpty(action)) return;
        EmitSignal(SignalName.ActionSelected, action);
    }

    private void _OnQuickSlotPressed(int slotIndex)
    {
        BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_click");
        TriggerQuickSlot(slotIndex);
    }

    /// <summary>右键技能槽显示详情 tooltip</summary>
    private void _ShowSkillTooltip(int slotIndex, Vector2 mousePos)
    {
        if (_quickSlotDescriptions == null || slotIndex < 0 || slotIndex >= 10) return;
        var desc = _quickSlotDescriptions[slotIndex];
        if (string.IsNullOrEmpty(desc)) return;

        if (_skillTooltip == null)
        {
            _skillTooltip = new PanelContainer();
            _skillTooltip.TopLevel = true;
            _skillTooltip.ZIndex = 100;
            _skillTooltip.MouseFilter = Control.MouseFilterEnum.Ignore;
            _skillTooltip.CustomMinimumSize = new Vector2(280, 0);
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.97f);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.55f, 0.45f, 0.30f, 0.9f);
            style.SetCornerRadiusAll(6);
            style.SetContentMarginAll(14);
            style.ShadowColor = new Color(0, 0, 0, 0.5f);
            style.ShadowSize = 6;
            _skillTooltip.AddThemeStyleboxOverride("panel", style);

            var rtl = new RichTextLabel();
            rtl.BbcodeEnabled = true;
            rtl.ScrollActive = false;
            rtl.FitContent = true;
            rtl.CustomMinimumSize = new Vector2(252, 0);
            rtl.MouseFilter = Control.MouseFilterEnum.Ignore;
            rtl.AddThemeFontSizeOverride("normal_font_size", 14);
            rtl.AddThemeFontSizeOverride("bold_font_size", 16);
            rtl.Name = "Text";
            _skillTooltip.AddChild(rtl);
            AddChild(_skillTooltip);
        }

        var textNode = _skillTooltip.GetNode<RichTextLabel>("Text");
        textNode.Text = desc;
        _skillTooltip.Visible = true;

        // 定位在鼠标上方
        var vpSize = GetViewport().GetVisibleRect().Size;
        float px = mousePos.X - 140;
        float py = mousePos.Y - 160;
        if (px < 8) px = 8;
        if (px + 280 > vpSize.X) px = vpSize.X - 288;
        if (py < 8) py = mousePos.Y + 24;
        _skillTooltip.GlobalPosition = new Vector2(px, py);
    }

    /// <summary>从 SkillRegistry 获取技能描述(格式化为美观的 BBCode)</summary>
    private static string GetSkillDescription(string skillEffect, string nodeName)
    {
        var registry = BladeHex.Combat.SkillRegistry.GetSkillConfig(skillEffect);
        if (registry != null && registry.Count > 0)
        {
            string name = registry.ContainsKey("name") ? registry["name"].AsString() : nodeName;
            string desc = registry.ContainsKey("description") ? registry["description"].AsString() : "";
            int apCost = registry.ContainsKey("action_cost") ? registry["action_cost"].AsInt32() : 0;

            // 目标类型
            string targetText = "";
            if (registry.ContainsKey("target"))
            {
                int targetType = registry["target"].AsInt32();
                targetText = targetType switch
                {
                    0 => "单体敌人",
                    1 => "单体友军",
                    2 => "自身",
                    3 => "全体敌人",
                    4 => "全体友军",
                    5 => "区域",
                    _ => "",
                };
            }

            // 组装 BBCode
            var sb = new System.Text.StringBuilder();
            // 标题行(金色粗体)
            sb.Append($"[color=#e8c864][b]{name}[/b][/color]\n");
            // 分隔线
            sb.Append("[color=#555]────────────────────[/color]\n");
            // 描述(白色)
            if (!string.IsNullOrEmpty(desc))
                sb.Append($"[color=#ddd]{desc}[/color]\n\n");
            // 属性行(灰标签 + 亮值)
            if (apCost > 0)
                sb.Append($"[color=#999]AP消耗:[/color]  [color=#7cf]{apCost}[/color]\n");
            if (!string.IsNullOrEmpty(targetText))
                sb.Append($"[color=#999]目标:[/color]    [color=#aed]{targetText}[/color]\n");

            return sb.ToString().TrimEnd();
        }
        return $"[color=#e8c864][b]{nodeName}[/b][/color]\n[color=#888](无详细描述)[/color]";
    }

    public override void _ExitTree()
    {
        BladeHex.View.Combat.CombatSpeed.MultiplierChanged -= _OnSpeedChanged;
    }
}
