// HexOverworldTile.cs
// 大地图六边形瓦片数据模型 — 存储单个六角格的完整地形信息
// 战场兄弟风格: 大量瓦片在游戏世界初始化时按规则和逻辑生成
using Godot;
using System;

namespace BladeHex.Map;

/// <summary>
/// 大地图六边形瓦片 — 纯数据类，存储地形/寻路/定居点/迷雾信息
/// 坐标约定: Axial(q,r) 存储, Cube(q,r,s) 计算, 像素通过 HexLayoutConfig
/// </summary>
[GlobalClass]
public partial class HexOverworldTile : Resource
{
    // ========================================
    // 地形类型枚举
    // ========================================

    public enum TerrainType
    {
        DeepWater,     // 深水 — 不可通行
        ShallowWater,  // 浅水
        Sand,          // 沙滩/荒漠
        Plains,        // 平原
        Grassland,     // 草地
        Forest,        // 森林
        DenseForest,   // 密林
        Jungle,        // 丛林 (炎热湿润)
        Taiga,         // 针叶林 (寒冷)
        Bog,           // 冻土沼泽 (寒冷潮湿)
        Swamp,         // 沼泽 (温带/炎热)
        Savanna,       // 稀树草原 (炎热干燥)
        Wasteland,     // 荒原 (温带极干)
        Rocky,         // 岩石荒地 (寒冷极干)
        Hills,         // 丘陵
        Mountain,      // 山地 — 不可通行
        MountainSnow,  // 雪山 — 不可通行
        Snow,          // 雪地
        Ice,           // 冰原
        Road,          // 道路
        River,         // 河流 — 不可通行
    }

    /// <summary>
    /// 战斗场景地形分类 — 将 21 种大地图地形映射到 7 种战斗语义类别
    /// </summary>
    public enum BattleTerrainCategory
    {
        Plains,
        Forest,
        Mountain,
        Swamp,
        Water,
        Road,
        Desert,
    }

    /// <summary>
    /// 将大地图地形映射为战斗场景地形分类
    /// </summary>
    public static BattleTerrainCategory GetBattleCategory(TerrainType terrain)
    {
        return terrain switch
        {
            TerrainType.Forest or TerrainType.DenseForest or TerrainType.Jungle or TerrainType.Taiga
                => BattleTerrainCategory.Forest,
            TerrainType.Mountain or TerrainType.MountainSnow or TerrainType.Hills or TerrainType.Snow
                => BattleTerrainCategory.Mountain,
            TerrainType.Swamp or TerrainType.Bog
                => BattleTerrainCategory.Swamp,
            TerrainType.DeepWater or TerrainType.ShallowWater or TerrainType.River
                => BattleTerrainCategory.Water,
            TerrainType.Road
                => BattleTerrainCategory.Road,
            TerrainType.Sand or TerrainType.Savanna or TerrainType.Wasteland
                => BattleTerrainCategory.Desert,
            _ => BattleTerrainCategory.Plains,
        };
    }

    // ========================================
    // 常量
    // ========================================

    /// <summary>六边形外径 (大地图专用, 与纹理 313px 对齐)</summary>
    public const float HexSize = 156.0f;

    /// <summary>
    /// 大地图地形纹理根目录（2D 羊皮纸画风，新）
    /// 六边形地形贴图（grassland/forest/...）放这里；变体命名：{key}_{variant}.png
    /// </summary>
    public const string OverworldTextureBasePath = "res://src/assets/tiles/overworld";

    /// <summary>
    /// 叠加层纹理根目录（村庄/城堡/营地/桥等，暂与旧资产共享目录）
    /// 资产美术迁移完成后可改为 "res://src/assets/tiles/overworld/overlays"
    /// </summary>
    public const string OverlayTextureBasePath = "res://src/assets/tiles/hex_terrain";

    /// <summary>向后兼容：旧代码引用 TextureBasePath 的指向叠加层目录</summary>
    public const string TextureBasePath = OverlayTextureBasePath;

    /// <summary>6个邻居在 Cube 空间的偏移 (flat-top)</summary>
    public static readonly Vector3I[] CubeDirections =
    [
        new(+1, 0, -1),  // 0: 东 (右)
        new(+1, -1, 0),  // 1: 东北 (右上)
        new(0, -1, +1),  // 2: 西北 (左上)
        new(-1, 0, +1),  // 3: 西 (左)
        new(-1, +1, 0),  // 4: 西南 (左下)
        new(0, +1, -1),  // 5: 东南 (右下)
    ];

    // ========================================
    // 坐标与空间
    // ========================================

    /// <summary>轴向坐标 (q, r)</summary>
    public Vector2I Coord = Vector2I.Zero;

    /// <summary>预计算的世界像素坐标</summary>
    public Vector2 PixelPos = Vector2.Zero;

    // ========================================
    // 地形属性
    // ========================================

    public TerrainType Terrain = TerrainType.Plains;
    public float Elevation = 0.0f;
    public float Moisture = 0.5f;
    public float Temperature = 0.5f;

    // ========================================
    // 寻路与通行
    // ========================================

    public bool IsPassable = true;
    public float MoveCost = 1.0f;

    // ========================================
    // 线性特征 (道路/河流)
    // ========================================

    public bool IsRoad = false;
    public bool IsRiver = false;
    public int RoadDirections = 0;
    public int RiverDirections = 0;

    // ========================================
    // 定居点
    // ========================================

    public bool HasSettlement = false;
    public int SettlementType = 0;
    public string PoiId = "";
    public string RegionName = "";

    // ========================================
    // 战争迷雾
    // ========================================

    public int Visibility = 0;  // 0=未探索, 1=已探索, 2=当前可见

    // ========================================
    // 静态布局引用
    // ========================================

    private static HexLayoutConfig? _layout;

    public static HexLayoutConfig GetLayout()
    {
        if (_layout == null)
        {
            _layout = new HexLayoutConfig();
        }
        return _layout;
    }

    public static void SetLayout(HexLayoutConfig layout)
    {
        _layout = layout;
    }

    // ========================================
    // 工厂方法
    // ========================================

    /// <summary>创建完整初始化的瓦片</summary>
    public static HexOverworldTile Create(int q, int r, TerrainType terrain, float elev, float moist, float temp)
    {
        var tile = new HexOverworldTile();
        tile.Coord = new Vector2I(q, r);
        tile.PixelPos = AxialToPixel(q, r);
        tile.Terrain = terrain;
        tile.Elevation = elev;
        tile.Moisture = moist;
        tile.Temperature = temp;
        tile.UpdateTerrainProperties();
        return tile;
    }

    /// <summary>创建空白瓦片 (仅设坐标)</summary>
    public static HexOverworldTile CreateEmpty(int q, int r)
    {
        var tile = new HexOverworldTile();
        tile.Coord = new Vector2I(q, r);
        tile.PixelPos = AxialToPixel(q, r);
        return tile;
    }

    // ========================================
    // Cube 坐标工具
    // ========================================

    /// <summary>Axial → Cube: s = -q - r</summary>
    public static Vector3I AxialToCube(int q, int r)
    {
        return new Vector3I(q, r, -q - r);
    }

    /// <summary>Cube → Axial: 丢弃 s</summary>
    public static Vector2I CubeToAxial(Vector3I cube)
    {
        return new Vector2I(cube.X, cube.Y);
    }

    /// <summary>获取邻居坐标 (Axial 返回值, Cube 内部计算)</summary>
    public static Vector2I GetNeighbor(int q, int r, int direction)
    {
        int d = ((direction % 6) + 6) % 6;
        var cube = AxialToCube(q, r);
        var offset = CubeDirections[d];
        return CubeToAxial(cube + offset);
    }

    /// <summary>获取邻居的 Cube 坐标</summary>
    public static Vector3I GetNeighborCube(Vector3I cube, int direction)
    {
        int d = ((direction % 6) + 6) % 6;
        return cube + CubeDirections[d];
    }

    /// <summary>Cube 距离</summary>
    public static int CubeDistance(Vector3I a, Vector3I b)
    {
        return Mathf.Max(Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y)), Mathf.Abs(a.Z - b.Z));
    }

    /// <summary>Axial 距离</summary>
    public static int HexDistance(int q1, int r1, int q2, int r2)
    {
        return CubeDistance(AxialToCube(q1, r1), AxialToCube(q2, r2));
    }

    /// <summary>Cube 舍入: 浮点 Cube → 整数 Cube</summary>
    public static Vector3I CubeRound(float fq, float fr, float fs)
    {
        float rq = Mathf.Round(fq);
        float rr = Mathf.Round(fr);
        float rs = Mathf.Round(fs);

        float qDiff = Mathf.Abs(rq - fq);
        float rDiff = Mathf.Abs(rr - fr);
        float sDiff = Mathf.Abs(rs - fs);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;
        else
            rs = -rq - rr;

        return new Vector3I((int)rq, (int)rr, (int)rs);
    }

    /// <summary>Axial 舍入</summary>
    public static Vector2I AxialRound(float fq, float fr)
    {
        float fs = -fq - fr;
        var cube = CubeRound(fq, fr, fs);
        return CubeToAxial(cube);
    }

    /// <summary>Cube 线性插值</summary>
    public static Vector3 CubeLerp(Vector3I a, Vector3I b, float t)
    {
        return new Vector3(
            Mathf.Lerp(a.X, b.X, t),
            Mathf.Lerp(a.Y, b.Y, t),
            Mathf.Lerp(a.Z, b.Z, t)
        );
    }

    /// <summary>Cube 画线 (Redblobgames 算法)</summary>
    public static Vector2I[] CubeLine(Vector3I a, Vector3I b)
    {
        int n = CubeDistance(a, b);
        if (n == 0) return [CubeToAxial(a)];

        var results = new Vector2I[n + 1];
        var aNudge = new Vector3(a.X + 1e-6f, a.Y + 1e-6f, a.Z - 2e-6f);
        var bNudge = new Vector3(b.X + 1e-6f, b.Y + 1e-6f, b.Z - 2e-6f);

        float step = 1.0f / n;
        for (int i = 0; i <= n; i++)
        {
            float t = i * step;
            float fq = Mathf.Lerp(aNudge.X, bNudge.X, t);
            float fr = Mathf.Lerp(aNudge.Y, bNudge.Y, t);
            float fs = Mathf.Lerp(aNudge.Z, bNudge.Z, t);
            results[i] = CubeToAxial(CubeRound(fq, fr, fs));
        }
        return results;
    }

    /// <summary>六角环: 半径 ringR 的所有格子 (Cube 空间)</summary>
    public static Vector3I[] CubeRing(Vector3I center, int ringR)
    {
        if (ringR == 0) return [center];
        var results = new System.Collections.Generic.List<Vector3I>(ringR * 6);
        var current = center + CubeDirections[4] * ringR;
        for (int side = 0; side < 6; side++)
        {
            for (int step = 0; step < ringR; step++)
            {
                results.Add(current);
                current = GetNeighborCube(current, side);
            }
        }
        return results.ToArray();
    }

    /// <summary>Axial 环</summary>
    public static Vector2I[] HexRing(int q, int r, int radius)
    {
        var ring = CubeRing(AxialToCube(q, r), radius);
        var results = new Vector2I[ring.Length];
        for (int i = 0; i < ring.Length; i++)
            results[i] = CubeToAxial(ring[i]);
        return results;
    }

    // ========================================
    // 像素坐标转换
    // ========================================

    /// <summary>轴向 → 像素</summary>
    public static Vector2 AxialToPixel(int q, int r)
    {
        return GetLayout().AxialToPixel(q, r);
    }

    /// <summary>像素 → 分数轴向</summary>
    public static Vector2 PixelToFractionalAxial(float px, float py)
    {
        return GetLayout().PixelToFractionalAxial(px, py);
    }

    /// <summary>像素 → 最近六角格 Axial 坐标</summary>
    public static Vector2I PixelToAxial(float px, float py)
    {
        var frac = PixelToFractionalAxial(px, py);
        return AxialRound(frac.X, frac.Y);
    }

    // ========================================
    // 地形属性自动计算
    // ========================================

    public void UpdateTerrainProperties()
    {
        (IsPassable, MoveCost) = Terrain switch
        {
            TerrainType.DeepWater => (false, 99.0f),
            TerrainType.ShallowWater => (true, 3.0f),
            TerrainType.Sand => (true, 1.5f),
            TerrainType.Plains => (true, 1.0f),
            TerrainType.Grassland => (true, 1.0f),
            TerrainType.Forest => (true, 1.5f),
            TerrainType.DenseForest => (true, 2.5f),
            TerrainType.Jungle => (true, 2.5f),
            TerrainType.Taiga => (true, 1.5f),
            TerrainType.Bog => (true, 3.0f),
            TerrainType.Swamp => (true, 2.5f),
            TerrainType.Savanna => (true, 1.0f),
            TerrainType.Wasteland => (true, 1.2f),
            TerrainType.Rocky => (true, 1.8f),
            TerrainType.Hills => (true, 2.0f),
            TerrainType.Mountain => (false, 99.0f),
            TerrainType.MountainSnow => (false, 99.0f),
            TerrainType.Snow => (true, 2.0f),
            TerrainType.Ice => (true, 2.0f),
            TerrainType.Road => (true, 0.2f),
            TerrainType.River => (false, 99.0f),
            _ => (true, 1.0f),
        };
    }

    /// <summary>设置地形并更新属性</summary>
    public void SetTerrain(TerrainType newTerrain)
    {
        Terrain = newTerrain;
        UpdateTerrainProperties();
    }

    // ========================================
    // 方向位操作
    // ========================================

    public bool HasDirectionBit(int directions, int dir)
    {
        return (directions & (1 << dir)) != 0;
    }

    public int SetDirectionBit(int directionsVar, int dir)
    {
        return directionsVar | (1 << dir);
    }

    // ========================================
    // 渲色与纹理
    // ========================================

    /// <summary>获取地形纹理文件名前缀（委托到 TerrainVisualRegistry）</summary>
    public static string TerrainTextureName(TerrainType t) => TerrainVisualRegistry.Get(t).OverworldKey;

    /// <summary>获取地形变体数（委托到 TerrainVisualRegistry）</summary>
    public static int TerrainVariantCount(TerrainType t) => TerrainVisualRegistry.Get(t).OverworldVariantCount;

    /// <summary>获取叠加层纹理文件名前缀</summary>
    public static string OverlayTextureName(string overlayType) => overlayType switch
    {
        "road" => "crossroads",
        "river" => "bridge",
        "settlement" => "village",
        "town" => "castle",
        "fort" => "fort",
        "market" => "market",
        "mine" => "mine",
        "ruins" => "ruins",
        "docks" => "docks",
        "camp" => "camp",
        "farmland" => "farmland",
        "quarry" => "quarry",
        "graveyard" => "graveyard",
        _ => "",
    };

    /// <summary>获取叠加层变体数</summary>
    public static int OverlayVariantCount(string overlayType) => overlayType switch
    {
        "road" => 1,
        "river" => 1,
        "settlement" => 3,
        "town" => 1,
        "fort" => 4,
        "market" => 1,
        "mine" => 2,
        "ruins" => 7,
        "docks" => 3,
        "camp" => 2,
        "farmland" => 3,
        "quarry" => 2,
        "graveyard" => 1,
        _ => 1,
    };

    /// <summary>获取完整地形纹理路径（大地图）</summary>
    public static string GetTerrainTexturePath(TerrainType t, int variant = 0)
    {
        var profile = TerrainVisualRegistry.Get(t);
        int maxV = Mathf.Max(profile.OverworldVariantCount, 1);
        return $"{OverworldTextureBasePath}/{profile.OverworldKey}_{variant % maxV}.png";
    }

    /// <summary>获取叠加层纹理路径</summary>
    public static string GetOverlayTexturePath(string overlayType, int variant = 0)
    {
        string name = OverlayTextureName(overlayType);
        if (name == "") return "";
        int maxV = Mathf.Max(OverlayVariantCount(overlayType), 1);
        return $"{OverlayTextureBasePath}/{name}_{variant % maxV}.png";
    }

    /// <summary>获取地形调试颜色</summary>
    public Color GetTerrainColor() => TerrainColorMap(Terrain);

    /// <summary>获取带高程调整的地形颜色</summary>
    public Color GetTerrainColorWithHeight()
    {
        var baseColor = GetTerrainColor();
        float tweak = Elevation * 0.15f;
        return new Color(
            Mathf.Clamp(baseColor.R + tweak, 0.0f, 1.0f),
            Mathf.Clamp(baseColor.G + tweak, 0.0f, 1.0f),
            Mathf.Clamp(baseColor.B + tweak * 0.5f, 0.0f, 1.0f)
        );
    }

    private static Color TerrainColorMap(TerrainType t) => t switch
    {
        TerrainType.DeepWater => new Color(0.18f, 0.30f, 0.55f),
        TerrainType.ShallowWater => new Color(0.30f, 0.45f, 0.70f),
        TerrainType.Sand => new Color(0.85f, 0.75f, 0.50f),
        TerrainType.Plains => new Color(0.72f, 0.68f, 0.48f),
        TerrainType.Grassland => new Color(0.55f, 0.70f, 0.35f),
        TerrainType.Forest => new Color(0.22f, 0.45f, 0.18f),
        TerrainType.DenseForest => new Color(0.12f, 0.30f, 0.08f),
        TerrainType.Jungle => new Color(0.15f, 0.35f, 0.10f),
        TerrainType.Taiga => new Color(0.25f, 0.35f, 0.30f),
        TerrainType.Bog => new Color(0.35f, 0.40f, 0.38f),
        TerrainType.Hills => new Color(0.58f, 0.52f, 0.38f),
        TerrainType.Mountain => new Color(0.40f, 0.38f, 0.42f),
        TerrainType.MountainSnow => new Color(0.85f, 0.88f, 0.92f),
        TerrainType.Snow => new Color(0.92f, 0.95f, 0.98f),
        TerrainType.Ice => new Color(0.75f, 0.85f, 0.95f),
        TerrainType.Swamp => new Color(0.38f, 0.48f, 0.28f),
        TerrainType.Savanna => new Color(0.70f, 0.65f, 0.30f),
        TerrainType.Wasteland => new Color(0.65f, 0.55f, 0.45f),
        TerrainType.Rocky => new Color(0.45f, 0.45f, 0.50f),
        TerrainType.Road => new Color(0.65f, 0.55f, 0.38f),
        TerrainType.River => new Color(0.25f, 0.42f, 0.68f),
        _ => new Color(0.5f, 0.5f, 0.5f),
    };

    /// <summary>地形类型名称</summary>
    public static string TerrainToString(TerrainType t) => t switch
    {
        TerrainType.DeepWater => "深水",
        TerrainType.ShallowWater => "浅水",
        TerrainType.Sand => "荒漠",
        TerrainType.Plains => "平原",
        TerrainType.Grassland => "草地",
        TerrainType.Forest => "森林",
        TerrainType.DenseForest => "密林",
        TerrainType.Jungle => "丛林",
        TerrainType.Taiga => "针叶林",
        TerrainType.Bog => "冻土沼泽",
        TerrainType.Hills => "丘陵",
        TerrainType.Mountain => "山脉",
        TerrainType.MountainSnow => "雪山",
        TerrainType.Snow => "雪地",
        TerrainType.Ice => "冰原",
        TerrainType.Swamp => "沼泽",
        TerrainType.Savanna => "稀树草原",
        TerrainType.Wasteland => "荒原",
        TerrainType.Rocky => "岩石荒地",
        TerrainType.Road => "道路",
        TerrainType.River => "河流",
        _ => "未知",
    };

    // ========================================
    // 序列化
    // ========================================

    /// <summary>序列化为 Godot Dictionary</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            ["q"] = Coord.X,
            ["r"] = Coord.Y,
            ["terrain"] = (int)Terrain,
            ["elevation"] = Elevation,
            ["moisture"] = Moisture,
            ["temperature"] = Temperature,
            ["is_road"] = IsRoad,
            ["is_river"] = IsRiver,
            ["road_dirs"] = RoadDirections,
            ["river_dirs"] = RiverDirections,
            ["has_settlement"] = HasSettlement,
            ["settlement_type"] = SettlementType,
            ["poi_id"] = PoiId,
            ["region_name"] = RegionName,
            ["visibility"] = Visibility,
        };
    }

    /// <summary>从 Godot Dictionary 反序列化</summary>
    public static HexOverworldTile Deserialize(Godot.Collections.Dictionary data)
    {
        var tile = new HexOverworldTile();
        tile.Coord = new Vector2I(
            data.ContainsKey("q") ? (int)data["q"] : 0,
            data.ContainsKey("r") ? (int)data["r"] : 0
        );
        tile.PixelPos = AxialToPixel(tile.Coord.X, tile.Coord.Y);
        tile.Terrain = data.ContainsKey("terrain") ? (TerrainType)(int)data["terrain"] : TerrainType.Plains;
        tile.Elevation = data.ContainsKey("elevation") ? (float)data["elevation"] : 0.0f;
        tile.Moisture = data.ContainsKey("moisture") ? (float)data["moisture"] : 0.5f;
        tile.Temperature = data.ContainsKey("temperature") ? (float)data["temperature"] : 0.5f;
        tile.IsRoad = data.ContainsKey("is_road") && (bool)data["is_road"];
        tile.IsRiver = data.ContainsKey("is_river") && (bool)data["is_river"];
        tile.RoadDirections = data.ContainsKey("road_dirs") ? (int)data["road_dirs"] : 0;
        tile.RiverDirections = data.ContainsKey("river_dirs") ? (int)data["river_dirs"] : 0;
        tile.HasSettlement = data.ContainsKey("has_settlement") && (bool)data["has_settlement"];
        tile.SettlementType = data.ContainsKey("settlement_type") ? (int)data["settlement_type"] : 0;
        tile.PoiId = data.ContainsKey("poi_id") ? (string)data["poi_id"] : "";
        tile.RegionName = data.ContainsKey("region_name") ? (string)data["region_name"] : "";
        tile.Visibility = data.ContainsKey("visibility") ? (int)data["visibility"] : 0;
        tile.UpdateTerrainProperties();
        return tile;
    }
}
