using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 战争迷雾系统 — 分种族初始揭示 + 三级迷雾 + 存档持久化
/// </summary>
[GlobalClass]
public partial class FogOfWar : RefCounted
{
    /// <summary>迷雾状态枚举</summary>
    public enum FogState : byte
    {
        Unexplored = 0,  // 未探索
        Revealed   = 1,  // 已揭示（永久）
        InVision  = 2,  // 当前视野
    }

    // 网格数据：[y, x] -> FogState 值
    public byte[,] ExploredGrid { get; private set; } = new byte[0, 0];

    // 状态计数器 — 供调试/UI 以 O(1) 读取，避免每次全扫网格
    public int CountUnexplored { get; private set; } = 0;
    public int CountRevealed { get; private set; } = 0;
    public int CountInVision { get; private set; } = 0;

    public int GridW { get; private set; } = 0;
    public int GridH { get; private set; } = 0;
    public int CellSize { get; private set; } = 16;
    public int MapWidthPx { get; private set; } = 0;
    public int MapHeightPx { get; private set; } = 0;

    public float VisionRange { get; set; } = 1248.0f; // 8 格 × 156px = 1248px
    public float ScoutMultiplier { get; set; } = 1.0f;

    /// <summary>调试用：完全禁用迷雾（所有格子视为 InVision）</summary>
    public bool DisableFog { get; set; } = false;

    // 上一帧标记为 InVision 的格子列表 — 用于高效降级
    private List<Vector2I> _lastVisionCells = new();

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(int mapWPx, int mapHPx, int pCellSize)
    {
        MapWidthPx = mapWPx;
        MapHeightPx = mapHPx;
        CellSize = pCellSize;
        GridW = mapWPx / CellSize;
        GridH = mapHPx / CellSize;

        ExploredGrid = new byte[GridH, GridW];
        CountUnexplored = GridH * GridW;
        CountRevealed = 0;
        CountInVision = 0;
    }

    /// <summary>
    /// 基于实际国家领土 tile 坐标揭示迷雾。
    /// 每个 tile 揭示其覆盖的所有 fog cell（确保无缝覆盖）。
    /// </summary>
    public void RevealTerritory(HashSet<Godot.Vector2I> territoryTiles)
    {
        foreach (var tileCoord in territoryTiles)
        {
            var pixelPos = Map.HexOverworldTile.AxialToPixel(tileCoord.X, tileCoord.Y);

            // 揭示以 tile 中心为圆心的区域
            // hex tile 间距约 234×270px，fog cell = 128px
            // 用半径 3 个 cell（384px）确保相邻 tile 的揭示区域完全重叠无缝
            int centerGx = (int)(pixelPos.X / CellSize);
            int centerGy = (int)(pixelPos.Y / CellSize);
            int radius = 3;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int gx = centerGx + dx;
                    int gy = centerGy + dy;
                    if (gy >= 0 && gy < GridH && gx >= 0 && gx < GridW)
                    {
                        if (ExploredGrid[gy, gx] == (byte)FogState.Unexplored)
                            SetCell(gy, gx, (byte)FogState.Revealed);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 硬编码矩形区域揭示（无领土数据时的 fallback）
    /// </summary>
    public void RevealRaceRegionFallback(RaceData.Race raceId)
    {
        ApplyRaceInitialReveal(raceId);
    }

    // ========================================
    // 种族初始揭示
    // ========================================

    private static List<Rect2> GetRaceInitialRegions(RaceData.Race raceId)
    {
        return raceId switch
        {
            RaceData.Race.Human => new List<Rect2>
            {
                new Rect2(0.05f, 0.2f, 0.85f, 0.55f),
                new Rect2(0.0f, 0.25f, 0.15f, 0.5f)
            },
            RaceData.Race.Elf => new List<Rect2>
            {
                new Rect2(0.0f, 0.2f, 0.25f, 0.6f),
                new Rect2(0.2f, 0.3f, 0.1f, 0.2f)
            },
            RaceData.Race.Dwarf => new List<Rect2>
            {
                new Rect2(0.1f, 0.0f, 0.8f, 0.25f),
                new Rect2(0.15f, 0.2f, 0.2f, 0.1f)
            },
            RaceData.Race.HalfOrc => new List<Rect2>
            {
                new Rect2(0.65f, 0.25f, 0.35f, 0.45f),
                new Rect2(0.55f, 0.35f, 0.15f, 0.15f)
            },
            RaceData.Race.HalfElf => new List<Rect2>
            {
                new Rect2(0.1f, 0.25f, 0.6f, 0.5f),
                new Rect2(0.0f, 0.25f, 0.15f, 0.4f)
            },
            _ => new List<Rect2> { new Rect2(0.3f, 0.35f, 0.4f, 0.3f) }
        };
    }

    private void ApplyRaceInitialReveal(RaceData.Race raceId)
    {
        var regions = GetRaceInitialRegions(raceId);
        foreach (var region in regions)
        {
            int xStart = (int)(region.Position.X * GridW);
            int yStart = (int)(region.Position.Y * GridH);
            int xEnd = Math.Min((int)((region.Position.X + region.Size.X) * GridW), GridW);
            int yEnd = Math.Min((int)((region.Position.Y + region.Size.Y) * GridH), GridH);

            for (int gy = yStart; gy < yEnd; gy++)
            {
                for (int gx = xStart; gx < xEnd; gx++)
                {
                    if (gy >= 0 && gy < GridH && gx >= 0 && gx < GridW)
                    {
                        SetCell(gy, gx, (byte)FogState.Revealed);
                    }
                }
            }
        }
    }

    // ========================================
    // 内部 — 计数器维护
    // ========================================

    /// <summary>O(1) 更新单格状态并维护三项计数器</summary>
    private void SetCell(int gy, int gx, byte newVal)
    {
        byte old = ExploredGrid[gy, gx];
        if (old == newVal) return;
        switch (old)
        {
            case (byte)FogState.Unexplored: CountUnexplored--; break;
            case (byte)FogState.Revealed:   CountRevealed--;   break;
            case (byte)FogState.InVision:   CountInVision--;   break;
        }
        switch (newVal)
        {
            case (byte)FogState.Unexplored: CountUnexplored++; break;
            case (byte)FogState.Revealed:   CountRevealed++;   break;
            case (byte)FogState.InVision:   CountInVision++;   break;
        }
        ExploredGrid[gy, gx] = newVal;
    }

    private void RecomputeCounters()
    {
        CountUnexplored = 0;
        CountRevealed = 0;
        CountInVision = 0;
        for (int gy = 0; gy < GridH; gy++)
        {
            for (int gx = 0; gx < GridW; gx++)
            {
                byte v = ExploredGrid[gy, gx];
                switch (v)
                {
                    case (byte)FogState.Unexplored: CountUnexplored++; break;
                    case (byte)FogState.Revealed:   CountRevealed++;   break;
                    case (byte)FogState.InVision:   CountInVision++;   break;
                }
            }
        }
    }

    // ========================================
    // 每帧更新
    // ========================================

    public void UpdateVision(Vector2 playerPos)
    {
        // DisableFog 模式：所有格子强制 InVision，跳过正常逻辑
        if (DisableFog)
        {
            // 只在首次或切换时全量设置
            if (CountUnexplored > 0 || CountRevealed > 0)
            {
                for (int gy = 0; gy < GridH; gy++)
                    for (int gx = 0; gx < GridW; gx++)
                        SetCell(gy, gx, (byte)FogState.InVision);
            }
            return;
        }

        // 1. 降级上一帧的 InVision 格子
        foreach (var cell in _lastVisionCells)
        {
            if (cell.Y >= 0 && cell.Y < GridH && cell.X >= 0 && cell.X < GridW)
            {
                if (ExploredGrid[cell.Y, cell.X] == (byte)FogState.InVision)
                {
                    SetCell(cell.Y, cell.X, (byte)FogState.Revealed);
                }
            }
        }
        _lastVisionCells.Clear();

        // 2. 更新当前视野
        float effectiveRange = VisionRange * ScoutMultiplier;
        int centerGX = (int)(playerPos.X / CellSize);
        int centerGY = (int)(playerPos.Y / CellSize);
        int rangeCells = (int)(effectiveRange / CellSize) + 1;
        float rangeSq = effectiveRange * effectiveRange;

        int yMin = Math.Max(centerGY - rangeCells, 0);
        int yMax = Math.Min(centerGY + rangeCells, GridH - 1);
        int xMin = Math.Max(centerGX - rangeCells, 0);
        int xMax = Math.Min(centerGX + rangeCells, GridW - 1);

        for (int gy = yMin; gy <= yMax; gy++)
        {
            for (int gx = xMin; gx <= xMax; gx++)
            {
                float px = (gx + 0.5f) * CellSize;
                float py = (gy + 0.5f) * CellSize;
                float dx = px - playerPos.X;
                float dy = py - playerPos.Y;
                if (dx * dx + dy * dy <= rangeSq)
                {
                    SetCell(gy, gx, (byte)FogState.InVision);
                    _lastVisionCells.Add(new Vector2I(gx, gy));
                }
            }
        }
    }

    // ========================================
    // 揭示接口
    // ========================================

    /// <summary>一键揭示全图（所有格子设为 Revealed）</summary>
    public void RevealAll()
    {
        for (int gy = 0; gy < GridH; gy++)
        {
            for (int gx = 0; gx < GridW; gx++)
            {
                if (ExploredGrid[gy, gx] == (byte)FogState.Unexplored)
                    SetCell(gy, gx, (byte)FogState.Revealed);
            }
        }
    }

    public void RevealArea(Vector2 centerPx, float radiusPx)
    {
        int centerGX = (int)(centerPx.X / CellSize);
        int centerGY = (int)(centerPx.Y / CellSize);
        int rangeCells = (int)(radiusPx / CellSize) + 1;
        float rangeSq = radiusPx * radiusPx;

        int yMin = Math.Max(centerGY - rangeCells, 0);
        int yMax = Math.Min(centerGY + rangeCells, GridH - 1);
        int xMin = Math.Max(centerGX - rangeCells, 0);
        int xMax = Math.Min(centerGX + rangeCells, GridW - 1);

        for (int gy = yMin; gy <= yMax; gy++)
        {
            for (int gx = xMin; gx <= xMax; gx++)
            {
                float px = (gx + 0.5f) * CellSize;
                float py = (gy + 0.5f) * CellSize;
                float dx = px - centerPx.X;
                float dy = py - centerPx.Y;
                if (dx * dx + dy * dy <= rangeSq)
                {
                    if (ExploredGrid[gy, gx] == (byte)FogState.Unexplored)
                    {
                        SetCell(gy, gx, (byte)FogState.Revealed);
                    }
                }
            }
        }
    }

    /// <summary>将指定圆形区域内的格子设为未探索（覆盖迷雾）</summary>
    public void HideArea(Vector2 centerPx, float radiusPx)
    {
        int centerGX = (int)(centerPx.X / CellSize);
        int centerGY = (int)(centerPx.Y / CellSize);
        int rangeCells = (int)(radiusPx / CellSize) + 1;
        float rangeSq = radiusPx * radiusPx;

        int yMin = Math.Max(centerGY - rangeCells, 0);
        int yMax = Math.Min(centerGY + rangeCells, GridH - 1);
        int xMin = Math.Max(centerGX - rangeCells, 0);
        int xMax = Math.Min(centerGX + rangeCells, GridW - 1);

        for (int gy = yMin; gy <= yMax; gy++)
        {
            for (int gx = xMin; gx <= xMax; gx++)
            {
                float px = (gx + 0.5f) * CellSize;
                float py = (gy + 0.5f) * CellSize;
                float dx = px - centerPx.X;
                float dy = py - centerPx.Y;
                if (dx * dx + dy * dy <= rangeSq)
                {
                    SetCell(gy, gx, (byte)FogState.Unexplored);
                }
            }
        }
    }

    public void RevealRegionByName(string regionName)
    {
        var regionRect = GetRegionRectByName(regionName);
        if (regionRect.Size.X <= 0 || regionRect.Size.Y <= 0) return;

        int xStart = (int)(regionRect.Position.X * GridW);
        int yStart = (int)(regionRect.Position.Y * GridH);
        int xEnd = Math.Min((int)((regionRect.Position.X + regionRect.Size.X) * GridW), GridW);
        int yEnd = Math.Min((int)((regionRect.Position.Y + regionRect.Size.Y) * GridH), GridH);

        for (int gy = yStart; gy < yEnd; gy++)
        {
            for (int gx = xStart; gx < xEnd; gx++)
            {
                if (gy >= 0 && gy < GridH && gx >= 0 && gx < GridW)
                {
                    if (ExploredGrid[gy, gx] == (byte)FogState.Unexplored)
                        SetCell(gy, gx, (byte)FogState.Revealed);
                }
            }
        }
    }

    private Rect2 GetRegionRectByName(string regionName)
    {
        return regionName switch
        {
            "霜冠山脉" => new Rect2(0.1f, 0.0f, 0.8f, 0.2f),
            "银叶森林" => new Rect2(0.0f, 0.2f, 0.25f, 0.6f),
            "中央平原" => new Rect2(0.1f, 0.25f, 0.8f, 0.5f),
            "丘陵草原" => new Rect2(0.7f, 0.25f, 0.3f, 0.45f),
            "焦土荒原" => new Rect2(0.5f, 0.75f, 0.5f, 0.25f),
            "蛮荒沼泽" => new Rect2(0.0f, 0.75f, 0.4f, 0.25f),
            _ => new Rect2(0, 0, 0, 0)
        };
    }

    // ========================================
    // 查询接口
    // ========================================

    public byte GetFogStateAt(float px, float py)
    {
        int gx = (int)(px / CellSize);
        int gy = (int)(py / CellSize);
        if (gy < 0 || gy >= GridH || gx < 0 || gx >= GridW)
            return (byte)FogState.Unexplored;
        return ExploredGrid[gy, gx];
    }

    public bool IsRevealed(float px, float py) => GetFogStateAt(px, py) >= (byte)FogState.Revealed;
    public bool IsInVision(float px, float py) => GetFogStateAt(px, py) == (byte)FogState.InVision;
    public bool IsUnexplored(float px, float py) => GetFogStateAt(px, py) == (byte)FogState.Unexplored;

    public float GetExplorationProgress()
    {
        int total = GridW * GridH;
        if (total == 0) return 0.0f;
        // O(1) via maintained counters (Revealed + InVision = 已探索)
        int explored = CountRevealed + CountInVision;
        return (float)explored / total;
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var rleData = new Godot.Collections.Array<int>();
        int currentVal = -1;
        int runLength = 0;

        for (int gy = 0; gy < GridH; gy++)
        {
            for (int gx = 0; gx < GridW; gx++)
            {
                byte val = ExploredGrid[gy, gx];
                if (val == currentVal)
                {
                    runLength++;
                }
                else
                {
                    if (currentVal >= 0)
                    {
                        rleData.Add(currentVal);
                        rleData.Add(runLength);
                    }
                    currentVal = val;
                    runLength = 1;
                }
            }
        }
        if (currentVal >= 0)
        {
            rleData.Add(currentVal);
            rleData.Add(runLength);
        }

        return new Godot.Collections.Dictionary
        {
            { "grid_w", GridW },
            { "grid_h", GridH },
            { "cell_size", CellSize },
            { "vision_range", VisionRange },
            { "rle_data", rleData }
        };
    }

    public static FogOfWar Deserialize(Godot.Collections.Dictionary data)
    {
        var fog = new FogOfWar();
        fog.GridW = (int)data["grid_w"];
        fog.GridH = (int)data["grid_h"];
        fog.CellSize = (int)data["cell_size"];
        fog.VisionRange = (float)data["vision_range"];

        fog.ExploredGrid = new byte[fog.GridH, fog.GridW];

        var rleData = (Godot.Collections.Array<int>)data["rle_data"];
        int gx = 0, gy = 0;
        for (int i = 0; i < rleData.Count; i += 2)
        {
            byte val = (byte)rleData[i];
            int length = rleData[i + 1];
            for (int j = 0; j < length; j++)
            {
                if (gy < fog.GridH && gx < fog.GridW)
                    fog.ExploredGrid[gy, gx] = val;
                gx++;
                if (gx >= fog.GridW) { gx = 0; gy++; }
            }
        }
        fog.RecomputeCounters();
        return fog;
    }
}
