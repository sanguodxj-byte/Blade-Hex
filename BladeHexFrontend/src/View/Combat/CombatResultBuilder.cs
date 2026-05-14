// CombatResultBuilder.cs
// 战斗结果构建服务
using Godot;
using System.Linq;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 负责在战斗结束时构建 BattleOutcome，传递给战略层。
/// 从 CombatManager.EndCombat() 中拆分。
/// </summary>
public class CombatResultBuilder
{
    private readonly UnitRegistry _registry;

    public CombatResultBuilder(UnitRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>构建战斗结果</summary>
    public BattleOutcome Build(bool victory)
    {
        int survivingPlayers = _registry.PlayerUnits.Count;
        int survivingEnemies = _registry.EnemyUnits.Count;

        // XP 奖励 = 已击杀敌人等级累计 × 15 + 存活敌人等级 × 15
        int totalEnemyLevels = _registry.KilledEnemyLevels
            + _registry.EnemyUnits.Sum(e => e.Data?.Level ?? 1);

        var outcome = new BattleOutcome
        {
            AttackerWon = victory,
            AttackerDestroyed = survivingPlayers == 0,
            DefenderDestroyed = survivingEnemies == 0,
            XpGranted = totalEnemyLevels * 15,
            GoldGranted = totalEnemyLevels * 5,
            BattleType = "field_battle",
        };

        // 伤亡比例
        int initialPlayer = _registry.InitialPlayerCount;
        outcome.AttackerLossPercent = initialPlayer > 0
            ? 1.0f - ((float)survivingPlayers / initialPlayer)
            : 0f;

        int initialEnemy = _registry.InitialEnemyCount;
        outcome.DefenderLossPercent = initialEnemy > 0
            ? 1.0f - ((float)survivingEnemies / initialEnemy)
            : 1f;

        return outcome;
    }

    /// <summary>序列化为事件数据字典</summary>
    public Godot.Collections.Dictionary BuildEventData(bool victory)
    {
        var outcome = Build(victory);
        return new Godot.Collections.Dictionary
        {
            { "victory", victory },
            { "player_survivors", _registry.PlayerUnits.Count },
            { "outcome", outcome.Serialize() },
        };
    }
}
