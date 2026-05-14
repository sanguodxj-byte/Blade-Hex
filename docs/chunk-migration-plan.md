# Chunk 流式地图迁移方案（修订版）

## 核心设计理念

**规则程序化**（类 Minecraft / 战场兄弟）：
- 生成规则是**固定的**（精灵总在森林、矮人总在山里、人类占平原），但具体地理**每次不同**
- 每个种子产生一个独特的大陆，但结构上都是合理的（有海岸、山脉、森林、政治分布等）
- 生态环境与国家深度绑定：每种生物群落（biome）有自己的动植物、资源、可能的势力
- 世界一次性生成后**永久保留**（chunk 序列化到磁盘，不会因卸载而丢失修改）
- Chunk 只是**内存管理单元**：活跃 chunk 在内存中渲染，非活跃 chunk 在磁盘

关键区别（与旧方案）：
- ❌ 旧方案：硬编码 6 个矩形区域 `WorldGenerator.SetupRegions` 固定 `XRange/YRange`
- ✅ 新方案：地形由噪声生成 → 聚类分析得到区域 → 匹配国家偏好放置势力

## 前置条件

- [x] `OverworldEntityManager` 拆分完成（Core 处理器 + Frontend 薄壳）
- [x] `ITimeProvider` 接入完成（Core 层可自治读取天数）
- [x] `TriggerContext.CurrentDay` 已改为从 `TimeProvider` 读取
- [ ] `OverworldScene.gd` → C# 迁移完成（当前进行中）

本方案在 OverworldScene C# 迁移完成后执行。

---

## 一、目标

将大地图从"64×48 全图一次性生成并全部驻留内存"切换到"大世界一次性骨架生成 + Chunk 流式加载/卸载 + 持久化修改"。

**核心收益**：
1. 支持更大世界（128×96 chunk × 16 tile = 2048×1536 轴向格 vs 当前 64×48）
2. 内存占用从 O(全图) 降到 O(活跃 chunk)，非活跃 chunk 持久化到磁盘
3. 世界有完整的大陆形态和政治地理，不是随机拼凑
4. 遭遇从"预生成实体全局 tick"变为"按需触发 + 数据驱动"
5. 条件触发系统（TriggerEngine）正式上线

---

## 二、世界生成流水线（一次性，新游戏时执行）

### 2.1 生成阶段总览

```
新游戏 → WorldCreator.CreateWorld(seed, config)
  │
  ├─ Stage 1: 大陆形态（噪声 + 边缘衰减 → 海陆轮廓）
  ├─ Stage 2: 气候场（纬度 + 海拔 + 噪声 → 温度/湿度场）
  ├─ Stage 3: 生物群落（BiomeRules 按 tile 决策 → 每个 tile 的地形类型）
  ├─ Stage 4: 生态区聚类（Flood-fill + 面积阈值 → 识别出若干个"生态区"）
  ├─ Stage 5: 国家版图分配（每个国家按"偏好生态区类型"挑选合适的连通块作为领土）
  ├─ Stage 6: 河流/道路骨架（河流沿高程梯度 + 道路连接国家首都）
  ├─ Stage 7: POI 放置（国家内按密度分布 POI + 野外散布巢穴/废墟）
  ├─ Stage 8: 遭遇密度图（基于生态区危险度 + 距离势力中心）
  └─ Stage 9: 序列化全部 chunk 到磁盘
```

每个 stage 都是**规则驱动 + 种子随机**：同一种子得到同一世界，不同种子得到结构合理但细节不同的世界。

### 2.2 大陆形态（Stage 1）

**规则**：
- 用多层 Perlin/Simplex 噪声生成高程场
- 边缘衰减（距离地图边界 15% 以内的 elevation 线性衰减到 0）→ 被海洋包围
- 噪声参数调整使大陆形状每次不同（可能是椭圆、不规则、半岛状、群岛状）

**不固定的部分**：
- 大陆具体轮廓形状
- 山脉走向（可能南北向、东西向、对角线）
- 海岸线具体位置

**固定的部分**：
- 一定有连通的大陆主体
- 一定被海洋包围
- 一定有山脉/平原/森林/沙漠等多种地形

### 2.3 生态区（Biome Zones）与国家偏好（Stage 4-5）

**关键数据结构**：

```csharp
/// <summary>
/// 生态区 — 地形聚类后的连通区域
/// 一个生态区是"同类地形的一块连通土地"，由 flood-fill 识别
/// </summary>
public class BiomeZone
{
    public int Id;                          // 聚类 ID
    public BiomeType DominantBiome;         // 主导生态类型
    public HashSet<Vector2I> TileCoords;   // 属于这个生态区的所有 tile 坐标
    public int TileCount => TileCoords.Count;
    public Vector2I Centroid;               // 几何中心
    public float AverageElevation;
    public float AverageTemperature;
    public float AverageMoisture;
}

/// <summary>
/// 生态类型 — 用于国家偏好匹配
/// 不同于细粒度的 TerrainType（20+种），这里是粗粒度分类（~8种）
/// </summary>
public enum BiomeType
{
    Plains,        // 平原/草地（适合人类）
    Forest,        // 森林/密林（适合精灵）
    Mountain,      // 山地/丘陵（适合矮人）
    Wasteland,     // 荒原/沙漠/焦土（适合兽人）
    Swamp,         // 沼泽/湿地（适合蜥蜴人/暗影教团）
    Tundra,        // 冻土/雪原（适合冰霜生物）
    Jungle,        // 丛林（中立/危险）
    Coastal,       // 沿海（适合海盗/商贸）
}
```

**国家偏好（数据驱动，`NationConfig`）**：

```csharp
public class NationConfig
{
    public string Id;
    public string Race;

    // 偏好生态类型（有序，第一个最偏好）
    public BiomeType[] PreferredBiomes;

    // 生态区面积需求（太小的生态区容纳不下）
    public int MinTerritoryTiles;

    // 是否必须在大陆主体（避免精灵国被分到小岛上）
    public bool RequiresMainlandContinent;

    // POI 密度（每 1000 tile 多少个村庄/城镇）
    public float PoiDensityPer1000Tiles;

    // 人口规模因子（影响军事实力、POI 数量）
    public float PopulationScale;
}
```

**分配算法（Stage 5）**：

```
1. 所有国家按优先级排序（人类 > 精灵 > 矮人 > 兽人 > ...）
2. for 每个国家 N:
     候选生态区 = 所有还未被占领、且 DominantBiome 在 N.PreferredBiomes 中、且面积 ≥ N.MinTerritoryTiles 的生态区
     按"偏好优先级 × 面积 × 种子随机扰动"打分
     选最高分的生态区作为 N 的核心领土
     从剩余生态区中扣除该区域
3. 小势力（哥布林/狗头人等）在大国领土边缘或中立区的合适生态区散布
4. 未被任何势力占领的生态区 → 中立/野外
```

**不固定的部分**：
- 精灵国可能在大陆西部、东部、或者北部的森林里
- 人类王国的形状和数量（2-4 个，取决于平原生态区的数量和大小）
- 国家之间的相对位置
- 国家的具体边界（跟随生态区的不规则形状）

**固定的部分**：
- 精灵**一定**在森林生态区
- 矮人**一定**在山地生态区
- 人类**一定**在平原生态区
- 一定有至少 2 个人类王国、1 精灵国、1 矮人国、1-2 兽人部落
- 国家规模大小关系大致合理

### 2.4 生态-国家绑定带来的深度

每个国家不只是"一块领土"，而是**与其生态深度绑定的完整系统**：

| 国家 | 生态 | 建筑风格 | 特产物品 | 招募兵种 | 常见遭遇 | 特有资源 |
|---|---|---|---|---|---|---|
| 人类王国 | 平原 | 石砌城堡、木屋 | 粮食、马匹 | 骑士、长矛兵、弓箭手 | 山贼、野狼 | 小麦、铁矿 |
| 精灵王国 | 森林 | 树屋、石塔 | 精灵弓、药水 | 游侠、德鲁伊、弓箭手 | 树精、巨蛛 | 月银、药草 |
| 矮人王国 | 山地 | 地下堡垒 | 重甲、武器 | 战锤矮人、弩手 | 哥布林、地底怪物 | 矿石、宝石 |
| 兽人部落 | 荒原 | 骨营地、石堡 | 粗制武器 | 狂战士、野猪骑兵 | 食人魔、蜥蜴人 | 骨甲、战旗 |
| 哥布林聚落 | 森林边缘/洞穴 | 木栅栏、地洞 | 劣质装备 | 哥布林战士、弓手 | 自己就是遭遇 | — |
| 暗影教团 | 沼泽/废墟 | 黑石祭坛 | 诅咒物品 | 邪教徒、亡灵 | 不死生物 | 黑铁 |

**数据驱动配置**（建议放在 `resources/nations/*.tres`）：

```
kingdom_central.tres:
  id: "kingdom_central"
  race: "human"
  preferred_biomes: [Plains, Coastal]
  building_style: "stone_castle"
  poi_templates: ["human_town", "human_village", "human_castle"]
  recruit_pool: ["knight", "spearman", "archer"]
  trade_goods: ["wheat", "iron", "horse"]
  encounter_pool: ["bandit", "wolf_pack"]
```

### 2.5 河流/道路（Stage 6）

**河流**：
- 从高海拔向低海拔寻路（高程梯度下降）
- 遇到海洋/湖泊终止
- 多条河流汇入同一条（形成树状结构）
- 数量和位置每次不同（依赖高程场）

**道路**：
- 连接同国家内所有城镇（最小生成树）
- 连接盟友国家之间（根据外交关系）
- 敌对国家之间没有道路（玩家需要走野外）

**不固定**：具体路径、河流数量、道路网拓扑
**固定**：城镇一定被道路连接、河流一定从山流向海

### 2.6 POI 放置（Stage 7）

每个国家在自己的领土内按密度分布 POI：

```
国家领土 = {tile1, tile2, ...} (来自 Stage 5)
for 每个 POI 模板 T (town/village/castle):
  count = 领土面积 × 该 POI 类型密度
  for i in range(count):
    候选位置 = 领土内满足 T.TerrainRequirements 的 tile
    排除已被其他 POI 占用或距离太近的位置
    用种子随机挑选一个位置
```

**不固定**：每个村庄/城镇的具体位置
**固定**：每个国家都有 X 个城镇、Y 个村庄（密度规则）、城堡在战略位置

### 2.7 野外/中立区域

不属于任何国家的生态区 → 野外，可能有：
- 龙巢（山地/火山）
- 古墓/废墟（各种地形）
- 土匪营地（山脉隘口、森林边缘）
- 怪物巢穴（沼泽、密林）
- 资源点（矿脉、药草田）

这些 POI 的位置每次不同，但类型分布规则固定。

### 2.8 生成后持久化

世界生成完成后，**所有 chunk 立即序列化到存档目录**：
```
saves/<save_id>/
  world_meta.json       # 种子、世界尺寸、生态区清单、国家清单、POI 索引
  chunks/
    chunk_0_0.dat       # 序列化的 ChunkData
    chunk_0_1.dat
    ...
  fog.dat               # ChunkFogOfWar 状态
  triggers.dat          # TriggerHistory
  entities.dat          # 全局实体状态（POI、势力关系等）
```

运行时只加载活跃 chunk（玩家周围 LoadRadius 范围），修改后的 chunk 写回磁盘。

---

## 三、架构对照

| 维度 | 旧路径（当前运行） | 新路径（Chunk 模式） |
|---|---|---|
| 地形生成 | `HexOverworldGenerator.Generate()` 全图 64×48 | 新游戏时全部生成 → 按 chunk 序列化到磁盘 |
| 地形存储 | `HexOverworldGrid`（全部 tile 在内存） | `ChunkManager.ActiveChunks`（仅活跃 chunk 在内存，其余在磁盘） |
| 河流/道路 | `HexOverworldGenerator` 内部生成 | `RiverRoadGenerator` 全局骨架 → 生成时 stamp 到每个 chunk |
| 迷雾 | `FogOfWar`（像素网格 byte[,]） | `ChunkFogOfWar`（HashSet<Vector2I>） |
| 遭遇 | `OverworldEntityManager` 全局 AI tick | `EncounterSpawner` 生成时预计算槽位 + `TriggerEngine` 运行时条件判定 |
| 寻路 | `HexOverworldAStar`（全图 A*） | 分层寻路：chunk 级粗路径 + 活跃 chunk 内精确路径 |
| POI | `OverworldScene._generate_world_pois()` 硬编码 | 生成流水线 Stage 7 数据驱动 |
| 实体 | `OverworldEntityManager` 全局移动 + 每日决策 | POI 驱动 + 视野内 cosmetic 动画 + TriggerEngine 事件 |

---

## 四、迁移步骤（按 commit 粒度）

### Phase 1: 生态规则 SSOT + 国家配置

**目标**：替代原有的"矩形区域硬编码"，改为"生态类型 + 国家偏好"数据驱动。

**新文件**：
- `BladeHexCore/src/Map/BiomeType.cs` — 粗粒度生态类型枚举（8种）
- `BladeHexCore/src/Map/TerrainToBiome.cs` — TerrainType → BiomeType 映射
- `BladeHexCore/src/Strategic/NationConfig.cs` — 国家配置数据类
- `resources/nations/*.tres` — 每个国家一个 .tres 资源文件（Godot Resource 数据驱动）

```csharp
namespace BladeHex.Map;

public enum BiomeType { Plains, Forest, Mountain, Wasteland, Swamp, Tundra, Jungle, Coastal }

public static class TerrainToBiome
{
    public static BiomeType Map(HexOverworldTile.TerrainType terrain) => terrain switch
    {
        HexOverworldTile.TerrainType.Plains or ... => BiomeType.Plains,
        HexOverworldTile.TerrainType.Forest or ... => BiomeType.Forest,
        // ...
    };
}
```

```csharp
namespace BladeHex.Strategic;

[GlobalClass]
public partial class NationConfig : Resource
{
    [Export] public string Id = "";
    [Export] public string DisplayName = "";
    [Export] public string Race = "human";
    [Export] public Godot.Collections.Array<BiomeType> PreferredBiomes = new();
    [Export] public int MinTerritoryTiles = 500;
    [Export] public bool RequiresMainlandContinent = true;
    [Export] public float PoiDensityPer1000Tiles = 3.0f;
    [Export] public float PopulationScale = 1.0f;
    [Export] public Godot.Collections.Array<string> PoiTemplates = new();
    [Export] public Godot.Collections.Array<string> RecruitPool = new();
    [Export] public Godot.Collections.Array<string> TradeGoods = new();
    [Export] public Godot.Collections.Array<string> EncounterPool = new();
    [Export] public string BuildingStyle = "";
}
```

**删除**：旧的 `WorldGenerator.SetupRegions()`（硬编码的 6 个矩形区域）

**验证**：加载 .tres 文件，断言所有必要字段非空，没有偏好冲突

---

### Phase 2: 生物群落规则 SSOT

**目标**：`BiomeDecision` 逻辑只保留一份。

**新文件**：`BladeHexCore/src/Map/BiomeRules.cs`

```csharp
namespace BladeHex.Map;

public static class BiomeRules
{
    public const float SeaLevel = 0.30f;
    public const float ShallowLevel = 0.35f;
    public const float BeachLevel = 0.38f;
    public const float MountainLevel = 0.78f;

    public static HexOverworldTile.TerrainType Decide(float elevation, float moisture, float temperature) { ... }
}
```

**改动**：
- `ChunkGenerator.BiomeDecision` → 调用 `BiomeRules.Decide`
- `HexOverworldGenerator.BiomeDecision` → 调用 `BiomeRules.Decide`
- `RiverRoadGenerator.LiteTerrainInfo` → 调用 `BiomeRules.Decide`

**验证**：同种子生成的地形与旧路径一致

---

### Phase 3: 生态区聚类器

**目标**：从 tile 级地形聚类出生态区（`BiomeZone`），供 Stage 4-5 使用。

**新文件**：
- `BladeHexCore/src/Map/BiomeZone.cs` — 生态区数据类
- `BladeHexCore/src/Map/BiomeZoneAnalyzer.cs` — Flood-fill 聚类器

```csharp
namespace BladeHex.Map;

public class BiomeZoneAnalyzer
{
    /// <summary>
    /// 对所有 chunk 执行 flood-fill 聚类，识别出所有生态区
    /// </summary>
    public List<BiomeZone> Analyze(Dictionary<Vector2I, ChunkData> allChunks)
    {
        var zones = new List<BiomeZone>();
        var visited = new HashSet<Vector2I>();

        foreach (var chunk in allChunks.Values)
        {
            foreach (var tile in chunk.Tiles.Values)
            {
                if (visited.Contains(tile.Coord)) continue;
                if (!tile.IsPassable) { visited.Add(tile.Coord); continue; }

                var biome = TerrainToBiome.Map(tile.Terrain);
                var zone = FloodFill(tile.Coord, biome, allChunks, visited);

                if (zone.TileCount >= MinZoneSize)
                    zones.Add(zone);
            }
        }

        return zones;
    }

    private BiomeZone FloodFill(Vector2I start, BiomeType targetBiome,
        Dictionary<Vector2I, ChunkData> chunks, HashSet<Vector2I> visited) { ... }
}
```

**关键参数**：
- `MinZoneSize`（默认 200 tile）：太小的区域不计为独立生态区，避免碎片化

**输出**：`List<BiomeZone>` 全量生态区清单

---

### Phase 4: 国家版图分配器

**新文件**：`BladeHexCore/src/Strategic/NationAllocator.cs`

```csharp
namespace BladeHex.Strategic;

public class NationAllocator
{
    /// <summary>
    /// 为每个国家分配领土（基于生态偏好）
    /// </summary>
    public Dictionary<string, HashSet<Vector2I>> AllocateTerritories(
        List<BiomeZone> zones,
        List<NationConfig> nations,
        int seed)
    {
        var rng = new Random(seed);
        var nationTerritories = new Dictionary<string, HashSet<Vector2I>>();
        var available = new List<BiomeZone>(zones);

        // 按优先级排序（大国优先）
        nations.Sort((a, b) => b.PopulationScale.CompareTo(a.PopulationScale));

        foreach (var nation in nations)
        {
            var candidates = available
                .Where(z => nation.PreferredBiomes.Contains(z.DominantBiome))
                .Where(z => z.TileCount >= nation.MinTerritoryTiles)
                .ToList();

            if (candidates.Count == 0) continue;

            // 打分：偏好优先级权重 + 面积 + 种子随机扰动
            var scored = candidates.Select(z => (
                zone: z,
                score: ScoreZone(z, nation, rng)
            )).OrderByDescending(x => x.score).ToList();

            var selected = scored[0].zone;
            nationTerritories[nation.Id] = new HashSet<Vector2I>(selected.TileCoords);
            available.Remove(selected);
        }

        return nationTerritories;
    }

    private float ScoreZone(BiomeZone zone, NationConfig nation, Random rng)
    {
        int biomeRank = Array.IndexOf(nation.PreferredBiomes.ToArray(), zone.DominantBiome);
        float biomeScore = 1.0f / (biomeRank + 1); // 首选偏好权重最高
        float sizeScore = Mathf.Log(zone.TileCount);
        float randomScore = (float)rng.NextDouble() * 0.3f;
        return biomeScore * 10 + sizeScore + randomScore;
    }
}
```

**规则**：
- 大国（高 PopulationScale）优先挑选大生态区
- 首选偏好的生态类型得分最高
- 种子随机扰动避免同样规模下完全确定性

**输出**：`Dictionary<string, HashSet<Vector2I>>` — nationId → 领土 tile 集合

---

### Phase 5: WorldCreator 全量生成器

**前置**：Phase 1-4 完成。

**新文件**：`BladeHexCore/src/Strategic/WorldCreator.cs`

```csharp
namespace BladeHex.Strategic;

public class WorldCreator
{
    public WorldData CreateWorld(int seed, WorldCreationConfig config)
    {
        // Stage 1-3: 生成全部 chunk 地形
        var chunks = GenerateAllTerrain(seed, config);

        // Stage 4: 生态区聚类
        var zones = new BiomeZoneAnalyzer().Analyze(chunks);
        GD.Print($"[WorldCreator] 识别出 {zones.Count} 个生态区");

        // Stage 5: 国家版图分配
        var territories = new NationAllocator().AllocateTerritories(
            zones, config.Nations.ToList(), seed);
        GD.Print($"[WorldCreator] {territories.Count} 个国家分配完毕");

        // Stage 6: 河流/道路骨架
        var skeleton = GenerateRiverRoads(seed, chunks, territories);
        StampRiverRoadsToChunks(chunks, skeleton);

        // Stage 7: POI 放置（按国家领土分布）
        var pois = PlacePOIsByNation(chunks, territories, config.Nations, seed);
        // 野外 POI（在未被分配的区域）
        PlaceWildPOIs(chunks, zones, territories, pois, seed);

        // Stage 8: 遭遇密度预计算
        PrecomputeEncounterDensity(chunks, territories, zones);

        // Stage 9: 序列化到磁盘
        SerializeWorld(chunks, pois, skeleton, zones, territories);

        return new WorldData
        {
            Seed = seed,
            Chunks = chunks,
            Pois = pois,
            Skeleton = skeleton,
            Zones = zones,
            Territories = territories,
        };
    }
}
```

**核心流程**：规则驱动 + 种子随机 → 每次生成都不同但合理

---

### Phase 6: ChunkManager 改为磁盘加载模式

**改动**：`ChunkManager.LoadOrGenerateChunk` 的 TODO 实现，改为从磁盘加载已生成的 chunk。

**新文件**：`BladeHexCore/src/Map/Chunk/ChunkPersistence.cs`

```csharp
public static class ChunkPersistence
{
    public static ChunkData? LoadChunk(string saveId, Vector2I coord) { ... }
    public static void SaveChunk(string saveId, ChunkData chunk) { ... }
    public static void SaveModifiedChunks(string saveId, IEnumerable<ChunkData> chunks) { ... }
}
```

卸载时保存修改：chunk 被卸载时，如果有修改（遭遇状态变化、tile 被玩家改变等），写回磁盘。

---

### Phase 7: OverworldScene C# 版接入

**前置**：OverworldScene.gd → C# 迁移已完成。

**核心改动**：

```csharp
// 新游戏
public void StartNewGame(int seed, RaceData.Race race)
{
    // 显示 loading screen
    var config = WorldCreationConfig.Default(seed);
    var worldData = new WorldCreator().CreateWorld(seed, config);

    // 初始化运行时管理器
    _worldGenerator = new WorldGenerator();
    _worldGenerator.LoadExistingWorld(worldData); // 不再 InitializeChunkWorld
}

// 读档
public void LoadGame(string saveId)
{
    _worldGenerator = new WorldGenerator();
    _worldGenerator.LoadFromSave(saveId); // 从磁盘恢复 ChunkManager + Fog + Triggers
}

// 每帧
public override void _Process(double delta)
{
    var playerCoord = PixelToAxial(playerParty.Position);

    // chunk 加载/卸载（从磁盘加载，不是生成）
    var newChunks = _worldGenerator.UpdatePlayerPosition(
        playerCoord.X, playerCoord.Y,
        playerLevel, TimeProvider.CurrentDay);

    // 空间触发
    if (newChunks.Count > 0 && _worldGenerator.TriggerSystem != null)
    {
        var ctx = BuildTriggerContext(playerCoord);
        var results = _worldGenerator.TriggerSystem.EvaluateSpatial(ctx);
        HandleTriggerResults(results);
    }

    // 渲染同步
    _chunkRenderer.SyncVisuals(_worldGenerator.ChunkMgr.ActiveChunks);
    _fogRenderer.Render(_worldGenerator.Fog);
}
```

---

### Phase 8: Chunk 渲染器

**新文件**：`BladeHexFrontend/src/View/Map/ChunkRenderer.cs`

**职责**：
- 维护 `Dictionary<Vector2I, ChunkVisual>` — 每个活跃 chunk 一个渲染节点
- `SyncVisuals(activeChunks)` — 对比上一帧，新增的 chunk 创建 visual，移除的 chunk 释放 visual
- 每个 `ChunkVisual` 内部用 `MultiMesh` 或 `TileMap` 渲染 16×16 hex

**关键设计**：
- 渲染器不持有 tile 数据，只从 `ChunkData.Tiles` 读取
- 迷雾遮罩由 `ChunkFogOfWar.GetState` 驱动：`Unexplored` = 全黑，`Revealed` = 灰色半透明，`InVision` = 无遮罩

---

### Phase 9: Chunk-Aware 寻路

**问题**：旧 `HexOverworldAStar` 需要全图 tile 数据。Chunk 模式下只有活跃 chunk 在内存。

**方案**：分层寻路

```
Layer 1: Chunk 级 A*（粗粒度）
  - 节点 = chunk 坐标
  - 边权 = chunk 平均移动代价（生成时预计算并存入 ChunkData）
  - 用于长距离路径规划

Layer 2: Tile 级 A*（细粒度，仅在活跃 chunk 内）
  - 节点 = 全局轴向坐标
  - 边权 = tile.MoveCost
  - 用于当前 chunk 及相邻 chunk 内的精确路径
```

**新文件**：`BladeHexCore/src/Map/Chunk/ChunkAStar.cs`

```csharp
public class ChunkAStar
{
    /// <summary>
    /// 在活跃 chunk 范围内寻路
    /// 如果目标超出活跃范围，先用 chunk 级 A* 规划粗路径，
    /// 再在活跃 chunk 内做精确路径（到活跃边界为止）
    /// </summary>
    public Vector2[] FindPath(Vector2I fromWorld, Vector2I toWorld, ChunkManager mgr) { ... }
}
```

**预计算**：世界生成时为每个 chunk 计算 `AverageMoveCost` 和 `IsTraversable` 标记，存入 `ChunkData`。

---

### Phase 10: 实体系统调整

**核心变化**：实体不再是"全局持续移动的对象"，而是**POI 驱动的事件源**。

| 旧模式 | 新模式 |
|---|---|
| 全局实体列表 + 每帧移动 + 每日 AI 决策 | POI 属性驱动 + 视野内 cosmetic 动画 |
| 商队/掠夺队/领主军队持续移动 | 逻辑上瞬时（每日结算位置），视觉上在视野内播放移动动画 |
| `OverworldEntityManager.TickMovement` | 仅用于视野内 cosmetic 动画 |
| `DailyDecisionProcessor` 全局决策 | 保留，但只处理视野内 + 相邻 chunk 的实体 |

**保留的实体行为**：
- 玩家视野内的实体仍然有移动动画和 AI 行为（给玩家"活着的世界"感觉）
- 视野外的实体通过每日结算更新位置（瞬时跳转，不做路径动画）
- 掠夺队/商队的"存在"仍然是独立实体，但只在相关 chunk 加载时才 tick

---

### Phase 11: 迷雾切换

**改动**：
- 删除 `FogOfWar`（像素级，345 行）
- `ChunkFogOfWar` 成为唯一迷雾系统
- `FogOfWarRenderer.gd` → 改为读 `ChunkFogOfWar.GetState(chunkCoord)` 驱动 shader

**兼容**：
- 旧存档的 `FogOfWar` 数据可以一次性转换为 `ChunkFogOfWar`（遍历像素网格，标记对应 chunk 为 Revealed）

---

### Phase 12: SaveManager 接入

**改动**：
- `SaveManager.Save()` 增加：
  - 修改过的 chunk 写回磁盘（`ChunkPersistence.SaveModifiedChunks`）
  - `ChunkFogOfWar.Serialize()` → 存探索进度
  - `WorldGenerator.TriggerRecord.Serialize()` → 存触发历史
  - `OverworldPOI[].Serialize()` → 存 POI 状态
  - 实体状态序列化
- `SaveManager.Load()` 增加对应反序列化
- 存档结构见第二节 2.4

---

### Phase 13: 清理旧路径

**删除**（标记 `[Obsolete]` 一个版本后物理删除）：
- `HexOverworldGenerator.cs`（全图生成）
- `HexOverworldGrid.cs`（全图存储）
- `HexOverworldAStar.cs`（全图寻路）
- `FogOfWar.cs`（像素级迷雾）
- `FogOfWarRenderer.gd`（旧渲染器）
- `WorldGenerator.SetupRegions()`（已被 RegionRegistry 替代）
- `WorldGenerator.HexGrid / HexGen` 兼容字段

---

## 五、风险与缓解

| 风险 | 缓解 |
|---|---|
| 新游戏生成耗时（300万格） | 分批生成 + loading screen 进度条；实测 ChunkGenerator 16×16 约 <1ms，128×96 chunk ≈ 12s |
| Chunk 边界接缝（地形/河流/道路） | 全量生成保证一致性；`RiverRoadGenerator` 全局骨架 + `StampRiverRoadOnChunk` 跨 chunk 方向位掩码 |
| 寻路跨 chunk 不连续 | 分层寻路 + chunk 级预计算 `AverageMoveCost` |
| 旧存档不兼容 | 存档版本号 + 迁移函数（FogOfWar → ChunkFogOfWar） |
| 磁盘 IO 延迟 | chunk 文件小（~4KB/chunk），加载用异步 IO + 预加载相邻 chunk |
| POI 跨 chunk 可见性 | POI 全局存储（不随 chunk 卸载），渲染由 `ChunkFogOfWar.IsRevealed` 控制 |
| 世界感觉"空旷" | 国家版图 + 密集 POI + 道路网 + 遭遇密度图保证内容密度 |

---

## 六、执行顺序与并行度

```
Phase 1 (BiomeType + NationConfig)    ─┐
Phase 2 (BiomeRules)                   ─┤── 可并行，无依赖
                                        │
Phase 3 (BiomeZoneAnalyzer 生态聚类)    ─┤── 依赖 Phase 1+2
Phase 4 (NationAllocator 国家分配)      ─┤── 依赖 Phase 3
Phase 5 (WorldCreator 全量生成流水线)   ─┤── 依赖 Phase 1-4
Phase 6 (ChunkManager 磁盘加载)         ─┤── 依赖 Phase 5
Phase 7 (Scene 接入)                    ─┤── 依赖 Phase 6 + OverworldScene C# 迁移
Phase 8 (ChunkRenderer)                 ─┤── 依赖 Phase 7
Phase 9 (ChunkAStar)                    ─┘── 依赖 Phase 7
                                        │
Phase 10 (实体调整)                     ─── 依赖 Phase 7 + 9
Phase 11 (迷雾切换)                     ─── 依赖 Phase 7 + 8
Phase 12 (SaveManager)                  ─── 依赖 Phase 7 + 10 + 11
Phase 13 (清理)                         ─── 最后执行
```

**最小可运行切片**：Phase 1-9 完成后即可切换到 Chunk 模式运行，Phase 10-13 可渐进式完成。

---

## 七、验证标准

**生成规则（规则程序化）**：
- [ ] 同种子生成的世界完全一致（确定性）
- [ ] 不同种子生成的世界布局差异明显（非固定布局）
- [ ] 新游戏生成的大陆有清晰的海岸线、山脉、平原、沙漠、沼泽等多样地形
- [ ] 世界中一定有 2-4 个人类王国、1 精灵国、1 矮人国、1-2 兽人部落
- [ ] 精灵国的核心领土一定在森林生态区，矮人在山地，人类在平原（偏好规则生效）
- [ ] 国家的具体位置、边界形状每次生成都不同（非硬编码）
- [ ] 生态-国家绑定：每个国家的 POI 风格、招募兵种、遭遇池与生态一致

**运行时（Chunk 系统）**：
- [ ] 玩家移动时 chunk 从磁盘加载无卡顿（<16ms per frame）
- [ ] 河流/道路在 chunk 边界无断裂
- [ ] 迷雾状态正确：未探索 chunk 全黑，已探索灰色，活跃 chunk 清晰
- [ ] 遭遇触发正常：进入新 chunk 后有概率触发空间事件
- [ ] 存档/读档后 chunk 修改状态、迷雾、触发历史、POI 状态、国家关系正确恢复
- [ ] 旧存档可迁移（FogOfWar → ChunkFogOfWar 转换无数据丢失）
