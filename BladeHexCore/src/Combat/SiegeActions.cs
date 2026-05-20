// SiegeActions.cs
// 攻城战交互逻辑 — 可破坏城门 + 云梯架设
//
// 规则：
//   - 城门（Gate）：可被攻击破坏，耐久归零后变为 Ruins + elevation=1
//   - 城墙（Rampart）：角色在城墙周围 1 格内可右键选择"架设云梯"
//     - 每次消耗 8 AP，需要 3 次完成（总计 24 AP）
//     - 完成后该城墙格 elevation 降为 1，变为可攀登
//   - 云梯进度跨回合保留，不会重置
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Combat;

/// <summary>
/// 攻城行动 — 纯逻辑层，不依赖 View
/// </summary>
public static class SiegeActions
{
    /// <summary>架设云梯的 AP 消耗</summary>
    public const int LadderApCost = 8;

    /// <summary>云梯完成所需次数</summary>
    public const int LadderRequiredSteps = 3;

    /// <summary>
    /// 检查角色是否可以对目标格架设云梯
    /// </summary>
    /// <param name="unitPos">角色当前位置</param>
    /// <param name="targetCell">目标城墙格</param>
    /// <param name="currentAp">角色当前 AP</param>
    /// <returns>(canDo, reason)</returns>
    public static (bool canDo, string reason) CanBuildLadder(
        Vector2I unitPos, BattleCellData? targetCell, Vector2I targetPos, float currentAp)
    {
        if (targetCell == null)
            return (false, "无效目标");

        if (targetCell.terrainType != BattleCellData.TerrainType.Rampart)
            return (false, "只能对城墙架设云梯");

        if (targetCell.HasLadder)
            return (false, "云梯已完成");

        int dist = HexUtils.AxialDistance(unitPos, targetPos);
        if (dist > 1)
            return (false, "距离过远（需要在城墙周围1格内）");

        if (currentAp < LadderApCost)
            return (false, $"行动力不足（需要{LadderApCost}，当前{currentAp:F0}）");

        return (true, "");
    }

    /// <summary>
    /// 执行一次云梯架设（消耗 AP，推进进度）
    /// </summary>
    /// <returns>是否完成（进度达到 3）</returns>
    public static bool BuildLadder(BattleCellData targetCell)
    {
        targetCell.ladderProgress++;

        if (targetCell.HasLadder)
        {
            // 云梯完成：城墙 elevation 降为 1，变为可攀登
            targetCell.elevation = 1;
            targetCell.specialEffect = "ladder_complete";
            return true;
        }
        return false;
    }

    /// <summary>
    /// 检查角色是否可以攻击可破坏地形
    /// </summary>
    public static (bool canDo, string reason) CanAttackDestructible(
        Vector2I unitPos, BattleCellData? targetCell, Vector2I targetPos, float currentAp, int weaponApCost)
    {
        if (targetCell == null)
            return (false, "无效目标");

        if (!targetCell.isDestructible)
            return (false, "该地形不可破坏");

        if (targetCell.durability <= 0)
            return (false, "已被破坏");

        int dist = HexUtils.AxialDistance(unitPos, targetPos);
        if (dist > 1)
            return (false, "距离过远（需要近战范围）");

        if (currentAp < weaponApCost)
            return (false, $"行动力不足（需要{weaponApCost}，当前{currentAp:F0}）");

        return (true, "");
    }

    /// <summary>
    /// 对可破坏地形攻击一次（按次计算，每次 -1 耐久）
    /// </summary>
    /// <returns>是否被破坏（耐久归零）</returns>
    public static bool DamageDestructible(BattleCellData targetCell)
    {
        targetCell.durability = System.Math.Max(0, targetCell.durability - 1);

        if (targetCell.durability <= 0)
        {
            // 城门被破坏：变为废墟 + elevation=1
            targetCell.terrainType = BattleCellData.TerrainType.Ruins;
            targetCell.elevation = 1;
            targetCell.isPassable = true;
            targetCell.isDestructible = false;
            targetCell.moveCost = 2;
            targetCell.coverLevel = 1;
            targetCell.blocksLineOfSight = false;
            targetCell.specialEffect = "gate_destroyed";
            return true;
        }
        return false;
    }
}
