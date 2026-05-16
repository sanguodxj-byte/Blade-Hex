// OverworldScene3D.Roads.cs
// 道路渲染 — 此文件仅保留 partial 代理，实际实现在 Components/RoadRenderer.cs。
//
// 重构于 Sprint 6（架构优化 spec R5）：从大块 partial 抽出独立 Component。
using Godot;
using BladeHex.Map;
using BladeHex.Scenes.Overworld.Components;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    private RoadRenderer? _roadRenderer;

    /// <summary>初始化时渲染已加载 chunk 的道路</summary>
    private void RenderRoadsAndRivers()
    {
        if (_roadRenderer == null)
        {
            _roadRenderer = new RoadRenderer { Name = "RoadRenderer" };
            AddChild(_roadRenderer);
            _roadRenderer.Initialize(_grid, _chunkManager, this);
        }
        _roadRenderer.RenderAll();
    }

    /// <summary>新 chunk 加载时追加道路</summary>
    private void OnNewChunkRoads(ChunkData chunk, Vector2I chunkCoord)
    {
        _roadRenderer?.OnNewChunk(chunk, chunkCoord);
    }
}
