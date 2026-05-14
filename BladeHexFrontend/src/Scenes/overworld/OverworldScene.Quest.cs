// OverworldScene.Quest.cs
// 委托闭环系统 — 接取→生成目标实体→玩家到达→战斗→完成→领赏
//
// 流程：
// 1. QuestBoardPanel 接取委托 → QuestGenerator.AcceptQuest 返回 QuestData
// 2. 本文件 AcceptQuestFromBoard → QuestManager.AcceptQuest → SpawnQuestTarget
// 3. 目标点在大地图上渲染（无视迷雾）+ 生成对应的敌方实体
// 4. 玩家到达目标点 → 触发战斗
// 5. 战斗胜利 → QuestManager.UpdateProgress → 标记完成
// 6. 玩家回到发布城镇 → 自动领赏
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene
{
    // ========================================
    // 委托系统字段
    // ========================================

    /// <summary>动态委托生成器</summary>
    private QuestGenerator? _questGenerator;

    /// <summary>已接取的委托对应的目标实体（questId → entity）</summary>
    private readonly Dictionary<string, OverworldEntity> _questTargetEntities = new();

    /// <summary>已接取的委托对应的视觉节点（questId → visual）</summary>
    private readonly Dictionary<string, Node2D> _questTargetVisualMap = new();

    /// <summary>待领赏的已完成委托</summary>
    private readonly List<QuestData> _completedQuestsAwaitingReward = new();

    // ========================================
    // 初始化
    // ========================================

    /// <summary>初始化委托系统（在 InitEntityManager 之后调用）</summary>
    private void InitQuestSystem()
    {
        _questGenerator = new QuestGenerator();
        int seed = 42;
        var gs = GetNodeOrNull<BladeHex.Data.GlobalState>("/root/GlobalState");
        if (gs != null && gs.WorldSeed != 0) seed = gs.WorldSeed;
        _questGenerator.Initialize(WorldPois, seed);
    }

    // ========================================
    // 接取委托（由 QuestBoardPanel.quest_accepted 信号触发）
    // ========================================

    /// <summary>
    /// 从任务板接取委托 — 由 GD 信号 quest_accepted(quest_id) 调用
    /// </summary>
    public void AcceptQuestFromBoard(string questId)
    {
        if (_questGenerator == null || QuestMgr == null) return;

        // 从 QuestGenerator 的池中找到对应的 QuestData
        // （AcceptQuest 已经在 GD 侧调用过了，这里需要从 QuestManager 的 ActiveQuests 找）
        QuestData? quest = null;
        foreach (var q in QuestMgr.ActiveQuests)
        {
            if (q.QuestId == questId) { quest = q; break; }
        }

        // 如果 QuestManager 里没有（说明 GD 侧没调 QuestManager.AcceptQuest），手动加
        if (quest == null && _questGenerator != null)
        {
            // 尝试从生成器获取（可能已被 GD 侧的 AcceptQuest 消费）
            // 这种情况下直接用 QuestId 搜索 ActiveQuests
            GD.Print($"[Quest] 委托 {questId} 未在 QuestManager 中找到，尝试手动注册");
            return;
        }

        if (quest == null) return;

        // 生成目标实体/标记
        SpawnQuestTargetEntity(quest);

        GD.Print($"[Quest] 接取委托: {quest.QuestName} → 目标已生成");
    }

    /// <summary>
    /// 直接接取一个 QuestData（由 C# 侧调用，完整流程）
    /// </summary>
    public void AcceptQuestDirect(QuestData quest)
    {
        if (quest == null || QuestMgr == null) return;

        // 注册到 QuestManager
        QuestMgr.AcceptQuest(quest);

        // 生成目标实体
        SpawnQuestTargetEntity(quest);

        GD.Print($"[Quest] 接取委托: {quest.QuestName}");
    }

    // ========================================
    // 目标实体生成
    // ========================================

    /// <summary>
    /// 在目标位置生成对应的敌方实体（讨伐类）或标记点（护送/探索类）
    /// 无视迷雾，始终可见
    /// </summary>
    private void SpawnQuestTargetEntity(QuestData quest)
    {
        if (quest.TargetWorldPosition == Vector2.Zero) return;

        // 讨伐类：生成一个敌方实体在目标位置
        if (quest.questType == QuestData.QuestType.Extermination)
        {
            var entity = new OverworldEntity
            {
                EntityName = quest.TargetDescription,
                EntityTypeEnum = OverworldEntity.EntityType.RaidingParty,
                Position = quest.TargetWorldPosition,
                HomePosition = quest.TargetWorldPosition,
                TerritoryCenter = quest.TargetWorldPosition,
                TerritoryRadius = 200f,
                MoveSpeed = 0f, // 静止不动（营地）
                PartySize = quest.TargetCount / 2 + 1,
                PartyLevel = (int)quest.difficulty + 1,
                CombatPower = quest.TargetCount * 5f,
                Faction = "hostile",
                IsHostileToPlayer = true,
                IsAlive = true,
                CurrentAIState = OverworldEntity.AIState.Idle,
                PatrolRadius = 100f,
            };

            // 设置 SourceSettlement 用于 EncounterUnitFactory
            entity.SourceSettlement = new OverworldPOI
            {
                PoiName = quest.TargetDescription,
                SettlementRaceValue = InferRaceFromDescription(quest.TargetDescription),
                ThreatLevel = ((int)quest.difficulty + 1) * 0.3f,
            };

            // 加入实体管理器
            if (EntityMgr != null)
            {
                EntityMgr.Entities.Add(entity);
                EntityMgr.EmitSignal(OverworldEntityManager.SignalName.EntitySpawned, entity);
            }

            _questTargetEntities[quest.QuestId] = entity;
        }

        // 所有类型：生成视觉标记（始终可见，无视迷雾）
        SpawnQuestMarkerVisual(quest);
    }

    /// <summary>生成委托目标的视觉标记（始终可见）</summary>
    private void SpawnQuestMarkerVisual(QuestData quest)
    {
        var marker = new Node2D();
        marker.Name = $"QuestMarker_{quest.QuestId}";
        marker.Position = quest.TargetWorldPosition;

        // 用一个醒目的标记（黄色菱形 + 感叹号）
        var poly = new Polygon2D();
        float s = 20f;
        poly.Polygon = new Vector2[]
        {
            new(0, -s), new(s, 0), new(0, s), new(-s, 0)
        };
        poly.Color = new Color(1.0f, 0.85f, 0.0f); // 金色
        marker.AddChild(poly);

        // 标签
        var label = new Label();
        label.Text = $"! {quest.QuestName}";
        label.Position = new Vector2(-60, -35);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(120, 20);
        label.AddThemeFontSizeOverride("font_size", 11);
        marker.AddChild(label);

        AddChild(marker);
        _questTargetVisualMap[quest.QuestId] = marker;
    }

    // ========================================
    // 每帧检测：玩家接近目标点
    // ========================================

    private const float QuestTargetApproachDist = 150.0f;

    /// <summary>每帧检测玩家是否接近委托目标点</summary>
    private void UpdateQuestTargetProximity()
    {
        if (PlayerParty == null || QuestMgr == null) return;
        var playerPos = PlayerParty.Position;

        foreach (var quest in QuestMgr.ActiveQuests)
        {
            if (quest.Status != QuestData.QuestStatus.Active) continue;
            if (quest.TargetWorldPosition == Vector2.Zero) continue;

            float dist = playerPos.DistanceTo(quest.TargetWorldPosition);
            if (dist < QuestTargetApproachDist && quest.QuestId != _lastApproachedQuestId)
            {
                _lastApproachedQuestId = quest.QuestId;
                OnPlayerReachedQuestTargetCs(quest);
                return;
            }
        }

        // 检查已完成委托的领赏（玩家回到发布城镇附近）
        CheckQuestRewardCollection(playerPos);
    }

    // ========================================
    // 到达目标点 → 触发战斗
    // ========================================

    /// <summary>玩家到达委托目标点 — 触发任务遭遇事件（伏击叙事+机制效果）</summary>
    private void OnPlayerReachedQuestTargetCs(QuestData quest)
    {
        GD.Print($"[Quest] 到达目标: {quest.QuestName}");
        if (PlayerParty != null) PlayerParty.IsMoving = false;

        if (quest.questType == QuestData.QuestType.Extermination)
        {
            // 讨伐类：生成伏击遭遇事件，在玩家附近刷出敌人
            var encounterEvt = CreateQuestAmbushEvent(quest);
            _currentEncounterEvent = encounterEvt;
            encounterEvt.Publish();

            // 在玩家附近生成敌方实体（模拟伏击出现）
            SpawnAmbushEnemyNearPlayer(quest);

            // 触发战斗
            TriggerQuestCombat(quest);
        }
        else if (quest.questType == QuestData.QuestType.Escort)
        {
            // 护送类：到达目的地即完成
            CompleteQuest(quest);
        }
        else if (quest.questType == QuestData.QuestType.Exploration)
        {
            // 探索类：可能触发伏击遭遇，也可能安全到达
            if (ShouldTriggerExplorationAmbush(quest))
            {
                var encounterEvt = CreateQuestAmbushEvent(quest);
                _currentEncounterEvent = encounterEvt;
                encounterEvt.Publish();
                SpawnAmbushEnemyNearPlayer(quest);
                TriggerQuestCombat(quest);
            }
            else
            {
                CompleteQuest(quest);
            }
        }
    }

    /// <summary>创建任务伏击遭遇事件 — 携带叙事文本和机制效果</summary>
    private EncounterEvent CreateQuestAmbushEvent(QuestData quest)
    {
        int enemyCount = quest.TargetCount;
        int level = (int)quest.difficulty + 1;
        string entityType = quest.questType == QuestData.QuestType.Extermination ? "BanditParty" : "GenericHostile";

        // 根据任务难度决定伏击强度
        var evt = new EncounterEvent
        {
            Type = EncounterEvent.EncounterType.BanditAmbush,
            EntityName = quest.TargetDescription,
            EntityCount = enemyCount,
            EntityLevel = level,
            ThreatRating = enemyCount * level * 2.5f,
            NarrativeText = InteractionDescriptions.GetEnemyDescription(
                quest.TargetDescription, true, enemyCount, entityType),
        };

        // 任务伏击总是有先手惩罚（玩家被引诱到此地）
        switch (quest.difficulty)
        {
            case QuestData.QuestDifficulty.Easy:
                evt.Effects = EncounterEvent.EffectFlags.EnemyHasInitiative | EncounterEvent.EffectFlags.CanFlee;
                evt.InitiativeModifier = -2;
                evt.MoraleModifier = -3;
                evt.FleeChancePercent = 60;
                break;
            case QuestData.QuestDifficulty.Medium:
                evt.Effects = EncounterEvent.EffectFlags.EnemyHasInitiative | EncounterEvent.EffectFlags.EnemySurrounded | EncounterEvent.EffectFlags.CanFlee;
                evt.InitiativeModifier = -3;
                evt.MoraleModifier = -5;
                evt.FleeChancePercent = 45;
                evt.TerrainOverride = "forest_ambush";
                break;
            case QuestData.QuestDifficulty.Hard:
                evt.Effects = EncounterEvent.EffectFlags.EnemyHasInitiative | EncounterEvent.EffectFlags.EnemySurrounded | EncounterEvent.EffectFlags.TerrainNarrow;
                evt.InitiativeModifier = -4;
                evt.MoraleModifier = -8;
                evt.FleeChancePercent = 30;
                evt.TerrainOverride = "canyon_ambush";
                break;
            case QuestData.QuestDifficulty.Boss:
                evt.Effects = EncounterEvent.EffectFlags.EnemyHasInitiative | EncounterEvent.EffectFlags.MoraleShock | EncounterEvent.EffectFlags.TerrainNarrow;
                evt.Type = EncounterEvent.EncounterType.MonsterEncounter;
                evt.InitiativeModifier = -2;
                evt.MoraleModifier = -15;
                evt.FleeChancePercent = 20;
                evt.TerrainOverride = "boss_lair";
                break;
        }

        return evt;
    }

    /// <summary>在玩家附近刷出伏击敌人（模拟从四周涌出）</summary>
    private void SpawnAmbushEnemyNearPlayer(QuestData quest)
    {
        if (PlayerParty == null) return;

        // 如果目标实体已存在（SpawnQuestTargetEntity已创建），将其移动到玩家附近
        if (_questTargetEntities.TryGetValue(quest.QuestId, out var entity))
        {
            // 将敌人移动到玩家附近（50-100像素范围内随机方向）
            var rng = new System.Random();
            float angle = (float)(rng.NextDouble() * System.Math.PI * 2);
            float dist = 50f + (float)(rng.NextDouble() * 50f);
            var offset = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
            entity.Position = PlayerParty.Position + offset;

            GD.Print($"[Quest] 伏击！敌人出现在玩家附近: {quest.TargetDescription}");
        }
    }

    /// <summary>探索类任务是否触发伏击（50%概率）</summary>
    private static bool ShouldTriggerExplorationAmbush(QuestData quest)
    {
        // 难度越高，伏击概率越大
        float chance = quest.difficulty switch
        {
            QuestData.QuestDifficulty.Easy => 0.2f,
            QuestData.QuestDifficulty.Medium => 0.4f,
            QuestData.QuestDifficulty.Hard => 0.6f,
            QuestData.QuestDifficulty.Boss => 0.8f,
            _ => 0.3f,
        };
        return new System.Random().NextDouble() < chance;
    }

    /// <summary>触发委托战斗</summary>
    private void TriggerQuestCombat(QuestData quest)
    {
        // 从目标实体生成敌方单位
        if (_questTargetEntities.TryGetValue(quest.QuestId, out var entity))
        {
            _pendingEncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(entity);
            _pendingEncounterEntity = entity;
            _pendingQuestId = quest.QuestId;
        }
        else
        {
            // 没有实体，用 EncounterData 生成
            var encounter = new EncounterData
            {
                WorldCoord = new Vector2I((int)quest.TargetWorldPosition.X, (int)quest.TargetWorldPosition.Y),
                Type = EncounterType.WildMonsters,
                EncounterLevel = (int)quest.difficulty + 1,
                PartySize = quest.TargetCount / 2 + 1,
                EnemyTemplateIds = new List<string> { "goblin_warrior", "bandit" },
            };
            _pendingEncounterEnemies = EncounterUnitFactory.BuildEnemyUnits(encounter);
            _pendingQuestId = quest.QuestId;
        }

        // 构造 BattleContext
        var ctx = new BattleContext();
        ctx.EncounterCoord = HexOverworldTile.PixelToAxial(
            quest.TargetWorldPosition.X, quest.TargetWorldPosition.Y);

        EnterCombatSceneFromCs(ctx);
    }

    /// <summary>当前战斗关联的委托 ID</summary>
    private string? _pendingQuestId;

    // ========================================
    // 战斗结束 → 委托完成判定
    // ========================================

    /// <summary>战斗结束后检查委托完成（在 OnEncounterCombatFinished 中调用）</summary>
    private void CheckQuestCompletionAfterCombat(bool victory)
    {
        if (!victory || string.IsNullOrEmpty(_pendingQuestId)) return;

        var quest = FindActiveQuest(_pendingQuestId);
        if (quest == null) { _pendingQuestId = null; return; }

        // 讨伐类：战斗胜利 = 完成
        if (quest.questType == QuestData.QuestType.Extermination)
        {
            quest.UpdateProgress(quest.TargetCount); // 直接满进度
            CompleteQuest(quest);
        }

        _pendingQuestId = null;
    }

    // ========================================
    // 委托完成 + 领赏
    // ========================================

    /// <summary>标记委托完成</summary>
    private void CompleteQuest(QuestData quest)
    {
        quest.Status = QuestData.QuestStatus.Completed;

        // 清理目标实体
        if (_questTargetEntities.TryGetValue(quest.QuestId, out var entity))
        {
            entity.IsAlive = false;
            EntityMgr?.Entities.Remove(entity);
            _questTargetEntities.Remove(quest.QuestId);
        }

        // 清理视觉标记
        if (_questTargetVisualMap.TryGetValue(quest.QuestId, out var visual))
        {
            if (GodotObject.IsInstanceValid(visual)) visual.QueueFree();
            _questTargetVisualMap.Remove(quest.QuestId);
        }

        // 加入待领赏列表
        _completedQuestsAwaitingReward.Add(quest);

        GD.Print($"[Quest] 委托完成: {quest.QuestName}！回到 {quest.IssuerName} 领取奖励");
    }

    /// <summary>检查玩家是否回到发布城镇附近（自动领赏）</summary>
    private void CheckQuestRewardCollection(Vector2 playerPos)
    {
        for (int i = _completedQuestsAwaitingReward.Count - 1; i >= 0; i--)
        {
            var quest = _completedQuestsAwaitingReward[i];
            var issuerPos = new Vector2(quest.IssuerLocation.X, quest.IssuerLocation.Y);
            float dist = playerPos.DistanceTo(issuerPos);

            if (dist < 200f) // 回到发布者附近
            {
                GrantQuestReward(quest);
                _completedQuestsAwaitingReward.RemoveAt(i);
            }
        }
    }

    /// <summary>发放委托奖励</summary>
    private void GrantQuestReward(QuestData quest)
    {
        if (EconomyMgr != null && quest.RewardGold > 0)
            EconomyMgr.AddGold(quest.RewardGold);

        // 声望
        if (!string.IsNullOrEmpty(quest.RewardFaction) && quest.RewardReputation > 0)
            _reputationTracker.OnQuestCompleted(quest.RewardFaction, quest.RewardReputation);

        // 经验分给队伍
        if (PlayerParty?.Roster != null)
        {
            var alive = PlayerParty.Roster.GetDeployableMembers();
            if (alive.Count > 0)
            {
                int xpEach = (quest.RewardGold / 2) / alive.Count;
                foreach (var m in alive) m.Xp += xpEach;
            }
        }

        GD.Print($"[Quest] 领赏: {quest.QuestName} → +{quest.RewardGold}金, 声望+{quest.RewardReputation} ({quest.RewardFaction})");

        // 从 QuestManager 移除
        var qm = QuestMgr as QuestManager;
        qm?.ActiveQuests.Remove(quest);
        qm?.CompletedQuestIds.Add(quest.QuestId);
    }

    // ========================================
    // 工具
    // ========================================

    private QuestData? FindActiveQuest(string questId)
    {
        if (QuestMgr == null) return null;
        var qm = QuestMgr as QuestManager;
        if (qm == null) return null;
        foreach (var q in qm.ActiveQuests)
            if (q.QuestId == questId) return q;
        return null;
    }

    private static OverworldPOI.SettlementRace InferRaceFromDescription(string desc)
    {
        if (desc.Contains("哥布林")) return OverworldPOI.SettlementRace.Goblin;
        if (desc.Contains("狗头人")) return OverworldPOI.SettlementRace.Kobold;
        if (desc.Contains("牛头人")) return OverworldPOI.SettlementRace.Minotaur;
        if (desc.Contains("暗影") || desc.Contains("亡灵")) return OverworldPOI.SettlementRace.ShadowCult;
        if (desc.Contains("山贼") || desc.Contains("劫匪")) return OverworldPOI.SettlementRace.Bandit;
        return OverworldPOI.SettlementRace.Goblin;
    }
}
