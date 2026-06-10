// BattleJoinPanel.cs
// 战场加入专用面板 — 左右划分攻守阵营，基于 POIPanelBase 统一样式
// 规则:
// - 仅允许加入中立或敌对阵营的另一边
// - 禁止攻击友好阵营
// - 自动禁用不可加入的一方按钮

using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Localization;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class BattleJoinPanel : POIPanelBase
{
    // ============================================================================
    // 信号 - Godot Signal 不支持自定义类参数，使用 Action 事件代替
    // ============================================================================
    public event System.Action<JoinOpportunity, bool>? JoinSelected;
    public event System.Action? LeaveSelected;

    // ============================================================================
    // 字段
    // ============================================================================
    private JoinOpportunity? _currentOpportunity;
    private string _playerFaction = "";
    private WorldEventEngine? _worldEngine;
    private ReputationTracker? _reputationTracker;  // 声望追踪器单独传入

    // 按钮缓存
    private Button? _joinAttackerBtn;
    private Button? _joinDefenderBtn;

    // ============================================================================
    // 统一布局数据
    // ============================================================================

    protected override Color GetIllustrationColor()
    {
        return new Color(0.12f, 0.08f, 0.08f, 1.0f); // 战场血色基调
    }

    protected override string GetIllustrationText()
    {
        if (_currentOpportunity == null) return "[ 战场 ]";
        return _currentOpportunity.Type switch
        {
            WarBattleType.Siege => "[ 围城战 ]",
            WarBattleType.FieldBattle => "[ 野战 ]",
            WarBattleType.ArmyJoin => "[ 军团集结 ]",
            _ => "[ 战场 ]"
        };
    }

    protected override string? GetIllustrationPath() => null; // 暂不使用插图

    protected override string GetPanelTitle() => "";

    protected override string GetInfoText()
    {
        if (_currentOpportunity == null) return "";

        return _currentOpportunity.Type switch
        {
            WarBattleType.Siege => $"围城战 · 距离 {(int)_currentOpportunity.Distance}m",
            WarBattleType.FieldBattle => $"野外遭遇战 · 距离 {(int)_currentOpportunity.Distance}m",
            WarBattleType.ArmyJoin => $"军团集结 · 距离 {(int)_currentOpportunity.Distance}m",
            _ => ""
        };
    }

    protected override string GetDescriptionText()
    {
        if (_currentOpportunity == null) return "";

        if (_currentOpportunity.Type == WarBattleType.ArmyJoin)
        {
            string targetName = _currentOpportunity.ArmyRef?.TargetPoiName ?? "未知目标";
            return $"[color=#e0d090]你的国家正在组织军团远征。\n" +
                   $"统帅: {_currentOpportunity.Attacker.EntityName}\n" +
                   $"目标: {targetName}[/color]";
        }

        string desc = _currentOpportunity.Type == WarBattleType.Siege
            ? $"[color=#e0d090]{_currentOpportunity.Attacker.EntityName} ({_currentOpportunity.Attacker.Faction}) 正在围攻 {_currentOpportunity.DefenderPoi?.PoiName} ({_currentOpportunity.DefenderPoi?.OwningFaction})[/color]"
            : $"[color=#e0d090]{_currentOpportunity.Attacker.EntityName} ({_currentOpportunity.Attacker.Faction}) 与 {_currentOpportunity.DefenderEntity?.EntityName} ({_currentOpportunity.DefenderEntity?.Faction}) 正在交战[/color]";

        // NvN 战场附加信息
        if (_currentOpportunity.Attackers.Count > 1)
            desc += $"\n[color=#c8a060]攻击方联军: {_currentOpportunity.Attackers.Count} 支部队 (战力 {(int)_currentOpportunity.AttackerTotalPower})[/color]";
        if (_currentOpportunity.Defenders.Count > 1)
            desc += $"\n[color=#c8a060]防御方联军: {_currentOpportunity.Defenders.Count} 支部队 (战力 {(int)_currentOpportunity.DefenderTotalPower})[/color]";
        else if (_currentOpportunity.DefenderPoi != null)
            desc += $"\n[color=#c8a060]守军: {_currentOpportunity.DefenderPoi.GarrisonCurrent}/{_currentOpportunity.DefenderPoi.GarrisonMax}[/color]";

        return desc;
    }

    protected override string GetLeaveButtonText() => "离开";

    protected override void PopulateActions(VBoxContainer actionsContainer)
    {
        if (_currentOpportunity == null) return;

        // ArmyJoin 只有单一加入按钮
        if (_currentOpportunity.Type == WarBattleType.ArmyJoin)
        {
            var btn = CreateActionButton("加入军团");
            btn.Pressed += () => OnJoinPressed(true);
            actionsContainer.AddChild(btn);
            return;
        }

        // 标题行
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 20);
        titleRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actionsContainer.AddChild(titleRow);

        var atkTitle = CreateBodyLabel("[攻击方]", ThemeTextNegative);
        atkTitle.HorizontalAlignment = HorizontalAlignment.Center;
        atkTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(atkTitle);

        var separator1 = CreateSeparatorV();
        separator1.CustomMinimumSize = new Vector2(2, 0);
        titleRow.AddChild(separator1);

        var defTitle = CreateBodyLabel("[防御方]", new Color(0.5f, 0.7f, 0.9f));
        defTitle.HorizontalAlignment = HorizontalAlignment.Center;
        defTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(defTitle);

        // 阵营信息行
        var factionRow = new HBoxContainer();
        factionRow.AddThemeConstantOverride("separation", 20);
        factionRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actionsContainer.AddChild(factionRow);

        // 攻击方阵营列表
        var atkFactionBox = new VBoxContainer();
        atkFactionBox.AddThemeConstantOverride("separation", 4);
        atkFactionBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        factionRow.AddChild(atkFactionBox);

        PopulateFactionInfo(atkFactionBox, GetAttackerFactions(), true);

        var separator2 = CreateSeparatorV();
        separator2.CustomMinimumSize = new Vector2(2, 0);
        factionRow.AddChild(separator2);

        // 防御方阵营列表
        var defFactionBox = new VBoxContainer();
        defFactionBox.AddThemeConstantOverride("separation", 4);
        defFactionBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        factionRow.AddChild(defFactionBox);

        PopulateFactionInfo(defFactionBox, GetDefenderFactions(), false);

        actionsContainer.AddChild(CreateSeparatorH());

        // 战力对比
        var powerRow = new HBoxContainer();
        powerRow.AddThemeConstantOverride("separation", 20);
        powerRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actionsContainer.AddChild(powerRow);

        float atkPower = CalculateAttackerPower();
        float defPower = CalculateDefenderPower();

        var atkPowerLbl = CreateBodyLabel($"总战力: {(int)atkPower}", ThemeTextSecondary);
        atkPowerLbl.HorizontalAlignment = HorizontalAlignment.Center;
        atkPowerLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        powerRow.AddChild(atkPowerLbl);

        var separator3 = CreateSeparatorV();
        separator3.CustomMinimumSize = new Vector2(2, 0);
        powerRow.AddChild(separator3);

        var defPowerLbl = CreateBodyLabel($"总战力: {(int)defPower}", ThemeTextSecondary);
        defPowerLbl.HorizontalAlignment = HorizontalAlignment.Center;
        defPowerLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        powerRow.AddChild(defPowerLbl);

        actionsContainer.AddChild(CreateSeparatorH());

        // 加入按钮行
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 20);
        btnRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        actionsContainer.AddChild(btnRow);

        // 计算可加入性
        var joinability = EvaluateJoinability();

        _joinAttackerBtn = CreateActionButton("协助攻击方");
        _joinAttackerBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _joinAttackerBtn.Disabled = !joinability.CanJoinAttacker;
        if (!joinability.CanJoinAttacker)
        {
            _joinAttackerBtn.TooltipText = joinability.AttackerReason;
            _joinAttackerBtn.Modulate = new Color(1, 1, 1, 0.4f);
        }
        _joinAttackerBtn.Pressed += () => OnJoinPressed(true);
        btnRow.AddChild(_joinAttackerBtn);

        _joinDefenderBtn = CreateActionButton("协助防御方");
        _joinDefenderBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _joinDefenderBtn.Disabled = !joinability.CanJoinDefender;
        if (!joinability.CanJoinDefender)
        {
            _joinDefenderBtn.TooltipText = joinability.DefenderReason;
            _joinDefenderBtn.Modulate = new Color(1, 1, 1, 0.4f);
        }
        _joinDefenderBtn.Pressed += () => OnJoinPressed(false);
        btnRow.AddChild(_joinDefenderBtn);
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void ShowForBattle(JoinOpportunity opportunity, string playerFaction, WorldEventEngine? worldEngine, ReputationTracker? reputationTracker = null)
    {
        _currentOpportunity = opportunity;
        _playerFaction = playerFaction;
        _worldEngine = worldEngine;
        _reputationTracker = reputationTracker;

        ShowPanel();
    }

    public override void HidePanel()
    {
        base.HidePanel();
        _currentOpportunity = null;
        _joinAttackerBtn = null;
        _joinDefenderBtn = null;
    }

    // ============================================================================
    // 关闭处理
    // ============================================================================

    protected override void OnCloseRequested()
    {
        HidePanel();
        LeaveSelected?.Invoke();
    }

    // ============================================================================
    // 阵营信息填充
    // ============================================================================

    private void PopulateFactionInfo(VBoxContainer container, List<string> factions, bool isAttacker)
    {
        if (factions.Count == 0)
        {
            var lbl = CreateMutedLabel("无");
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            container.AddChild(lbl);
            return;
        }

        foreach (var faction in factions.Distinct())
        {
            var lbl = CreateBodyLabel(GetFactionDisplayName(faction));
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.AddThemeColorOverride("font_color", GetFactionColor(faction));
            container.AddChild(lbl);
        }
    }

    private List<string> GetAttackerFactions()
    {
        if (_currentOpportunity == null) return new List<string>();

        var factions = new List<string>();
        if (_currentOpportunity.Attackers.Count > 0)
        {
            foreach (var entity in _currentOpportunity.Attackers)
                factions.Add(entity.Faction);
        }
        else if (_currentOpportunity.Attacker != null)
        {
            factions.Add(_currentOpportunity.Attacker.Faction);
        }

        return factions;
    }

    private List<string> GetDefenderFactions()
    {
        if (_currentOpportunity == null) return new List<string>();

        var factions = new List<string>();
        if (_currentOpportunity.Defenders.Count > 0)
        {
            foreach (var entity in _currentOpportunity.Defenders)
                factions.Add(entity.Faction);
        }
        else if (_currentOpportunity.DefenderEntity != null)
        {
            factions.Add(_currentOpportunity.DefenderEntity.Faction);
        }
        else if (_currentOpportunity.DefenderPoi != null)
        {
            factions.Add(_currentOpportunity.DefenderPoi.OwningFaction);
        }

        return factions;
    }

    private float CalculateAttackerPower()
    {
        if (_currentOpportunity == null) return 0f;
        if (_currentOpportunity.Attackers.Count > 0)
            return _currentOpportunity.AttackerTotalPower;
        return _currentOpportunity.Attacker?.CombatPower * _currentOpportunity.Attacker?.PartySize ?? 0f;
    }

    private float CalculateDefenderPower()
    {
        if (_currentOpportunity == null) return 0f;
        if (_currentOpportunity.Defenders.Count > 0)
            return _currentOpportunity.DefenderTotalPower;
        if (_currentOpportunity.DefenderEntity != null)
            return _currentOpportunity.DefenderEntity.CombatPower * _currentOpportunity.DefenderEntity.PartySize;
        if (_currentOpportunity.DefenderPoi != null)
            return _currentOpportunity.DefenderPoi.GarrisonCurrent * 5f; // 简化估算
        return 0f;
    }

    // ============================================================================
    // 可加入性判定
    // ============================================================================

    private struct JoinabilityResult
    {
        public bool CanJoinAttacker;
        public bool CanJoinDefender;
        public string AttackerReason;
        public string DefenderReason;
    }

    private JoinabilityResult EvaluateJoinability()
    {
        var result = new JoinabilityResult
        {
            CanJoinAttacker = true,
            CanJoinDefender = true,
            AttackerReason = "",
            DefenderReason = ""
        };

        if (_currentOpportunity == null || string.IsNullOrEmpty(_playerFaction))
        {
            result.CanJoinAttacker = false;
            result.CanJoinDefender = false;
            result.AttackerReason = "无效战场";
            result.DefenderReason = "无效战场";
            return result;
        }

        var atkFactions = GetAttackerFactions();
        var defFactions = GetDefenderFactions();

        // 规则1: 禁止攻击友好阵营
        bool atkIsFriendly = IsFriendlyToPlayer(atkFactions);
        bool defIsFriendly = IsFriendlyToPlayer(defFactions);

        if (atkIsFriendly)
        {
            result.CanJoinDefender = false;
            result.DefenderReason = "不能攻击友方";
        }

        if (defIsFriendly)
        {
            result.CanJoinAttacker = false;
            result.AttackerReason = "不能攻击友方";
        }

        // 规则2: 如果双方都是友方或都是敌方，禁止加入（避免内战或帮敌人打敌人）
        if (atkIsFriendly && defIsFriendly)
        {
            result.CanJoinAttacker = false;
            result.CanJoinDefender = false;
            result.AttackerReason = "友方内战，不可介入";
            result.DefenderReason = "友方内战，不可介入";
        }

        bool atkIsHostile = IsHostileToPlayer(atkFactions);
        bool defIsHostile = IsHostileToPlayer(defFactions);

        if (atkIsHostile && defIsHostile)
        {
            // 敌方互打，可以选边（中立规则）
            result.CanJoinAttacker = true;
            result.CanJoinDefender = true;
        }

        // 规则3: 仅允许加入中立或敌对阵营的另一边
        // 已由规则1-2覆盖

        return result;
    }

    private bool IsFriendlyToPlayer(List<string> factions)
    {
        foreach (var faction in factions.Distinct())
        {
            if (faction == _playerFaction || faction == "player")
                return true;

            // 使用声望判定友好关系
            if (_reputationTracker != null)
            {
                int rep = _reputationTracker.GetReputation(faction);
                if (rep >= 30) // 友好阈值
                    return true;
            }
        }
        return false;
    }

    private bool IsHostileToPlayer(List<string> factions)
    {
        foreach (var faction in factions.Distinct())
        {
            if (faction == "hostile" || faction == "bandit")
                return true;

            if (_reputationTracker != null)
            {
                int rep = _reputationTracker.GetReputation(faction);
                if (rep <= -30) // 敌对阈值
                    return true;
            }
        }
        return false;
    }

    // ============================================================================
    // UI 辅助方法
    // ============================================================================

    private string GetFactionDisplayName(string faction)
    {
        return faction switch
        {
            "player" => L10n.Tr("FACTION_PLAYER"),
            "neutral" => L10n.Tr("FACTION_NEUTRAL"),
            "hostile" => "敌对势力",
            "bandit" => "强盗",
            _ => faction
        };
    }

    private Color GetFactionColor(string faction)
    {
        if (faction == _playerFaction || faction == "player")
            return new Color(0.3f, 0.85f, 0.4f); // 绿色

        if (faction == "hostile" || faction == "bandit")
            return new Color(0.9f, 0.3f, 0.25f); // 红色

        if (_reputationTracker != null)
        {
            int rep = _reputationTracker.GetReputation(faction);
            if (rep >= 30) return new Color(0.3f, 0.85f, 0.4f); // 友好
            if (rep <= -30) return new Color(0.9f, 0.3f, 0.25f); // 敌对
        }

        return ThemeTextSecondary; // 中立
    }

    // ============================================================================
    // 事件处理
    // ============================================================================

    private void OnJoinPressed(bool joinAttacker)
    {
        if (_currentOpportunity != null)
        {
            JoinSelected?.Invoke(_currentOpportunity, joinAttacker);
            HidePanel();
        }
    }
}
