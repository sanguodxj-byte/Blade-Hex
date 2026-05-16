// OverworldScene3D.Navigation.cs
// NavigationServer3D 自由移动 — 按地形类型分 region，支持代价
// 关键：相邻 hex tile 共享边顶点坐标完全一致，NavigationServer 自动连接
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // Navigation 字段
    // ========================================

    /// <summary>按地形类型分组的 region（每种地形一个 region，设置不同 travel_cost）</summary>
    private readonly Dictionary<int, Rid> _navRegions = new();

    /// <summary>按地形类型分组的 navmesh 数据</summary>
    private readonly Dictionary<int, NavMeshBuilder> _navBuilders = new();

    /// <summary>已加入 navmesh 的 tile 坐标</summary>
    private readonly HashSet<Vector2I> _navTileCoords = new();

    private NavigationAgent3D? _navAgent;
    private Node3D? _navAgentHolder;
    private bool _navDirty = false;
    private float _navRebuildTimer = 0f;

    /// <summary>NavMesh 构建辅助 — 收集顶点和多边形</summary>
    private class NavMeshBuilder
    {
        /// <summary>顶点坐标 → 索引（去重共享顶点）</summary>
        public Dictionary<long, int> VertexIndex = new();
        public List<Vector3> Vertices = new();
        public List<int[]> Polygons = new();

        /// <summary>添加一个六边形多边形，自动去重共享顶点</summary>
        public void AddHexagon(Vector3 center, float radius)
        {
            var indices = new int[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.DegToRad(60.0f * i); // flat-top（与渲染器一致）
                float x = center.X + radius * Mathf.Cos(angle);
                float z = center.Z + radius * Mathf.Sin(angle);

                // 量化坐标用于去重（精度 0.001）
                long key = QuantizeKey(x, z);

                if (!VertexIndex.TryGetValue(key, out int idx))
                {
                    idx = Vertices.Count;
                    Vertices.Add(new Vector3(x, 0, z));
                    VertexIndex[key] = idx;
                }
                indices[i] = idx;
            }
            Polygons.Add(indices);
        }

        /// <summary>将坐标量化为 long key（精度 0.001）</summary>
        private static long QuantizeKey(float x, float z)
        {
            int ix = (int)Mathf.Round(x * 1000.0f);
            int iz = (int)Mathf.Round(z * 1000.0f);
            return ((long)ix << 32) | (long)(uint)iz;
        }

        /// <summary>构建 NavigationMesh</summary>
        public NavigationMesh Build()
        {
            var navMesh = new NavigationMesh();
            navMesh.Vertices = Vertices.ToArray();
            foreach (var poly in Polygons)
                navMesh.AddPolygon(poly);
            return navMesh;
        }
    }

    // ========================================
    // 地形代价映射
    // ========================================

    /// <summary>地形类型 → 通行代价（1.0 = 正常，越高越慢）</summary>
    private static float GetTerrainTravelCost(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.Road => 0.5f,      // 道路最快
            HexOverworldTile.TerrainType.Plains => 1.0f,
            HexOverworldTile.TerrainType.Grassland => 1.0f,
            HexOverworldTile.TerrainType.Savanna => 1.2f,
            HexOverworldTile.TerrainType.Sand => 1.5f,
            HexOverworldTile.TerrainType.Forest => 1.5f,
            HexOverworldTile.TerrainType.Taiga => 1.5f,
            HexOverworldTile.TerrainType.DenseForest => 2.0f,
            HexOverworldTile.TerrainType.Jungle => 2.0f,
            HexOverworldTile.TerrainType.Hills => 2.0f,
            HexOverworldTile.TerrainType.Rocky => 2.5f,
            HexOverworldTile.TerrainType.Swamp => 2.5f,
            HexOverworldTile.TerrainType.Bog => 2.5f,
            HexOverworldTile.TerrainType.Snow => 2.0f,
            HexOverworldTile.TerrainType.Ice => 2.5f,
            HexOverworldTile.TerrainType.Wasteland => 1.8f,
            _ => 1.0f,
        };
    }

    // ========================================
    // 初始化
    // ========================================

    private void InitNavigation()
    {
        // NavigationAgent3D
        _navAgentHolder = new Node3D();
        _navAgentHolder.Name = "NavAgentHolder";
        AddChild(_navAgentHolder);

        _navAgent = new NavigationAgent3D();
        _navAgent.Name = "NavAgent";
        _navAgent.PathDesiredDistance = 0.2f;
        _navAgent.TargetDesiredDistance = 0.3f;
        _navAgent.PathMaxDistance = 500.0f;
        _navAgent.NavigationLayers = 1;
        _navAgent.AvoidanceEnabled = false;
        _navAgentHolder.AddChild(_navAgent);

        // 初始位置
        var startWorld = CoordConverter.PixelToWorld3D(_playerPixelPos);
        _navAgentHolder.Position = new Vector3(startWorld.X, 0, startWorld.Z);

        GD.Print("[OverworldScene3D] NavigationServer3D 初始化完成");
    }

    private void BuildInitialNavMesh()
    {
        // 收集所有已加载的可通行 tile
        var tiles = CollectAllPassableTiles();
        if (tiles.Count == 0) return;

        // 按地形类型分组构建
        foreach (var tile in tiles)
        {
            if (_navTileCoords.Contains(tile.Coord)) continue;
            _navTileCoords.Add(tile.Coord);
            AddTileToBuilder(tile);
        }

        // 提交所有 region
        CommitAllRegions();

        GD.Print($"[OverworldScene3D] NavMesh: {_navTileCoords.Count} tiles, {_navRegions.Count} terrain regions");
    }

    // ========================================
    // 动态更新
    // ========================================

    public void OnNewChunkNavigation(ChunkData chunk)
    {
        bool added = false;
        foreach (var tile in chunk.Tiles.Values)
        {
            if (!tile.IsPassable) continue;
            if (_navTileCoords.Contains(tile.Coord)) continue;
            _navTileCoords.Add(tile.Coord);
            AddTileToBuilder(tile);
            added = true;
        }
        if (added) _navDirty = true;
    }

    private void UpdateNavigation(float dt)
    {
        if (!_navDirty) return;
        _navRebuildTimer += dt;
        if (_navRebuildTimer >= 0.5f)
        {
            _navRebuildTimer = 0f;
            _navDirty = false;
            CommitAllRegions();
        }
    }

    // ========================================
    // 构建辅助
    // ========================================

    private void AddTileToBuilder(HexOverworldTile tile)
    {
        int terrainKey = (int)tile.Terrain;
        // 道路 tile 归入道路类型
        if (tile.IsRoad) terrainKey = (int)HexOverworldTile.TerrainType.Road;

        if (!_navBuilders.TryGetValue(terrainKey, out var builder))
        {
            builder = new NavMeshBuilder();
            _navBuilders[terrainKey] = builder;
        }

        var center = CoordConverter.PixelToWorld3D(tile.PixelPos);
        builder.AddHexagon(center, CoordConverter.WorldHexSize);
    }

    /// <summary>将所有 builder 的数据提交到 NavigationServer</summary>
    private void CommitAllRegions()
    {
        var map = GetWorld3D().NavigationMap;
        int totalPolygons = 0;
        int totalVertices = 0;

        foreach (var (terrainKey, builder) in _navBuilders)
        {
            if (builder.Polygons.Count == 0) continue;

            // 获取或创建 region
            if (!_navRegions.TryGetValue(terrainKey, out var regionRid))
            {
                regionRid = NavigationServer3D.RegionCreate();
                NavigationServer3D.RegionSetMap(regionRid, map);
                _navRegions[terrainKey] = regionRid;
            }

            // 设置地形代价
            var terrain = (HexOverworldTile.TerrainType)terrainKey;
            float cost = GetTerrainTravelCost(terrain);
            NavigationServer3D.RegionSetTravelCost(regionRid, cost);

            // 提交 navmesh
            var navMesh = builder.Build();
            NavigationServer3D.RegionSetNavigationMesh(regionRid, navMesh);

            totalPolygons += builder.Polygons.Count;
            totalVertices += builder.Vertices.Count;
        }

        GD.Print($"[Nav] CommitAllRegions: {_navRegions.Count} regions, {totalPolygons} polygons, {totalVertices} vertices, map={map}");
    }

    private List<HexOverworldTile> CollectAllPassableTiles()
    {
        var tiles = new List<HexOverworldTile>();
        if (_chunkManager != null)
        {
            foreach (var kvp in _chunkManager.ActiveChunks)
                foreach (var tile in kvp.Value.Tiles.Values)
                    if (tile.IsPassable) tiles.Add(tile);

            var gen = _chunkManager.Generator;
            if (gen != null)
            {
                int cw = gen.WorldWidth / ChunkData.ChunkSize;
                int ch = gen.WorldHeight / ChunkData.ChunkSize;
                for (int cq = 0; cq < cw; cq++)
                    for (int cr = 0; cr < ch; cr++)
                    {
                        var coord = new Vector2I(cq, cr);
                        if (_chunkManager.ActiveChunks.ContainsKey(coord)) continue;
                        if (_chunkManager.TryGetFromCache(coord, out var chunk))
                            foreach (var tile in chunk.Tiles.Values)
                                if (tile.IsPassable) tiles.Add(tile);
                    }
            }
        }
        else
        {
            foreach (var tile in _grid.Tiles.Values)
                if (tile.IsPassable) tiles.Add(tile);
        }
        return tiles;
    }

    // ========================================
    // 寻路 + 移动
    // ========================================

    private void StartPathfinding(Vector2 targetPixel)
    {
        if (_navAgent == null)
        {
            GD.PrintErr("[Nav] StartPathfinding: _navAgent is null!");
            return;
        }

        // 检查目标可通行
        var targetCoord = HexOverworldTile.PixelToAxial(targetPixel.X, targetPixel.Y);
        HexOverworldTile? targetTile = _chunkManager != null
            ? _chunkManager.GetTile(targetCoord.X, targetCoord.Y)
            : _grid.GetTile(targetCoord.X, targetCoord.Y);

        if (targetTile == null || !targetTile.IsPassable)
        {
            GD.Print($"[Nav] 目标不可通行: coord={targetCoord}, tile={targetTile?.Terrain}");
            return;
        }

        var targetWorld = CoordConverter.PixelToWorld3D(targetPixel);
        GD.Print($"[Nav] StartPathfinding: target={targetWorld}, agentPos={_navAgentHolder?.Position}, regions={_navRegions.Count}, tiles={_navTileCoords.Count}");

        _navAgent.TargetPosition = targetWorld;
        _playerMoving = true;
        _cameraFollowing = true;

        // 显示路径预览线
        ShowPathPreview();

        // 下一帧检查路径状态
        CallDeferred(nameof(DebugNavState));
    }

    private void DebugNavState()
    {
        if (_navAgent == null) return;
        bool finished = _navAgent.IsNavigationFinished();
        var nextPos = _navAgent.GetNextPathPosition();
        var agentPos = _navAgentHolder?.Position ?? Vector3.Zero;
        bool reachable = _navAgent.IsTargetReachable();
        GD.Print($"[Nav] DebugState: finished={finished}, reachable={reachable}, nextPos={nextPos}, agentPos={agentPos}, target={_navAgent.TargetPosition}");
    }

    private void UpdateNavigationMovement(float dt)
    {
        if (!_playerMoving || _navAgent == null) return;

        // 导航完成
        if (_navAgent.IsNavigationFinished())
        {
            GD.Print("[Nav] 导航完成");
            _playerMoving = false;
            return;
        }

        var nextPos = _navAgent.GetNextPathPosition();
        var currentWorld = _navAgentHolder!.Position;

        var direction = nextPos - currentWorld;
        if (direction.LengthSquared() < 0.001f)
        {
            GD.Print($"[Nav] direction 为零, nextPos={nextPos}, current={currentWorld}");
            _playerMoving = false;
            return;
        }
        direction = direction.Normalized();

        // 使用 MovementSpeedComponent 计算最终速度（含地形/季节/昼夜/负重/坐骑/技能/天气/ZoC）
        float speed;
        if (PlayerParty?.SpeedComponent != null)
        {
            // 同步天气因子到速度组件
            PlayerParty.SpeedComponent.WeatherSpeedFactor = WeatherSpeedFactor;
            speed = PlayerParty.SpeedComponent.CalculateSpeed(_playerPixelPos) * CoordConverter.PixelToWorld;
        }
        else
        {
            speed = (PlayerMoveSpeed * CoordConverter.PixelToWorld) * WeatherSpeedFactor;
        }
        var newPos = currentWorld + direction * speed * dt;

        // 更新位置
        _navAgentHolder.Position = newPos;
        _playerPixelPos = CoordConverter.World3DToPixel(newPos);
        if (PlayerParty != null)
            PlayerParty.Position = _playerPixelPos;

        if (_playerMesh != null)
            _playerMesh.Position = new Vector3(newPos.X, 0.4f, newPos.Z);
    }

    // ========================================
    // 玩家移动入口
    // ========================================

    private bool _cameraFollowing = false;
    private MeshInstance3D? _pathPreviewLine;
    private Vector3 _lastMoveDirection = Vector3.Forward;

    private void UpdatePlayerMovement(float dt)
    {
        UpdateNavigationMovement(dt);
        UpdateNavigation(dt);

        // 玩家图标朝向移动方向
        UpdatePlayerRotation();

        // 路径预览线更新
        UpdatePathPreview();

        // 摄像头跟随玩家（寻路中）
        if (_cameraFollowing && _playerMoving && _navAgentHolder != null)
        {
            _camera?.FocusOn(_navAgentHolder.Position);
        }
        else if (_cameraFollowing && !_playerMoving)
        {
            // 到达目的地，停止跟随
            _cameraFollowing = false;
            ClearPathPreview();
        }
    }

    // ========================================
    // 路径预览线
    // ========================================

    /// <summary>点击寻路后显示路径预览线</summary>
    private void ShowPathPreview()
    {
        if (_navAgent == null) return;

        // 延迟一帧获取路径（agent 需要一帧计算路径）
        CallDeferred(nameof(BuildPathPreviewDeferred));
    }

    private void BuildPathPreviewDeferred()
    {
        if (_navAgent == null || _navAgent.IsNavigationFinished()) return;

        ClearPathPreview();

        // 获取完整路径点（NavigationAgent 没有直接暴露，用 NavigationServer 查询）
        var map = GetWorld3D().NavigationMap;
        var from = _navAgentHolder?.Position ?? Vector3.Zero;
        var to = _navAgent.TargetPosition;
        var pathPoints = NavigationServer3D.MapGetPath(map, from, to, true);

        if (pathPoints.Length < 2) return;

        // 用 ImmediateMesh 画线
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var point in pathPoints)
        {
            mesh.SurfaceSetColor(new Color(1.0f, 0.85f, 0.3f, 0.8f));
            mesh.SurfaceAddVertex(point + new Vector3(0, 0.08f, 0));
        }
        mesh.SurfaceEnd();

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1.0f, 0.85f, 0.3f, 0.8f);
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.VertexColorUseAsAlbedo = true;
        mesh.SurfaceSetMaterial(0, mat);

        _pathPreviewLine = new MeshInstance3D();
        _pathPreviewLine.Mesh = mesh;
        _pathPreviewLine.Name = "PathPreview";
        AddChild(_pathPreviewLine);
    }

    private void UpdatePathPreview()
    {
        // 到达后自动清除
        if (!_playerMoving && _pathPreviewLine != null)
            ClearPathPreview();
    }

    private void ClearPathPreview()
    {
        if (_pathPreviewLine != null && IsInstanceValid(_pathPreviewLine))
        {
            _pathPreviewLine.QueueFree();
            _pathPreviewLine = null;
        }
    }

    // ========================================
    // 玩家图标朝向
    // ========================================

    private void UpdatePlayerRotation()
    {
        if (_playerMesh == null || _navAgent == null || !_playerMoving) return;

        var nextPos = _navAgent.GetNextPathPosition();
        var currentPos = _navAgentHolder?.Position ?? Vector3.Zero;
        var dir = nextPos - currentPos;

        if (dir.LengthSquared() > 0.01f)
        {
            _lastMoveDirection = dir.Normalized();
            // 旋转玩家 mesh 面朝移动方向（绕 Y 轴）
            float angle = Mathf.Atan2(_lastMoveDirection.X, _lastMoveDirection.Z);
            _playerMesh.Rotation = new Vector3(0, angle, 0);
        }
    }
}
