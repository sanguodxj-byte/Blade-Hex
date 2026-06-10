// CombatWritebackService.cs
// 战后回写统一服务 — 玩家加入野战/围城后的统一回写
//
// 设计目标:
//   - 玩家加入野战/围城后，统一生成 BattleContext
//   - 战后回写只走一个模块:
//     - 胜方/败方状态
//     - 实体死亡/逃跑
//     - POI 转移
//     - 影响力变化
//     - 战场清理
//   - 野战加入和普通遭遇战不会重复删除实体
//   - 围城助攻/助守与 AI 围城结算规则一致
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic.Army;

namespace BladeHex.Strategic;

/// <summary>战后回写结果</summary>
public sealed class CombatWritebackResult
{
    public bool PlayerWon { get; set; }
    public List<OverworldEntity> KilledEntities { get; } = new();
    public List<OverworldEntity> FledEntities { get; } = new();
    public List<OverworldPOI> TransferredPois { get; } = new();
    public List<string> InfluenceChanges { get; } = new();
    public List<string> CleanedBattlefieldIds { get; } = new();
    public string? Summary { get; set; }
}

/// <summary>
/// 战后回写统一服务（Core 层）。
///
/// 管线位置:
///   CombatScene.OnCombatFinished → CombatWritebackService.Apply() → 实体/POI/战场状态更新
///
/// 用法:
///   var service = new CombatWritebackService();
///   var result = service.Apply(outcome, simCtx, battleResolver);
///
/// 职责:
///   - 统一处理玩家参与的野战、围城、普通遭遇的战后回写
///   - 防止重复删除实体（野战加入 vs 普通遭遇）
///   - 清理对应的 Battlefield 条目
///   - 处理 POI 阵营转移
/// </summary>
public sealed class CombatWritebackService
{
    /// <summary>
    /// 应用战后回写。
    /// </summary>
    /// <param name="outcome">战斗结果字典（由 CombatScene 生成）</param>
    /// <param name="ctx">模拟上下文（Core 层，包含 Entities/Pois/Armies/WorldEngine）</param>
    /// <param name="battleResolver">战斗结算器（用于清理 Battlefield）</param>
    /// <returns>回写结果摘要</returns>
    public CombatWritebackResult Apply(
        Godot.Collections.Dictionary outcome,
        OverworldSimulationContext ctx,
        BattleResolver battleResolver)
    {
        var result = new CombatWritebackResult();

        // 1. 解析战斗结果
        bool playerWon = outcome.ContainsKey("player_won") && (bool)outcome["player_won"];
        result.PlayerWon = playerWon;

        // 2. 处理实体死亡/逃跑
        ProcessEntityOutcomes(outcome, ctx, result);

        // 3. 处理 POI 转移（围城战后）
        ProcessPoiTransfers(outcome, ctx, result);

        // 4. 处理影响力变化
        ProcessInfluenceChanges(outcome, ctx, result);

        // 5. 清理战场
        CleanBattlefields(outcome, battleResolver, result);

        // 6. 生成摘要
        result.Summary = BuildSummary(result);

        OverworldDiagnostics.Log(OverworldDiagnostics.PrefixBattlefield,
            $"writeback: won={playerWon}, killed={result.KilledEntities.Count}, " +
            $"fled={result.FledEntities.Count}, pois={result.TransferredPois.Count}, " +
            $"cleaned={result.CleanedBattlefieldIds.Count}");

        return result;
    }

    // ========================================
    // 实体结果处理
    // ========================================

    private void ProcessEntityOutcomes(
        Godot.Collections.Dictionary outcome,
        OverworldSimulationContext ctx,
        CombatWritebackResult result)
    {
        // 处理死亡实体
        if (outcome.ContainsKey("dead_entities"))
        {
            var deadNames = ExtractStringList(outcome["dead_entities"]);
            foreach (var name in deadNames)
            {
                var entity = ctx.Entities.FirstOrDefault(e => e.EntityName == name);
                if (entity == null || !entity.IsAlive) continue;

                entity.IsAlive = false;
                result.KilledEntities.Add(entity);

                // 从军团中移除
                if (!string.IsNullOrEmpty(entity.ArmyId))
                {
                    var army = ctx.Armies.Get(entity.ArmyId);
                    army?.Members.Remove(entity);
                }

                ctx.SpatialIndex?.Remove(entity, entity.Position);
                ctx.Entities.Remove(entity);
            }
        }

        // 处理逃跑实体
        if (outcome.ContainsKey("fled_entities"))
        {
            var fledNames = ExtractStringList(outcome["fled_entities"]);
            foreach (var name in fledNames)
            {
                var entity = ctx.Entities.FirstOrDefault(e => e.EntityName == name);
                if (entity == null || !entity.IsAlive) continue;

                entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
                entity.IsMoving = true;
                result.FledEntities.Add(entity);
            }
        }
    }

    // ========================================
    // POI 转移
    // ========================================

    private void ProcessPoiTransfers(
        Godot.Collections.Dictionary outcome,
        OverworldSimulationContext ctx,
        CombatWritebackResult result)
    {
        if (!outcome.ContainsKey("poi_transfers")) return;

        var transfersVariant = outcome["poi_transfers"];
        if (transfersVariant.VariantType != Variant.Type.Array) return;

        var transfers = transfersVariant.AsGodotArray();
        foreach (var transfer in transfers)
        {
            if (transfer.VariantType != Variant.Type.Dictionary) continue;
            var dict = transfer.AsGodotDictionary();

            string poiName = dict.ContainsKey("poi") ? dict["poi"].AsString() : "";
            string newFaction = dict.ContainsKey("faction") ? dict["faction"].AsString() : "";

            var poi = ctx.Pois.FirstOrDefault(p => p.PoiName == poiName);
            if (poi == null || string.IsNullOrEmpty(newFaction)) continue;

            poi.OwningFaction = newFaction;
            result.TransferredPois.Add(poi);
        }
    }

    // ========================================
    // 影响力变化
    // ========================================

    private void ProcessInfluenceChanges(
        Godot.Collections.Dictionary outcome,
        OverworldSimulationContext ctx,
        CombatWritebackResult result)
    {
        if (!outcome.ContainsKey("influence_changes")) return;

        var changesVariant = outcome["influence_changes"];
        if (changesVariant.VariantType != Variant.Type.Array) return;

        var changes = changesVariant.AsGodotArray();
        foreach (var change in changes)
        {
            if (change.VariantType != Variant.Type.Dictionary) continue;
            var dict = change.AsGodotDictionary();

            string faction = dict.ContainsKey("faction") ? dict["faction"].AsString() : "";
            int delta = dict.ContainsKey("delta") ? (int)dict["delta"].AsInt64() : 0;

            // 影响力变化记录到日志
            if (!string.IsNullOrEmpty(faction))
            {
                OverworldDiagnostics.Log(OverworldDiagnostics.PrefixBattlefield,
                    $"influence_change: {faction} delta={delta:+#;-#;0}");
            }
            result.InfluenceChanges.Add($"{faction}: {delta:+#;-#;0}");
        }
    }

    // ========================================
    // 战场清理
    // ========================================

    private void CleanBattlefields(
        Godot.Collections.Dictionary outcome,
        BattleResolver battleResolver,
        CombatWritebackResult result)
    {
        if (!outcome.ContainsKey("battlefield_id")) return;

        string battlefieldId = (string)outcome["battlefield_id"];
        if (string.IsNullOrEmpty(battlefieldId)) return;

        var battlefield = battleResolver.Battlefields
            .FirstOrDefault(b => b.BattlefieldId == battlefieldId);

        if (battlefield != null)
        {
            battlefield.IsResolved = true;

            foreach (var participant in battlefield.AllParticipants.ToList())
            {
                if (participant.CurrentAIState == OverworldEntity.AIState.Engaged)
                {
                    participant.CurrentAIState = OverworldEntity.AIState.Patrolling;
                    participant.EngagedWith = null;
                    participant.BattlefieldId = "";
                }
            }

            result.CleanedBattlefieldIds.Add(battlefieldId);
            OverworldDiagnostics.LogBattlefieldCleared(battlefieldId, "writeback");
        }
    }

    // ========================================
    // 辅助方法
    // ========================================

    private static List<string> ExtractStringList(Variant variant)
    {
        var result = new List<string>();
        if (variant.VariantType == Variant.Type.Array)
        {
            var arr = variant.AsGodotArray();
            foreach (var item in arr)
            {
                if (item.VariantType == Variant.Type.String)
                    result.Add(item.AsString());
            }
        }
        return result;
    }

    private static string BuildSummary(CombatWritebackResult result)
    {
        var parts = new List<string>();
        parts.Add(result.PlayerWon ? "player_won" : "player_lost");
        if (result.KilledEntities.Count > 0)
            parts.Add($"killed={result.KilledEntities.Count}");
        if (result.FledEntities.Count > 0)
            parts.Add($"fled={result.FledEntities.Count}");
        if (result.TransferredPois.Count > 0)
            parts.Add($"pois_transferred={result.TransferredPois.Count}");
        if (result.CleanedBattlefieldIds.Count > 0)
            parts.Add($"battlefields_cleaned={result.CleanedBattlefieldIds.Count}");
        return string.Join(", ", parts);
    }
}
