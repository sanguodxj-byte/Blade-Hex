// FootprintCell.cs
// 比例尺统一 — POI footprint 中单个 hex 的描述
//
// 见 .kiro/specs/scale-unification/design.md
//
// FootprintCellRole 仅用于世界生成选址（地形约束），
// 不掺杂战斗逻辑或视觉差异（视觉差异走 VisualSpriteId）。

using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// Footprint cell 的地形约束 — 仅用于世界生成选址。
/// 战斗采样直接读 tile.Terrain，不读 Role。
/// </summary>
public enum FootprintCellRole
{
    /// <summary>无约束 — 任何可建造陆地</summary>
    Any,

    /// <summary>必须 ShallowWater 或邻接陆地的 DeepWater（港口 / 海岸城）</summary>
    CoastalDock,

    /// <summary>必须含 River（河港 / 河边磨坊）</summary>
    RiverDock,

    /// <summary>必须 Hills 或 Mountain（山城 / 山间要塞）</summary>
    MountainSlope,

    /// <summary>必须 Forest 或 DenseForest（林中据点 / 林缘村）</summary>
    ForestEdge,
}

/// <summary>POI footprint 中单个 hex 的描述</summary>
public readonly struct FootprintCell
{
    /// <summary>相对中心 hex 的 axial 偏移（中心是 (0,0)）</summary>
    public Vector2I Offset { get; init; }

    /// <summary>选址地形约束</summary>
    public FootprintCellRole Role { get; init; }

    /// <summary>可选：该 hex 渲染哪个建筑 sprite（"city_wall"/"dock"/...）。不影响地形或选址。</summary>
    public string? VisualSpriteId { get; init; }

    public FootprintCell(Vector2I offset, FootprintCellRole role = FootprintCellRole.Any, string? visualSpriteId = null)
    {
        Offset = offset;
        Role = role;
        VisualSpriteId = visualSpriteId;
    }
}
