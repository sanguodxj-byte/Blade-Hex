using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Strategic;

/// <summary>
/// 战略层大地图上的玩家队伍实体（无网格平滑移动版）
/// </summary>
[GlobalClass]
public partial class OverworldParty : Node2D, IOverworldMapEntity
{
    [Export] public float BaseMoveSpeed { get; set; } = 300.0f;
    [Export] public Texture2D? OverworldSprite { get; set; }
    [Export] public SpriteFrames? OverworldFrames { get; set; }

    // ========================================
    // 队伍名册（骑砍核心数据）
    // ========================================

    /// <summary>雇佣兵团名册 — 持有所有队员数据</summary>
    public PartyRoster Roster { get; set; } = new();

    /// <summary>队伍背包 — 持有战利品、装备、消耗品</summary>
    public PartyInventory Inventory { get; set; } = new();

    // ========================================
    // 运行时状态
    // ========================================

    public List<Vector2> Path = new();
    [Export] public bool IsMoving { get; set; } = false;

    /// <summary>最终目标位置 — 用于跨 chunk 持续寻路</summary>
    private Vector2 _finalTarget = Vector2.Zero;

    /// <summary>是否有跨 chunk 的远距离目标（需要到达边界后重新寻路）</summary>
    private bool _hasPendingTarget = false;

    // ========================================
    // 六边形地图导航（替代旧 overworld_map）
    // ========================================

    public HexOverworldGrid? HexGrid;
    public HexOverworldAStar? HexAStar;

    /// <summary>Chunk 模式专用寻路</summary>
    public ChunkAStar? ChunkAStar;
    public ChunkManager? ChunkManager;

    // ========================================
    // 船只与海上航行
    // ========================================

    /// <summary>当前拥有的船只（null=无船）</summary>
    public ShipData? CurrentShip { get; set; } = null;

    /// <summary>是否正在海上航行</summary>
    public bool IsAtSea { get; set; } = false;

    /// <summary>海上移动累计距离（用于遭遇检定）</summary>
    private float _seaDistanceTraveled = 0f;

    /// <summary>是否有待处理的海上遭遇（由 OverworldScene3D 消费）</summary>
    public bool SeaEncounterPending { get; set; } = false;

    // ========================================
    // 移动速度组件（由 OverworldScene3D 初始化注入依赖）
    // ========================================

    public MovementSpeedComponent? SpeedComponent;

    // ========================================
    // 视觉子节点
    // ========================================

    private BladeHex.View.Unit.CharacterView2D? _characterView;
    private Polygon2D? _fallbackPoly;

    public override void _Ready()
    {
        SetupVisuals();
    }

    private void SetupVisuals()
    {
        // 角色多层 2D 视图（与战斗 3D / UI 头像共用同一套换装解析）
        _characterView = new BladeHex.View.Unit.CharacterView2D
        {
            Name = "CharacterView2D",
            ContentScale = 0.6f,
        };
        AddChild(_characterView);

        // 占位符：如果暂时没有 Leader 数据，显示金黄色菱形
        _fallbackPoly = new Polygon2D();
        float radius = 15.0f;
        _fallbackPoly.Polygon = new Vector2[]
        {
            new(0, -radius),
            new(radius * 0.7f, 0),
            new(0, radius),
            new(-radius * 0.7f, 0)
        };
        _fallbackPoly.Color = new Color(1.0f, 0.8f, 0.0f);
        AddChild(_fallbackPoly);

        SyncVisualFromRoster();
    }

    /// <summary>从 Roster.Leader 同步当前视觉。Leader 装备/外观变化后调用。</summary>
    public void SyncVisualFromRoster()
    {
        if (_characterView == null || _fallbackPoly == null) return;

        var leader = Roster?.Leader;
        if (leader != null)
        {
            _characterView.Setup(leader);
            _characterView.Visible = true;
            _fallbackPoly.Visible = false;
        }
        else
        {
            _characterView.Visible = false;
            _fallbackPoly.Visible = true;
        }
    }

    public void PlayAnim(string animName)
    {
        _characterView?.PlayAnimation(animName);
    }

    public override void _Draw()
    {
        // 绘制大红色圆环（外圈）提高玩家队伍辨识度
        float ringRadius = 28.0f;
        float ringWidth = 3.0f;
        var ringColor = new Color(0.9f, 0.15f, 0.1f, 0.9f);
        DrawArc(Vector2.Zero, ringRadius, 0, Mathf.Tau, 32, ringColor, ringWidth, true);
    }

    public void SetHexNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        HexGrid = grid;
        HexAStar = astar;
    }

    /// <summary>设置 Chunk 模式寻路（chunk 模式专用，优先于 HexAStar）</summary>
    public void SetChunkNavigation(ChunkManager mgr, ChunkAStar astar)
    {
        ChunkManager = mgr;
        ChunkAStar = astar;
    }

    public void PlaceAt(float px, float py)
    {
        Position = new Vector2(px, py);
        Path.Clear();
        IsMoving = false;
    }

    /// <summary>获取显示名称（IOverworldMapEntity）</summary>
    public string GetDisplayName() => Roster?.Leader?.UnitName ?? "冒险者队伍";

    /// <summary>获取描述文本（IOverworldMapEntity）</summary>
    public string GetDescription() => $"{GetDisplayName()} — {Roster?.Count ?? 0} 人队伍";

    public void MoveTo(Vector2 targetPx)
    {
        Vector2[] newPath;

        // 优先使用 Chunk 模式寻路
        if (ChunkAStar != null && ChunkManager != null)
        {
            // 检测目标是否在水域 — 自动切换导航模式
            var targetAxial = HexOverworldTile.PixelToAxial(targetPx.X, targetPx.Y);
            var targetTile = ChunkManager.GetTile(targetAxial.X, targetAxial.Y);

            if (targetTile != null)
            {
                bool targetIsWater = targetTile.Terrain == HexOverworldTile.TerrainType.DeepWater ||
                                     targetTile.Terrain == HexOverworldTile.TerrainType.ShallowWater;

                if (targetIsWater && CurrentShip != null && !CurrentShip.IsBroken)
                {
                    // 有船且目标在水上 → 海上模式
                    ChunkAStar.Mode = ChunkAStar.NavigationMode.Sea;
                    IsAtSea = true;
                }
                else
                {
                    // 陆地模式
                    ChunkAStar.Mode = ChunkAStar.NavigationMode.Land;
                    if (IsAtSea) { IsAtSea = false; _seaDistanceTraveled = 0f; }
                }
            }

            newPath = ChunkAStar.FindPathPixels(Position, targetPx, ChunkManager);

            // 路径为空时尝试寻路到目标附近的可通行点（解决点击不可通行 tile 无反应的问题）
            if (newPath.Length == 0 && targetTile != null && !targetTile.IsPassable)
            {
                var nearPassable = FindNearestPassableTarget(targetAxial, ChunkManager);
                if (nearPassable != targetAxial)
                {
                    var altTarget = HexOverworldTile.AxialToPixel(nearPassable.X, nearPassable.Y);
                    newPath = ChunkAStar.FindPathPixels(Position, altTarget, ChunkManager);
                }
            }

            // 检查目标是否在已加载 chunk 内
            bool targetLoaded = ChunkManager.IsLoaded(targetAxial.X, targetAxial.Y);

            if (!targetLoaded)
            {
                _finalTarget = targetPx;
                _hasPendingTarget = true;
            }
            else
            {
                _hasPendingTarget = false;
            }
        }
        else if (HexGrid != null && HexAStar != null)
        {
            newPath = HexAStar.FindPathPixels(Position, targetPx);
            _hasPendingTarget = false;
        }
        else
        {
            return;
        }

        if (newPath.Length > 0)
        {
            // 1. 简化路径：移除共线/可直达的中间点
            var simplified = SimplifyPath(newPath);
            // 2. 平滑路径：Catmull-Rom 样条让转角自然
            Path = SmoothPath(simplified);
            IsMoving = true;
        }
    }

    /// <summary>BFS 搜索目标附近最近的可通行 tile（用于点击不可通行 tile 时的回退）</summary>
    private static Vector2I FindNearestPassableTarget(Vector2I coord, ChunkManager mgr)
    {
        var visited = new HashSet<Vector2I> { coord };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(coord);

        int maxSearch = 36;
        while (queue.Count > 0 && maxSearch-- > 0)
        {
            var current = queue.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (visited.Contains(neighbor)) continue;
                visited.Add(neighbor);

                var tile = mgr.GetTile(neighbor.X, neighbor.Y);
                if (tile != null && tile.IsPassable &&
                    tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                    return neighbor;

                if (tile != null)
                    queue.Enqueue(neighbor);
            }
        }

        return coord;
    }

    /// <summary>
    /// 路径简化 — 移除不必要的中间点。
    /// 如果从点 A 可以直线到达点 C（中间无障碍），则跳过点 B。
    /// 让路径看起来像自然的直线/弧线，而非六边形锯齿。
    /// 优化: 使用二分搜索找到最远可达点，减少 IsLinePassable 调用次数
    /// </summary>
    private Vector2[] SimplifyPath(Vector2[] path)
    {
        if (path.Length <= 2) return path;

        var result = new List<Vector2> { path[0] };
        int current = 0;

        while (current < path.Length - 1)
        {
            // 二分搜索找到从 current 可直达的最远点
            int lo = current + 1;
            int hi = path.Length - 1;
            int farthest = lo;

            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (IsLinePassable(path[current], path[mid]))
                {
                    farthest = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            result.Add(path[farthest]);
            current = farthest;
        }

        return result.ToArray();
    }

    /// <summary>
    /// 检查两点之间的直线是否全部经过可通行 tile。
    /// 沿直线每 50px 采样一次，检查该位置的 tile 是否可通行。
    /// </summary>
    private bool IsLinePassable(Vector2 from, Vector2 to)
    {
        float dist = from.DistanceTo(to);
        int steps = Mathf.Max(2, (int)(dist / 50.0f));

        for (int i = 1; i < steps; i++)
        {
            float t = (float)i / steps;
            Vector2 sample = from.Lerp(to, t);

            // 检查该像素位置的 tile 是否可通行
            if (ChunkManager != null)
            {
                var axial = HexOverworldTile.PixelToAxial(sample.X, sample.Y);
                var tile = ChunkManager.GetTile(axial.X, axial.Y);
                if (tile == null || !tile.IsPassable) return false;
            }
            else if (HexGrid != null)
            {
                var tile = HexGrid.GetTileAtPixel(sample.X, sample.Y);
                if (tile == null || !tile.IsPassable) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 将六边形中心点路径平滑为自然曲线路径。
    /// 使用 Catmull-Rom 样条在相邻路径点之间插值，
    /// 让玩家无法察觉底层六边形网格。
    /// </summary>
    private List<Vector2> SmoothPath(Vector2[] rawPath)
    {
        if (rawPath.Length <= 1)
            return new List<Vector2>(rawPath);

        // 短路径（2 点）直接用线性插值加密
        if (rawPath.Length == 2)
        {
            var result = new List<Vector2>();
            Vector2 a = rawPath[0], b = rawPath[1];
            int steps = Mathf.Max(4, (int)(a.DistanceTo(b) / 20.0f));
            for (int i = 0; i <= steps; i++)
                result.Add(a.Lerp(b, (float)i / steps));
            return result;
        }

        // Catmull-Rom 样条插值 — 高密度插值让移动完全平滑
        var smoothed = new List<Vector2>();
        int segmentSteps = 8; // 每段 8 个插值点（原来 4 个不够平滑）

        for (int i = 0; i < rawPath.Length - 1; i++)
        {
            Vector2 p0 = i > 0 ? rawPath[i - 1] : rawPath[i];
            Vector2 p1 = rawPath[i];
            Vector2 p2 = rawPath[i + 1];
            Vector2 p3 = i + 2 < rawPath.Length ? rawPath[i + 2] : rawPath[i + 1];

            for (int s = 0; s < segmentSteps; s++)
            {
                float t = (float)s / segmentSteps;
                smoothed.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        smoothed.Add(rawPath[^1]);
        return smoothed;
    }

    /// <summary>Catmull-Rom 样条插值</summary>
    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float x = 0.5f * (
            (2.0f * p1.X) +
            (-p0.X + p2.X) * t +
            (2.0f * p0.X - 5.0f * p1.X + 4.0f * p2.X - p3.X) * t2 +
            (-p0.X + 3.0f * p1.X - 3.0f * p2.X + p3.X) * t3
        );

        float y = 0.5f * (
            (2.0f * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2.0f * p0.Y - 5.0f * p1.Y + 4.0f * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3.0f * p1.Y - 3.0f * p2.Y + p3.Y) * t3
        );

        return new Vector2(x, y);
    }

    public override void _Process(double delta)
    {
        if (!IsMoving || Path.Count == 0) return;

        // 通过速度组件计算最终速度（含地形/季节/昼夜/负重/坐骑/技能修正）
        float currentSpeed = BaseMoveSpeed;
        if (SpeedComponent != null)
            currentSpeed = SpeedComponent.CalculateSpeed(Position);

        Vector2 targetPos = Path[0];
        Vector2 dir = (targetPos - Position).Normalized();
        float dist = Position.DistanceTo(targetPos);
        float step = currentSpeed * (float)delta;

        if (step >= dist)
        {
            Position = targetPos;
            Path.RemoveAt(0);

            // 海上遭遇检定
            if (IsAtSea)
            {
                _seaDistanceTraveled += dist;
                if (_seaDistanceTraveled >= SeaEncounterTable.EncounterCheckInterval)
                {
                    _seaDistanceTraveled -= SeaEncounterTable.EncounterCheckInterval;
                    // 遭遇检定由 OverworldScene3D 处理（通过信号或轮询 SeaEncounterPending）
                    SeaEncounterPending = true;
                }
            }

            if (Path.Count == 0)
            {
                IsMoving = false;

                // 到达目的地后检查是否登陆
                if (IsAtSea && ChunkManager != null)
                {
                    var axial = HexOverworldTile.PixelToAxial(Position.X, Position.Y);
                    var tile = ChunkManager.GetTile(axial.X, axial.Y);
                    if (tile != null && tile.Terrain != HexOverworldTile.TerrainType.DeepWater &&
                        tile.Terrain != HexOverworldTile.TerrainType.ShallowWater)
                    {
                        IsAtSea = false;
                        _seaDistanceTraveled = 0f;
                        if (ChunkAStar != null)
                            ChunkAStar.Mode = ChunkAStar.NavigationMode.Land;
                    }
                }

                // 跨 chunk 续路
                if (_hasPendingTarget)
                {
                    float distToFinal = Position.DistanceTo(_finalTarget);
                    if (distToFinal > 200.0f)
                    {
                        CallDeferred(nameof(ContinueToTarget));
                    }
                    else
                    {
                        _hasPendingTarget = false;
                    }
                }
            }
        }
        else
        {
            Position += dir * step;
        }
    }

    /// <summary>跨 chunk 续路 — 到达边界后继续向最终目标寻路</summary>
    private void ContinueToTarget()
    {
        if (!_hasPendingTarget) return;
        MoveTo(_finalTarget);
    }

    /// <summary>获取当前速度分解（供UI显示）</summary>
    public Godot.Collections.Dictionary GetSpeedBreakdown()
    {
        if (SpeedComponent != null)
            return SpeedComponent.GetSpeedBreakdown(Position);
        return new Godot.Collections.Dictionary { { "base", BaseMoveSpeed }, { "final", BaseMoveSpeed } };
    }
}
