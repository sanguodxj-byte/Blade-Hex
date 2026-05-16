// CombatUI.cs
// 战术战斗用户界面 — 悬浮层级天地流布局
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
    private CharacterDetailPanel? _characterDetail;
    private SkillTreeUI? _skillTreeUI;
    private Node _spellSelect = null!;
    private CanvasLayer? _battleResult;
    private RadialMenu _radialMenu = null!;
    private UnitInspectPanel _unitInspect = null!;

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
    private readonly Dictionary<string, Control> _attrLabels = new();
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
        root.AddThemeConstantOverride("margin_bottom", Theme.SpacingMd);
        AddChild(root);

        var mainVBox = new VBoxContainer();
        mainVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(mainVBox);

        // --- 1. 回合顺序栏 ---
        _turnOrderBar = new TurnOrderBar();
        _turnOrderBar.CustomMinimumSize = new Vector2(0, 60);
        mainVBox.AddChild(_turnOrderBar);

        // --- 2. 顶部内容区 ---
        var topContent = new HBoxContainer();
        topContent.MouseFilter = Control.MouseFilterEnum.Ignore;
        mainVBox.AddChild(topContent);

        // 2a. 战斗日志 (左)
        _battleLog = new BattleLogPanel();
        _battleLog.CustomMinimumSize = new Vector2(300, 140);
        topContent.AddChild(_battleLog);

        // 2b. 弹性占位
        var topSpacer = new Control();
        topSpacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        topSpacer.MouseFilter = Control.MouseFilterEnum.Ignore;
        topContent.AddChild(topSpacer);

        // 2c. 敌方信息 (右)
        var enemyVBox = new VBoxContainer();
        enemyVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        enemyVBox.Alignment = BoxContainer.AlignmentMode.End;
        topContent.AddChild(enemyVBox);

        var enemyListHBox = new HBoxContainer();
        enemyListHBox.Name = "EnemyList";
        enemyListHBox.Alignment = BoxContainer.AlignmentMode.End;
        enemyListHBox.AddThemeConstantOverride("separation", 5);
        enemyVBox.AddChild(enemyListHBox);

        _enemyInfoPanel = new EnemyInfoPanel();
        _enemyInfoPanel.CustomMinimumSize = new Vector2(280, 0);
        enemyVBox.AddChild(_enemyInfoPanel);

        // --- 3. 中部战术区 (弹性占位) ---
        var middleSpacer = new Control();
        middleSpacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        middleSpacer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mainVBox.AddChild(middleSpacer);

        // --- 4. 底部交互层 ---
        var interactionLayer = new MarginContainer();
        interactionLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        mainVBox.AddChild(interactionLayer);

        // 4a. 结束回合按钮 (右)
        var endTurnVBox = new VBoxContainer();
        endTurnVBox.MouseFilter = Control.MouseFilterEnum.Ignore;
        endTurnVBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        interactionLayer.AddChild(endTurnVBox);

        var endTurnBtn = new Button();
        endTurnBtn.Text = "结束回合";
        endTurnBtn.CustomMinimumSize = new Vector2(100, 45);
        endTurnBtn.Pressed += () =>
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_click");
            EmitSignal(SignalName.ActionSelected, "end_turn");
        };
        endTurnBtn.MouseEntered += () =>
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("ui_hover", -6.0f);
        };
        var btnStyle = Theme.MakePanelStyle(Theme.BgSecondary, Theme.BorderFriendly, 1, Theme.RadiusMd);
        endTurnBtn.AddThemeStyleboxOverride("normal", btnStyle);
        endTurnVBox.AddChild(endTurnBtn);

        var btnLiftSpacer = new Control();
        btnLiftSpacer.CustomMinimumSize = new Vector2(0, 30);
        endTurnVBox.AddChild(btnLiftSpacer);

        // 4b. 我方角色列表 (左)
        var allyListHBox = new HBoxContainer();
        allyListHBox.Name = "AllyList";
        allyListHBox.AddThemeConstantOverride("separation", 5);
        allyListHBox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        allyListHBox.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        interactionLayer.AddChild(allyListHBox);

        // --- 5. 底部信息面板 ---
        _bottomPanel = _CreatePanel(Vector2.Zero, Theme.BgPrimary, Theme.BorderDefault);
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

        // 5b. 属性信息
        var infoCol = new VBoxContainer();
        infoCol.AddThemeConstantOverride("separation", 2);
        bottomHBox.AddChild(infoCol);

        var charName = _CreateBodyLabel("未选择", Theme.TextAccent);
        charName.SetMeta("stat_key", "char_name");
        infoCol.AddChild(charName);
        _statLabels["char_name"] = charName;

        var hpBar = _CreateHpBar(140, 8);
        hpBar.SetMeta("stat_key", "hp_bar");
        infoCol.AddChild(hpBar);
        _statLabels["hp_bar"] = hpBar;

        var mpBar = _CreateManaBar(140, 6);
        mpBar.SetMeta("stat_key", "mp_bar");
        infoCol.AddChild(mpBar);
        _statLabels["mp_bar"] = mpBar;

        var attrGrid = new GridContainer();
        attrGrid.Columns = 3;
        attrGrid.AddThemeConstantOverride("h_separation", 12);
        infoCol.AddChild(attrGrid);
        _CreateAttrLabel(attrGrid, "str", "力");
        _CreateAttrLabel(attrGrid, "dex", "敏");
        _CreateAttrLabel(attrGrid, "con", "体");
        _CreateAttrLabel(attrGrid, "intel", "智");
        _CreateAttrLabel(attrGrid, "wis", "感");
        _CreateAttrLabel(attrGrid, "cha", "魅");

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5c. 战斗数值
        var combatGrid = new GridContainer();
        combatGrid.Columns = 2;
        combatGrid.AddThemeConstantOverride("h_separation", 15);
        bottomHBox.AddChild(combatGrid);
        _CreateStatLabel(combatGrid, "ac", "闪避", "10");
        _CreateStatLabel(combatGrid, "ap", "行动力", "12");
        _CreateStatLabel(combatGrid, "dmg", "伤害", "1-3");
        _CreateStatLabel(combatGrid, "crit", "暴击", "5%");

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5d. 武器槽
        var weaponVBox = new VBoxContainer();
        weaponVBox.Alignment = BoxContainer.AlignmentMode.Center;
        bottomHBox.AddChild(weaponVBox);

        var mainHand = _CreateCard(new Vector2(50, 50), true);
        mainHand.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
                EmitSignal(SignalName.ActionSelected, "switch_to_primary");
        };
        weaponVBox.AddChild(mainHand);
        _weaponPrimaryLabel = _CreateMutedLabel("主手");
        _weaponPrimaryLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        mainHand.AddChild(_weaponPrimaryLabel);

        var offHand = _CreateCard(new Vector2(50, 50), true);
        offHand.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
                EmitSignal(SignalName.ActionSelected, "switch_to_secondary");
        };
        weaponVBox.AddChild(offHand);
        _weaponSecondaryLabel = _CreateMutedLabel("副手");
        _weaponSecondaryLabel.AddThemeFontSizeOverride("font_size", Theme.FontSizeXs);
        offHand.AddChild(_weaponSecondaryLabel);

        bottomHBox.AddChild(_CreateSeparatorV());

        // 5e. 快捷操作栏
        var quickActions = new HBoxContainer();
        quickActions.Name = "QuickActions";
        quickActions.AddThemeConstantOverride("separation", 6);
        bottomHBox.AddChild(quickActions);

        for (int i = 0; i < 6; i++)
        {
            var p = _CreateCard(new Vector2(45, 45), true);
            var mod = p.Modulate;
            mod.A = 0.3f;
            p.Modulate = mod;
            quickActions.AddChild(p);
        }

        var bSpacer = new Control();
        bSpacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bottomHBox.AddChild(bSpacer);

        // --- 6. 悬浮/弹出组件 ---
        _hitPreviewTooltip = new HitPreviewTooltip();
        _hitPreviewTooltip.Visible = false;
        AddChild(_hitPreviewTooltip);

        _terrainTooltip = new TerrainTooltip();
        _terrainTooltip.Visible = false;
        AddChild(_terrainTooltip);

        _characterDetail = new CharacterDetailPanel();
        _characterDetail.Visible = false;
        AddChild(_characterDetail);

        _radialMenu = new RadialMenu();
        _radialMenu.Visible = false;
        _radialMenu.ActionSelected += _OnRadialMenuActionSelected;
        _radialMenu.ActionHovered += _OnRadialMenuActionHovered;
        AddChild(_radialMenu);

        _unitInspect = new UnitInspectPanel();
        AddChild(_unitInspect);

        _SetupEscMenu();
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
    // 属性 / 战斗数值标签辅助
    // ============================================================================

    private void _CreateAttrLabel(GridContainer parent, string id, string text)
    {
        var nameLabel = _CreateMutedLabel(text);
        nameLabel.CustomMinimumSize = new Vector2(25, 0);
        parent.AddChild(nameLabel);

        var valueLabel = _CreateBodyLabel("10");
        parent.AddChild(valueLabel);
        _attrLabels[id] = valueLabel;
    }

    private void _CreateStatLabel(GridContainer parent, string id, string text, string val)
    {
        var nameLabel = _CreateMutedLabel(text);
        parent.AddChild(nameLabel);

        var valueLabel = _CreateBodyLabel(val);
        parent.AddChild(valueLabel);
        _statLabels[id] = valueLabel;
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

        var data = unit.Data;

        // 头像 — 通过统一渲染组件展示当前选中单位
        _avatarControl?.SetUnit(data, !unit.UsingPrimaryWeapon);

        if (_statLabels.TryGetValue("char_name", out var ctrl) && ctrl is Label charLabel)
            charLabel.Text = data.UnitName;

        if (_attrLabels.TryGetValue("str", out ctrl) && ctrl is Label l) l.Text = data.Str.ToString();
        if (_attrLabels.TryGetValue("dex", out ctrl) && ctrl is Label l2) l2.Text = data.Dex.ToString();
        if (_attrLabels.TryGetValue("con", out ctrl) && ctrl is Label l3) l3.Text = data.Con.ToString();
        if (_attrLabels.TryGetValue("intel", out ctrl) && ctrl is Label l4) l4.Text = data.Intel.ToString();
        if (_attrLabels.TryGetValue("wis", out ctrl) && ctrl is Label l5) l5.Text = data.Wis.ToString();
        if (_attrLabels.TryGetValue("cha", out ctrl) && ctrl is Label l6) l6.Text = data.Cha.ToString();

        if (_statLabels.TryGetValue("ac", out ctrl) && ctrl is Label acL)
            acL.Text = unit.GetAc().ToString();

        if (_statLabels.TryGetValue("ap", out ctrl) && ctrl is Label apL)
            apL.Text = $"{(int)unit.CurrentAp}/{unit.Model.GetMaxAp()}";

        if (_statLabels.TryGetValue("dmg", out ctrl) && ctrl is Label dmgL)
        {
            var wpn = unit.GetMainHand() as WeaponData;
            if (wpn != null)
                dmgL.Text = $"{wpn.DamageDiceCount}-{wpn.DamageDiceCount * wpn.DamageDiceSides}";
            else
                dmgL.Text = "1-3";
        }

        if (_statLabels.TryGetValue("crit", out ctrl) && ctrl is Label critL)
        {
            int critThresh = unit.Model.GetCritThreshold();
            int critPct = (21 - critThresh) * 5;
            critL.Text = $"{critPct}%";
        }

        if (_statLabels.TryGetValue("hp_bar", out ctrl) && ctrl is ProgressBar hpBar)
        {
            hpBar.MaxValue = unit.GetMaxHp();
            hpBar.Value = unit.CurrentHp;
        }

        if (_statLabels.TryGetValue("mp_bar", out ctrl) && ctrl is ProgressBar mpBar)
        {
            mpBar.MaxValue = Mathf.Max(data.CurrentMana, 1);
            mpBar.Value = data.CurrentMana;
        }

        if (_weaponPrimaryLabel != null)
            _weaponPrimaryLabel.Text = data.PrimaryMainHand?.ItemName ?? "徒手";

        if (_weaponSecondaryLabel != null)
            _weaponSecondaryLabel.Text = data.SecondaryMainHand?.ItemName ?? "无";
    }

    /// <summary>写入战斗日志</summary>
    public void LogMessage(string msg)
    {
        _battleLog?.AddEntry(msg);
    }

    /// <summary>设置回合阶段文字</summary>
    public void SetTurnText(string text, Color? color = null)
    {
        _turnOrderBar?.SetPhaseText(text, color ?? Colors.White);
    }

    /// <summary>显示 / 隐藏底部操作面板</summary>
    public void SetActionBarVisible(bool visible)
    {
        if (_bottomPanel != null)
            _bottomPanel.Visible = visible;
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

    /// <summary>将小地图嵌入底部面板最右侧</summary>
    public void EmbedMinimap(Control minimap)
    {
        if (_bottomHBox != null && minimap != null)
        {
            minimap.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            _bottomHBox.AddChild(minimap);
        }
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
}
