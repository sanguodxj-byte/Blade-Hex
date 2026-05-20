# Requirements Document — combat-hex-from-overworld-state

## Introduction

战斗场景的可用 hex 区域(六边形 grid,半径 N)以及每个战斗 cell 的初始状态(地形 / 海拔 / 道路 / 河流 / 水域)需要从大地图的真实 tile 状态派生而来,而不是仅由 POI 类型查表。本特性是对**已有**链路 `BattleMapGenerator.GenerateFromOverworld(BattleContext)`(`BladeHexCore/src/Map/BattleMapGenerator.cs`,约 230 行)的优化。

当前实现已经把"footprint + K 圈外围邻居 → Voronoi → 战斗 cell"主流程跑通(K 当前固定为 1),但仍有以下问题需要本次解决:

- 采样、投影、Voronoi 投票、水洼、河流连线、装饰物全部内联在 `GenerateFromOverworld` 一个方法中,采样口径不是可独立测试的契约
- 大地图侧 21 种地形 → 战斗侧地形的映射当前只有 14 项显式 case + `_ => Plains` 兜底,本特性把战斗端补齐到 21 项并改为 1:1 显式映射,使 `Elevation / Moisture / IsRiver / IsRoad` 等附加状态规则可以被声明为不可变契约
- 大地图侧已存在的 `WeatherType` 不进入战斗 cell 状态,雪天/沙暴战的环境一致性缺失
- 跨 chunk 边界、POI footprint 全部落水、`EncounterCoord` 在 chunk 未生成区域等边缘场景没有显式回退
- 经验性魔法常量(水域 12% 封顶 / 水 sample 缩放 0.6 / splash 半径 / Elevation 阈值 0.30 / 0.65)没有出处,没有可验证的 SLA

本次优化遵循 SSOT(`docs/31-比例尺与距离体系.md`):大地图 hex ≈ 250 m,战斗 cell ≈ 20 m,**两者不是 1:1 物理映射**;POIScale 4 档绑定的战斗 hex 半径 N(7 / 8 / 11 / 14)与战斗 cell 数公式 `1 + 3·N·(N+1)` 不在本 spec 内重新讨论。

## 核心设计原则(钉死)

> **战斗地图是大地图被采样区域的"放大版"。**

具体含义:
- 战斗地图的**所有内容**(地形 / 海拔 / 道路 / 河流 / 桥 / POI 结构)都来自 sample 集合中真实存在的大地图 tile + 该 tile 关联的 POI;**不允许**从"模板表 / POI 类型表"凭空注入任何 cell。
- "放大"指 sample 集合中**单个**大地图 hex 在战斗地图上覆盖**多个** Battle_Cell(由 R2 的投影 + R3 的 Voronoi 实现);倍率不是物理比例(250 m ↛ 20 m × 12.5),而是"采样区域 → 战斗六边形"的几何映射。
- 战斗规模越大,采样圈数越多,反映"更广阔的周边地区";采样到什么就放大什么,采样不到的不出现。
- POI 在采样范围内,就在对应方向放置结构(Wall / Tower / Ruins / ...);中心 POI 占主导(规模完整),邻居 POI 只参与一小部分(规模按距离衰减)。
- 桥(R11)= 大地图 `IsBridge` 的派生体现;桥的位置、大小、方向都来自 sample tile,战斗端不自造桥。

任何与"放大版"原则冲突的设计——POI 模板覆盖采样、独立桥建设算法、固定部署区方向——都必须以本原则为准予以否决。

## Glossary

- **Overworld_Grid**:大地图 hex 网格,`HexOverworldGrid`,提供 `GetTileAtCoord` / `GetNeighbors` 接口
- **Overworld_Tile**:`HexOverworldTile`,大地图单格,直径约 250 m,字段含 `Terrain` / `Elevation` / `Moisture` / `Temperature` / `IsRoad` / `IsRiver` / `OccupyingPoiName` / `IsPoiCenter`
- **Battle_Cell**:`BattleCellData`,战斗单格,直径约 20 m,字段含 `terrainType` / `elevation` / `isPassable` / `moveCost`
- **Battle_Map**:`BattleMapData`,本次战斗的全部 cell 集合;六边形地图由 `HexRadius = N` 唯一决定 cell 数 `1 + 3·N·(N+1)`
- **POI_Footprint**:`OverworldPOI.OccupiedHexes`,该 POI 在大地图上占用的所有 axial 坐标(中心 + 附属)
- **Sample_Set**:本次战斗参与生成的 Overworld_Tile 集合
  - POI 战斗:Footprint 全部 hex + 沿 Footprint 外扩 `samplingRingCount` 圈邻居(去重,排除已在 Footprint 内的 hex);`samplingRingCount` 由 `POIScaleTable.Get(scale).SamplingRingCount` 决定
  - 野外遭遇:玩家所在 hex + 沿其外扩 `samplingRingCount`(默认 = Tiny 档,即 1)圈邻居
- **Sample_Center_Axial**:Sample_Set 在大地图上的代表中心 axial,用作"大地图 axial → 战斗 axial"投影的原点。POI 战斗用 `POI.CenterHex`,野外遭遇用 `EncounterCoord`
- **Projection**:把每个 Sample_Tile 的大地图 axial 偏移按缩放因子 `scale = battleRadius / sampleRadius` 投影到战斗 axial 平面的位置
- **Voronoi_Owner**:某个 Battle_Cell 在战斗 axial 上,axial-distance 最近的陆地 Sample_Tile;若多个并列,取迭代顺序中第一个
- **Land_Sample / Water_Sample**:按 `Sample_Tile.Terrain ∈ {DeepWater, ShallowWater, River} ∨ Sample_Tile.IsRiver` 分类
- **Battle_Context**:`BladeHex.Map.BattleContext`,本次战斗的输入,含 `OverworldGrid` / `EncounterCoord` / `DefendingPOI` / `Size` / `Engagement` / `Seed` 等
- **Overworld_Sampler**:本次新增的逻辑边界(类或可独立测试的函数集合),职责单一:输入 Battle_Context + Overworld_Grid,输出 Sample_Set 与 Sample_Center_Axial。本特性中明确把它从 `GenerateFromOverworld` 中分离出来
- **Battle_Cell_State**:Battle_Cell 上由本特性派生的初始状态,包括 `terrainType` / `elevation` / `isPassable` / 道路标记 / 水域标记;不含战斗中后续动态变化(燃烧、雾化等)
- **Overworld_Mapping_Table**:大地图地形 → 战斗地形的不可变查表(本 spec 内声明,实现位于 `BattleMapGenerator.MapOverworldToBattle`)

## Requirements

### Requirement 1 — 采样器作为独立可测试边界(采样半径与战斗规模绑定)

**User Story:** 作为战斗场景生成模块的维护者,我希望"从大地图采样"是一个独立、纯函数式、可单测的边界,以便后续修改 Voronoi 算法或地形映射不影响采样规则。**作为玩家,我希望大型战斗的战场反映"更广阔的周边地区",而不是把同一圈邻居拉伸成更大地图——所以采样半径必须随战斗规模放大。**

> **设计立场**:本特性的核心契约是"**战斗地图的所有地形都来自大地图采样**"——R3 的 Voronoi 分配、R4 的水域、R11 的桥(派生自 IsBridge)、R5 的 1:1 地形映射,统一从 Sample_Set 派生。POI 类型(R12)只影响"结构层"(Wall / Tower / Ruins / Bridge),**不再**直接覆盖任何 Battle_Cell 的 terrainType。

#### Acceptance Criteria

1. THE Overworld_Sampler SHALL 接受 `BattleContext` 与 `HexOverworldGrid` 作为输入,返回 `(IReadOnlyList<HexOverworldTile> sampleTiles, Vector2I sampleCenterAxial, int sampleRadius)` 三元组

2. THE Overworld_Sampler SHALL 根据本次战斗的 `POIScale`(POI 战斗)或 `EngagementType`(野外遭遇)在 `POIScaleTable` 中查 **采样圈数** `samplingRingCount`,具体数值如下:

   | POIScale / 场景 | footprint(POI 占用 hex 数) | samplingRingCount(向外扩张的圈数,1 圈 = 6 hex) | 期望 Sample_Set 大小(hex 数) |
   |---|---|---|---|
   | Tiny / 野外遭遇 | 1 | 1 | 1 + 6 = **7 hex** |
   | Small | 3 | 1 | 3 + 9(去重)≈ **12 hex** |
   | Medium | 5 | 2 | 5 + 25(去重)≈ **30 hex** |
   | Large | 7 | 3 | 7 + 42(去重)≈ **49 hex** |

   > "圈数"的几何含义:从 footprint 整体向外**扩张**多少层 hex 边。1 圈 = footprint 边界外的所有相邻 hex(对单 hex footprint 即 6 邻居);2 圈 = 1 圈 + 1 圈外再相邻 hex(每层 hex 数随距离增长)。"hex 数"是 sample 集合最终的 tile 总数(footprint + 外围 K 圈,去重)。

   > 数值未来可能由 design 阶段根据 sim 实测调整;但语义保持"战斗规模越大,采样半径越大",不允许出现"小规模反而采得多"的反向情况。`samplingRingCount` 字段 SHALL 加到 `POIScaleProfile`(`BladeHexCore/src/Strategic/Scale/POIScale.cs`)上,与 `BattleHexRadius` 同级。

3. WHEN `BattleContext.DefendingPOI` 非空且 `DefendingPOI.OccupiedHexes.Length > 0`,THE Overworld_Sampler SHALL 把 Footprint 全部 hex + 沿 footprint 外扩 `samplingRingCount` 圈邻居(去重)加入 Sample_Set,并返回 `sampleCenterAxial = DefendingPOI.CenterHex`。"外扩 K 圈"定义:取 footprint 中所有 hex 的并集,反复执行 `union ← union ∪ Σ neighbors(hex)` 共 K 次,最终结果减去 footprint 即为外围圈层。

4. WHEN `BattleContext.DefendingPOI` 为空(野外遭遇),THE Overworld_Sampler SHALL 把 `BattleContext.EncounterCoord` 所在 tile + 沿其外扩 `samplingRingCount`(默认 = Tiny 档,即 1)圈邻居加入 Sample_Set,并返回 `sampleCenterAxial = EncounterCoord`。

5. IF Overworld_Grid 对某个 axial 返回 null(跨 chunk 未生成 / 越界),THEN THE Overworld_Sampler SHALL 跳过该 axial 而不抛出异常,继续收集其余 tile

6. THE Overworld_Sampler SHALL 在 Sample_Set 内不出现重复 tile(以 `HexOverworldTile.Coord` 作为去重 key)

7. THE Overworld_Sampler SHALL 计算 `sampleRadius = max(AxialDistance(t.Coord, sampleCenterAxial))` over Sample_Set;当 Sample_Set 仅含 1 个 tile 时,`sampleRadius = 0`

8. THE Overworld_Sampler SHALL 不**也不被允许**根据 POI 类型注入任何"虚拟 sample"——所有地形派生必须可追溯到 sample 集合中实际存在的、来自大地图的 `HexOverworldTile`。

9. THE Overworld_Sampler 单次调用 SHALL 最多读取 `(footprint.Count + 6 × footprint.Count × samplingRingCount + 7)` 个 Overworld_Tile(配合 R8#3 重写)。


### Requirement 2 — 大地图 → 战斗 hex 的投影与缩放契约

**User Story:** 作为战斗地图生成器,我需要一个稳定的"大地图 axial → 战斗 axial"投影规则,使得 Sample_Set 完整填充战斗六边形 grid,同时不让单个 Sample_Tile 占据超出其代表范围。

#### Acceptance Criteria

1. THE Battle_Map SHALL 使用六边形 grid,半径 `N = POIScaleTable.Get(scale).BattleHexRadius`(POI 战斗)或 `N = POIScaleTable.Get(POIScale.Tiny).BattleHexRadius`(野外遭遇);本要求不重新定义 N,仅引用 SSOT
2. WHEN `sampleRadius > 0`,THE Projection SHALL 把每个 Sample_Tile 的大地图 axial 偏移 `(dq, dr) = tile.Coord − sampleCenterAxial` 缩放到战斗 axial `(round(dq · scale), round(dr · scale))`,其中 `scale = N / sampleRadius`
3. WHEN `sampleRadius = 0`(只有一个 sample tile),THE Projection SHALL 把该 tile 投影到战斗 axial 原点 `(0, 0)`
4. WHERE Sample_Tile 是 Water_Sample,THE Projection SHALL 使用降低后的有效缩放 `effScale = scale × 0.6`(避免水 sample 投影到地图边缘后大量水 cell 落在地图外)
5. THE Projection SHALL 对相同的 `(Sample_Set, sampleCenterAxial, N)` 三元组**多次调用**返回 byte-identical 的投影坐标列表;实现禁止依赖 `GD.Randi/Randf` 等全局随机源,也不允许依赖外部时间或进程状态。
6. IF 投影后的战斗 axial 距原点超出 `N`,THEN THE Projection SHALL 仍保留该投影点(后续 Voronoi 与 splash 阶段会自然忽略地图外的 cell);该投影点不进入 Battle_Map 但可作为 Voronoi 候选

### Requirement 3 — 陆地地形 Voronoi 投票

**User Story:** 作为战斗地图生成器,我希望每个战斗 cell 的初始陆地地形由"axial 距离最近的陆地 Sample_Tile"决定,这样陆地结构在战斗 grid 内呈现自然的 Voronoi 分区。

#### Acceptance Criteria

1. THE Battle_Map SHALL 为每个 Battle_Cell 指派一个 Voronoi_Owner —— 投影后的 Land_Sample 中 axial-distance 最近者。

   > **平局解决(确定性约束)**:当多个 Land_Sample 与某 Battle_Cell 的 axial 距离同时取得最小值时,实现 SHALL 按"sample 在 sampleProjections 列表中的迭代顺序"取**第一个**为 owner。同时,sampleProjections 在投影完成后 SHALL 按 axial 字典序 `(q ASC, r ASC)` 稳定排序,以保证相同输入产出 byte-identical 输出(配合 R8#1 的确定性要求)。
2. WHEN Battle_Cell 拥有 Voronoi_Owner,THE Battle_Cell.terrainType SHALL 由 Overworld_Mapping_Table 查表得到 base 类型,再叠加 Moisture-based 微变化(见 R5)
3. WHEN Battle_Cell 拥有 Voronoi_Owner,THE Battle_Cell.elevation SHALL = `Elevation_Map(Voronoi_Owner.Elevation)`,叠加由 detail noise 触发的 ±1 抖动(具体规则:`nv < -0.5 → 海拔 -1`,`nv > 0.5 → 海拔 +1`),最终值 clamp 到 `[0, 2]`。

   > 注:本条与 R5#4 都读取同一个 detail noise(`FastNoiseLite.GetNoise2D(coord.X*2, coord.Y*2)`),但**作用维度不同、阈值不同**。本条用 `±0.5` 的对称阈值修改 elevation 数值;R5#4 用 `≥ 0.65` 的单向阈值触发 moisture-based 的 terrainType 微变化。两者互相独立,可在同一 cell 上同时触发。
4. IF Sample_Set 中没有任何 Land_Sample(极端:港口 footprint 全在海上),THEN THE Battle_Map SHALL 用 `BattleCellData.TerrainType.Grassland` + `elevation = 1` 作为陆地兜底,使战斗依然可玩
5. WHEN Voronoi_Owner 的 `IsRoad = true`,THE Battle_Cell.terrainType SHALL 被改写为 `BattleCellData.TerrainType.Road`,`elevation = 1`(道路覆盖原地形)

### Requirement 4 — 水域(海/湖/河)分配规则与硬封顶

**User Story:** 作为战斗地图生成器,我希望水域 sample 在战斗 grid 上以"水洼 + 河流连线"形式出现,而不是占据整个 Voronoi 分区,且水域占比不会失控让玩家无法部署。

#### Acceptance Criteria

1. THE Battle_Map SHALL 在陆地 Voronoi 完成之后再放置水 cell;水 sample 不进入陆地 Voronoi 候选集合
2. WHEN Sample_Set 含至少一个 Water_Sample,THE Battle_Map SHALL 以每个 Water_Sample 投影点为中心,在 axial 半径 `splashRadius` 内按距离衰减概率把可改写 cell 改为 `ShallowWater`,其中 `splashRadius = 2`(当 Water_Sample 数 ≤ 2)或 `1`(当 Water_Sample 数 ≥ 3)
3. THE Battle_Map SHALL 维持水域硬封顶:水 cell 总数 ≤ `floor(totalCells × 0.12)`,其中 `totalCells = 1 + 3·N·(N+1)`
4. WHERE Sample_Set 完全没有 Land_Sample,THE Battle_Map SHALL 把水域硬封顶提升至 `floor(totalCells × 0.30)`,以表达"港口/海上遭遇"的水面占比
5. THE Battle_Map SHALL 不在 `Road` 或 `Wall` 类型 cell 上放置水
6. WHEN Sample_Set 中存在两个或更多 `Water_Sample.IsRiver = true` 的 sample,THE Battle_Map SHALL 沿其投影点之间的 hex line 放置 `ShallowWater` cell 形成连通河带,但不得使水 cell 总数突破 R4#3 的硬封顶
7. THE Battle_Cell SHALL 在 `terrainType ∈ {ShallowWater, DeepWater}` 时设置 `elevation = 0`

### Requirement 5 — 大地图地形 → 战斗地形映射表(SSOT)

**User Story:** 作为战斗地图生成器与 sim 验证脚本的共同消费者,我希望"大地图 21 种地形 → 战斗 21 种地形"的 1:1 映射是一份不可变查表,任何修改必须显式更新此表,避免代码中悄悄漂移。

#### Acceptance Criteria

1. THE Overworld_Mapping_Table SHALL 是 `HexOverworldTile.TerrainType → BattleCellData.TerrainType` 的**全函数**(域上 21 个枚举各自有恰好一个 case 分支),实现位于 `BattleMapGenerator.MapOverworldToBattle`,并由单元测试遍历 `Enum.GetValues<HexOverworldTile.TerrainType>()` 的全部值各执行一次断言、确保每个输入都能命中显式 case 而不是兜底分支。

2. 完整映射表如下(本 spec 钉死,任何修改必须先改本表并同步至 `docs/21 §三.1`):

   | 大地图地形 | → | 战斗地形 |
   |---|---|---|
   | 深水 DeepWater | → | 深水 DeepWater |
   | 浅水 ShallowWater | → | 浅水 ShallowWater |
   | 河流 River | → | 河流 River |
   | 沙地 Sand | → | 沙地 Sand |
   | 平原 Plains | → | 平原 Plains |
   | 草地 Grassland | → | 草地 Grassland |
   | 森林 Forest | → | 森林 Forest |
   | 密林 DenseForest | → | 密林 DenseForest |
   | 丛林 Jungle | → | 丛林 Jungle |
   | 针叶林 Taiga | → | 针叶林 Taiga |
   | 冻土沼泽 Bog | → | 冻土沼泽 Bog |
   | 沼泽 Swamp | → | 沼泽 Swamp |
   | 稀树草原 Savanna | → | 稀树草原 Savanna |
   | 荒原 Wasteland | → | 荒原 Wasteland |
   | 岩石荒地 Rocky | → | 岩石荒地 Rocky |
   | 丘陵 Hills | → | 丘陵 Hills |
   | 山地 Mountain | → | 山地 Mountain |
   | 雪山 MountainSnow | → | 雪山 MountainSnow |
   | 雪地 Snow | → | 雪地 Snow |
   | 冰原 Ice | → | 冰原 Ice |
   | 道路 Road | → | 道路 Road |

3. 地形映射表 SHALL 不包含"未匹配则归到平原"或任何其他默认兜底分支。当 switch 表达式遇到未列入的枚举值时,实现 SHALL 输出错误日志(`GD.PushError("[BattleMapGenerator] 未识别的大地图地形: {value}")`)并以 `Plains` 作为最后兜底返回,以让"未来新增大地图地形枚举漏改本表"的情况立刻在测试和运行中暴露,而不是被静默吞掉。
4. WHERE detail noise 在 Battle_Cell 上的归一化值 ≥ 0.65,THE Battle_Cell.terrainType SHALL 由 Moisture-based 微变化规则替换 base 类型(例:Grassland + moisture > 0.6 → Forest)。

   > 注:本条与 R3#3 都读取同一个 detail noise,但**作用维度与阈值不同**:本条用 `≥ 0.65` 的单向高阈值触发 terrainType 微变化;R3#3 用 `±0.5` 的对称阈值修改 elevation。两者互相独立。
5. THE Elevation_Map SHALL 把 `Overworld_Tile.Elevation ∈ [0, 1]` 映射为离散三档:`[0, 0.30) → 0`(低地)、`[0.30, 0.65] → 1`(平地)、`(0.65, 1] → 2`(高地)
6. THE Overworld_Mapping_Table SHALL 不读取 Overworld_Tile 上 `Terrain / Elevation / Moisture / IsRoad / IsRiver` 之外的字段;Settlement / Visibility / OccupyingPoiName 等不影响 Battle_Cell 初始地形

### Requirement 6 — 跨 chunk / 边缘场景的健壮性

**User Story:** 作为战斗场景生成器,即使玩家在 chunk 边界或 POI footprint 跨越未加载区域时触发战斗,我也要能产出一张可玩的战斗地图,而不是崩溃或只有一种地形。

#### Acceptance Criteria

1. IF Sample_Set 收集后为空(所有 axial 在 Overworld_Grid 上均返回 null),THEN THE Battle_Map SHALL 使用与模板路径(`GenerateFromTemplateInternal`)等价的"默认平原模板"行为生成,且记录一条 warning 日志说明 fallback 触发
2. IF `BattleContext.OverworldGrid` 为 null,THEN THE BattleMapGenerator.Generate SHALL 走 `GenerateFromTemplateInternal` 路径,而不调用 Overworld_Sampler;本特性不改变此分支(向后兼容 QuickCombat / 测试场景)
3. WHEN 一个或多个 Footprint hex 在 Overworld_Grid 上不存在,THE Overworld_Sampler SHALL 仍然返回非空 Sample_Set,只要至少一个 Footprint hex 或邻居被解析成功
4. IF `DefendingPOI.OccupiedHexes` 为空数组,THEN THE Overworld_Sampler SHALL 退化到野外遭遇路径(EncounterCoord + 6 邻居),并记录一条 warning 日志
5. THE BattleMapGenerator SHALL 在所有上述边缘路径下,确保返回的 `BattleMapData` 满足:`HexRadius = N`、`cells.Count = 1 + 3·N·(N+1)`、玩家与敌方部署区均非空且互通(连通性由现有 `EnsureConnectivity` 保障,本特性不改其行为)

### Requirement 7 — 大地图天气进入战斗(可选启用,新增字段)

**User Story:** 作为玩家,在雪天遭遇战时我希望战场上有更多 Snow cell;在沙暴遭遇时我希望战场更多 Sand,使大地图环境视觉与战斗体验贯通。

> **跨层依赖说明**:大地图 `WeatherType` 当前定义在 `BladeHexFrontend/src/View/Environment/WeatherType.cs`(枚举值 `Clear / Rain / Snow / Sandstorm`),Core 层 `BattleContext` 无法直接引用。本要求需要在 Core 层定义一份镜像或采用字符串契约,具体实现方式在 design 阶段决定(候选:把 `WeatherType` 移到 Core / Core 加 `BattleWeather` 镜像枚举 / `BattleContext.WeatherOverride` 字段类型为 `string`,值取 `"clear" / "rain" / "snow" / "sandstorm"`)。无论哪种方案,本要求语义按下述 4 种 weather 钉死。

#### Acceptance Criteria

1. WHERE `BattleContext.WeatherOverride` 字段非空(本特性新增字段),THE Battle_Map SHALL 在地形分配阶段之后,按以下规则全局改写 Battle_Cell:
   - `Snow` → 把 `Plains / Grassland / Savanna` cell 中的 25% 改写为 `Snow`
   - `Sandstorm` → 把 `Plains / Grassland / Savanna` cell 中的 25% 改写为 `Sand`
   - `Rain` → 不改写地形;但 `Battle_Map.EnvironmentEvent` SHALL 设置为 `"rain"`
   - `Clear` 或 null 或任何未列出值 → 不改写,记录一条 `info` 日志(便于将来扩展 weather 时发现漏 case)
2. WHEN 上述改写发生,THE Battle_Cell SHALL 不改写 `Road` / `Wall` / 水域 cell / `Bridge` cell / `Rampart / Tower / Gate / Staircase` 等据点建筑 cell
3. WHEN 全局天气与本次战斗的环境事件冲突(例如 `EnvironmentOverride = "lava_eruption"` 而 `WeatherOverride = Snow`),THE Battle_Map.EnvironmentEvent SHALL 优先使用 `EnvironmentOverride`,但地形改写仍按 weather 进行
4. THE BattleMapGenerator SHALL 在没有 `WeatherOverride` 字段(向后兼容)或字段为 `Clear` 时保持现有行为不变
5. THE 25% 改写比例 SHALL 用 `BattleContext.Seed` 派生的确定性随机源选 cell,而不是 `GD.Randf`(配合 R8#1)

### Requirement 8 — 性能与确定性

**User Story:** 作为玩家,我从大地图进入战斗时希望加载延迟感知不到由本特性引入的额外开销;作为 sim 测试编写者,我希望相同 Seed + 相同 Sample_Set 永远产出相同 Battle_Map。

#### Acceptance Criteria

1. WHEN `BattleContext.Seed` 设置为非零值,THE BattleMapGenerator.GenerateFromOverworld SHALL 是确定性的:相同 `(Seed, OverworldGrid 状态, BattleContext)` 输入产出 byte-identical 的 `BattleMapData`。

   > **可观测约束**:实现 SHALL 在所有"按集合迭代决定结果"的路径上使用稳定排序的列表(`List<T>` + `OrderBy`)替代直接迭代 `Dictionary` / `HashSet` / `Godot.Collections.Dictionary`,因为这些容器的迭代顺序在跨平台/跨进程时不保证一致。具体覆盖点:
   > - sampleProjections 投影完成后按 `(q ASC, r ASC)` 字典序排序(同 R3#1)
   > - landProjections / waterProjections 在切分时保持 sampleProjections 的排序顺序
   > - `mapData.IterateCoords()` 已按 hex 几何顺序输出,无需额外排序
2. WHEN BattleContext.Size 为 `Stronghold`(N = 14, 631 cells),THE BattleMapGenerator.GenerateFromOverworld SHALL 在测试机器(参考 sim_battle_check.txt 的运行环境)上单次生成耗时 ≤ 50 ms(P95),不开启 Profiler。

   > 注:50 ms 为目标值,当前(2026-05-17)代码中无现存基线数据。实施 design 阶段需先在 `SimulationHarness` 中加入 `GenerateFromOverworld` 的耗时采样,建立基线后再决定是否调整目标值。如果基线已远低于 50 ms 则保留作上限阈值;若基线接近或超过 50 ms,需先做性能优化再合并 R8。
3. THE Overworld_Sampler SHALL 单次调用最多读取 `(footprint.Count + 6 × footprint.Count × samplingRingCount + 7)` 个 Overworld_Tile(footprint 自身 + 外扩 K 圈邻居 + 野外路径 7 个),其中 `samplingRingCount` 由 `POIScaleTable` 提供(R1#2)。
4. WHERE 同一战斗实例需要多次生成(种子重抽 / 连通性失败),THE BattleMapGenerator SHALL 复用 Sample_Set 而不重新调用 Overworld_Sampler

### Requirement 9 — 与 SSOT 一致性约束

**User Story:** 作为长期维护者,我希望本特性引入的任何尺度相关常量都能追溯到 `docs/31` SSOT 或 `POIScaleTable`,杜绝魔法数字漂移。

#### Acceptance Criteria

1. THE BattleMapGenerator SHALL 不在 `GenerateFromOverworld` 路径中硬编码"大地图 hex 数 ↔ 战斗 cell 数"的物理换算(如 `× 7`、`× 12.5`)
2. THE BattleMapGenerator.GenerateFromOverworld SHALL 通过 `POIScaleTable.Get(scale).BattleHexRadius` 获取 `N`,不重复定义半径表
3. THE Overworld_Mapping_Table 与 R5#2 的 base 映射表 SHALL 与 `docs/21-战斗地图生成规则.md §三.1` 文档表保持一致;若两者冲突,以本要求为准并同步更新文档
4. THE Sample_Set 的"Footprint + K 圈"采样口径 SHALL 由 `POIScaleTable.Get(scale).SamplingRingCount` 唯一决定;K 的具体数值见 R1#2 表,本要求不允许其它代码处出现独立的"采样圈数"常量。
5. THE BattleMapGenerator SHALL 不在战斗代码中假设大地图 hex = 250 m 或战斗 cell = 20 m 的物理常量(它们仅用于设计文档的语义说明,代码层不读这两个值)
6. THE Overworld_Sampler 实现 SHALL 作为可独立单元测试的边界存在;具体形态在 design 阶段决定(选项:`BladeHex.Map.OverworldSampler` 独立类 / `BattleMapGenerator` 内部 `static` 方法集合 / `BattleContext` 上的 helper);**无论选择哪种,实现 SHALL 满足:不依赖 `GD.Randi/Randf` 等全局随机源、不持有可变状态、可在不构造完整 `BattleMapGenerator` 实例的前提下被测试代码直接调用**。

### Requirement 10 — 战斗端地形枚举对齐大地图(前置条件)

**User Story:** 作为本特性的实施者,我希望战斗端 `BattleCellData.TerrainType` 的自然地形枚举与大地图 `HexOverworldTile.TerrainType` 完全 1:1 对齐,这样 R5 的映射表能落地为简洁的"同名直映",未来新增大地图地形时也能立刻在 switch 表达式中暴露漏改。

> **现状核对(2026-05-17)**:`BattleCellData.TerrainType` 当前 21 个枚举值:
> - **13 个自然地形**:Plains / Grassland / Savanna / Forest / DenseForest / Hills / Mountain / ShallowWater / DeepWater / Swamp / Road / Sand / Snow
> - **4 个特殊物**:Wall / Ruins / PoisonMushroom / LuckyGrass
> - **4 个据点建筑**:Rampart / Tower / Gate / Staircase
>
> 其中 12 个自然地形(除 Road)对应大地图 21 项中的同名 12 项。`HexOverworldTile.TerrainType` 21 项里**多出**的 9 项需要在战斗端补齐:Jungle / Taiga / Bog / Wasteland / Rocky / MountainSnow / Ice / River + R11 派生的 Bridge。

> 本要求是 R5#2 与 R11 的实现前置:R5#2 表中要求出现的"丛林 / 针叶林 / 冻土沼泽 / 荒原 / 岩石荒地 / 雪山 / 冰原 / 河流"等 8 个战斗端目前不存在的枚举值,加上 R11 的 `Bridge`,共 **9 个新增枚举**,必须先由本要求补齐。

#### Acceptance Criteria

1. THE `BattleCellData.TerrainType` 枚举 SHALL 在现有 21 个枚举值的基础上,**新增** 以下 9 个枚举值:
   - `Jungle`(丛林 — 炎热湿润,语义同密林但带"潮湿叙事")
   - `Taiga`(针叶林 — 寒带林地)
   - `Bog`(冻土沼泽 — 寒带沼泽)
   - `Wasteland`(荒原 — 温带极干贫瘠地)
   - `Rocky`(岩石荒地 — 寒带极干硬地)
   - `MountainSnow`(雪山 — 不可通行,叙事上区别于普通山地)
   - `Ice`(冰原 — 低摩擦/滑动玩法预留)
   - `River`(河流 — 区别于浅水/深水,带"流动方向"叙事预留)
   - `Bridge`(桥 — 由 R11 从 sample 派生,跨水道路面)

   新增后 `BattleCellData.TerrainType` 枚举值总数 = **30**(13 自然 + 4 特殊物 + 4 据点建筑 + 9 新增)。

2. WHERE 新增枚举值的玩法属性(`moveCost / acBonus / coverLevel / blocksLineOfSight / isPassable / elevation / specialEffect`)在 v1 实现中尚无独立差异化设计,THE `BattleCellMetadata`(实际位于 `BattleCellData.GetTerrainProperties` 工厂方法内)SHALL 让该新枚举继承"语义最相近的既有枚举"的全部属性作为初始值,具体继承表如下:

   | 新枚举 | 属性继承自 | 备注 |
   |---|---|---|
   | Jungle | DenseForest | 视觉资源可不同,玩法属性同密林 |
   | Taiga | Forest | 同森林 |
   | Bog | Swamp | 同沼泽 |
   | Wasteland | Sand | 与沙地同等贫瘠/通行 |
   | Rocky | Hills | 与丘陵同等"硬地+起伏" |
   | MountainSnow | Mountain | 不可通行,加 `specialEffect = "snow"` |
   | Ice | Snow | 减速效果先和 Snow 一致;v2 再独立 |
   | River | ShallowWater | 浅水属性 + `isRiver = true` |
   | Bridge | Road | 但 `isPassable=true`、`moveCost=1`、`elevation=1`、`specialEffect="bridge"`、`isRiver=false` |

3. THE 战斗场景中所有读取 `BattleCellData.TerrainType` 的 switch 表达式 SHALL 显式覆盖 9 个新枚举,不允许使用 `_ => default` 兜底吞掉;包括但不限于:
   - `BattleMapGenerator.MapOverworldToBattle`(由 R5#2 改为同名直映)
   - `BattleCellData.GetTerrainProperties`(在工厂方法中加 case)
   - 战斗端地形 sprite / texture 加载(可暂时复用既有贴图,但需显式 case)
   - AI 决策相关的地形权重表(若引用 terrainType)
   - UI tooltip / 地形说明文本

4. WHEN 一个新枚举与既有枚举共享 sprite 资源时,THE 资源加载器 SHALL 通过显式 case 语句返回相同贴图路径,而**不**通过 fallback 实现共享;以保证未来替换贴图时不会因漏改 case 而出现"用错贴图"的静默 bug。

5. THE 序列化与存档(`BattleMapData.Cells` / `BattleCellData` 的 godot 资源序列化)SHALL 在新增枚举值后保持向后兼容:旧存档中不存在的新枚举不影响读旧存档;读到不识别的枚举值时按 `Plains` 兜底并输出 warning。

6. THE `OverworldTerrain.GetBattleTemplateName` 与 `BattleMapGenerator.RegisterTemplates` 的模板地形权重表 SHALL 在 v1 内**不必**为 9 个新枚举单独添加权重项;模板侧仍然使用既有 13 项地形,新枚举只在"从大地图采样生成"路径(`GenerateFromOverworld`)中作为输出出现。

### Requirement 11 — 桥(从大地图采样派生,不再是战斗端独立判定)

**User Story:** 作为玩家,我希望"桥"出现在战场的逻辑跟"道路 / 河流 / 浅水"是同一套——大地图上某个 tile 既是道路又是水域(`IsBridge = IsRoad ∧ Terrain ∈ {River, ShallowWater, DeepWater}`),sample 到这种 tile,战斗地图就在投影方向上自然出现一座桥连同对应水带。作为生成器,我**不需要**自己扫水带、找 hex line、判分割——这些都已在大地图层完成。

> **大地图侧既有事实**(代码中已实现,本要求不重新设计):
> `HexOverworldTile.IsBridge` 是派生属性,定义为:
> ```csharp
> public bool IsBridge => IsRoad && (Terrain == TerrainType.River
>     || Terrain == TerrainType.ShallowWater
>     || Terrain == TerrainType.DeepWater);
> ```
> 桥的存在等价于"道路被叠加在水 tile 上",不是单独的 `BridgeTile` 类型。

> **设计立场**:本要求把 R11 v1 的"扫水带 + 自造桥 hex line"算法**作废**,改为"sample 派生"。这跟 R3#5(IsRoad → Road)、R4#6(IsRiver → ShallowWater 河带)是同一思路的延伸:大地图属性透传到战斗端,加权叠加,不在战斗端做新决策。

> **新增枚举依赖**:本要求引入的 `Bridge` 枚举在 R10#1 已统一列出(R10 共 9 个新枚举),R10 的继承表里已包含 `Bridge → 属性继承自 Road,但 isPassable=true / moveCost=1 / elevation=1 / specialEffect="bridge"`,本要求不重复定义。

#### Acceptance Criteria

1. THE Overworld_Sampler SHALL 把 sample 集合内每个 tile 的 `IsBridge` 派生值如实保留(不重新计算),供后续投影阶段读取。

2. WHEN 投影后的 Sample_Tile 满足 `IsBridge = true`,THE Battle_Map SHALL 把该 tile 投影点对应的 Battle_Cell 改写为 `Bridge` 地形,同时按 R4 的 splash 规则在该 cell 周围继续生成水带,以保证桥的两端确实是水/河、而非凭空浮在陆地上。

3. THE 桥的"长度 / 宽度"SHALL 由 sample tile 的 `Terrain` 字段决定(因为大地图侧"桥的等级"等价于"被覆盖的水域类型"),具体对照(桥最短 2 cell):

   | Sample Tile.Terrain | 战斗端桥规模(cell 数) | splashRadius(配套水带) |
   |---|---|---|
   | River(细河流) | **2 cell**,沿河方向延展 | 1(细河带,与桥两端相接) |
   | ShallowWater(湖/浅水) | **3 cell** hex line | 2(中等水域) |
   | DeepWater(海/大型水体) | **4 cell** hex line | 2(深水带不扩散过远) |

   > 设计依据:"桥 2 cell 起"是为了让桥在战斗 grid 上有视觉与战术存在感(1 cell 桥几乎等同于普通 cell,无法作为关键节点);跨越深水的桥更长,符合"水越宽桥越长"的物理直觉。

4. THE 桥的"延展方向"SHALL 由"sample tile 的相邻 Road sample 投影点"决定:取与该桥 sample 在大地图上相邻的、`IsRoad = true` 的 sample 之投影点,沿其方向放置桥的额外 cell;若两侧都有相邻 Road sample,桥贯穿水带连接两端,长度按 R11#3 截取。

5. WHEN 没有任何相邻 Road sample(极端:孤立的"水中道路 tile",大地图层异常),THE Battle_Map SHALL 仍然在该投影点放置 1 cell 桥,但日志记录 warning(`"[BridgePlacer] 桥 sample 无相邻 Road sample,降级为 1 cell"`)。

6. THE Bridge cell SHALL 具备如下玩法属性(由 R10 的 `BattleCellMetadata` 工厂方法注入):
   - `isPassable = true`,`moveCost = 1`,`elevation = 1`(桥面比水高一档)
   - `isRiver = false`(虽然位于水带上,但桥本身不算水)
   - `coverLevel = 0`,`blocksLineOfSight = false`
   - `specialEffect = "bridge"`,用于"桥可被攻城破坏"等扩展玩法

7. THE Bridge cell 与 R4 水带分配 SHALL 协调:桥所占的 Battle_Cell 不进入水域硬封顶(R4#3 的 12% / R4#4 的 30%)的计数;但桥两端的 splash 水 cell 计入。

8. THE Bridge cell SHALL **不**与 R3#5 的"道路覆盖"规则冲突:如果同一个 Battle_Cell 同时是"陆地 sample 投影点(IsRoad = true)"与"桥 sample 投影点",取桥优先(IsBridge 隐含 IsRoad)。

9. THE 桥放置 SHALL 是确定性的:相同的 (sample 集合, sampleProjections) 输入产出 byte-identical 的桥 cell 列表与配套水带(配合 R8#1)。

10. THE Battle_Map 部署区(`PlayerDeployment` / `EnemyDeployment`)SHALL **不**包含 Bridge cell — 桥是中立通道,不允许任何一方在桥上开局(与 R13#12 一致)。

11. WHERE 桥的延展超出 Battle_Map 边界(`!ContainsCoord`),THE 超出部分 SHALL 静默截断;桥在地图内的部分仍然可用,允许"桥的另一端在战场外"这种叙事场景(玩家可向桥外推进但无更多 cell)。

12. THE Battle_Map SHALL 不在战斗端层做"水带连通分量分析 / 自造桥 hex line"等独立判定;桥的所有放置决策必须可追溯到 sample 集合中至少一个 `IsBridge = true` 的 tile。换句话说,**没有大地图桥 sample,战斗端不会出现 Bridge cell**。


### Requirement 12 — POI 类型只决定"结构层",不影响地形

**User Story:** 作为玩家,我希望"我打的是城堡"在战场上能看到城墙、塔楼、城门——这些**结构**是 POI 类型独有的辨识度;但战场的**地形**(草地/森林/丘陵/水)依然完全反映大地图采样到的 tile,不会因为"是 Castle 战"就强制把战场变成 Hills。作为生成器,我把 POI 类型的语义限定在"放置非地形结构"层面,不再对 Battle_Cell 的 terrainType 做任何偏置。

> **设计立场(关键)**:本要求**作废** R12 v1 的"POI 影响地形偏置"思路。新约束:
> - 战场地形 100% 来自 R1 采样 + R3 Voronoi + R5 映射,**任何 POI 影响都不允许动 terrainType**
> - POI 类型只决定"非地形结构"的种类与数量:`Wall / Rampart / Tower / Gate / Staircase / Ruins / 装饰物`
> - 桥(R11)是"道路 + 水"的派生,与 POI 类型**无关**
> - **任何在 sample 集合内被采样到的 POI**(中心或邻居)都在战斗地图的对应方向上生成结构;邻居 POI 的结构规模缩小("只参与一小部分"),按 R12#5 的衰减规则计算

> **范围限制(v1)**:本要求处理 sample 集合内的所有 POI,中心 POI 占主导(完整结构),邻居 POI 占少量结构(按距离衰减)。野外遭遇(`DefendingPOI = null` 且 sample 内无 POI)不生成结构,纯地形战。

#### Acceptance Criteria

1. THE BattleMapGenerator SHALL 在 sample/Voronoi/水域/桥/道路阶段**之后**单独执行 "POI 结构放置" 阶段;此阶段只能改写 cell 的 terrainType 为 R10 列出的"据点建筑"类型(`Wall / Rampart / Tower / Gate / Staircase`)与"特殊物"类型(`Ruins`),**不允许**改写到任何"自然地形"类型(Plains / Grassland / Forest / Hills / Mountain / 水域 / Snow ...等)。

2. THE POI 结构放置 SHALL 同时考虑 sample 集合内**所有** POI(中心 POI + 邻居 POI),按 #5 的衰减规则分配各自的结构上限;野外遭遇且 sample 集合内无任何 POI 时,**不**生成任何结构。

3. THE 11 种 POIType 主类型的"结构偏置规则"(`POIStructureRule`)SHALL 在 design 阶段全部 1:1 落地,初步形态如下(数值 design 阶段细化):

   | POIType | 主结构 | 数量上限 | 备注 |
   |---|---|---|---|
   | Town | Ruins(民居)簇 | 4~8 | 街道感由 R3#5 道路提供,无 Wall |
   | Village | Ruins(木屋)簇 + 0~1 Wall | 2~4 + 0~2 | 小聚落,可有简易栅栏 |
   | Castle | Rampart + Tower + Gate + Staircase | 完整城墙环 | 攻防战核心 |
   | Settlement | 由 SettlementRace 子类型决定 | — | 详见 #4 |
   | Lair | 由 LairType 子类型决定 | — | 详见 #5 |
   | Tavern | 1 Ruins(单建筑) | 1 | 路边旅店 |
   | Outpost | 1~2 Tower + 简易 Wall | 1~2 + 1~3 | 前哨站 |
   | Mine | Ruins(矿洞口)+ 0~2 Wall(矿车轨道) | 1 + 0~2 | 资源点 |
   | Farm | Ruins(木屋)+ 简易 Wall(畜栏) | 1~2 + 1~3 | 农庄 |
   | Shrine | 1 Ruins(神龛) | 1 | 神圣点 |
   | Port | Ruins(码头建筑)+ Bridge 特殊放置(见 #7) | 2~3 + 0~1 | 海港 |

4. WHERE `PoiTypeEnum = Settlement`,THE 结构放置 SHALL 由 `Settlement.Race` 决定具体形态:

   | SettlementRace | 主结构 | 备注 |
   |---|---|---|
   | Goblin | Ruins(部落帐篷)簇,无 Wall | 哥布林营地 |
   | Kobold | Ruins(矿洞口)+ Wall + Tower | 狗头人矿坑 |
   | Minotaur | Wall(石堡)+ Tower + Gate | 牛头人石堡 |
   | ShadowCult | Ruins(祭坛)+ 部分 Wall | 暗影教团 |
   | Bandit | Ruins(木栅栏)+ 简易 Wall | 山贼营地 |
   | Robber | Ruins(简陋窝点)| 劫匪窝点 |
   | Pirate | Ruins(码头) + Bridge 特殊放置 | 海寇巢穴 |

5. WHERE `PoiTypeEnum = Lair`,THE 结构放置 SHALL 由 `Lair.LairType` 决定:

   | LairType | 主结构 | 备注 |
   |---|---|---|
   | DragonLair | Wall(龙巢洞口)+ Ruins | 龙巢 |
   | AncientTomb | Wall(墓室)+ Ruins(石棺) | 古代墓穴 |
   | Ruins | Ruins + 部分 Wall | 远古遗迹 |
   | GolemForge | Wall(铸造厂)+ Ruins | 魔像工坊 |
   | BanditCamp | Ruins(简陋营地) | 山贼窝点(规模比 Settlement 小) |
   | RobberHideout | Ruins(地下窝点入口) | 劫匪窝点 |
   | PirateCove | Ruins(码头) | 海寇洞穴 |
   | RaiderOutpost | 简易 Wall + 1 Tower | 劫掠队据点 |

6. THE POI 结构放置算法 SHALL **不**改写以下 cell:
   - 任何水域 cell(`ShallowWater / DeepWater / River`)— 结构在水中无意义
   - `Bridge` cell — 桥是中立通道
   - `Road` cell — 道路保持连通性
   - 已被前一阶段(R3 / R4 / R11)改写过的 cell 不再被结构放置覆盖,除非该 cell 当前是自然陆地地形且符合放置规则的几何约束(例如 Wall 必须放在围绕 POI 中心的环上)

7. THE Port POI 与 Pirate Settlement 与 PirateCove Lair SHALL **不**单独触发"额外的桥放置"——R11 是"sample 派生桥",由大地图侧 `IsBridge = IsRoad ∧ Terrain ∈ 水` 唯一决定。如果大地图层 Port 周围本来就没有桥 sample,战斗端不会因为"它是 Port"而强行造桥。

8. THE 11 种 POIType + 7 种 SettlementRace + 8 种 LairType **全部** 在 `POIStructureRule` 表中显式列出;未匹配的枚举值 SHALL 输出错误日志(`GD.PushError("[POIStructure] 未识别的 PoiType/Race/LairType: {value}")`),fallback 行为是"不放任何结构"。

9. WHERE 结构放置因为地图过小、地形被水/桥占满等原因无法满足"数量上限"约束,THE 实现 SHALL 优先放最少必要数量(主结构 1 个),其余跳过并输出 warning;不允许因放置失败抛异常。

10. THE 邻居 sample 中的 POI(非中心 POI)在 v1 内 SHALL 也生成结构,但**只参与一小部分**:
    - **结构上限**:邻居 POI 实际放置的结构数量 = `ceil(中心 POI 上限 × structureScale)`,其中 `structureScale = 0.25`(默认)
    - **空间约束**:邻居 POI 的结构只允许放在该 POI 投影点的"局部簇"——以投影点为圆心、axial 半径 ≤ 2 的 hex 区域;不允许蔓延到战场其它方向
    - **类型约束**:邻居 POI 只生成主结构种类(参考 R12#3 / #4 / #5 表的"主结构"),不生成可选/装饰结构
    - **冲突约束**:邻居 POI 投影簇与中心 POI 结构区重叠时,中心 POI 优先;邻居 POI 跳过该 cell

11. WHERE 多个邻居 POI 的投影簇互相重叠,THE 实现 SHALL 按"距 sampleCenterAxial 越近、structureScale 越大"的反距离权重分配 cell,具体公式在 design 阶段细化;但邻居 POI 总结构占比 SHALL 不超过中心 POI 总结构占比的 50%(防止"邻居反客为主")。

12. THE 结构放置阶段 SHALL 在 R8#1 的确定性约束下运行:相同 (sample 集合, sampleProjections, terrainMap) 输入产出 byte-identical 的结构 cell 列表。

13. THE BattleMapGenerator SHALL 在 `BattleMapData.TemplateName` 字段上记录中心 POI + 邻居 POI 摘要,格式为 `"poi_<type>[_<subtype>]_with_<neighbor_summary>"`,例如 `"poi_castle_with_village+farm"`;若没有邻居 POI 则省略 `_with_` 部分(`"poi_castle"`);野外遭遇为 `"wild_<terrain>"`,例如 `"wild_forest"`。该字段仅供日志/sim 调试,不影响游戏逻辑。

14. THE BattleMapGenerator SHALL 在 sample 集合内**只**考虑大地图侧已知的 POI(`HexOverworldTile.OccupyingPoiName != null` 且对应 POI 资源能在 `OverworldGrid.GetPoi(name)` 中找到);引用失败的 POI 名 SHALL 跳过并输出 warning。


### Requirement 13 — 接战方向决定开局部署位置

**User Story:** 作为玩家,当我从大地图东侧逼近一个 POI 时,战斗开局应该是"我在战场东侧、敌人在战场西侧",而不是固定的"玩家左下、敌人右上"。作为生成器,我需要把"接战方向"从大地图层转译成战斗六边形 grid 上的部署区方位。

> **当前实现状况(2026-05-17)**:
> - `BattleContext.ApproachDirection` **字段已存在**,类型是 `Vector2I?`(原始 axial 偏移向量,而非枚举);`CreateFromEncounter` 工厂方法已经在 POI 战斗时自动计算 `POI.CenterHex - PlayerCoord`。
> - `DeploymentZone.GenerateZones` **当前不读 ApproachDirection**,只读 `EngagementType`(Normal / Ambush / Ambushed),固定按 q 轴左右两端分配。
>
> 本要求是**对部署区生成的扩展**,不需要重新加字段(避免重名/类型变动),只需要让 `DeploymentZone.GenerateZones` 读这个已有字段。

#### Acceptance Criteria

1. THE BattleContext SHALL **复用**已存在的 `ApproachDirection` 字段(类型 `Vector2I?`)作为"敌方相对于玩家的接近方向";本要求不引入新枚举类型,Vector2I 的语义即"axial 偏移",DeploymentZone 在使用时按需归一化到 6 邻接方向之一。

   > **野外遭遇 ApproachDirection 派生(本要求新增)**:`CreateFromEncounter` 当前只在 `poi != null` 时计算 ApproachDirection,本要求扩展到野外:取 `attacker.GridPos - defender.GridPos`(以防御方为玩家视角);若双方坐标相等或都为 null,fallback 到 `null`。

1b. THE BattleContext SHALL 新增字段 `AttackingSide`(类型 `BattleSide` 枚举,值为 `Player` / `Enemy`),表示"主动发起战斗的一方";默认 `AttackingSide = Player`(玩家点击 POI 攻击)。当 POI 主动派兵攻击玩家(防御战)时设为 `AttackingSide = Enemy`。该字段在 R11a 的"城堡战必须有一方在城堡内"约束中决定哪一方部署在城堡内。

2. THE DeploymentZone.GenerateZones SHALL 把 `ApproachDirection` 解释为"**敌人来的方向**":
   - 实现先把 `ApproachDirection` 归一化到 6 个 axial 单位方向之一(取与 `ApproachDirection` 夹角最小的 hex 方向)
   - 敌方部署区位于该方向一侧(战场中心向该方向延伸的扇形 / 半边)
   - 玩家部署区位于**相反**方向
   - 中间留出 1 ~ 2 行 hex 作为"中间区",不属任一方

3. WHEN `ApproachDirection = null` 或 `EngagementType = Normal` 且 `ApproachDirection = null`,THE DeploymentZone.GenerateZones SHALL 维持现有"q 轴左右两端"行为,以保证 QuickCombat / 测试场景向后兼容。

4. THE EncounterSpawner / 战斗触发逻辑 SHALL 在创建 `BattleContext` 时**自动**计算 `ApproachDirection`(本要求扩展现有 `CreateFromEncounter` 的派生逻辑):
   - POI 战(已实现):`POI.CenterHex - PlayerCoord`
   - 野外遭遇(本要求新增):`attacker.GridPos - defender.GridPos`,以防御方为参考点
   - 若双方坐标相等或派生失败,fallback 到 `null`(让 GenerateZones 走 Normal)

5. WHEN `EngagementType = Ambush`(玩家伏击),THE DeploymentZone.GenerateZones SHALL 在 `ApproachDirection` 基础上**额外**做:
   - 玩家部署区分散到地图边缘的 3 个角(围 360°),而不是单一方向
   - 敌方部署区集中在地图中央
   - 即使 `ApproachDirection != null`,Ambush 语义优先于方向感知

6. WHEN `EngagementType = Ambushed`(玩家被伏击),THE DeploymentZone.GenerateZones SHALL 与 R13#5 对称:
   - 玩家部署区集中在地图中央
   - 敌方部署区分散到 3 个角(围 360°)
   - 中间区不留(玩家被三面包夹)

7. THE 部署区生成 SHALL 满足以下硬约束:
   - 玩家部署区 ≥ `ceil(playerUnitCount × 1.5)` 个可通行 cell(留余地让玩家选位置)
   - 敌方部署区 ≥ `ceil(enemyUnitCount × 1.5)` 个可通行 cell
   - 玩家与敌方部署区不重叠
   - 两区之间 axial 距离 ≥ 3 cell(避免开局直接近战)

8. WHERE 战场尺寸过小或地形过差(R13#7 的硬约束无法满足),THE DeploymentZone.GenerateZones SHALL 回退到现有"q 轴左右两端"逻辑并记录 warning,而**不**因约束失败抛异常。

9. THE EnsureConnectivity SHALL 在部署区分配后再运行,确保两区之间存在至少一条可通行路径(陆地或桥);如果方向感知导致路径被水域阻断,EnsureConnectivity 凿陆地通道补救。

10. THE 部署区生成 SHALL 对相同输入产出 byte-identical 输出(配合 R8#1);具体来说,扇形选择、cell 排序、备选 cell 列表都按 axial 字典序遍历。

11. WHERE POI 的 `IsPoiCenter` cell 位于战场中央(典型城堡/城镇战),THE 部署区生成 SHALL 让**敌方**(防御方)部署在 POI 中心周围(半径 ≤ N/3 的环),玩家(进攻方)部署在外围(半径 ≥ 2N/3 的环);此时 `ApproachDirection` 用作"玩家进攻起点的方位",而不是单纯的两端对峙。

11a. WHERE 中心 POI 的 `PoiTypeEnum = Castle`(以及任何含完整城墙环的 POI:Castle / 部分 Settlement 类型 / 部分 Lair 类型),THE 部署区生成 SHALL **强制满足**"一方在城堡内"约束,具体规则:
   - **防御方**(`AttackingSide = Player` 时为敌方,`AttackingSide = Enemy` 时为玩家)开局部署区**全部** cell SHALL 位于由 R12 放置的 `Wall / Rampart / Tower / Gate` 围成的封闭区域内部
   - **进攻方**部署区全部 cell SHALL 位于该封闭区域外部
   - 城堡内部空间不足以容纳防御方全部单位时,允许溢出到 `Wall` cell 的内侧相邻 hex(攻打城堡前的"贴墙"姿态),但仍需在 `ApproachDirection` 的反方向,远离进攻方
   - 进攻方部署区 SHALL 距离 `Gate`(城门)有清晰路径,允许从城门方向进攻

11b. WHEN R12 放置城墙环失败(地图过小 / 地形不允许)导致没有"封闭区域"可用,THE 部署区生成 SHALL **降级**为 R13#11 的"中心环 vs 外围环"语义,记录一条 warning 日志(`"[Deployment] Castle 战城墙环不完整,降级为中心环部署"`),但不抛异常。

11c. WHERE 中心 POI 含 `Gate`(城门)cell,THE 进攻方部署区 SHALL 至少包含一条到 Gate 的可通行路径(由 EnsureConnectivity 验证);防御方部署区允许包含 Gate 的内侧 cell。Gate cell 本身不进入任何部署区(中立通道,与 R13#12 一致)。

12. WHEN R12 邻近 POI 影响在战场上引入了"码头/桥/路口"等关键节点,THE 部署区生成 SHALL **避免**让任何一方的部署区压在这些节点上;具体:`Bridge` / `Gate` / `Tower` 这三种 cell 不允许进入任何部署区。
