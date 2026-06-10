using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>
/// Single source of truth for star-chart triangular tile figures.
/// A node is anchored on GridPosition, but gameplay uses the whole figure.
/// </summary>
public static class SkillNodeShape
{
    public static SkillNodeFigureData GetFigure(SkillNodeData node)
    {
        var tiles = GetTiles(node);
        return new SkillNodeFigureData
        {
            FigureId = node.GetFigureId(),
            FigureName = node.GetFigureName(),
            TemplateId = node.GetFigureTemplate(),
            ActivationShape = node.GetActivationShape(),
            Region = node.CurrentRegion,
            Tiles = tiles,
            IsCareerDefining = ClassTitleResolver.IsCareerDefiningNode(node),
        };
    }

    public static Vector2I[] GetTiles(SkillNodeData node)
    {
        if (node.ExplicitTiles.Length > 0)
            return node.ExplicitTiles;

        if (node.CurrentNodeType == SkillNodeData.NodeType.Start)
            return GetStartTiles(node.GridPosition);

        return BuildTemplateTiles(node.GetFigureTemplate(), node.GridPosition, node.GetRequiredTileCount());
    }

    public static Vector2I[] GetTilesForAnchor(SkillNodeData node, Vector2I anchor)
    {
        if (node.CurrentNodeType == SkillNodeData.NodeType.Start)
            return GetStartTiles(anchor);

        return BuildTemplateTiles(node.GetFigureTemplate(), anchor, node.GetRequiredTileCount());
    }

    public static bool TouchesAnyActivatedTile(SkillNodeData node, HashSet<Vector2I> activatedTiles)
    {
        foreach (var tile in GetTiles(node))
        {
            foreach (var neighbor in SkillTreeCoord.GetTileNeighbors(tile))
            {
                if (activatedTiles.Contains(neighbor))
                    return true;
            }
        }

        return false;
    }

    public static bool IsInsideHex(Vector2I[] tiles, int radius)
    {
        foreach (var tile in tiles)
        {
            if (!SkillTreeCoord.IsTileInsideHex(tile, radius))
                return false;
        }

        return true;
    }

    public static bool Overlaps(Vector2I[] tiles, HashSet<Vector2I> occupiedTiles)
    {
        foreach (var tile in tiles)
        {
            if (occupiedTiles.Contains(tile))
                return true;
        }

        return false;
    }

    public static bool Touches(Vector2I[] tiles, HashSet<Vector2I> occupiedTiles)
    {
        foreach (var tile in tiles)
        {
            foreach (var neighbor in SkillTreeCoord.GetTileNeighbors(tile))
            {
                if (occupiedTiles.Contains(neighbor))
                    return true;
            }
        }

        return false;
    }

    public static void AddTo(Vector2I[] tiles, HashSet<Vector2I> occupiedTiles)
    {
        foreach (var tile in tiles)
            occupiedTiles.Add(tile);
    }

    private static Vector2I[] BuildContiguousTiles(Vector2I anchor, int count)
    {
        count = Mathf.Max(1, count);
        var result = new List<Vector2I>(count) { anchor };
        var seen = new HashSet<Vector2I> { anchor };
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(anchor);

        while (result.Count < count && frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in SkillTreeCoord.GetTileNeighbors(current)
                .OrderByDescending(TileRadialDistance)
                .ThenBy(t => t.X)
                .ThenBy(t => t.Y))
            {
                if (!seen.Add(neighbor)) continue;
                result.Add(neighbor);
                frontier.Enqueue(neighbor);
                if (result.Count >= count) break;
            }
        }

        return result.ToArray();
    }

    private static Vector2I[] BuildTemplateTiles(string templateId, Vector2I anchor, int fallbackCount)
    {
        return templateId switch
        {
            "pip_1" => [anchor],
            "attribute_pair_2" => BuildDiamondCells(anchor, (0, 0)),
            "passive_triad_3" => BuildRelativeTiles(anchor, (0, 0, 0), (0, 0, 1), (1, 0, 0)),
            "passive_triangle_4" => BuildRelativeTiles(anchor, (0, 0, 1), (0, 0, 0), (1, 0, 0), (0, 1, 0)),
            "active_kite_4" => BuildRelativeTiles(anchor, (0, 0, 0), (0, 0, 1), (-1, 0, 1), (0, -1, 1)),
            "keystone_crown_6" => BuildDiamondCells(anchor, (0, 0), (1, 0), (0, 1)),
            "apex_rune_12" => BuildDiamondCells(anchor, (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)),
            "apex_sunburst_12" => BuildDiamondCells(anchor, (1, 0), (0, 1), (1, 1), (2, 1), (1, 2), (2, 2)),
            "apex_arrowhead_12" => BuildDiamondCells(anchor, (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (0, 2)),
            "apex_bastion_12" => BuildDiamondCells(anchor, (1, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2)),
            "apex_crystal_12" => BuildDiamondCells(anchor, (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1)),
            "apex_hourglass_12" => BuildDiamondCells(anchor, (0, 0), (0, 1), (1, 0), (1, 1), (1, 2), (2, 1)),
            "apex_crown_12" => BuildDiamondCells(anchor, (2, 0), (1, 1), (2, 1), (0, 2), (1, 2), (2, 2)),
            _ => BuildContiguousTiles(anchor, fallbackCount),
        };
    }

    private static Vector2I[] BuildDiamondCells(Vector2I anchor, params (int q, int r)[] cells)
    {
        var result = new List<Vector2I>(cells.Length * 2);
        foreach (var (dq, dr) in cells)
        {
            result.AddRange(BuildRelativeTiles(anchor, (dq, dr, 0), (dq, dr, 1)));
        }

        return result.ToArray();
    }

    private static Vector2I[] BuildRelativeTiles(Vector2I anchor, params (int q, int r, int t)[] offsets)
    {
        var (anchorQ, anchorR, _) = SkillTreeCoord.DecodeTile(anchor);
        var result = new Vector2I[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var (dq, dr, t) = offsets[i];
            result[i] = SkillTreeCoord.EncodeTile(anchorQ + dq, anchorR + dr, t);
        }

        return result;
    }

    private static int TileRadialDistance(Vector2I tile)
    {
        var (q, r, t) = SkillTreeCoord.DecodeTile(tile);
        var vertices = t == 0
            ? new[] { new Vector2I(q, r), new Vector2I(q + 1, r), new Vector2I(q, r + 1) }
            : new[] { new Vector2I(q + 1, r), new Vector2I(q, r + 1), new Vector2I(q + 1, r + 1) };

        int max = 0;
        foreach (var v in vertices)
        {
            int s = -v.X - v.Y;
            max = Mathf.Max(max, Mathf.Max(Mathf.Max(Mathf.Abs(v.X), Mathf.Abs(v.Y)), Mathf.Abs(s)));
        }

        return max;
    }

    private static Vector2I[] GetStartTiles(Vector2I anchor)
    {
        var (q, r, _) = SkillTreeCoord.DecodeTile(anchor);
        return
        [
            SkillTreeCoord.EncodeTile(q, r, 0),
            SkillTreeCoord.EncodeTile(q - 1, r, 0),
            SkillTreeCoord.EncodeTile(q, r - 1, 0),
            SkillTreeCoord.EncodeTile(q - 1, r, 1),
            SkillTreeCoord.EncodeTile(q, r - 1, 1),
            SkillTreeCoord.EncodeTile(q - 1, r - 1, 1),
        ];
    }
}
