// OverworldTerrainQuery.cs
// Core 层大地图地形查询 Adapter：隐藏 chunk/轴坐标细节，默认只查询活跃地图。
using Godot;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图地形查询 Adapter。
///
/// 当前实现只读取 ChunkManager.GetTile，即 ActiveChunks。全图缓存查询必须显式走
/// ChunkManager.GetTileAnywhere，避免运行时移动/生成逻辑意外读到旧的全图数据。
/// </summary>
public sealed class OverworldTerrainQuery
{
    private readonly ChunkManager _chunkManager;

    private OverworldTerrainQuery(ChunkManager chunkManager)
    {
        _chunkManager = chunkManager;
    }

    public static OverworldTerrainQuery ForActiveChunks(ChunkManager chunkManager)
    {
        return new OverworldTerrainQuery(chunkManager);
    }

    public HexOverworldTile? GetActiveTileAtPixel(Vector2 pixelPosition)
    {
        var axial = HexOverworldTile.PixelToAxial(pixelPosition.X, pixelPosition.Y);
        return _chunkManager.GetTile(axial.X, axial.Y);
    }

    public float GetSpeedFactorAtPixel(Vector2 pixelPosition)
    {
        var tile = GetActiveTileAtPixel(pixelPosition);
        return tile == null ? 1.0f : TerrainCostTable.GetSpeedFactor(tile);
    }

    public string GetTerrainNameAtPixel(Vector2 pixelPosition)
    {
        var tile = GetActiveTileAtPixel(pixelPosition);
        return tile == null ? "未知" : HexOverworldTile.TerrainToString(tile.Terrain);
    }

    public bool IsPassableAtPixel(Vector2 pixelPosition, bool unknownIsPassable = true)
    {
        var tile = GetActiveTileAtPixel(pixelPosition);
        return tile == null ? unknownIsPassable : tile.IsPassable;
    }
}
