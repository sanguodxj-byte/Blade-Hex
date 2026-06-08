// BattleContextFactory.cs
// 战斗上下文工厂 — 收敛大地图实体/POI 到战斗场景的上下文创建
using Godot;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗上下文工厂。
///
/// 职责边界：
/// - 大地图场景只提供触发源、玩家位置、网格与随机种子。
/// - 工厂负责推导遭遇坐标、地形、部署描述与上下文元数据。
/// - CombatScene 只消费 BattleContext，不反查 OverworldEntity。
/// </summary>
public static class BattleContextFactory
{
    public static BattleContext CreatePlayerVsEntity(
        OverworldEntity defender,
        HexOverworldGrid? grid,
        Vector2 playerPixelPosition,
        int seed = 0)
    {
        var coord = HexOverworldTile.PixelToAxial(playerPixelPosition.X, playerPixelPosition.Y);
        var context = BattleContext.CreateFromEncounter(
            attacker: null,
            defender: defender,
            poi: null,
            grid: grid,
            coord: coord);

        context.Seed = seed != 0 ? seed : (int)GD.Randi();
        context.EncounterPosition = new Vector2I((int)defender.Position.X, (int)defender.Position.Y);

        if (grid != null)
        {
            var tile = grid.GetTileAtPixel(defender.Position.X, defender.Position.Y);
            if (tile != null)
                context.Terrain = tile.Terrain;
        }

        return context;
    }
}
