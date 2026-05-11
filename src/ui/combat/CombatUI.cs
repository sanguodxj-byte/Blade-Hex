using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.UI;
using BladeHex.UI.Combat;

namespace BladeHex.Combat.UI;

/// <summary>
/// 战斗UI主控制器 —— 负责协调各子面板显示与信号分发
/// 迁移自 GDScript CombatUI.gd
/// </summary>
public partial class CombatUI : CanvasLayer
{
    [Signal] public delegate void ActionSelectedEventHandler(string actionName);
    [Signal] public delegate void SpellSelectedEventHandler(SpellData spell);
    [Signal] public delegate void EnemyHoveredInPanelEventHandler(Unit unit);
    [Signal] public delegate void UnitSelectedInListEventHandler(Unit unit);

    private TurnOrderBar _turnOrderBar = null!;
    private EnemyInfoPanel _enemyInfoPanel = null!;
    private BattleLogPanel _battleLog = null!;
    private RadialMenu _radialMenu = null!;
    private SpellSelectionPanel _spellSelectionPanel = null!;
    private HitPreviewTooltip _hitPreview = null!;
    private TerrainTooltip _terrainTooltip = null!;
    private BattleResultPanel _resultPanel = null!;
    private StatusEffectDisplay _statusDisplay = null!;
    
    private readonly UIFactory _factory = new();
    private UITheme _theme => UITheme.Instance;

    private Dictionary<string, Label> _attrLabels = new();
    private Dictionary<string, Control> _statLabels = new();
    private Label _weaponPrimaryLabel = null!;
    private Label _weaponSecondaryLabel = null!;

    public override void _Ready()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        var root = new MarginContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddThemeConstantOverride("margin_left", _theme.SpacingMd);
        root.AddThemeConstantOverride("margin_right", _theme.SpacingMd);
        root.AddThemeConstantOverride("margin_top", _theme.SpacingMd);
        root.AddThemeConstantOverride("margin_bottom", _theme.SpacingMd);
        AddChild(root);

        var mainVbox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(mainVbox);

        // --- 1. 顶栏 (回合顺序) ---
        _turnOrderBar = new TurnOrderBar();
        _turnOrderBar.CustomMinimumSize = new Vector2(0, 60);
        mainVbox.AddChild(_turnOrderBar);

        // --- 2. 顶部内容区 (日志与敌方) ---
        var topContent = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        mainVbox.AddChild(topContent);

        _battleLog = new BattleLogPanel();
        _battleLog.CustomMinimumSize = new Vector2(300, 140);
        topContent.AddChild(_battleLog);

        var topSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        topContent.AddChild(topSpacer);

        _enemyInfoPanel = new EnemyInfoPanel();
        _enemyInfoPanel.CustomMinimumSize = new Vector2(280, 0);
        topContent.AddChild(_enemyInfoPanel);

        // --- 3. 中部弹性占位 ---
        var middleSpacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        mainVbox.AddChild(middleSpacer);

        // --- 4. 底部交互层 ---
        var interactionLayer = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        mainVbox.AddChild(interactionLayer);

        var endTurnBtn = _factory.CreateButton("结束回合", new Vector2(100, 45));
        endTurnBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        endTurnBtn.Pressed += () => EmitSignal(SignalName.ActionSelected, "end_turn");
        interactionLayer.AddChild(endTurnBtn);

        // --- 5. 底部面板 ---
        var bottomPanel = _factory.CreatePanel(Vector2.Zero, _theme.BgPrimary, _theme.BorderDefault);
        mainVbox.AddChild(bottomPanel);

        var bottomMargin = _factory.CreateMargin(12, 12, 10, 10);
        bottomPanel.AddChild(bottomMargin);

        var bottomHbox = new HBoxContainer();
        bottomHbox.AddThemeConstantOverride("separation", _theme.SpacingLg);
        bottomMargin.AddChild(bottomHbox);

        // 头像
        var avatarBg = _factory.CreatePortrait(80);
        bottomHbox.AddChild(avatarBg);

        // 信息列
        var infoCol = new VBoxContainer();
        infoCol.AddThemeConstantOverride("separation", 2);
        bottomHbox.AddChild(infoCol);

        var charName = _factory.CreateBodyLabel("未选择", _theme.TextAccent);
        infoCol.AddChild(charName);
        _statLabels["char_name"] = charName;

        var hpBar = _factory.CreateHpBar(140, 8);
        infoCol.AddChild(hpBar);
        _statLabels["hp_bar"] = hpBar;
        
        _statusDisplay = new StatusEffectDisplay();
        infoCol.AddChild(_statusDisplay);

        var mpBar = _factory.CreateManaBar(140, 6);
        infoCol.AddChild(mpBar);
        _statLabels["mp_bar"] = mpBar;

        // 武器
        var weaponVbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bottomHbox.AddChild(weaponVbox);

        _weaponPrimaryLabel = _factory.CreateMutedLabel("主手", _theme.FontSizeXs);
        weaponVbox.AddChild(_weaponPrimaryLabel);
        
        _weaponSecondaryLabel = _factory.CreateMutedLabel("副手", _theme.FontSizeXs);
        weaponVbox.AddChild(_weaponSecondaryLabel);

        // --- 6. 浮动交互层 ---
        _radialMenu = new RadialMenu { Visible = false };
        _radialMenu.ActionSelected += (action) => EmitSignal(SignalName.ActionSelected, action);
        AddChild(_radialMenu);

        _spellSelectionPanel = new SpellSelectionPanel { Visible = false };
        _spellSelectionPanel.SpellSelected += (spell) => EmitSignal(SignalName.SpellSelected, spell);
        AddChild(_spellSelectionPanel);

        _hitPreview = new HitPreviewTooltip { Visible = false };
        AddChild(_hitPreview);

        _terrainTooltip = new TerrainTooltip { Visible = false };
        AddChild(_terrainTooltip);

        _resultPanel = new BattleResultPanel { Visible = false };
        _resultPanel.Confirmed += () => EmitSignal(SignalName.ActionSelected, "exit_combat");
        AddChild(_resultPanel);
    }

    public void UpdateUnitInfo(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || unit.Data == null) return;
        
        ((Label)_statLabels["char_name"]).Text = unit.Data.UnitName;
        var hpBar = (ProgressBar)_statLabels["hp_bar"];
        hpBar.MaxValue = unit.GetMaxHp();
        hpBar.Value = unit.CurrentHp;
        
        var mpBar = (ProgressBar)_statLabels["mp_bar"];
        mpBar.MaxValue = Math.Max(unit.Data.CurrentMana, 1);
        mpBar.Value = unit.Data.CurrentMana;

        _statusDisplay.UpdateEffects(unit.Data.ActiveStatusEffects);

        _weaponPrimaryLabel.Text = unit.Data.PrimaryMainHand?.ItemName ?? "徒手";
        _weaponSecondaryLabel.Text = unit.Data.SecondaryMainHand?.ItemName ?? "无";
    }


    public void ShowHitPreview(Unit attacker, Unit target, int cover, int elev) => _hitPreview.ShowPreview(attacker, target, null, cover, elev);
    public void HideHitPreview() => _hitPreview.HidePreview();
    public void ShowTerrainInfo(Vector2 pos, string type, Vector2I coord) => _terrainTooltip.ShowTerrainInfo(pos, type, coord);
    public void HideTerrainInfo() => _terrainTooltip.HideTooltip();
    
    public void ShowVictory(int xp, int gold, List<string> loot) => _resultPanel.ShowVictory(xp, gold, loot);
    public void ShowDefeat(int survivors) => _resultPanel.ShowDefeat(survivors);

    public void LogMessage(string message) => _battleLog.AddEntry(message);
    public void RegisterAlly(Unit unit) { /* TODO: 实现角色列表 */ }
    public void RegisterEnemy(Unit unit) => _enemyInfoPanel.AddEnemy(unit);
    public void UpdateEnemyInfo(Unit unit) => _enemyInfoPanel.UpdateEnemy(unit);
    public void RemoveEnemy(Unit unit) => _enemyInfoPanel.RemoveEnemy(unit);
    
    public void UpdateTurnOrder(List<Unit> units, Unit? active, int turn)
    {
        _turnOrderBar.SetTurnNumber(turn);
        _turnOrderBar.SetUnitOrder(units, active);
    }

    public void SetTurnText(string text, Color color) { /* TODO */ }
    public void SetActionBarVisible(bool visible) { /* TODO */ }
    public void RefreshAllyList() { /* TODO */ }
    public void HighlightActionMode(string mode) { /* TODO */ }
    public void ShowConfirmDialog(string text, Action onConfirm) => onConfirm?.Invoke();
    
    public void OpenSpellPanel(Unit unit, Node spellManager)
    {
        if (spellManager is SpellManager sm)
        {
            _spellSelectionPanel.Open(unit, sm);
        }
    }

    public void ShowRadialMenu(Vector2 screenPos, Dictionary<string, string> options)
    {
        _radialMenu.Setup(options);
        _radialMenu.ShowMenu(screenPos);
    }
}
