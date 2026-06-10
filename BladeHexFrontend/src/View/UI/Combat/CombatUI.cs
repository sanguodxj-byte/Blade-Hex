// CombatUI.cs
// 战术战斗主用户界面
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.UI;
using BladeHex.Map;

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
    private SkillPreviewTooltip _skillPreviewTooltip = null!;
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
    private ProgressBar? _apBgBar;      // 稳定的暗褐色背景条
    private ProgressBar? _apPreviewBar; // 顶层稳定黄褐色剩余AP条

    // 顶部回合提示
    private Label? _turnPhaseLabel;

    // 快捷技能槽
    private GridContainer? _quickSlotContainer;
    private Button[]? _quickSlots;
    private string?[]? _quickSlotSkills;
    private string?[]? _quickSlotDescriptions;
    private PanelContainer? _skillTooltip;

    // 职业技能UI
    private PanelContainer? _careerSkillPanel;
    private Label? _careerSkillNameLabel;
    private Label? _careerSkillApLabel;

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
    private static readonly Color CombatHudBg = new(0.075f, 0.065f, 0.055f, 0.94f);
    private static readonly Color CombatHudBgSoft = new(0.10f, 0.085f, 0.070f, 0.90f);
    private static readonly Color CombatHudBorder = new(0.43f, 0.35f, 0.24f, 0.82f);
    private static readonly Color CombatHudBorderDim = new(0.25f, 0.22f, 0.18f, 0.72f);
    private static readonly Color CombatHudGold = new(0.78f, 0.62f, 0.34f, 0.95f);

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
        _ApplyCombatButtonTheme(_speedBtn, false);
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
        _ApplyCombatButtonTheme(endTurnBtn, true);
        turnBarRow.AddChild(endTurnBtn);

        // --- 4. 底部信息面板 (居中，紧贴回合顺序栏下方) ---
        _bottomPanel = _CreatePanel(Vector2.Zero, CombatHudBg, CombatHudBorder);
        _bottomPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _bottomPanel.MouseFilter = Control.MouseFilterEnum.Pass;
        mainVBox.AddChild(_bottomPanel);

        var bottomMargin = _CreateMargin(12, 12, 6, 6);
        bottomMargin.MouseFilter = Control.MouseFilterEnum.Pass;
        _bottomPanel.AddChild(bottomMargin);

        var bottomHBox = new HBoxContainer();
        bottomHBox.AddThemeConstantOverride("separation", Theme.SpacingLg);
        bottomHBox.MouseFilter = Control.MouseFilterEnum.Pass;
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

        if (Theme.CombatPortraitFrameTexture != null)
        {
            var frameOverlay = new TextureRect();
            frameOverlay.Texture = Theme.CombatPortraitFrameTexture;
            frameOverlay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            frameOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            frameOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
            avatarBg.AddChild(frameOverlay);
        }

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5b. 角色信息列 — 名称居中 + 条件资源条(HP/DR/MP/AP)
        var infoCol = new VBoxContainer();
        infoCol.AddThemeConstantOverride("separation", 3);
        infoCol.CustomMinimumSize = new Vector2(150, 0);
        infoCol.MouseFilter = Control.MouseFilterEnum.Pass;
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

        // AP(行动力)条（三层重叠：背景层、中间呼吸闪烁消耗层、顶层稳定剩余AP层）
        var apContainer = new MarginContainer();
        apContainer.CustomMinimumSize = new Vector2(150, 7);

        // 1. 背景层：提供稳定的背景，防止呼吸淡入淡出时背景闪烁
        _apBgBar = new ProgressBar();
        _apBgBar.CustomMinimumSize = new Vector2(150, 7);
        _apBgBar.ShowPercentage = false;
        Theme.ApplyBarTheme(_apBgBar, new Color(0, 0, 0, 0f), new Color(0.15f, 0.12f, 0.05f, 0.6f));
        _apBgBar.Value = 0; // 只显示背景
        apContainer.AddChild(_apBgBar);

        // 2. 中间层：在有预览时充当 CurrentAp 的橙红色呼吸条，平常为正常黄褐色 AP 条
        _apBar = new ProgressBar();
        _apBar.CustomMinimumSize = new Vector2(150, 7);
        _apBar.ShowPercentage = false;
        Theme.ApplyBarTheme(_apBar, new Color(0.85f, 0.75f, 0.3f), new Color(0, 0, 0, 0f)); // 背景透明
        apContainer.AddChild(_apBar);

        // 3. 顶层：在有预览时作为稳定黄褐色剩余 AP 段（CurrentAp - Cost），背景透明
        _apPreviewBar = new ProgressBar();
        _apPreviewBar.CustomMinimumSize = new Vector2(150, 7);
        _apPreviewBar.ShowPercentage = false;
        Theme.ApplyBarTheme(_apPreviewBar, new Color(0.85f, 0.75f, 0.3f), new Color(0, 0, 0, 0f)); // 背景透明
        _apPreviewBar.Value = 0; // 默认不覆盖
        apContainer.AddChild(_apPreviewBar);

        infoCol.AddChild(apContainer);
        _statLabels["ap_bar"] = _apBar;

        // 5e. 职业技能栏 — 角色信息列最底部，紧贴AP条下方
        _careerSkillPanel = new PanelContainer();
        _careerSkillPanel.Visible = false;
        _careerSkillPanel.CustomMinimumSize = new Vector2(200, 36);
        _careerSkillPanel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        var careerStyle = new StyleBoxFlat();
        careerStyle.BgColor = new Color(0.18f, 0.14f, 0.08f, 0.92f);
        careerStyle.SetBorderWidthAll(2);
        careerStyle.BorderColor = new Color(0.8f, 0.6f, 0.2f, 0.9f);
        careerStyle.SetCornerRadiusAll(4);
        careerStyle.SetContentMarginAll(6);
        _careerSkillPanel.AddThemeStyleboxOverride("panel", careerStyle);

        var careerHBox = new HBoxContainer();
        careerHBox.AddThemeConstantOverride("separation", 8);
        careerHBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _careerSkillPanel.AddChild(careerHBox);

        // 前缀标签 "职业"
        var prefixLabel = new Label();
        prefixLabel.Text = "[职业]";
        prefixLabel.AddThemeFontSizeOverride("font_size", 12);
        prefixLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.6f, 0.2f));
        careerHBox.AddChild(prefixLabel);

        // 技能名
        _careerSkillNameLabel = new Label();
        _careerSkillNameLabel.AddThemeFontSizeOverride("font_size", 13);
        _careerSkillNameLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.5f));
        careerHBox.AddChild(_careerSkillNameLabel);

        // AP消耗
        _careerSkillApLabel = new Label();
        _careerSkillApLabel.AddThemeFontSizeOverride("font_size", 11);
        _careerSkillApLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1.0f));
        careerHBox.AddChild(_careerSkillApLabel);

        // 点击事件 — 左键激活，右键显示描述
        _careerSkillPanel.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    GD.Print("[SkillTarget] CombatUI: career_skill panel clicked!");
                    BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_click");
                    EmitSignal(SignalName.ActionSelected, "career_skill");
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _ShowCareerSkillPopup(_currentDisplayedUnit?.GetCareerSkill(), mb.GlobalPosition);
                }
            }
        };
        _careerSkillPanel.MouseEntered += () =>
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_hover", -6.0f);
            EmitSignal(SignalName.ActionHovered, "career_skill");
        };
        _careerSkillPanel.MouseExited += () =>
        {
            EmitSignal(SignalName.ActionHovered, "none");
        };

        infoCol.AddChild(_careerSkillPanel);

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5c. 武器槽
        var weaponVBox = new VBoxContainer();
        weaponVBox.Alignment = BoxContainer.AlignmentMode.Center;
        weaponVBox.AddThemeConstantOverride("separation", 4);
        bottomHBox.AddChild(weaponVBox);

        var mainHand = _CreateCard(new Vector2(64, 64), true);
        mainHand.AddThemeStyleboxOverride("panel", _CreateSlotStyle(false));
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
        mainHand.MouseEntered += () => EmitSignal(SignalName.ActionHovered, "switch_to_primary");
        mainHand.MouseExited += () => {
            _itemPopup?.Hide();
            EmitSignal(SignalName.ActionHovered, "none");
        };
        weaponVBox.AddChild(mainHand);
        _weaponPrimaryLabel = _CreateMutedLabel("主手");
        _weaponPrimaryLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        _weaponPrimaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _weaponPrimaryLabel.VerticalAlignment = VerticalAlignment.Center;
        _weaponPrimaryLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainHand.AddChild(_weaponPrimaryLabel);

        var offHand = _CreateCard(new Vector2(64, 64), true);
        offHand.AddThemeStyleboxOverride("panel", _CreateSlotStyle(false));
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
        offHand.MouseEntered += () => EmitSignal(SignalName.ActionHovered, "switch_to_secondary");
        offHand.MouseExited += () => {
            _itemPopup?.Hide();
            EmitSignal(SignalName.ActionHovered, "none");
        };
        weaponVBox.AddChild(offHand);
        _weaponSecondaryLabel = _CreateMutedLabel("副手");
        _weaponSecondaryLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        _weaponSecondaryLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _weaponSecondaryLabel.VerticalAlignment = VerticalAlignment.Center;
        _weaponSecondaryLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        offHand.AddChild(_weaponSecondaryLabel);

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5d. 快捷技能槽(1-0 快捷键,共 10 个) — 5 列 2 行网格
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
            
            slot.AddThemeStyleboxOverride("normal", _CreateSlotStyle(false));
            slot.AddThemeStyleboxOverride("hover", _CreateSlotStyle(true));
            slot.AddThemeStyleboxOverride("pressed", _CreateSlotStyle(false, true));
            slot.AddThemeStyleboxOverride("disabled", _CreateSlotStyle(false));
            slot.AddThemeColorOverride("font_color", Theme.TextSecondary);
            slot.AddThemeColorOverride("font_hover_color", Theme.TextAccent);
            slot.AddThemeColorOverride("font_pressed_color", Theme.TextPrimary);
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
            slot.MouseEntered += () => {
                if (_quickSlotSkills != null && slotIdx < _quickSlotSkills.Length && !string.IsNullOrEmpty(_quickSlotSkills[slotIdx]))
                {
                    EmitSignal(SignalName.ActionHovered, _quickSlotSkills[slotIdx]!);
                }
            };
            slot.MouseExited += () => {
                _skillTooltip?.Hide();
                EmitSignal(SignalName.ActionHovered, "none");
            };
            _quickSlotContainer.AddChild(slot);
            _quickSlots[i] = slot;
        }

        // --- 5. 战斗日志面板 (左上角，绝对定位) ---

        // 战斗日志 — 使用绝对定位，不影响主布局
        _battleLog = new BattleLogPanel();
        _battleLog.CustomMinimumSize = new Vector2(350, 160);
        _battleLog.AnchorLeft = 0;
        _battleLog.AnchorTop = 0;
        _battleLog.AnchorRight = 0;
        _battleLog.AnchorBottom = 0;
        _battleLog.OffsetLeft = Theme.SpacingMd;
        _battleLog.OffsetTop = Theme.SpacingMd;
        _battleLog.OffsetRight = Theme.SpacingMd + 350;
        _battleLog.OffsetBottom = Theme.SpacingMd + 160;
        _battleLog.Visible = true;
        AddChild(_battleLog);  // 添加到根节点，而不是 mainVBox

        // 敌方信息面板 — 隐藏但保留注册/更新功能
        _enemyInfoPanel = new EnemyInfoPanel();
        _enemyInfoPanel.CustomMinimumSize = new Vector2(280, 0);
        _enemyInfoPanel.Visible = false;
        AddChild(_enemyInfoPanel);

        // --- 6. 悬浮/弹出组件 ---
        _hitPreviewTooltip = new HitPreviewTooltip();
        _hitPreviewTooltip.Visible = false;
        AddChild(_hitPreviewTooltip);

        _skillPreviewTooltip = new SkillPreviewTooltip();
        _skillPreviewTooltip.Visible = false;
        AddChild(_skillPreviewTooltip);

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

    /// <summary>右键职业技能弹出技能描述</summary>
    private void _ShowCareerSkillPopup(BladeHex.Strategic.CareerSkillData? career, Vector2 globalMousePos)
    {
        if (career == null) return;

        // 构建描述文本
        string desc = $"[b][color=#FFD700]{career.DisplayName}[/color][/b]\n";
        desc += $"[color=#AAAAAA]职业技能[/color]\n\n";

        if (!string.IsNullOrEmpty(career.Description))
        {
            desc += $"{career.Description}\n\n";
        }

        desc += $"[color=#87CEEB]AP消耗: {career.ApCost}[/color]\n";
        desc += $"[color=#87CEEB]使用限制: 每场战斗1次[/color]\n";

        // 使用 HitPreviewTooltip 显示（复用现有组件）
        if (_hitPreviewTooltip != null)
        {
            _hitPreviewTooltip.ShowCareerSkillPreview(desc, globalMousePos);
        }
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

        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.SetBorderWidthAll(1);
        style.BorderColor = border;
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(Theme.SpacingMd);
        style.ShadowColor = new Color(0, 0, 0, 0.38f);
        style.ShadowSize = 8;
        style.ShadowOffset = new Vector2(0, 2);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private PanelContainer _CreateCard(Vector2 minSize, bool hoverable)
    {
        var card = new PanelContainer();
        if (minSize != Vector2.Zero)
            card.CustomMinimumSize = minSize;
        card.AddThemeStyleboxOverride("panel", _CreateSlotStyle(false));
        if (hoverable)
            card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        return card;
    }

    private StyleBoxFlat _CreateSlotStyle(bool hover, bool pressed = false)
    {
        var style = new StyleBoxFlat();
        style.BgColor = pressed
            ? new Color(0.045f, 0.040f, 0.036f, 0.96f)
            : hover
                ? new Color(0.135f, 0.110f, 0.082f, 0.96f)
                : new Color(0.075f, 0.066f, 0.058f, 0.92f);
        style.SetBorderWidthAll(1);
        style.BorderColor = hover ? CombatHudGold : CombatHudBorderDim;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(5);
        return style;
    }

    private void _ApplyCombatButtonTheme(Button button, bool primary)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = primary ? new Color(0.13f, 0.105f, 0.072f, 0.94f) : CombatHudBgSoft,
            BorderColor = primary ? CombatHudGold : CombatHudBorder,
        };
        normal.SetBorderWidthAll(1);
        normal.SetCornerRadiusAll(5);
        normal.SetContentMarginAll(8);

        var hover = new StyleBoxFlat
        {
            BgColor = primary ? new Color(0.19f, 0.145f, 0.085f, 0.98f) : new Color(0.14f, 0.12f, 0.095f, 0.96f),
            BorderColor = CombatHudGold,
        };
        hover.SetBorderWidthAll(1);
        hover.SetCornerRadiusAll(5);
        hover.SetContentMarginAll(8);

        var pressed = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.048f, 0.042f, 0.98f),
            BorderColor = new Color(0.54f, 0.43f, 0.24f, 0.95f),
        };
        pressed.SetBorderWidthAll(1);
        pressed.SetCornerRadiusAll(5);
        pressed.SetContentMarginAll(8);

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeColorOverride("font_color", Theme.TextPrimary);
        button.AddThemeColorOverride("font_hover_color", Theme.TextAccent);
        button.AddThemeColorOverride("font_pressed_color", Theme.TextSecondary);
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
        style.BgColor = CombatHudBorderDim;
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
    // AP 渐变淡入淡出消耗预览逻辑
    // ============================================================================
    private float _apPreviewCost = 0f;

    /// <summary>设置 AP 消耗预览。设置大于0的值开始渐变淡入淡出，设置0清除</summary>
    public void SetApPreview(float apCost)
    {
        if (_currentDisplayedUnit == null || _apBar == null || _apPreviewBar == null)
            return;

        float currentAp = _currentDisplayedUnit.CurrentAp;
        float maxAp = _apBar.MaxValue > 0 ? (float)_apBar.MaxValue : _currentDisplayedUnit.Model.GetMaxAp();

        // 确保 MaxValue 保持一致
        _apBar.MaxValue = maxAp;
        _apPreviewBar.MaxValue = maxAp;
        if (_apBgBar != null) _apBgBar.MaxValue = maxAp;

        if (apCost > 0f)
        {
            if (_apPreviewCost <= 0f || _apPreviewCost != apCost)
            {
                _apPreviewCost = apCost;
                
                // 将中间层 _apBar 的前景色设为橙红色，展示真实 AP 长度，用于呼吸渐变消耗段
                Theme.ApplyBarTheme(_apBar, new Color(0.9f, 0.3f, 0.2f, 0.9f), new Color(0, 0, 0, 0f));
                _apBar.Value = currentAp;

                // 将顶层 _apPreviewBar 的前景色设为稳定黄褐色，展示扣减后的剩余 AP
                Theme.ApplyBarTheme(_apPreviewBar, new Color(0.85f, 0.75f, 0.3f), new Color(0, 0, 0, 0f));
                _apPreviewBar.Value = Mathf.Max(0f, currentAp - apCost);
            }
        }
        else
        {
            _apPreviewCost = 0f;
            _apBar.Modulate = new Color(1f, 1f, 1f, 1f); // 恢复完全不透明
            
            // 恢复 _apBar 为正常的黄褐色，且值为真实 AP
            Theme.ApplyBarTheme(_apBar, new Color(0.85f, 0.75f, 0.3f), new Color(0, 0, 0, 0f));
            _apBar.Value = currentAp;

            // 隐藏顶层条
            _apPreviewBar.Value = 0f;
        }
    }

    public override void _Process(double delta)
    {
        if (_apPreviewCost > 0f && _currentDisplayedUnit != null && _apBar != null)
        {
            double t = Time.GetTicksMsec() / 1000.0;
            // 平滑正弦呼吸：透明度在 0.25 到 0.85 之间循环
            float pulseAlpha = 0.25f + 0.6f * (float)(Mathf.Sin(t * 6.0) + 1.0) * 0.5f;
            _apBar.Modulate = new Color(1f, 1f, 1f, pulseAlpha);
        }

        UpdateHudOcclusion(delta);
    }

    // ============================================================================
    // 底部 HUD 遮挡半透明
    // ============================================================================
    private Camera3D? _occlusionCamera;
    private System.Collections.Generic.List<Unit>? _occlusionUnits;
    private float _hudTargetAlpha = 1f;
    private float _hudCurrentAlpha = 1f;
    private const float HudOpaqueAlpha = 1f;
    private const float HudTransparentAlpha = 0.22f;
    private const float HudSoftTransparentAlpha = 0.45f;
    private const float HudFadeSpeed = 6f; // 每秒变化速率

    /// <summary>
    /// 注入遮挡检测所需的相机和单位列表。
    /// 由 CombatSceneBase 在初始化时调用。
    /// </summary>
    public void SetOcclusionSources(Camera3D camera, System.Collections.Generic.List<Unit> units)
    {
        _occlusionCamera = camera;
        _occlusionUnits = units;
    }

    /// <summary>检测是否有单位被底部 HUD 遮挡，并平滑调整透明度</summary>
    private void UpdateHudOcclusion(double delta)
    {
        if (_bottomPanel == null || _occlusionCamera == null || _occlusionUnits == null) return;

        var panelRect = _bottomPanel.GetGlobalRect();
        var softRect = panelRect.Grow(48f);
        var hardRect = panelRect.Grow(12f);

        float targetAlpha = HudOpaqueAlpha;
        foreach (var unit in _occlusionUnits)
        {
            if (unit == null || !GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;

            if (!TryGetUnitScreenRect(unit, out var unitRect)) continue;

            if (hardRect.Intersects(unitRect))
            {
                targetAlpha = HudTransparentAlpha;
                break;
            }
            if (softRect.Intersects(unitRect))
                targetAlpha = Mathf.Min(targetAlpha, HudSoftTransparentAlpha);
        }

        _hudTargetAlpha = targetAlpha;

        // 平滑过渡
        _hudCurrentAlpha = Mathf.MoveToward(_hudCurrentAlpha, _hudTargetAlpha, (float)delta * HudFadeSpeed);
        _bottomPanel.Modulate = new Color(1f, 1f, 1f, _hudCurrentAlpha);
    }

    private bool TryGetUnitScreenRect(Unit unit, out Rect2 rect)
    {
        rect = default;
        if (_occlusionCamera == null) return false;

        var points = new[]
        {
            unit.GlobalPosition,
            unit.GlobalPosition + new Vector3(0f, 36f, 0f),
            unit.GlobalPosition + new Vector3(0f, 72f, 0f),
        };

        bool any = false;
        Vector2 min = Vector2.Zero;
        Vector2 max = Vector2.Zero;

        foreach (var point in points)
        {
            if (!_occlusionCamera.IsPositionInFrustum(point)) continue;

            var screenPos = _occlusionCamera.UnprojectPosition(point);
            if (!any)
            {
                min = screenPos;
                max = screenPos;
                any = true;
            }
            else
            {
                min = new Vector2(Mathf.Min(min.X, screenPos.X), Mathf.Min(min.Y, screenPos.Y));
                max = new Vector2(Mathf.Max(max.X, screenPos.X), Mathf.Max(max.Y, screenPos.Y));
            }
        }

        if (!any) return false;

        rect = new Rect2(min, max - min).Grow(28f);
        return true;
    }

    /// <summary>用当前选中单位刷新底部信息面板</summary>
    public void UpdateUnitInfo(Unit? unit)
    {
        SetApPreview(0f); // 切换单位时重置闪烁

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
            float maxAp = unit.Model.GetMaxAp();
            _apBar.MaxValue = maxAp;
            _apBar.Value = unit.CurrentAp;
            if (_apBgBar != null) _apBgBar.MaxValue = maxAp;
            if (_apPreviewBar != null)
            {
                _apPreviewBar.MaxValue = maxAp;
                _apPreviewBar.Value = 0f; // 切换单位时确保顶层不覆盖
            }
        }

        if (_weaponPrimaryLabel != null)
            _weaponPrimaryLabel.Text = data.PrimaryMainHand?.ItemName ?? "徒手";

        if (_weaponSecondaryLabel != null)
            _weaponSecondaryLabel.Text = data.SecondaryMainHand?.ItemName ?? "无";

        // 刷新快捷技能槽
        RefreshQuickSlots(unit);
        // 刷新职业技能UI
        RefreshCareerSkill(unit);
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
    public void ShowHitPreview(Vector2 mousePos, Unit attacker, Unit target, HexGrid? grid = null, int coverType = 0, int elevationDiff = 0, bool hasFlanking = false, bool hasSneak = false)
    {
        if (_hitPreviewTooltip != null && attacker != null && target != null)
            _hitPreviewTooltip.ShowPreview(attacker, target, grid, coverType, elevationDiff, hasFlanking, hasSneak);
    }

    /// <summary>显示超出射程预览（命中率 0%）</summary>
    public void ShowOutOfRangePreview(Vector2 mousePos, Unit target, int distance, int maxRange)
    {
        if (_hitPreviewTooltip != null && target != null)
            _hitPreviewTooltip.ShowOutOfRange(target, distance, maxRange);
    }

    /// <summary>显示行动力不足预览</summary>
    public void ShowApDeficientPreview(Vector2 mousePos, Unit target, float requiredAp, float currentAp)
    {
        if (_hitPreviewTooltip != null && target != null)
            _hitPreviewTooltip.ShowApDeficient(target, 0, requiredAp, currentAp);
    }

    public void ShowAttackBlockedPreview(Vector2 mousePos, Unit target, string reason)
    {
        if (_hitPreviewTooltip != null && target != null)
            _hitPreviewTooltip.ShowBlocked(target, reason);
    }

    /// <summary>隐藏命中预览</summary>
    public void HideHitPreview()
    {
        if (_hitPreviewTooltip != null && _hitPreviewTooltip.Visible)
            _hitPreviewTooltip.Visible = false;
    }

    /// <summary>显示技能效果预览提示</summary>
    public void ShowSkillPreview(Vector2 mousePos, Unit caster, SkillTargetingInfo info, List<Unit> affectedUnits)
    {
        if (_skillPreviewTooltip != null && caster != null)
            _skillPreviewTooltip.ShowPreview(caster, info, affectedUnits);
    }

    /// <summary>隐藏技能效果预览</summary>
    public void HideSkillPreview()
    {
        if (_skillPreviewTooltip != null && _skillPreviewTooltip.Visible)
            _skillPreviewTooltip.Visible = false;
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

        float side = Mathf.Max(minimap.CustomMinimumSize.X, minimap.CustomMinimumSize.Y);
        if (side < 150f) side = 150f;
        minimap.CustomMinimumSize = new Vector2(side, side);
        minimap.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
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

    /// <summary>
    /// 当选中单位变化时，按 UnitData.EquippedSkills 顺序填充 10 个快捷槽。
    /// v0.7：装备制 — 玩家在技能盘 UI 底部手动选 10 个上场，战斗 HUD 只读。
    /// 槽位 9（最后 1 格）保留给职业技能（如有）。
    /// </summary>
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

        if (unit?.Data == null || unit.SkillTree == null)
        {
            GD.Print($"[SkillTarget] RefreshQuickSlots: unit={unit?.Name ?? "null"} data={(unit?.Data == null ? "null" : "ok")} tree={(unit?.SkillTree == null ? "null" : "ok")}");
            return;
        }

        // 按 UnitData.EquippedSkills（最多 10 个）填槽
        var data = unit.Data;
        var tree = unit.SkillTree;
        int filled = 0;
        for (int i = 0; i < 10; i++)
        {
            string entry = data.GetEquippedSkill(i);
            if (string.IsNullOrEmpty(entry)) continue;

            if (SpellStudyCatalog.IsEquippedSpellEntry(entry))
            {
                string spellId = SpellStudyCatalog.GetSpellIdFromEntry(entry);
                var spell = SpellStudyCatalog.GetKnownSpell(data, spellId);
                if (spell == null) continue;

                _quickSlotSkills[i] = entry;
                _quickSlotDescriptions[i] = GetSpellDescription(spell);
                _quickSlots[i].Text = spell.SpellName.Length > 3 ? spell.SpellName[..3] : spell.SpellName;
                _quickSlots[i].TooltipText = spell.SpellName;
                var spellMod = _quickSlots[i].Modulate;
                spellMod.A = 1.0f;
                _quickSlots[i].Modulate = spellMod;
                filled++;
                continue;
            }

            string effect = entry;
            if (!BladeHex.Combat.SkillRegistry.IsEquippableCombatSkill(effect)) continue;

            // 找到对应技能盘节点（拿 NodeName 用作槽位标签）
            string nodeName = effect;
            BladeHex.Strategic.SkillNodeData? matchedNode = null;
            foreach (var node in tree.GetActiveSkills())
            {
                if (node.SkillEffect == effect) { matchedNode = node; nodeName = node.NodeName; break; }
            }

            // 数据校验：装备的技能必须在已激活节点列表里。否则槽位灰显。
            if (matchedNode == null) continue;

            _quickSlotSkills[i] = $"skill_{effect}";
            _quickSlotDescriptions[i] = GetSkillDescription(effect, nodeName);
            _quickSlots[i].Text = nodeName.Length > 3 ? nodeName[..3] : nodeName;
            _quickSlots[i].TooltipText = nodeName;
            var mod = _quickSlots[i].Modulate;
            mod.A = 1.0f;
            _quickSlots[i].Modulate = mod;
            filled++;
        }

        // 职业技能：不在技能栏显示（使用专用的职业技能面板）
        // 职业技能通过底部面板的 _careerSkillPanel 点击触发

        // 诊断：统计实际有技能的槽位数
        int populated = 0;
        for (int i = 0; i < 10; i++)
            if (!string.IsNullOrEmpty(_quickSlotSkills[i])) populated++;
        GD.Print($"[SkillTarget] RefreshQuickSlots: total populated={populated}/10 careerSkill={(unit.GetCareerSkill() != null ? unit.GetCareerSkill()!.DisplayName : "null")}");
    }

    /// <summary>
    /// 刷新职业技能 UI 显示。根据单位是否有职业称号技能显示/隐藏面板。
    /// v1.0: 仅五/六属性主动显示按钮; 被动职业不显示。
    /// </summary>
    public void RefreshCareerSkill(Unit? unit)
    {
        if (_careerSkillPanel == null || _careerSkillNameLabel == null || _careerSkillApLabel == null)
            return;

        var career = unit?.GetCareerSkill();
        // v1.0: 只有 ShowInCombatUi=true 的技能(五/六属性主动)显示按钮
        if (career == null || !career.ShowInCombatUi || !career.IsActive)
        {
            _careerSkillPanel.Visible = false;
            return;
        }

        _careerSkillNameLabel.Text = career.DisplayName;

        // v1.0: 根据分阶显示 AP 要求
        if (career.RequiresFullAp)
        {
            bool apFull = unit!.CurrentAp >= unit.GetMaxAp() - 0.01f;
            _careerSkillApLabel.Text = apFull ? "满AP ✓" : "需满AP";
            _careerSkillApLabel.Modulate = apFull
                ? new Color(0.6f, 0.85f, 1.0f)
                : new Color(1.0f, 0.4f, 0.4f);
        }
        else if (career.IsOncePerTurn)
        {
            _careerSkillApLabel.Text = "每回合1次";
            _careerSkillApLabel.Modulate = new Color(0.6f, 0.85f, 1.0f);
        }
        else
        {
            _careerSkillApLabel.Text = $"AP {career.ApCost}";
            _careerSkillApLabel.Modulate = new Color(0.6f, 0.85f, 1.0f);
        }

        _careerSkillPanel.Visible = true;
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
            rtl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
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
            string apText = BuildActionCostText(registry);
            int manaCost = registry.ContainsKey("mana_cost") ? registry["mana_cost"].AsInt32() : 0;
            int cooldown = registry.ContainsKey("cooldown") ? registry["cooldown"].AsInt32() : 0;
            int usesPerBattle = registry.ContainsKey("uses_per_battle") ? registry["uses_per_battle"].AsInt32() : -1;
            int range = SkillRegistry.GetRange(skillEffect);
            int aoeRadius = SkillRegistry.GetAoeRadius(skillEffect);
            string equipmentText = SkillRegistry.GetEquipmentRequirementText(skillEffect);

            // 目标类型（int 与 TargetType 枚举对应）
            string targetText = "";
            if (registry.ContainsKey("target"))
            {
                int targetType = registry["target"].AsInt32();
                targetText = targetType switch
                {
                    0 => "自身",
                    1 => "单体敌人",
                    2 => "单体友军",
                    3 => "周围敌人",
                    4 => "小范围AOE",
                    5 => "锥形范围",
                    6 => "远程单体",
                    7 => "远程AOE",
                    8 => "全体友军",
                    9 => "地面",
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
            if (!string.IsNullOrEmpty(apText))
                sb.Append($"[color=#999]AP消耗:[/color]  [color=#7cf]{apText}[/color]\n");
            if (manaCost > 0)
                sb.Append($"[color=#999]法力消耗:[/color]  [color=#9cf]{manaCost}[/color]\n");
            if (cooldown > 0)
                sb.Append($"[color=#999]冷却:[/color]    [color=#aed]{cooldown} 回合[/color]\n");
            if (usesPerBattle > 0)
                sb.Append($"[color=#999]次数:[/color]    [color=#aed]每场战斗 {usesPerBattle} 次[/color]\n");
            if (!string.IsNullOrEmpty(equipmentText))
                sb.Append($"[color=#999]装备需求:[/color]  [color=#fca]{equipmentText}[/color]\n");
            if (!string.IsNullOrEmpty(targetText))
                sb.Append($"[color=#999]目标:[/color]    [color=#aed]{targetText}[/color]\n");
            if (range > 0)
                sb.Append($"[color=#999]射程:[/color]    [color=#aed]{range} 格[/color]\n");
            if (aoeRadius > 0)
                sb.Append($"[color=#999]范围:[/color]    [color=#aed]半径 {aoeRadius} 格[/color]\n");

            return sb.ToString().TrimEnd();
        }
        return $"[color=#e8c864][b]{nodeName}[/b][/color]\n[color=#888](无详细描述)[/color]";
    }

    private static string BuildActionCostText(Godot.Collections.Dictionary registry)
    {
        if (registry.ContainsKey("weapon_ap_bonus"))
        {
            int bonus = registry["weapon_ap_bonus"].AsInt32();
            return bonus == 0 ? "武器AP" : $"武器AP+{bonus}";
        }
        if (registry.ContainsKey("movement_ap_bonus"))
        {
            int bonus = registry["movement_ap_bonus"].AsInt32();
            return bonus == 0 ? "距离AP" : $"距离AP+{bonus}";
        }
        if (!registry.ContainsKey("action_cost"))
            return "";

        int cost = registry["action_cost"].AsInt32();
        return cost == 0 ? "0 (免费行动)" : cost.ToString();
    }

    private static string GetSpellDescription(SpellData spell)
    {
        int apCost = spell.castingTime == SpellData.CastingTime.MainAction ? 4 : 0;
        string shapeText = spell.shape switch
        {
            SpellData.SpellShape.Self => "自身",
            SpellData.SpellShape.Single => "单体",
            SpellData.SpellShape.Ray => "射线",
            SpellData.SpellShape.Cone => "锥形",
            SpellData.SpellShape.Sphere => "范围",
            SpellData.SpellShape.Line => "线形",
            SpellData.SpellShape.Cross => "十字",
            SpellData.SpellShape.Touch => "触碰",
            _ => "",
        };

        var sb = new System.Text.StringBuilder();
        sb.Append($"[color=#e8c864][b]{spell.SpellName}[/b][/color]\n");
        sb.Append("[color=#555]────────────────────[/color]\n");
        if (!string.IsNullOrEmpty(spell.Description))
            sb.Append($"[color=#ddd]{spell.Description}[/color]\n\n");
        sb.Append($"[color=#999]环阶:[/color]    [color=#aed]{(int)spell.tier} 环[/color]\n");
        sb.Append($"[color=#999]AP消耗:[/color]  [color=#7cf]{apCost}[/color]\n");
        sb.Append($"[color=#999]法力消耗:[/color] [color=#9cf]{spell.ManaCost}[/color]\n");
        if (!string.IsNullOrEmpty(shapeText))
            sb.Append($"[color=#999]目标:[/color]    [color=#aed]{shapeText}[/color]\n");
        sb.Append($"[color=#999]射程:[/color]    [color=#aed]{spell.RangeCells} 格[/color]");
        if (spell.ShapeSize > 1)
            sb.Append($"\n[color=#999]范围:[/color]    [color=#aed]半径/长度 {spell.ShapeSize}[/color]");
        return sb.ToString();
    }

    public override void _ExitTree()
    {
        BladeHex.View.Combat.CombatSpeed.MultiplierChanged -= _OnSpeedChanged;
    }
}
