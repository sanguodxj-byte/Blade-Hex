# Tech Design — combat-hex-from-overworld-state

> 数据版本:2026-05-17
> 来源:`requirements.md` v1(13 条 R)
> 范围:`BladeHexCore/src/Map/BattleMapGenerator.cs` 的 `GenerateFromOverworld` 链路 + `BattleCellData.TerrainType` 枚举 + `DeploymentZone` 部署区生成 + `BattleContext` 字段
> 不在范围:大地图地形系统本身、战斗规则数值、POI 视觉资源、战斗 AI 决策

---

## 0. 设计目标速览

| 目标 | 来源 | 说明 |
|---|---|---|
| **采样契约可独立测试** | R1 | 把"footprint + K 圈邻居"采样从 `GenerateFromOverworld` 提取到 `OverworldSampler` |
| **战斗端枚举与大地图 1:1 对齐** | R10 | `BattleCellData.TerrainType` 增 9 个值(8 自然 + Bridge) |
| **`MapOverworldToBattle` 全函数** | R5 | 21 case 直映,fallback 改为 `PushError + Plains` |
| **桥从 sample 派生** | R11 | 取消战斗端"扫水带自造桥",改为 `IsBridge = true` sample 投影 |
| **POI 类型只决定结构层** | R12 | terrainType 由 sample 决定;POI 类型只放 Wall/Tower/Gate/Ruins |
| **接战方向 → 部署区** | R13 | `DeploymentZone.GenerateZones` 读 `ApproachDirection` |
| **跨 chunk / 全水域 / null grid 健壮性** | R6 | 三条 fallback 路径不抛异常 |
| **天气进入战斗** | R7 | 新增 `WeatherOverride` 字段(`string`) |
| **确定性 byte-identical** | R8 | 不再用 `GD.Randi`;用 `BattleContext.Seed` 派生本地 RNG |
| **性能 P95 ≤ 50 ms (Stronghold)** | R8#2 | design 阶段先建立基线 |

## 1. 架构总览

### 1.1 改动后的 `GenerateFromOverworld` 流水线

```
Input: BattleContext + HexOverworldGrid
  │
  ├─ Stage 1: Overworld_Sampler  ────────────►  (sampleTiles, sampleCenterAxial, sampleRadius)
  │             (R1, R6 边缘)
  │
  ├─ Stage 2: Projection  ───────────────────►  sampleProjections: List<(tile, battleAxial)>
  │             (R2, R8 确定性, axial 字典序排序)
  │
  ├─ Stage 3: TerrainAssigner
  │     │
  │     ├─ 3a. Land Voronoi    ─────────────►  Dict<axial, base terrain>
  │     │       (R3 Voronoi + R5 mapping + Moisture 微变化)
  │     ├─ 3b. WaterPlacer     ─────────────►  + ShallowWater splash + 河带连线
  │     │       (R4 水域,12% / 30% 硬封顶)
  │     ├─ 3c. RoadPainter     ─────────────►  + Road 覆盖 (R3#5)
  │     ├─ 3d. BridgePlacer    ─────────────►  + Bridge cell + 配套水带 (R11)
  │     └─ 3e. WeatherOverlay  ─────────────►  Snow/Sand 25% 改写 (R7)
  │
  ├─ Stage 4: StructurePlacer  ──────────────►  + Wall/Rampart/Tower/Gate/Ruins
  │             (R12 POI 类型 → 结构,不动 terrainType)
  │
  ├─ Stage 5: DeploymentZoneGenerator  ──────►  PlayerDeployment / EnemyDeployment
  │             (R13 ApproachDirection 感知)
  │
  └─ Stage 6: EnsureConnectivity  ───────────►  保证两区可通行(已有,不改逻辑)

Output: BattleMapData
```

### 1.2 模块边界(新增 / 修改)

| 模块 | 类型 | 路径 | 备注 |
|---|---|---|---|
| `OverworldSampler` | **新增** static class | `BladeHexCore/src/Map/Generation/OverworldSampler.cs` | 纯函数,可单测 |
| `BattleProjection` | **新增** static helper | `BladeHexCore/src/Map/Generation/BattleProjection.cs` | 投影 + 字典序排序 |
| `BridgePlacer` | **新增** static helper | `BladeHexCore/src/Map/Generation/BridgePlacer.cs` | R11 实现 |
| `StructurePlacer` | **新增** static helper | `BladeHexCore/src/Map/Generation/StructurePlacer.cs` | R12 POI 结构层 |
| `BattleMapGenerator.GenerateFromOverworld` | 重构 | `BladeHexCore/src/Map/BattleMapGenerator.cs` | 拆成 6 个 stage,主流程精简 |
| `BattleMapGenerator.MapOverworldToBattle` | 修改 | 同上 | 改为 21 case 全函数 + PushError |
| `BattleCellData.TerrainType` | 增 9 值 | `BladeHexCore/src/Data/BattleCellData.cs` | 见 §2.1 |
| `BattleCellData.GetTerrainProperties` | 增 9 case | 同上 | 见 §2.1 继承表 |
| `BattleContext` | 增字段 | `BladeHexCore/src/Strategic/BattleContext.cs` | `WeatherOverride: string`、`AttackingSide: BattleSide` |
| `POIScaleProfile` | 增字段 | `BladeHexCore/src/Strategic/Scale/POIScale.cs` | `SamplingRingCount: int` |
| `DeploymentZone.GenerateZones` | 增方向感知 | `BladeHexCore/src/Combat/Deployment/DeploymentZone.cs` | R13 实现 |
| `BattleSide` | **新增** enum | `BladeHexCore/src/Strategic/BattleContext.cs` 内部 | `Player` / `Enemy` |

> **方向选择**:`OverworldSampler`、`BattleProjection`、`BridgePlacer`、`StructurePlacer` 都做成纯静态类,签名只接收"输入数据 + RNG",不持有可变状态;符合 R8 / R9#6 的"无依赖、可独立单测"约束。

---

## 2. 关键数据结构

### 2.1 `BattleCellData.TerrainType` 增 9 值(R10)

新增枚举值放在原枚举末尾(保持序列化兼容):

```csharp
public enum TerrainType
{
    // 现有 21 项...
    Plains, Grassland, Savanna, Forest, DenseForest, Hills, Mountain,
    ShallowWater, DeepWater, Swamp, Road, Sand, Snow,
    Wall, Ruins, PoisonMushroom, LuckyGrass,
    Rampart, Tower, Gate, Staircase,

    // === R10 新增(2026-05-17) — 9 个值,索引 21..29 ===
    Jungle,         // 索引 21,继承 DenseForest
    Taiga,          // 索引 22,继承 Forest
    Bog,            // 索引 23,继承 Swamp
    Wasteland,      // 索引 24,继承 Sand
    Rocky,          // 索引 25,继承 Hills
    MountainSnow,   // 索引 26,继承 Mountain + specialEffect="snow"
    Ice,            // 索引 27,继承 Snow
    River,          // 索引 28,继承 ShallowWater + isRiver=true
    Bridge,         // 索引 29,继承 Road + specialEffect="bridge", elevation=1
}
```

`GetTerrainProperties` 工厂方法增 9 case;每个 case 调用对应的"父类"case 拷贝 `TerrainProperties` 后再覆盖差异字段:

```csharp
TerrainType.Jungle => GetTerrainProperties(TerrainType.DenseForest)
    with { TerrainName = "丛林", TerrainColor = new Color(0.20f, 0.45f, 0.15f) },

TerrainType.Bridge => GetTerrainProperties(TerrainType.Road)
    with { TerrainName = "桥", SpecialEffect = "bridge",
           Elevation = 1 /* 桥面高于水 */, IsRiver = false },
```

> **`with` 语法依赖**:`TerrainProperties` 当前是 `record struct` 还是 class 不确定,实施时需要先看 `BattleCellData.cs` 的 TerrainProperties 定义;若是 class,改为 ctor 复制。

### 2.2 `BattleContext` 字段扩展(R7 + R13)

```csharp
public partial class BattleContext : Resource
{
    // 现有字段...

    // R7 新增:天气覆盖(用 string 而非 enum,避免 Core ↔ Frontend 跨层依赖)
    // 取值:"clear" / "rain" / "snow" / "sandstorm" / null;其它值忽略
    public string? WeatherOverride = null;

    // R13 新增:谁是进攻方
    public BattleSide AttackingSide = BattleSide.Player;
}

public enum BattleSide { Player, Enemy }
```

> **WeatherOverride 用 string 的理由**:大地图侧 `BladeHex.View.Environment.WeatherType` 在 Frontend,Core 层 BattleContext 不能直接引用。string 是最低成本的跨层契约,Frontend 调用方负责 `WeatherType.ToString().ToLower()`。design 阶段决定不另设 Core 层枚举(避免双枚举同步)。

### 2.3 `POIScaleProfile.SamplingRingCount`(R1#2)

```csharp
public readonly struct POIScaleProfile
{
    // 现有字段...
    public int SamplingRingCount { get; init; }  // R1#2 新增
}

public static POIScaleProfile Get(POIScale scale) => scale switch
{
    POIScale.Tiny   => new POIScaleProfile(/* ... */, samplingRingCount: 1),
    POIScale.Small  => new POIScaleProfile(/* ... */, samplingRingCount: 1),
    POIScale.Medium => new POIScaleProfile(/* ... */, samplingRingCount: 2),
    POIScale.Large  => new POIScaleProfile(/* ... */, samplingRingCount: 3),
    _ => throw new System.NotImplementedException(),
};
```

> 注:`POIScaleProfile` 是 `readonly struct` 且字段是 `init`-only,加新字段需要更新 ctor 签名 + 所有 `Get` 内 `new`。

### 2.4 采样输出三元组

```csharp
public readonly struct SampleSet
{
    public IReadOnlyList<HexOverworldTile> Tiles { get; init; }
    public Vector2I CenterAxial { get; init; }
    public int Radius { get; init; }  // max axial distance from CenterAxial
    public bool IsEmpty => Tiles.Count == 0;
}
```

### 2.5 投影输出

```csharp
public readonly struct SampleProjection
{
    public HexOverworldTile Tile { get; init; }
    public Vector2I BattleAxial { get; init; }  // 投影后的战斗 axial(可能在地图外)
    public bool IsLand { get; init; }           // 用于 R3#1 Voronoi 候选筛选
    public bool IsWater { get; init; }
    public bool IsBridge { get; init; }
}
```

`sampleProjections: List<SampleProjection>` 在 stage 2 末尾按 `(BattleAxial.X ASC, BattleAxial.Y ASC)` 字典序排序(R8#1)。

---

## 3. 模块详细设计

### 3.1 `OverworldSampler`(R1, R6)

```csharp
public static class OverworldSampler
{
    /// <summary>
    /// R1 主入口。POI 战 = footprint + K 圈邻居;野外 = encounterCoord + K 圈邻居。
    /// 不依赖任何全局状态;不抛异常(grid null / footprint 空 / 跨 chunk 失败均走 fallback)。
    /// </summary>
    public static SampleSet Sample(BattleContext context, HexOverworldGrid? grid, int samplingRingCount)
    {
        if (grid == null) return SampleSet.Empty;  // R6#2 上层走模板路径

        var footprint = ResolveFootprint(context);
        if (footprint.Count == 0)
        {
            // R6#4 警告并退化到野外路径
            GD.PushWarning("[OverworldSampler] DefendingPOI.OccupiedHexes 为空,退化到 EncounterCoord");
            footprint = new[] { context.EncounterCoord };
        }

        var hexes = ExpandRings(footprint, samplingRingCount);  // 包含 footprint 自身
        var tiles = new List<HexOverworldTile>(hexes.Count);
        foreach (var h in hexes)
        {
            var tile = grid.GetTileAtCoord(h);
            if (tile != null) tiles.Add(tile);
            // R1#5: grid 返回 null 时跳过,不抛
        }

        if (tiles.Count == 0) return SampleSet.Empty;  // R6#1 上层走模板兜底

        var center = context.DefendingPOI?.CenterHex ?? context.EncounterCoord;
        int radius = 0;
        foreach (var t in tiles)
        {
            int d = HexUtils.AxialDistance(t.Coord, center);
            if (d > radius) radius = d;
        }

        return new SampleSet { Tiles = tiles, CenterAxial = center, Radius = radius };
    }

    private static List<Vector2I> ResolveFootprint(BattleContext ctx)
    {
        if (ctx.DefendingPOI != null && ctx.DefendingPOI.OccupiedHexes.Length > 0)
            return new List<Vector2I>(ctx.DefendingPOI.OccupiedHexes);
        return new List<Vector2I> { ctx.EncounterCoord };  // 野外
    }

    private static HashSet<Vector2I> ExpandRings(IList<Vector2I> footprint, int K)
    {
        var union = new HashSet<Vector2I>(footprint);
        for (int i = 0; i < K; i++)
        {
            var next = new HashSet<Vector2I>(union);
            foreach (var h in union)
                foreach (var nb in HexUtils.GetNeighbors(h.X, h.Y))
                    next.Add(nb);
            union = next;
        }
        return union;
    }
}
```

**测试要点**(R1 acceptance criteria 1-9):
- Tiny POI(footprint 1 + K=1)→ 7 hex
- Small POI(footprint 3 + K=1)→ ~12 hex(去重后)
- Medium POI(footprint 5 + K=2)→ ~30 hex
- Large POI(footprint 7 + K=3)→ ~49 hex
- 野外 → 7 hex
- grid 中部分 hex 返回 null → tiles 数减少但不抛
- DefendingPOI.OccupiedHexes 空 → 走 EncounterCoord + warning

### 3.2 `BattleProjection`(R2)

```csharp
public static class BattleProjection
{
    public static List<SampleProjection> Project(SampleSet samples, int battleHexRadius)
    {
        var result = new List<SampleProjection>(samples.Tiles.Count);
        if (samples.Radius == 0)
        {
            // R2#3:单 tile 直接放原点
            var t = samples.Tiles[0];
            result.Add(MakeProjection(t, Vector2I.Zero));
            return result;
        }

        float scale = (float)battleHexRadius / samples.Radius;
        foreach (var tile in samples.Tiles)
        {
            int dq = tile.Coord.X - samples.CenterAxial.X;
            int dr = tile.Coord.Y - samples.CenterAxial.Y;

            // R2#4:水 sample 用降低后的有效缩放(避免水落地图外)
            float effScale = IsWaterSample(tile) ? scale * 0.6f : scale;
            int bq = Mathf.RoundToInt(dq * effScale);
            int br = Mathf.RoundToInt(dr * effScale);
            result.Add(MakeProjection(tile, new Vector2I(bq, br)));
        }

        // R8#1: 按 axial 字典序稳定排序
        result.Sort((a, b) =>
        {
            int c = a.BattleAxial.X.CompareTo(b.BattleAxial.X);
            return c != 0 ? c : a.BattleAxial.Y.CompareTo(b.BattleAxial.Y);
        });
        return result;
    }

    private static bool IsWaterSample(HexOverworldTile t) =>
        t.Terrain is HexOverworldTile.TerrainType.DeepWater
        or HexOverworldTile.TerrainType.ShallowWater
        or HexOverworldTile.TerrainType.River
        || t.IsRiver;

    private static SampleProjection MakeProjection(HexOverworldTile tile, Vector2I battleAxial) =>
        new()
        {
            Tile = tile,
            BattleAxial = battleAxial,
            IsLand = !IsWaterSample(tile),
            IsWater = IsWaterSample(tile),
            IsBridge = tile.IsBridge,  // R11 派生
        };
}
```

### 3.3 `BattleMapGenerator.MapOverworldToBattle` 改为 21 case 全函数(R5)

```csharp
static BattleCellData.TerrainType MapOverworldToBattle(HexOverworldTile.TerrainType t) => t switch
{
    HexOverworldTile.TerrainType.DeepWater     => BattleCellData.TerrainType.DeepWater,
    HexOverworldTile.TerrainType.ShallowWater  => BattleCellData.TerrainType.ShallowWater,
    HexOverworldTile.TerrainType.River         => BattleCellData.TerrainType.River,         // R10 新枚举
    HexOverworldTile.TerrainType.Sand          => BattleCellData.TerrainType.Sand,
    HexOverworldTile.TerrainType.Plains        => BattleCellData.TerrainType.Plains,
    HexOverworldTile.TerrainType.Grassland     => BattleCellData.TerrainType.Grassland,
    HexOverworldTile.TerrainType.Forest        => BattleCellData.TerrainType.Forest,
    HexOverworldTile.TerrainType.DenseForest   => BattleCellData.TerrainType.DenseForest,
    HexOverworldTile.TerrainType.Jungle        => BattleCellData.TerrainType.Jungle,        // R10
    HexOverworldTile.TerrainType.Taiga         => BattleCellData.TerrainType.Taiga,         // R10
    HexOverworldTile.TerrainType.Bog           => BattleCellData.TerrainType.Bog,           // R10
    HexOverworldTile.TerrainType.Swamp         => BattleCellData.TerrainType.Swamp,
    HexOverworldTile.TerrainType.Savanna       => BattleCellData.TerrainType.Savanna,
    HexOverworldTile.TerrainType.Wasteland     => BattleCellData.TerrainType.Wasteland,     // R10
    HexOverworldTile.TerrainType.Rocky         => BattleCellData.TerrainType.Rocky,         // R10
    HexOverworldTile.TerrainType.Hills         => BattleCellData.TerrainType.Hills,
    HexOverworldTile.TerrainType.Mountain      => BattleCellData.TerrainType.Mountain,
    HexOverworldTile.TerrainType.MountainSnow  => BattleCellData.TerrainType.MountainSnow,  // R10
    HexOverworldTile.TerrainType.Snow          => BattleCellData.TerrainType.Snow,
    HexOverworldTile.TerrainType.Ice           => BattleCellData.TerrainType.Ice,           // R10
    HexOverworldTile.TerrainType.Road          => BattleCellData.TerrainType.Road,
    _ => Fallback(t),
};

static BattleCellData.TerrainType Fallback(HexOverworldTile.TerrainType t)
{
    GD.PushError($"[BattleMapGenerator] 未识别的大地图地形: {t}");  // R5#3
    return BattleCellData.TerrainType.Plains;
}
```

### 3.4 `BridgePlacer`(R11)

```csharp
public static class BridgePlacer
{
    /// <summary>
    /// R11:对每个 IsBridge=true 的 SampleProjection,根据其 Terrain 决定桥规模
    /// (River=2, ShallowWater=3, DeepWater=4),沿邻近 Road sample 方向延展。
    /// </summary>
    public static void Place(
        List<SampleProjection> projections,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        BattleMapData mapData)
    {
        foreach (var bridgeProj in projections.Where(p => p.IsBridge))
        {
            int length = bridgeProj.Tile.Terrain switch
            {
                HexOverworldTile.TerrainType.River        => 2,
                HexOverworldTile.TerrainType.ShallowWater => 3,
                HexOverworldTile.TerrainType.DeepWater    => 4,
                _ => 2,
            };

            // 找邻近 Road sample 决定延展方向
            var direction = ResolveBridgeDirection(bridgeProj, projections);
            if (direction == Vector2I.Zero)
            {
                GD.PushWarning("[BridgePlacer] 桥 sample 无相邻 Road sample,降级为 1 cell");
                length = 1;
                direction = new Vector2I(1, 0);  // 任意默认
            }

            // 沿方向放置 length 个 Bridge cell
            for (int i = 0; i < length; i++)
            {
                var pos = bridgeProj.BattleAxial + direction * i;
                if (!mapData.ContainsCoord(pos)) continue;  // R11#11 静默截断
                terrainMap[pos] = BattleCellData.TerrainType.Bridge;
            }

            // 配套 splash 水带(R11#3)
            int splashRadius = bridgeProj.Tile.Terrain switch
            {
                HexOverworldTile.TerrainType.River => 1,
                _ => 2,
            };
            // 在 bridge 周围 splashRadius 内放 ShallowWater(避免覆盖到桥本身)
            // ... 复用 R4 WaterPlacer 的 splash 逻辑
        }
    }
}
```

### 3.5 `StructurePlacer`(R12)

POI 类型 → 结构规则查表(R12#3 / #4 / #5)统一抽到 `POIStructureRule.Resolve(poi)`:

```csharp
public readonly struct POIStructureRule
{
    public BattleCellData.TerrainType MainStructure { get; init; }
    public int MinCount { get; init; }
    public int MaxCount { get; init; }
    public bool HasWallRing { get; init; }  // 是否生成完整城墙环(Castle / Minotaur Settlement / DragonLair...)
    public bool HasGate { get; init; }
    public bool HasTower { get; init; }
    public BattleCellData.TerrainType[] AdditionalStructures { get; init; }
}

public static class POIStructureRuleTable
{
    public static POIStructureRule Resolve(OverworldPOI poi) => poi.PoiTypeEnum switch
    {
        PoiTypeEnum.Castle => new() { MainStructure = Rampart, HasWallRing = true,
                                       HasGate = true, HasTower = true,
                                       AdditionalStructures = new[] { Staircase } },
        PoiTypeEnum.Town => new() { MainStructure = Ruins, MinCount = 4, MaxCount = 8 },
        PoiTypeEnum.Settlement => ResolveSettlement(poi),  // R12#4 按 Race 分支
        PoiTypeEnum.Lair => ResolveLair(poi),              // R12#5 按 LairType 分支
        // ... 11 种 PoiType 全部显式 case
        _ => Fallback(poi),
    };
}
```

`StructurePlacer.Place`:
- 中心 POI 按 rule.MaxCount 全量放
- 邻居 POI 按 `ceil(rule.MaxCount × 0.25)` 缩量,且只在该 POI 投影点的 axial 半径 ≤ 2 的局部簇内
- 不改写水域 / Road / Bridge / 已被前一阶段写过的非自然地形 cell(R12#6)

### 3.6 `DeploymentZone.GenerateZones` 方向感知(R13)

```csharp
public static (List<Vector2I> player, List<Vector2I> enemy) GenerateZones(
    BattleMapData mapData, BattleContext context)
{
    // 现有 EngagementType 分支保留(Ambush / Ambushed)
    if (context.Engagement == EngagementType.Ambush)
        return GenerateAmbushZones(mapData, context, /* ApproachDir 仅作敌方集中点参考 */);
    if (context.Engagement == EngagementType.Ambushed)
        return GenerateAmbushedZones(mapData, context);

    // R13#11a:Castle 战(或带城墙环 POI)走"墙内 vs 墙外"
    if (HasFullWallRing(mapData) && context.DefendingPOI != null)
        return GenerateCastleZones(mapData, context);

    // R13:有方向感知就用方向,否则走 q 轴左右
    if (context.ApproachDirection.HasValue)
        return GenerateDirectionalZones(mapData, context.ApproachDirection.Value);

    // R13#3:fallback 到现有"q 轴左右"
    return GenerateLegacyLeftRightZones(mapData);
}

private static (List<Vector2I> p, List<Vector2I> e) GenerateDirectionalZones(
    BattleMapData mapData, Vector2I approachDir)
{
    // 1. 把 approachDir 归一化到 6 个 axial 单位方向之一
    var enemyDir = NormalizeToHexDir(approachDir);
    var playerDir = -enemyDir;

    // 2. 取战场中心,沿 enemyDir 方向延伸至边缘的扇形 cell 为敌方区
    //    沿 playerDir 方向延伸为玩家区,中间 1~2 行 hex 为中间区
    // 3. 按 R13#7 硬约束验证可通行 cell ≥ 1.5×单位数;否则 fallback 到 R13#8
    // ...
}
```

`NormalizeToHexDir` 实现:取 6 个 axial 单位向量,选与 `approachDir` 点积最大的(等价于夹角最小)。

### 3.7 重构后的 `GenerateFromOverworld` 主流程

```csharp
BattleMapData GenerateFromOverworld(BattleContext context)
{
    // === 0. 基础数据 ===
    var poiScale = ResolvePoiScale(context);  // 从 PoiType 或 Size 推
    var profile = POIScaleTable.Get(poiScale);
    int N = profile.BattleHexRadius;
    int K = profile.SamplingRingCount;
    var rng = new SeededRng(context.Seed);  // R8#1:本地 RNG

    var mapData = CreateMapData(context.Size, new BattleMapTemplate { TemplateName = "overworld_dynamic" });

    // === Stage 1: 采样 ===
    var samples = OverworldSampler.Sample(context, context.OverworldGrid, K);
    if (samples.IsEmpty)
    {
        // R6#1:fallback 到模板路径
        GD.PushWarning("[BattleMapGenerator] Sample_Set 为空,fallback 到 template");
        return GenerateFromTemplateInternal(context);
    }

    // === Stage 2: 投影 ===
    var projections = BattleProjection.Project(samples, N);

    // === Stage 3: 地形分配 ===
    var terrainMap = new Dictionary<Vector2I, BattleCellData.TerrainType>();
    AssignLandVoronoi(projections, mapData, terrainMap, rng);  // R3
    PlaceWater(projections, mapData, terrainMap, rng);          // R4
    PaintRoads(projections, mapData, terrainMap);                // R3#5
    BridgePlacer.Place(projections, terrainMap, mapData);        // R11
    if (!string.IsNullOrEmpty(context.WeatherOverride))
        ApplyWeatherOverride(context.WeatherOverride, terrainMap, rng);  // R7

    // === Stage 4: 结构层 ===
    StructurePlacer.Place(projections, terrainMap, mapData, context, rng);  // R12

    // === Stage 5: 部署区 ===
    var (player, enemy) = DeploymentZone.GenerateZones(mapData, context);   // R13
    mapData.PlayerDeployment = player;
    mapData.EnemyDeployment = enemy;

    // === Stage 6: 写回 cells + 连通性 ===
    WriteCellsFromTerrainMap(mapData, terrainMap);
    EnsureConnectivity(mapData);

    // === 7. TemplateName 摘要 ===
    mapData.TemplateName = BuildTemplateNameSummary(samples, context);  // R12#13
    return mapData;
}
```

---

## 4. 确定性 RNG(R8#1)

新增 `SeededRng` 替代 `GD.Randf` / `GD.Randi`:

```csharp
internal sealed class SeededRng
{
    private uint _state;
    public SeededRng(int seed) { _state = (uint)(seed == 0 ? 1 : seed); }
    public float NextFloat() { /* xorshift */ }
    public int NextRange(int min, int max) { /* */ }
}
```

替换点:
- `BattleProjection.Project`:无随机(纯几何)
- `OverworldSampler.Sample`:无随机
- `BridgePlacer.Place`:方向选择确定性
- `StructurePlacer.Place`:cell 选择 + 数量 roll 用 `SeededRng`
- `WeatherOverlay`:25% 改写选 cell 用 `SeededRng`
- `AssignLandVoronoi` 的 detail noise:`FastNoiseLite` 用 `context.Seed` 作种子(已支持)

---

## 5. 性能基线计划(R8#2)

R8#2 要求 Stronghold(N=14, 631 cells)P95 ≤ 50 ms。当前无基线数据,实施时:

### 5.1 基线采样

在 `SimulationHarness` 增 `BatchScale` scenario 子项:

```csharp
case "battle_scale":
    return RunBattleScaleBatch(battles, seed);
```

逻辑:固定 100 次循环,每次构造 `BattleContext{ Size = Stronghold, OverworldGrid = 真实 grid }`,统计:
- P50 / P95 / P99 耗时
- 各 stage 占比(用 `Stopwatch` 分段)

### 5.2 已知热点风险

| 阶段 | 风险点 | 缓解 |
|---|---|---|
| `OverworldSampler.ExpandRings` | K=3 时 hex 数 ~50,grid lookup ~50 次 | grid 已是 chunk 字典,O(1) lookup,可接受 |
| `AssignLandVoronoi` | 631 cells × 30 land samples = ~19k axial distance | 可接受(每次距离计算 ~10 ns) |
| `BattleProjection.Project.Sort` | 50 sample 字典序排序 | O(n log n) 完全不是热点 |
| `EnsureConnectivity` | 已有逻辑,跨水域可能 BFS 整张图 | 不在本特性优化范围 |

预估总耗时:Stronghold 单次 < 10 ms。50 ms 阈值富余 5×,如果实测 P95 < 30 ms 则 OK,否则按 §5.3 优化。

### 5.3 性能优化候选(实测后再决定)

- Voronoi 用 KD-tree 替代线性扫描(samples 增加到 100+ 时可能要)
- `terrainMap` 改为 `Dictionary<Vector2I, ...>` → `Span<TerrainType>` 平铺数组
- `StructurePlacer` 的 cell 候选预筛
- 避免 `IReadOnlyList<T>.ToList()` 的隐式分配

---

## 6. 测试策略

### 6.1 单元测试(`BladeHexCore/tests/Map/Generation/`)

| 测试用例 | 覆盖 R |
|---|---|
| `OverworldSampler_TinyPoi_Returns7Tiles` | R1#2 #3 |
| `OverworldSampler_LargePoi_K3_Returns49Tiles` | R1#2 |
| `OverworldSampler_NullGrid_ReturnsEmpty` | R6#2 |
| `OverworldSampler_OutOfChunk_SkipsNullTiles` | R1#5 R6#3 |
| `OverworldSampler_EmptyFootprint_FallsBackToEncounterCoord` | R6#4 |
| `BattleProjection_SingleTile_ReturnsOrigin` | R2#3 |
| `BattleProjection_WaterScaleReduced` | R2#4 |
| `BattleProjection_AxialOrderStable` | R8#1 |
| `MapOverworldToBattle_All21Values_HasExplicitCase` | R5#1 |
| `MapOverworldToBattle_FallbackPushesError` | R5#3 |
| `BattleCellData_GetTerrainProperties_All30Values_HasCase` | R10#3 |
| `BridgePlacer_BridgeSampleNeighborRoad_Places2Cell` | R11#3 #4 |
| `BridgePlacer_BridgeSampleNoRoadNeighbor_DowngradesTo1Cell` | R11#5 |
| `StructurePlacer_Castle_PlacesWallRing` | R12#3 |
| `StructurePlacer_NoStructureCellOnWater` | R12#6 |
| `StructurePlacer_NeighborPoi_Limited25Percent` | R12#10 |
| `DeploymentZone_ApproachEast_PlayerDeploysWest` | R13#2 |
| `DeploymentZone_NullApproach_FallsBackToLegacy` | R13#3 |
| `DeploymentZone_Castle_DefenderInsideWalls` | R13#11a |
| `WeatherOverlay_Snow_25PercentChange` | R7#1 |
| `WeatherOverlay_DoesNotTouchWaterRoadStructure` | R7#2 |
| `Determinism_SameSeed_BiteIdenticalMap` | R8#1 |

### 6.2 集成测试

`BattleMapGeneratorTests.GenerateFromOverworld_*`:
- `_Castle_HasFullWallRing`
- `_Port_PlacesBridgeIfWaterRoadSampled`
- `_WildEncounter_NoStructures`
- `_FullWaterFootprint_30PercentWaterCap`(R4#4)
- `_Stronghold_Cells631_AndConnected`(R6#5)

### 6.3 性能基线

`SimulationHarness.battle_scale` scenario(§5.1)。实施时先跑 100 次 baseline,记录到 `combat-numerics-audit.md`。

### 6.4 现有 96 单测保持

`HighLevelSanityCheck` 等不调用 `GenerateFromOverworld`,应不受影响。`BattleMapGenerator` 改动需要确保:
- `GenerateFromTemplateInternal` 路径不变(R6#2 向后兼容)
- 现有模板地形权重表不动(R10#6)
- 旧存档反序列化不识别 enum 时按 Plains 兜底(R10#5)— 这个是 Godot 资源序列化默认行为,不需要新代码

---

## 7. 实施风险与未决问题

### 7.1 风险

| 风险 | 影响 | 缓解 |
|---|---|---|
| `TerrainProperties` 是否支持 `with` 表达式 | §2.1 9 case 写法依赖 | 实施前先看代码,若是 class 改为复制 ctor |
| `POIScaleProfile` 是 `readonly struct`,加字段需要更新所有 `new` | 编译错误 | 改 ctor 签名 + Get 4 处 new,一次性全改 |
| `OverworldPOI.OccupiedHexes` 是否总是含 `CenterHex` | 影响 footprint 解析 | 实施前看 `OverworldPOI.cs`,必要时显式合并 |
| 现有 `StructurePlacer` 不存在,从零实现 | 工作量 | R12 是本 spec 最大块,单独拆 task |
| `EnvironmentAudioComponent.WeatherType`(10 值)与 `View/Environment/WeatherType`(4 值)双枚举共存 | 命名冲突 | `BattleContext.WeatherOverride: string` 规避(§2.2) |

### 7.2 未决问题(留到实施 task 时再讨论)

- **POI 中心是否总在 footprint 第一个**:`OverworldPOI.OccupiedHexes[0] == CenterHex` 是约定还是巧合?StructurePlacer 需要知道 POI 中心方便环放置
- **Castle WallRing 几何形状**:R12#3 说"完整城墙环"但没规定半径;design 阶段假设半径 = `N / 3`(里圈占 1/3),实施时如果地图过小可能形成不了完整环 → R13#11b 已设 fallback
- **R13#11a 的"封闭区域"判定**:用 BFS 从 Gate 开始算"墙内"区域?还是单纯按 axial 距离 < N/3?— 倾向 BFS,但开销可能高
- **`SeededRng` 算法**:用 xorshift32 还是引入 `System.Random`?xorshift32 更轻、跨平台一致;System.Random 在 .NET 内部实现可能跨版本变化,不推荐

---

## 8. 实施分阶段建议

按"低风险高价值"排序,task 拆分参考:

| 阶段 | 任务 | 预计 |
|---|---|---|
| **Phase 1** | 枚举对齐(R10):BattleCellData 增 9 值 + GetTerrainProperties 增 9 case + 现有 switch 显式覆盖 | 2 h |
| **Phase 1** | MapOverworldToBattle 改 21 case 全函数(R5) | 30 min |
| **Phase 1** | BattleContext 增 WeatherOverride / AttackingSide 字段 | 30 min |
| **Phase 1** | POIScaleProfile 增 SamplingRingCount 字段 + ctor 更新 | 30 min |
| **Phase 2** | OverworldSampler 抽离 + 单测(R1 R6) | 4 h |
| **Phase 2** | BattleProjection 抽离 + 单测(R2 R8) | 2 h |
| **Phase 2** | GenerateFromOverworld 主流程改写为 6 stage 调用 | 3 h |
| **Phase 3** | BridgePlacer 实现 + 单测(R11) | 4 h |
| **Phase 3** | WaterPlacer 抽离(已有逻辑,只是把 12% / 30% 硬封顶 / 河带连线显式)(R4) | 2 h |
| **Phase 3** | WeatherOverlay 实现(R7) | 1 h |
| **Phase 4** | StructurePlacer 实现(R12)— **最大块,单独 PR** | 1 day |
| **Phase 4** | POIStructureRuleTable 11 PoiType + 7 SettlementRace + 8 LairType 全表 | 2 h |
| **Phase 5** | DeploymentZone.GenerateZones 方向感知(R13) | 4 h |
| **Phase 5** | DeploymentZone Castle 战墙内 vs 墙外(R13#11a) | 4 h |
| **Phase 6** | 性能基线(R8#2)`battle_scale` scenario | 2 h |
| **Phase 6** | 集成测试 + 文档同步(`docs/21-战斗地图生成规则.md`) | 2 h |

总估时:**~5 工作日**。Phase 1 / 2 / 3 可并行;Phase 4 单独 PR;Phase 5 / 6 收尾。

---

## 9. 文档同步清单

实施后需要同步:
- `docs/21-战斗地图生成规则.md` §三.1:R5 表 + R10 新增 9 项
- `docs/21-战斗地图生成规则.md`:R12 POI 结构层规则表
- `docs/31-比例尺与距离体系.md`(若新增比例尺常量),否则不需要
- `combat-numerics-audit.md`:Wave 4 章节记录本 spec 落地点

---

> 本 design 已穷举 R1 ~ R13 的 acceptance criteria 到具体模块/函数;实施时按 §8 分阶段拆 task,每个 task 闭环单测 + 集成测试。
