// BattleResolver.cs
// 战斗结算处理器 — 处理实体间交互和AI战斗
// 从 OverworldEntityManager 拆出的 Core 层组件
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗结算处理器 — 管理实体间交互检测和AI战斗结算
/// </summary>
public class BattleResolver
{
    private const float INTERACTION_DIST = 500.0f;

    /// <summary>检测并处理所有实体间的交互</summary>
    public void ProcessEntityInteractions(List<OverworldEntity> entities)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            var a = entities[i];
            if (!a.IsAlive) continue;

            for (int j = i + 1; j < entities.Count; j++)
            {
                var b = entities[j];
                if (!b.IsAlive) continue;

                float dist = a.Position.DistanceTo(b.Position);

                if (dist < INTERACTION_DIST)
                    CheckEntityPairInteraction(a, b);
                else if (dist < a.VisionRange)
                    CheckVisionDetection(a, b);

                if (dist < b.VisionRange)
                    CheckVisionDetection(b, a);
            }
        }
    }

    private void CheckEntityPairInteraction(OverworldEntity a, OverworldEntity b)
    {
        if (a.Faction == b.Faction) return;
        if (!AreHostile(a, b)) return;

        var result = OverworldAIResolver.ResolveBattle(a, b);

        if ((bool)result["attacker_destroyed"]) a.IsAlive = false;
        if ((bool)result["defender_destroyed"]) b.IsAlive = false;

        if (!(bool)result["attacker_won"] && a.IsAlive)
            a.CurrentAIState = OverworldEntity.AIState.Fleeing;
        if ((bool)result["attacker_won"] && b.IsAlive)
            b.CurrentAIState = OverworldEntity.AIState.Fleeing;
    }

    private void CheckVisionDetection(OverworldEntity detector, OverworldEntity target)
    {
        if (!AreHostile(detector, target)) return;
        if (detector.CurrentAIState == OverworldEntity.AIState.Fleeing || detector.CurrentAIState == OverworldEntity.AIState.Returning)
            return;

        float powerRatio = detector.EvaluatePowerRatio(target);

        if (powerRatio > 1.5f)
        {
            if (detector.CurrentAIState == OverworldEntity.AIState.Idle || detector.CurrentAIState == OverworldEntity.AIState.Patrolling)
            {
                detector.CurrentAIState = OverworldEntity.AIState.Chasing;
                detector.ChaseTarget = target;
                detector.TargetPosition = target.Position;
            }
        }
        else if (powerRatio < 0.7f)
        {
            if (detector.CurrentAIState == OverworldEntity.AIState.Idle || detector.CurrentAIState == OverworldEntity.AIState.Patrolling)
            {
                detector.CurrentAIState = OverworldEntity.AIState.Fleeing;
            }
        }
    }

    private static bool AreHostile(OverworldEntity a, OverworldEntity b)
    {
        if (a.Faction == b.Faction) return false;
        if (a.Faction == "hostile" || b.Faction == "hostile") return true;
        return false;
    }
}