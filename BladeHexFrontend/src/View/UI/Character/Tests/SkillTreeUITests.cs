using BladeHex.Strategic;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BladeHex.UI.Tests;

public static class SkillTreeUITests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0;
        int failed = 0;

        foreach (var (name, run) in EnumerateTests())
        {
            try
            {
                var (ok, message) = run();
                if (ok)
                {
                    passed++;
                    details.Add($"  [PASS] {name}");
                }
                else
                {
                    failed++;
                    details.Add($"  [FAIL] {name}: {message}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"  [FAIL] {name}: Exception {ex.GetType().Name}: {ex.Message}");
            }
        }

        return (passed, failed, details);
    }

    private static IEnumerable<(string name, Func<(bool, string)> run)> EnumerateTests()
    {
        yield return (nameof(SkillTreeUI_SectorGeometryUsesHexVertexTriangles), SkillTreeUI_SectorGeometryUsesHexVertexTriangles);
        yield return (nameof(SkillTreeUI_StartHexUsesSectorRegionOrder), SkillTreeUI_StartHexUsesSectorRegionOrder);
        yield return (nameof(SkillTreeUI_RendersInnerMiddleOuterRingBoundaries), SkillTreeUI_RendersInnerMiddleOuterRingBoundaries);
        yield return (nameof(SkillTreeUI_BuildsExplicitTileCachesAndMesh), SkillTreeUI_BuildsExplicitTileCachesAndMesh);
        yield return (nameof(SkillTreeUI_HitTestsEveryNodeTileCentroid), SkillTreeUI_HitTestsEveryNodeTileCentroid);
    }

    private static (bool, string) SkillTreeUI_SectorGeometryUsesHexVertexTriangles()
    {
        var vertices = GetPrivateStatic<Vector2I[]>(typeof(SkillTreeUI), "HexOutlineVertices");
        var regions = GetPrivateStatic<SkillNodeData.Region[]>(typeof(SkillTreeUI), "HexSectorRegions");
        int radius = SkillTreeData.FixedLayoutRadius;

        var expectedVertices = new[]
        {
            new Vector2I(radius, 0),
            new Vector2I(0, radius),
            new Vector2I(-radius, radius),
            new Vector2I(-radius, 0),
            new Vector2I(0, -radius),
            new Vector2I(radius, -radius),
        };
        var expectedRegions = new[]
        {
            SkillNodeData.Region.Int,
            SkillNodeData.Region.Con,
            SkillNodeData.Region.Str,
            SkillNodeData.Region.Dex,
            SkillNodeData.Region.Cha,
            SkillNodeData.Region.Wis,
        };

        if (!vertices.SequenceEqual(expectedVertices))
            return (false, $"hex outline vertices do not match R={radius} document order");
        if (!regions.SequenceEqual(expectedRegions))
            return (false, "sector region order does not match document/UI labels");

        var coord = new SkillTreeCoord { HexSize = 1.0f };
        var center = coord.VertexToPixel(0, 0);
        for (int i = 0; i < vertices.Length; i++)
        {
            var left = coord.VertexToPixel(vertices[i].X, vertices[i].Y);
            var right = coord.VertexToPixel(vertices[(i + 1) % vertices.Length].X, vertices[(i + 1) % vertices.Length].Y);
            float a = center.DistanceTo(left);
            float b = center.DistanceTo(right);
            float c = left.DistanceTo(right);
            if (MathF.Abs(a - b) > 0.001f || MathF.Abs(a - c) > 0.001f)
                return (false, $"sector {i} is not equilateral: center-left={a:0.000}, center-right={b:0.000}, edge={c:0.000}");
        }

        return (true, "");
    }

    private static (bool, string) SkillTreeUI_StartHexUsesSectorRegionOrder()
    {
        var sectors = GetPrivateStatic<SkillNodeData.Region[]>(typeof(SkillTreeUI), "HexSectorRegions");
        var start = GetPrivateStatic<SkillNodeData.Region[]>(typeof(SkillTreeUI), "StartHexRegions");

        if (!start.SequenceEqual(sectors))
            return (false, "start r1 wedge colors must match the six attribute sector order");

        return (true, "");
    }

    private static (bool, string) SkillTreeUI_RendersInnerMiddleOuterRingBoundaries()
    {
        var radii = GetPrivateStatic<int[]>(typeof(SkillTreeUI), "RingBoundaryRadii");
        if (!radii.SequenceEqual(new[] { 8, 15 }))
            return (false, $"ring boundary radii expected 8/15, actual={string.Join(",", radii)}");

        for (int radiusIndex = 0; radiusIndex < radii.Length; radiusIndex++)
        {
            int radius = radii[radiusIndex];
            for (int i = 0; i < 6; i++)
            {
                var vertex = InvokePrivateStatic<Vector2I>(typeof(SkillTreeUI), "GetRingBoundaryVertex", radius, i);
                int s = -vertex.X - vertex.Y;
                int actualRadius = Math.Max(Math.Abs(vertex.X), Math.Max(Math.Abs(vertex.Y), Math.Abs(s)));
                if (actualRadius != radius)
                    return (false, $"boundary r{radius} vertex {i} is at radius {actualRadius}: {vertex}");
            }
        }

        return (true, "");
    }

    private static (bool, string) SkillTreeUI_BuildsExplicitTileCachesAndMesh()
    {
        var treeData = new SkillTreeData();
        var characterTree = new CharacterSkillTree(treeData, level: 20, randomAttributeSeed: 12345);
        var ui = CreateOpenedUi(characterTree, treeData);

        try
        {
            var shapeTiles = GetPrivate<Dictionary<string, Vector2I[]>>(ui, "_nodeShapeTiles");
            int expectedNodeShapes = treeData.GetNodeCount() - 1;
            if (shapeTiles.Count != expectedNodeShapes)
                return (false, $"shape tile cache count expected {expectedNodeShapes}, actual {shapeTiles.Count}");

            int cachedContentTiles = shapeTiles.Values.Sum(tiles => tiles.Length);
            if (cachedContentTiles != 2394)
                return (false, $"cached content tiles expected 2394, actual {cachedContentTiles}");

            var worldVertices = GetPrivate<Dictionary<string, Vector2[][]>>(ui, "_nodeShapeWorldVertices");
            if (worldVertices.Count != expectedNodeShapes)
                return (false, $"world vertex cache count expected {expectedNodeShapes}, actual {worldVertices.Count}");

            var mesh = GetPrivate<ArrayMesh?>(ui, "_starryMesh");
            if (mesh == null || mesh.GetSurfaceCount() == 0)
                return (false, "star chart mesh was not built");

            var outlineMesh = GetPrivate<ArrayMesh?>(ui, "_starryOutlineMesh");
            if (outlineMesh == null || outlineMesh.GetSurfaceCount() == 0)
                return (false, "star chart outline mesh was not built");

            return (true, "");
        }
        finally
        {
            FreeOpenedUi(ui);
        }
    }

    private static (bool, string) SkillTreeUI_HitTestsEveryNodeTileCentroid()
    {
        var treeData = new SkillTreeData();
        var characterTree = new CharacterSkillTree(treeData, level: 20, randomAttributeSeed: 12345);
        var ui = CreateOpenedUi(characterTree, treeData);

        try
        {
            var shapeTiles = GetPrivate<Dictionary<string, Vector2I[]>>(ui, "_nodeShapeTiles");
            var viewport = GetPrivate<SkillTreeViewportState>(ui, "_viewport");

            foreach (var (nodeId, tiles) in shapeTiles)
            {
                foreach (var tile in tiles)
                {
                    var screen = viewport.WorldToScreen(viewport.Coord.TileCentroid(tile));
                    string actual = InvokePrivate<string>(ui, "HitTestNode", screen);
                    if (actual != nodeId)
                        return (false, $"tile centroid hit mismatch at {nodeId}/{tile}: actual={actual}");
                }
            }

            return (true, "");
        }
        finally
        {
            FreeOpenedUi(ui);
        }
    }

    private static SkillTreeUI CreateOpenedUi(CharacterSkillTree characterTree, SkillTreeData treeData)
    {
        var root = GetSceneRoot();
        EnsureTheme(root);

        var host = new Control
        {
            Name = "SkillTreeUITestHost",
            Size = new Vector2(1600, 1000),
            CustomMinimumSize = new Vector2(1600, 1000),
        };
        root.AddChild(host);

        var ui = new SkillTreeUI
        {
            Name = "SkillTreeUITestInstance",
        };
        host.AddChild(ui);

        ui.OpenSkillTree(characterTree, treeData);
        InvokePrivate<object?>(ui, "DeferredOpen");
        return ui;
    }

    private static void FreeOpenedUi(SkillTreeUI ui)
    {
        var parent = ui.GetParent();
        if (parent?.Name == "SkillTreeUITestHost")
            parent.Free();
        else
            ui.Free();
    }

    private static Window GetSceneRoot()
    {
        if (Engine.GetMainLoop() is not SceneTree sceneTree)
            throw new InvalidOperationException("Godot main loop is not a SceneTree");
        return sceneTree.Root;
    }

    private static void EnsureTheme(Node root)
    {
        if (UITheme.Instance != null)
            return;

        var theme = new UITheme { Name = "SkillTreeUITestTheme" };
        root.AddChild(theme);
    }

    private static T GetPrivate<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        return (T)field.GetValue(instance)!;
    }

    private static T GetPrivateStatic<T>(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(type.FullName, fieldName);
        return (T)field.GetValue(null)!;
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return (T)method.Invoke(instance, args)!;
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, methodName);
        return (T)method.Invoke(null, args)!;
    }
}
