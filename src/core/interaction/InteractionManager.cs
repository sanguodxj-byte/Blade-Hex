using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 交互管理器 — 根据实体类型生成可用交互选项，处理玩家选择
/// </summary>
[GlobalClass]
public partial class InteractionManager : Node
{
    // ========================================
    // 信号
    // ========================================

    [Signal] public delegate void InteractionRequestedEventHandler(Node2D entity, Godot.Collections.Array<InteractionOption> options);
    [Signal] public delegate void InteractionCompletedEventHandler(string result);
    [Signal] public delegate void CombatRequestedEventHandler(BattleContext battleContext);
    [Signal] public delegate void DialogueRequestedEventHandler(Resource npcProfile);
    [Signal] public delegate void TradeRequestedEventHandler(string sourceName);
    [Signal] public delegate void TownEnteredEventHandler(OverworldTown town);
    [Signal] public delegate void RestRequestedEventHandler(int facilityType);
    [Signal] public delegate void TrainRequestedEventHandler();
    [Signal] public delegate void HealRequestedEventHandler();
    [Signal] public delegate void ArenaRequestedEventHandler();
    [Signal] public delegate void QuestRequestedEventHandler();
    [Signal] public delegate void RepairRequestedEventHandler();

    // ========================================
    // 成员变量
    // ========================================

    private Node2D? _currentEntity = null;
    public Node2D? PlayerParty = null; // 简单起见，这里先用 Node2D，后续可改为 OverworldParty
    public HexOverworldGrid? HexGrid = null;

    private bool _isPaused = false;
    private Node2D? _lastInteractedEntity = null;
    private Vector2 _lastInteractionPlayerPos = Vector2.Zero;

    // ========================================
    // 公共方法
    // ========================================

    public void TriggerInteraction(Node2D entity)
    {
        if (_isPaused) return;
        if (entity == _lastInteractedEntity && IsInCooldown()) return;

        _currentEntity = entity;
        _isPaused = true;
        _lastInteractedEntity = entity;

        // if (PlayerParty != null) PlayerParty.IsMoving = false;

        var options = GetInteractionOptions(entity);
        EmitSignal(SignalName.InteractionRequested, entity, new Godot.Collections.Array<InteractionOption>(options));
    }

    public List<InteractionOption> GetInteractionOptions(Node2D entity)
    {
        if (entity is OverworldEnemy enemy)
        {
            if (enemy.NpcProfile != null)
                return BuildHumanoidOptions(enemy.NpcProfile);
            return BuildNonHumanoidOptions(enemy);
        }
        else if (entity is OverworldTown town)
        {
            return BuildTownOptions(town);
        }
        return new List<InteractionOption> { InteractionOption.CreateLeave() };
    }

    public void ExecuteOption(InteractionOption option, Node2D? entity = null)
    {
        entity ??= _currentEntity;
        if (entity == null) return;

        switch (option.CurrentInteractionType)
        {
            case InteractionType.Type.Attack: HandleAttack(entity); break;
            case InteractionType.Type.Talk: HandleTalk(entity); break;
            case InteractionType.Type.Trade: HandleTrade(entity); break;
            case InteractionType.Type.Leave: HandleLeave(); break;
            case InteractionType.Type.Rest: EmitSignal(SignalName.RestRequested, 2); break; // Tavern=2
            case InteractionType.Type.Train: EmitSignal(SignalName.TrainRequested); break;
            case InteractionType.Type.Repair: EmitSignal(SignalName.RepairRequested); break;
            case InteractionType.Type.Heal: EmitSignal(SignalName.HealRequested); break;
            case InteractionType.Type.Quest: EmitSignal(SignalName.QuestRequested); break;
            case InteractionType.Type.Arena: EmitSignal(SignalName.ArenaRequested); break;
            default: HandleLeave(); break;
        }
    }

    public void EndInteraction()
    {
        if (PlayerParty != null) _lastInteractionPlayerPos = PlayerParty.Position;
        _isPaused = false;
        _currentEntity = null;
    }

    private bool IsInCooldown()
    {
        if (PlayerParty == null || _lastInteractedEntity == null) return false;
        float dist = PlayerParty.Position.DistanceTo(_lastInteractedEntity.Position);
        return dist < 80.0f;
    }

    // ========================================
    // 选项构建
    // ========================================

    private List<InteractionOption> BuildNonHumanoidOptions(OverworldEnemy enemy)
    {
        return new List<InteractionOption>
        {
            InteractionOption.CreateAttack(),
            InteractionOption.CreateLeave()
        };
    }

    private List<InteractionOption> BuildHumanoidOptions(Resource profile)
    {
        var options = new List<InteractionOption> { InteractionOption.CreateTalk() };
        // 简化实现
        options.Add(InteractionOption.CreateTrade());
        options.Add(InteractionOption.CreateAttack());
        options.Add(InteractionOption.CreateLeave());
        return options;
    }

    private List<InteractionOption> BuildTownOptions(OverworldTown town)
    {
        var options = new List<InteractionOption>();
        foreach (var facility in town.Facilities)
        {
            if (facility.IsAvailable)
            {
                var opt = new InteractionOption(facility.FacilityName.ToLower(), facility.FacilityName, facility.AssociatedInteractionType, facility.Description);
                opt.IconName = TownFacility.GetTypeIcon(facility.CurrentFacilityType);
                opt.Metadata["facility_type"] = (int)facility.CurrentFacilityType;
                options.Add(opt);
            }
        }
        options.Add(InteractionOption.CreateLeave());
        return options;
    }

    // ========================================
    // 执行处理
    // ========================================

    private void HandleAttack(Node2D entity)
    {
        if (HexGrid != null)
        {
            var tile = HexGrid.GetTileAtPixel(entity.Position.X, entity.Position.Y);
            var terrain = (BattleContext.OverworldTerrainType)(tile?.Terrain ?? 0);
            var ctx = BattleContext.Create(terrain, BattleContext.BattleSize.Mercenary, BattleContext.EngagementType.Normal);
            ctx.EncounterPosition = new Vector2I((int)entity.Position.X, (int)entity.Position.Y);
            EmitSignal(SignalName.CombatRequested, ctx);
        }
        else EmitSignal(SignalName.InteractionCompleted, "attack_no_target");
    }

    private void HandleTalk(Node2D entity)
    {
        if (entity is OverworldEnemy enemy && enemy.NpcProfile != null)
            EmitSignal(SignalName.DialogueRequested, enemy.NpcProfile);
        else EmitSignal(SignalName.InteractionCompleted, "talk_failed");
    }

    private void HandleTrade(Node2D entity)
    {
        string name = entity is OverworldTown town ? town.TownName : "未知";
        EmitSignal(SignalName.TradeRequested, name);
    }

    private void HandleLeave()
    {
        EndInteraction();
        EmitSignal(SignalName.InteractionCompleted, "leave");
    }
}
