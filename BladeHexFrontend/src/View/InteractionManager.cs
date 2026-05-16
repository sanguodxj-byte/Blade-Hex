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
    public Node2D? GetCurrentEntity() => _currentEntity;
    public Node2D? PlayerParty = null; // 简单起见，这里先用 Node2D，后续可改为 OverworldParty
    public HexOverworldGrid? HexGrid = null;

    private bool _isPaused = false;
    private Node2D? _lastInteractedEntity = null;
    private Vector2 _lastInteractionPlayerPos = Vector2.Zero;
    private double _lastInteractionTime = 0.0;
    private const double InteractionCooldownSec = 1.5; // 1.5秒冷却防止重复触发

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
        _lastInteractionTime = Time.GetTicksMsec() / 1000.0;

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
        double now = Time.GetTicksMsec() / 1000.0;
        return (now - _lastInteractionTime) < InteractionCooldownSec;
    }

    // ========================================
    // 选项构建
    // ========================================

    private List<InteractionOption> BuildNonHumanoidOptions(OverworldEnemy enemy)
    {
        // 判断是否为敌方主动接近（追击状态）
        bool enemyInitiated = false;
        if (enemy.EntityRef != null)
        {
            enemyInitiated = enemy.EntityRef.CurrentAIState == OverworldEntity.AIState.Chasing;
        }

        if (enemyInitiated)
        {
            // 敌方主动接近 → 迎战/逃跑
            return new List<InteractionOption>
            {
                new("fight", "迎战", InteractionType.Type.Attack, "正面迎击来犯之敌"),
                new("flee", "逃跑", InteractionType.Type.Leave, "尝试逃离敌人的追击"),
            };
        }

        // 玩家主动接近 → 袭击/离开
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
        if (entity == null) return;
        BladeHex.Debug.GameLog.Info($"[InteractionManager] HandleAttack 触发，实体: {entity.Name}");
        if (HexGrid != null)
        {
            var tile = HexGrid.GetTileAtPixel(entity.Position.X, entity.Position.Y);
            var terrain = tile?.Terrain ?? Map.HexOverworldTile.TerrainType.Plains;
            var ctx = BattleContext.Create(terrain, BattleContext.BattleSize.Mercenary, BattleContext.EngagementType.Normal);
            ctx.EncounterPosition = new Vector2I((int)entity.Position.X, (int)entity.Position.Y);
            BladeHex.Debug.GameLog.Info($"[InteractionManager] 发射 CombatRequested 信号，地形={terrain}");
            EmitSignal(SignalName.CombatRequested, ctx);
        }
        else
        {
            BladeHex.Debug.GameLog.Err("[InteractionManager] HexGrid 为 null，无法创建战斗上下文");
            EmitSignal(SignalName.InteractionCompleted, "attack_no_target");
        }
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
