// Overworld2DCallChainTest.cs
// 2D 大地图调用链测试 — 验证初始化和更新流程
using Godot;
using System.Collections.Generic;
using System.Text;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.View.UI.Overworld;
using BladeHex.Scenes.Overworld;
using BladeHex.Scenes.Overworld2d;
using BladeHex.Scenes.Overworld2d.Components;
using BladeHex.Tests.ShaderTests;

namespace BladeHex.Tests;

/// <summary>
/// 2D 大地图调用链测试 — 验证初始化和更新流程
/// </summary>
public partial class Overworld2DCallChainTest : Node
{
    private int _passed = 0;
    private int _failed = 0;
    private List<string> _details = new();

    public override void _Ready()
    {
        GD.Print("========================================");
        GD.Print("  Overworld2D Call Chain Tests");
        GD.Print("========================================");
        GD.Print();

        RunAllTests();

        GD.Print();
        GD.Print("========================================");
        GD.Print($"  RESULT: {_passed} passed, {_failed} failed");
        GD.Print("========================================");

        if (DisplayServer.GetName() == "headless")
            GetTree().Quit(_failed == 0 ? 0 : 1);
    }

    private void RunAllTests()
    {
        GD.Print("[Test] Starting RunAllTests...");

        // 地图生成测试
        RunTest("HexOverworldGenerator.Generate", TestHexOverworldGenerator);
        RunTest("HexOverworldGrid.TileCount", TestHexOverworldGridTileCount);
        RunTest("HexOverworldGrid.GetNeighbors", TestHexOverworldGridGetNeighbors);

        // 渲染器测试
        RunTest("HexOverworldRenderer2D.Initialize", TestRendererInitialize);
        RunTest("HexOverworldRenderer2D.LoadFromGrid", TestRendererLoadFromGrid);
        RunTest("HexOverworldRenderer2D.LoadTiles", TestRendererLoadTiles);
        RunTest("HexOverworldRenderer2D.ClearAll", TestRendererClearAll);
        RunTest("HexOverworldRenderer2D.AnyNewOptimization", TestHexOverworldRenderer2DAnyNewOptimization);

        // Prop 渲染器测试
        RunTest("OverworldPropRenderer2D.Initialize", TestPropRendererInitialize);
        RunTest("OverworldPropRenderer2D.TextureFiltering", TestPropRendererTextureFiltering);
        RunTest("OverworldPropRenderer2D.ImportSettings", TestPropImportSettings);
        RunTest("OverworldPropRenderer2D.LoadPropsForTiles", TestPropRendererLoadProps);
        RunTest("OverworldPropRenderer2D.ClearAll", TestPropRendererClearAll);
        RunTest("RoadRenderer.FullyBuiltIntercept", TestRoadRendererFullyBuiltIntercept);
        RunTest("RoadRenderer.DirectionAwareJunctions", TestRoadRendererDirectionAwareJunctions);
        RunTest("RoadRenderer.MergesR1Junctions", TestRoadRendererMergesR1Junctions);
        RunTest("RiverRenderer.FullyBuiltIntercept", TestRiverRendererFullyBuiltIntercept);

        // 相机测试
        RunTest("OverworldCamera2D.FocusOn", TestCameraFocusOn);
        RunTest("OverworldCamera2D.Zoom", TestCameraZoom);
        RunTest("OverworldCamera2D.SettleWithoutOvershoot", TestCameraSettleWithoutOvershoot);

        // A* 寻路测试
        RunTest("HexOverworldAStar.FindPath", TestAStarFindPath);
        RunTest("HexOverworldAStar.FindPathPixels", TestAStarFindPathPixels);

        // Chunk 系统测试
        RunTest("ChunkManager.Initialize", TestChunkManagerInitialize);
        RunTest("ChunkManager.UpdateChunks", TestChunkManagerUpdateChunks);

        // 性能监控测试
        RunTest("PerformanceStats.GetStats", TestPerformanceStats);

        // ========================================
        // POI 面板调用链测试
        // ========================================

        // TownPanel 信号测试
        RunTest("TownPanel.Instantiate", TestTownPanelInstantiate);
        RunTest("TownPanel.SignalFacilitySelected", TestTownPanelSignalFacilitySelected);
        RunTest("TownPanel.SignalLeaveTown", TestTownPanelSignalLeaveTown);
        RunTest("TownPanel.ShowTown", TestTownPanelShowTown);

        // PoiTownAdapter 测试
        RunTest("PoiTownAdapter.CreateTownNode", TestPoiTownAdapterCreateTownNode);
        RunTest("PoiTownAdapter.OpensTownPanelDirectly", TestPoiTownAdapterOpensTownPanelDirectly);

        // 二级面板实例化测试
        RunTest("SmithyPanel.Instantiate", TestSmithyPanelInstantiate);
        RunTest("RecruitPanel.Instantiate", TestRecruitPanelInstantiate);
        RunTest("TemplePanel.Instantiate", TestTemplePanelInstantiate);
        RunTest("ArenaPanel.Instantiate", TestArenaPanelInstantiate);
        RunTest("RestPanel.Instantiate", TestRestPanelInstantiate);
        RunTest("QuestBoardPanel.Instantiate", TestQuestBoardPanelInstantiate);

        // 二级面板信号测试
        RunTest("SmithyPanel.SignalSmithyFinished", TestSmithyPanelSignalFinished);
        RunTest("RecruitPanel.SignalRecruitFinished", TestRecruitPanelSignalFinished);
        RunTest("TemplePanel.SignalTempleFinished", TestTemplePanelSignalFinished);
        RunTest("ArenaPanel.SignalArenaFinished", TestArenaPanelSignalFinished);
        RunTest("RestPanel.SignalRestFinished", TestRestPanelSignalFinished);

        // 实体交互选项闭环测试
        RunTest("InteractionManager.TalkEmitsDialogue", TestInteractionManagerTalkEmitsDialogue);
        RunTest("InteractionManager.TradeEmitsTrade", TestInteractionManagerTradeEmitsTrade);
        RunTest("InteractionManager.AttackEmitsCombat", TestInteractionManagerAttackEmitsCombat);

        // PoiSecondaryPanelRouter 路由测试
        RunTest("PoiSecondaryPanelRouter.Instantiate", TestPoiSecondaryPanelRouterInstantiate);
        RunTest("PoiSecondaryPanelRouter.TryCloseActivePanel", TestPoiSecondaryPanelRouterTryClose);

        // 百科图鉴与发现日志测试
        RunTest("Encyclopedia.LoadBestiaryData", TestEncyclopediaLoadBestiaryData);
        RunTest("DiscoveryJournal.EncounterCreature", TestJournalEncounterCreature);
        RunTest("DiscoveryJournal.DefeatLegendary", TestJournalDefeatLegendary);

        // Shader 及环境系统测试
        RunTest("DayNightController2D.Tick", () => ShaderSystemTests.TestDayNightController());
        RunTest("NightLightingController2D.ShaderAndActivation", () => ShaderSystemTests.TestNightLightingController(this));
        RunTest("FogOverlay2D.ShaderAndMask", () => ShaderSystemTests.TestFogOverlayShader(this));

        GD.Print("[Test] RunAllTests completed.");
    }

    private void RunTest(string name, System.Func<bool> test)
    {
        try
        {
            bool result = test();
            if (result)
            {
                _passed++;
                _details.Add($"  ✓ {name}");
                GD.Print($"  ✓ {name}");
            }
            else
            {
                _failed++;
                _details.Add($"  ✗ {name} — FAILED");
                GD.Print($"  ✗ {name} — FAILED");
            }
        }
        catch (System.Exception ex)
        {
            _failed++;
            _details.Add($"  ✗ {name} — EXCEPTION: {ex.Message}");
            GD.Print($"  ✗ {name} — EXCEPTION: {ex.Message}");
        }
    }

    // ========================================
    // 测试用例
    // ========================================

    private bool TestHexOverworldGenerator()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);
        return grid != null && grid.TileCount() > 0;
    }

    private bool TestHexOverworldGridTileCount()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);
        return grid.TileCount() == 64 * 48;
    }

    private bool TestHexOverworldGridGetNeighbors()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);
        var neighbors = grid.GetNeighbors(32, 24);
        return neighbors != null && neighbors.Length > 0 && neighbors.Length <= 6;
    }

    private bool TestRendererInitialize()
    {
        var renderer = new HexOverworldRenderer2D();
        renderer.Initialize();
        return renderer.Name == "HexOverworldRenderer2D";
    }

    private bool TestRendererLoadFromGrid()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var renderer = new HexOverworldRenderer2D();
        renderer.Initialize();
        renderer.LoadFromGrid(grid);

        // 验证加载成功（通过检查子节点数量）
        return renderer.GetChildCount() > 0;
    }

    private bool TestRendererLoadTiles()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var renderer = new HexOverworldRenderer2D();
        renderer.Initialize();

        // 只加载部分 tiles
        var tiles = new List<HexOverworldTile>();
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (count++ >= 100) break;
            tiles.Add(tile);
        }
        renderer.LoadTiles(tiles);

        return renderer.GetChildCount() > 0;
    }

    private bool TestRendererClearAll()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var renderer = new HexOverworldRenderer2D();
        renderer.Initialize();
        renderer.LoadFromGrid(grid);
        renderer.ClearAll();

        return renderer.GetChildCount() == 0;
    }

    private bool TestHexOverworldRenderer2DAnyNewOptimization()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);
        var renderer = new HexOverworldRenderer2D();
        renderer.Initialize();

        var tiles = new List<HexOverworldTile>();
        int count = 0;
        foreach (var tile in grid.Tiles.Values)
        {
            if (count++ >= 10) break;
            tiles.Add(tile);
        }

        // 1. 第一次加载（会更新 _dataDirty = true 并调用 RebuildGroundSprite，之后 _dataDirty 会被设为 false）
        renderer.LoadTiles(tiles);

        var dataDirtyField = typeof(HexOverworldRenderer2D).GetField("_dataDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // 2. 将脏标记手动清零，代表已经被处理完毕
        dataDirtyField.SetValue(renderer, false);

        // 3. 再次重复加载完全相同的 tiles。应该被 LoadedTiles HashSet 直接拦截，anyNew 为 false。
        renderer.LoadTiles(tiles);

        // 4. _dataDirty 应该依然为 false
        bool isDataDirty = (bool)dataDirtyField.GetValue(renderer);
        return !isDataDirty;
    }

    private bool TestPropRendererInitialize()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var propRenderer = new OverworldPropRenderer2D();
        propRenderer.Initialize(12345, grid);

        return propRenderer.Name == "OverworldPropRenderer2D";
    }

    private bool TestPropRendererTextureFiltering()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var propRenderer = new OverworldPropRenderer2D();
        propRenderer.Initialize(12345, grid);

        return propRenderer.TextureFilter == CanvasItem.TextureFilterEnum.LinearWithMipmaps;
    }

    private bool TestPropImportSettings()
    {
        string[] names =
        [
            "forest_0", "forest_1", "forest_2", "forest_3",
            "grassland_0", "grassland_1", "grassland_2", "grassland_3",
            "mountain_0", "mountain_1", "mountain_2", "mountain_3",
        ];

        foreach (string name in names)
        {
            string path = $"res://BladeHexFrontend/src/assets/tiles/overworld/{name}.png.import";
            if (!FileAccess.FileExists(path))
                return false;

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
                return false;

            string text = file.GetAsText();
            if (!text.Contains("mipmaps/generate=true"))
                return false;
            if (!text.Contains("process/fix_alpha_border=true"))
                return false;
            if (!text.Contains("process/premult_alpha=false"))
                return false;
        }

        return true;
    }

    private bool TestPropRendererLoadProps()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var propRenderer = new OverworldPropRenderer2D();
        propRenderer.Initialize(12345, grid);
        propRenderer.LoadPropsForTiles(grid.Tiles.Values);

        return propRenderer.PropCount > 0;
    }

    private bool TestPropRendererClearAll()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var propRenderer = new OverworldPropRenderer2D();
        propRenderer.Initialize(12345, grid);
        propRenderer.LoadPropsForTiles(grid.Tiles.Values);
        propRenderer.ClearAll();

        return propRenderer.PropCount == 0;
    }

    private bool TestRoadRendererFullyBuiltIntercept()
    {
        var roadRenderer = new RoadRenderer();
        var field = typeof(RoadRenderer).GetField("_roadMeshes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<MeshInstance2D>)field.GetValue(roadRenderer);
        list.Add(new MeshInstance2D());

        roadRenderer.Initialize(new ChunkManager());

        // 1. 测试拦截：FullyBuilt = true 时，OnNewChunksLoaded 应该被拦截，列表大小不改变
        roadRenderer.FullyBuilt = true;
        roadRenderer.OnNewChunksLoaded(new List<ChunkData>());
        if (list.Count != 1) return false;

        // 2. 测试不拦截：FullyBuilt = false 时，OnNewChunksLoaded 应该触发 rebuild 并由于没有道路而清空列表
        roadRenderer.FullyBuilt = false;
        var chunk = new ChunkData();
        chunk.ChunkCoord = new Vector2I(0, 0);
        var tile = new HexOverworldTile();
        tile.Coord = new Vector2I(0, 0);
        tile.IsRoad = true;
        chunk.Tiles[new Vector2I(0, 0)] = tile;
        roadRenderer.OnNewChunksLoaded(new List<ChunkData> { chunk });
        return list.Count == 0;
    }

    private bool TestRoadRendererDirectionAwareJunctions()
    {
        var chunks = new Dictionary<Vector2I, ChunkData>();
        AddRoadTile(chunks, new Vector2I(14, 0), 1 << 0);
        AddRoadTile(chunks, new Vector2I(15, 0), 1 << 0);
        AddRoadTile(chunks, new Vector2I(16, 0), 1 << 0);
        AddRoadTile(chunks, new Vector2I(17, 0), 1 << 0);

        // Adjacent road cells from another road should not create a visual junction unless RoadDirections connect them.
        AddRoadTile(chunks, new Vector2I(16, -1), 1 << 0);
        AddRoadTile(chunks, new Vector2I(17, -1), 1 << 0);

        var manager = new ChunkManager();
        foreach (var kvp in chunks)
            manager.ActiveChunks[kvp.Key] = kvp.Value;

        var roadRenderer = new RoadRenderer();
        AddChild(roadRenderer);
        roadRenderer.Initialize(manager);
        roadRenderer.RebuildFromChunks();

        bool result = roadRenderer.RoadCount > 0 && roadRenderer.DebugJunctionCount == 0;
        roadRenderer.QueueFree();
        return result;
    }

    private bool TestRoadRendererMergesR1Junctions()
    {
        var chunks = new Dictionary<Vector2I, ChunkData>();
        AddRoadTile(chunks, new Vector2I(0, 0), DirectionMask(0, 3, 5));
        AddRoadTile(chunks, new Vector2I(1, 0), DirectionMask(0, 2, 3));
        AddRoadTile(chunks, new Vector2I(-1, 0), 0);
        AddRoadTile(chunks, new Vector2I(0, 1), 0);
        AddRoadTile(chunks, new Vector2I(2, 0), 0);
        AddRoadTile(chunks, new Vector2I(1, -1), 0);

        var manager = new ChunkManager();
        foreach (var kvp in chunks)
            manager.ActiveChunks[kvp.Key] = kvp.Value;

        var roadRenderer = new RoadRenderer();
        AddChild(roadRenderer);
        roadRenderer.Initialize(manager);
        roadRenderer.RebuildFromChunks();

        bool result = roadRenderer.RoadCount > 0 && roadRenderer.DebugJunctionCount == 1;
        roadRenderer.QueueFree();
        return result;
    }

    private static void AddRoadTile(Dictionary<Vector2I, ChunkData> chunks, Vector2I coord, int roadDirections)
    {
        var chunkCoord = ChunkData.WorldToChunk(coord.X, coord.Y);
        if (!chunks.TryGetValue(chunkCoord, out var chunk))
        {
            chunk = new ChunkData
            {
                ChunkCoord = chunkCoord,
                IsActive = true,
                IsGenerated = true,
            };
            chunks[chunkCoord] = chunk;
        }

        chunk.Tiles[coord] = new HexOverworldTile
        {
            Coord = coord,
            PixelPos = HexOverworldTile.AxialToPixel(coord.X, coord.Y),
            IsRoad = true,
            RoadDirections = roadDirections,
            RoadClassVal = 1,
        };
    }

    private static int DirectionMask(params int[] directions)
    {
        int result = 0;
        foreach (int direction in directions)
            result |= 1 << direction;
        return result;
    }

    private bool TestRiverRendererFullyBuiltIntercept()
    {
        var riverRenderer = new RiverRenderer();
        var field = typeof(RiverRenderer).GetField("_riverMeshes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = (List<MeshInstance2D>)field.GetValue(riverRenderer);
        list.Add(new MeshInstance2D());

        riverRenderer.Initialize(new ChunkManager());

        // 1. 测试拦截：FullyBuilt = true 时，RebuildFromChunks 应该直接返回，列表大小不改变
        riverRenderer.FullyBuilt = true;
        riverRenderer.RebuildFromChunks();
        if (list.Count != 1) return false;

        // 2. 测试不拦截：FullyBuilt = false 时，RebuildFromChunks 应该清空列表
        riverRenderer.FullyBuilt = false;
        riverRenderer.RebuildFromChunks();
        return list.Count == 0;
    }

    private bool TestCameraFocusOn()
    {
        var camera = new OverworldCamera2D();
        AddChild(camera);

        camera.FocusOnImmediate(new Vector2(1000, 1000));

        // 验证相机位置已更新
        bool result = camera.Position.X > 0 && camera.Position.Y > 0;

        camera.QueueFree();
        return result;
    }

    private bool TestCameraZoom()
    {
        var camera = new OverworldCamera2D();
        AddChild(camera);

        camera.Zoom = new Vector2(2.0f, 2.0f);

        bool result = camera.Zoom.X == 2.0f && camera.Zoom.Y == 2.0f;

        camera.QueueFree();
        return result;
    }

    private bool TestCameraSettleWithoutOvershoot()
    {
        var camera = new OverworldCamera2D();
        AddChild(camera);

        camera.FocusOnImmediate(Vector2.Zero);
        var target = new Vector2(1000, 0);
        camera.FocusOn(target);

        // Simulate a few hitchy frames after map streaming. Camera smoothing must not overshoot,
        // otherwise pixel-grid snapping can make world sprites jitter after movement stops.
        for (int i = 0; i < 6; i++)
        {
            camera._Process(0.25);
            if (camera.Position.X < -0.001f || camera.Position.X > target.X + 0.001f)
            {
                camera.QueueFree();
                return false;
            }
        }

        bool result = camera.Position.DistanceTo(target) <= 1.0f;
        camera.QueueFree();
        return result;
    }

    private bool TestAStarFindPath()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var astar = new HexOverworldAStar { Grid = grid, IgnorePassability = true };
        var path = astar.FindPath(new Vector2I(10, 10), new Vector2I(20, 20));

        return path != null && path.Length > 0;
    }

    private bool TestAStarFindPathPixels()
    {
        var gen = new HexOverworldGenerator();
        var grid = gen.Generate(64, 48, 12345);

        var astar = new HexOverworldAStar { Grid = grid, IgnorePassability = true };
        var start = HexOverworldTile.AxialToPixel(10, 10);
        var end = HexOverworldTile.AxialToPixel(20, 20);
        var path = astar.FindPathPixels(start, end);

        return path != null && path.Length > 0;
    }

    private bool TestChunkManagerInitialize()
    {
        var chunkManager = new ChunkManager();
        chunkManager.Initialize(12345, 64, 48, 16, 16);

        return chunkManager != null;
    }

    private bool TestChunkManagerUpdateChunks()
    {
        var chunkManager = new ChunkManager();
        chunkManager.Initialize(12345, 64, 48, 16, 16);

        var newChunks = chunkManager.UpdateChunks(32, 24);

        return newChunks != null;
    }

    private bool TestPerformanceStats()
    {
        // 测试性能统计收集不会崩溃
        var memInfo = OS.GetMemoryInfo();
        double fps = Engine.GetFramesPerSecond();

        return memInfo.ContainsKey("physical") && fps >= 0;
    }

    // ========================================
    // POI 面板调用链测试
    // ========================================

    private bool TestTownPanelInstantiate()
    {
        var panel = new TownPanel();
        return panel != null;
    }

    private bool TestTownPanelSignalFacilitySelected()
    {
        var panel = new TownPanel();
        AddChild(panel);

        bool signalReceived = false;
        int receivedType = -1;

        // 连接信号
        panel.FacilitySelected += (int facilityType) =>
        {
            signalReceived = true;
            receivedType = facilityType;
        };

        // 模拟发射信号（Market = 0）
        panel.EmitSignal(TownPanel.SignalName.FacilitySelected, (int)TownFacility.FacilityType.Market);

        bool result = signalReceived && receivedType == (int)TownFacility.FacilityType.Market;

        panel.QueueFree();
        return result;
    }

    private bool TestTownPanelSignalLeaveTown()
    {
        var panel = new TownPanel();
        AddChild(panel);

        bool signalReceived = false;
        panel.LeaveTown += () => signalReceived = true;

        panel.EmitSignal(TownPanel.SignalName.LeaveTown);

        panel.QueueFree();
        return signalReceived;
    }

    private bool TestTownPanelShowTown()
    {
        var panel = new TownPanel();
        AddChild(panel);

        // 创建测试城镇
        var town = new OverworldTown
        {
            TownName = "测试城镇",
            Prosperity = 50,
            Faction = "kingdom"
        };
        town.SetupDefaultFacilities();

        panel.ShowTown(town);
        bool result = panel.IsPanelVisible();

        panel.QueueFree();
        town.QueueFree();
        return result;
    }

    private bool TestPoiTownAdapterCreateTownNode()
    {
        var poi = new OverworldPOI
        {
            PoiName = "测试村庄",
            Prosperity = 30,
            OwningFaction = "neutral",
            GarrisonCurrent = 10,
            GarrisonMax = 20,
            PoiTypeEnum = OverworldPOI.POIType.Village
        };

        var town = PoiTownAdapter.CreateTownNode(poi);

        bool result = town != null
            && town.TownName == "测试村庄"
            && town.Prosperity == 30
            && town.Faction == "neutral";

        town.QueueFree();
        return result;
    }

    private bool TestPoiTownAdapterOpensTownPanelDirectly()
    {
        // 城镇、村庄、城堡、矿场、农庄应该直接打开TownPanel
        bool townResult = PoiTownAdapter.OpensTownPanelDirectly(OverworldPOI.POIType.Town);
        bool villageResult = PoiTownAdapter.OpensTownPanelDirectly(OverworldPOI.POIType.Village);
        bool castleResult = PoiTownAdapter.OpensTownPanelDirectly(OverworldPOI.POIType.Castle);
        bool mineResult = PoiTownAdapter.OpensTownPanelDirectly(OverworldPOI.POIType.Mine);
        bool farmResult = PoiTownAdapter.OpensTownPanelDirectly(OverworldPOI.POIType.Farm);

        // 其他类型不应该直接打开
        bool lairResult = !PoiTownAdapter.OpensTownPanelDirectly(OverworldPOI.POIType.Lair);

        return townResult && villageResult && castleResult && mineResult && farmResult && lairResult;
    }

    private bool TestSmithyPanelInstantiate()
    {
        var panel = new SmithyPanel();
        AddChild(panel);
        panel.QueueFree();
        return true;
    }

    private bool TestRecruitPanelInstantiate()
    {
        var panel = new RecruitPanel();
        AddChild(panel);
        panel.QueueFree();
        return true;
    }

    private bool TestTemplePanelInstantiate()
    {
        var panel = new TemplePanel();
        AddChild(panel);
        panel.QueueFree();
        return true;
    }

    private bool TestArenaPanelInstantiate()
    {
        var panel = new ArenaPanel();
        AddChild(panel);
        panel.QueueFree();
        return true;
    }

    private bool TestRestPanelInstantiate()
    {
        var panel = new RestPanel();
        AddChild(panel);
        panel.QueueFree();
        return true;
    }

    private bool TestQuestBoardPanelInstantiate()
    {
        var panel = new QuestBoardPanel();
        AddChild(panel);
        panel.QueueFree();
        return true;
    }

    private bool TestSmithyPanelSignalFinished()
    {
        var panel = new SmithyPanel();
        AddChild(panel);

        bool signalReceived = false;
        panel.SmithyFinished += () => signalReceived = true;

        panel.EmitSignal(SmithyPanel.SignalName.SmithyFinished);

        panel.QueueFree();
        return signalReceived;
    }

    private bool TestRecruitPanelSignalFinished()
    {
        var panel = new RecruitPanel();
        AddChild(panel);

        bool signalReceived = false;
        bool receivedValue = false;
        panel.RecruitFinished += (bool hired) =>
        {
            signalReceived = true;
            receivedValue = hired;
        };

        panel.EmitSignal(RecruitPanel.SignalName.RecruitFinished, true);

        panel.QueueFree();
        return signalReceived && receivedValue;
    }

    private bool TestTemplePanelSignalFinished()
    {
        var panel = new TemplePanel();
        AddChild(panel);

        bool signalReceived = false;
        panel.TempleFinished += () => signalReceived = true;

        panel.EmitSignal(TemplePanel.SignalName.TempleFinished);

        panel.QueueFree();
        return signalReceived;
    }

    private bool TestArenaPanelSignalFinished()
    {
        var panel = new ArenaPanel();
        AddChild(panel);

        bool signalReceived = false;
        panel.ArenaFinished += () => signalReceived = true;

        panel.EmitSignal(ArenaPanel.SignalName.ArenaFinished);

        panel.QueueFree();
        return signalReceived;
    }

    private bool TestRestPanelSignalFinished()
    {
        var panel = new RestPanel();
        AddChild(panel);

        bool signalReceived = false;
        panel.RestFinished += () => signalReceived = true;

        panel.EmitSignal(RestPanel.SignalName.RestFinished);

        panel.QueueFree();
        return signalReceived;
    }

    private bool TestInteractionManagerTalkEmitsDialogue()
    {
        var mgr = new InteractionManager();
        AddChild(mgr);

        var enemy = MakeHumanoidInteractionEnemy("测试旅人");
        AddChild(enemy);

        bool signalReceived = false;
        mgr.DialogueRequested += profile =>
        {
            signalReceived = profile is NPCProfile;
        };

        mgr.ExecuteOption(InteractionOption.CreateTalk(), enemy);

        enemy.QueueFree();
        mgr.QueueFree();
        return signalReceived;
    }

    private bool TestInteractionManagerTradeEmitsTrade()
    {
        var mgr = new InteractionManager();
        AddChild(mgr);

        var enemy = MakeHumanoidInteractionEnemy("测试商队");
        AddChild(enemy);

        bool signalReceived = false;
        string source = "";
        mgr.TradeRequested += name =>
        {
            signalReceived = true;
            source = name;
        };

        mgr.ExecuteOption(InteractionOption.CreateTrade(), enemy);

        enemy.QueueFree();
        mgr.QueueFree();
        return signalReceived && source == "测试商队";
    }

    private bool TestInteractionManagerAttackEmitsCombat()
    {
        var grid = new HexOverworldGrid();
        grid.Initialize(8, 8);

        var mgr = new InteractionManager { HexGrid = grid };
        AddChild(mgr);

        var enemy = MakeHumanoidInteractionEnemy("测试敌人");
        enemy.Position = HexOverworldTile.AxialToPixel(1, 1);
        AddChild(enemy);

        bool signalReceived = false;
        mgr.CombatRequested += ctx =>
        {
            signalReceived = ctx != null && ctx.EncounterPosition != Vector2I.Zero;
        };

        mgr.ExecuteOption(InteractionOption.CreateAttack(), enemy);

        enemy.QueueFree();
        mgr.QueueFree();
        return signalReceived;
    }

    private static OverworldEnemy MakeHumanoidInteractionEnemy(string name)
    {
        return new OverworldEnemy
        {
            DisplayName = name,
            NpcProfile = new NPCProfile
            {
                npcName = name,
                npcType = NPCProfile.NpcType.Merchant,
                attitude = NPCProfile.Attitude.Friendly,
            },
        };
    }

    private bool TestPoiSecondaryPanelRouterInstantiate()
    {
        // 创建路由器所需的依赖
        var parent = new Node();
        AddChild(parent);

        TownPanel? townPanel = null;
        OverworldTown? currentTown = null;
        EconomyManager? economy = null;
        OverworldParty? party = null;

        var router = new PoiSecondaryPanelRouter(
            parent,
            () => townPanel,
            () => currentTown,
            () => economy,
            () => party,
            null, // recruitService
            null, // questGenerator
            null, // questManager
            null, // overworldUi
            null, // tournamentService
            null, // entityMgr
            null, // reputationTracker
            (BattleContext ctx, int prize) => { }, // arenaCombatRequested
            (BattleContext ctx, Godot.Collections.Dictionary state) => { }, // tournamentCombatRequested
            () => { }  // cleanupInteraction
        );

        bool result = router != null;

        parent.QueueFree();
        return result;
    }

    private bool TestPoiSecondaryPanelRouterTryClose()
    {
        var parent = new Node();
        AddChild(parent);

        var router = new PoiSecondaryPanelRouter(
            parent,
            () => null,
            () => null,
            () => null,
            () => null,
            null, null, null, null, null, null, null,
            (BattleContext ctx, int prize) => { },
            (BattleContext ctx, Godot.Collections.Dictionary state) => { },
            () => { }
        );

        // 没有活跃面板时应该返回false
        bool result = !router.TryCloseActivePanel();

        parent.QueueFree();
        return result;
    }

    private bool TestEncyclopediaLoadBestiaryData()
    {
        var panel = new BladeHex.View.UI.Encyclopedia.EncyclopediaIndexPanel();
        panel.LoadBestiaryData();

        // 验证文件是否已回写且有数据
        const string path = "res://BladeHexFrontend/src/View/UI/Encyclopedia/bestiary_entries.json";
        if (!FileAccess.FileExists(path)) return false;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return false;

        var json = file.GetAsText();
        return json.Contains("grunt_goblin_warrior") && json.Contains("legend_banshee_queen");
    }

    private bool TestJournalEncounterCreature()
    {
        var journal = new BladeHex.Strategic.Encyclopedia.DiscoveryJournal();
        bool added = journal.EncounterCreature("grunt_goblin_warrior");
        bool exists = journal.EncounteredCreatures.Contains("grunt_goblin_warrior");

        // 重复添加应该返回 false
        bool dup = journal.EncounterCreature("grunt_goblin_warrior");

        return added && exists && !dup;
    }

    private bool TestJournalDefeatLegendary()
    {
        var journal = new BladeHex.Strategic.Encyclopedia.DiscoveryJournal();
        bool addedEncounter = journal.EncounterLegendary("legend_hydra");
        bool addedDefeat = journal.DefeatLegendary("legend_hydra");

        bool isDefeated = journal.IsLegendaryDefeated("legend_hydra");
        bool isEncountered = journal.EncounteredLegendary.Contains("legend_hydra");

        return addedEncounter && addedDefeat && isDefeated && isEncountered;
    }
}
