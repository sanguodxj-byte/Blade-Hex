# Implementation Tasks — combat-hex-from-overworld-state

> 数据版本:2026-05-17
> 来源:`requirements.md`(13 R)+ `design.md`(§8 phase 拆分)
> 实施顺序:Phase 1 → 2 → 3 → 4 → 5 → 6,前面 phase 完成才能开始下一 phase

> **现状核对(2026-05-17)**:在生成 tasks 前已确认:
> - `BattleContext.ApproachDirection` 字段已存在(`Vector2I?`)
> - `DeploymentZone.GenerateZones` 已接受 `approachDirection` 参数,`GenerateNormal` 已实装 `AssignByApproachDirection` 半平面切分
> - **R13#1~#10 已基本落地**;本 spec 还需要做:R13#11(Castle 战墙内 vs 墙外)、R13#11a/b/c(进攻/防御方向)、R13#12(Bridge/Gate/Tower 不进部署区)

> **Task 依赖图(Task Dependency Graph)**
>
> ```
>   T1 ─┐
>   T2 ─┼─► T6 ─► T7 ─► T8 ─► T11 ─► T13 ─► T14
>   T3 ─┤                          │
>   T4 ─┤                  T9 ─────┤
>   T5 ─┘                  T10 ────┤
>                                   │
>                          T12 ─────┤
>                                   │
>                          T15 ─────┴─► T16
> ```
> 横向并行:Phase 1 的 5 个 task(T1-T5)可并行;Phase 2 的 T6/T7 必须串行(T7 用 T6 的输出);Phase 3 的 T8 是核心重写,T9/T10/T12 是子模块可并行,但都要在 T8 之后接入

---

## Phase 1 — 基础对齐(无依赖,可并行)

### T1. `BattleCellData.TerrainType` 增 9 个枚举值

- [ ] **R10#1, #2** 在 `BladeHexCore/src/Data/BattleCellData.cs` 的 `TerrainType` enum 末尾增加 9 个值:`Jungle / Taiga / Bog / Wasteland / Rocky / MountainSnow / Ice / River / Bridge`,索引 21..29
- [ ] 在 `GetTerrainProperties` switch 中增 9 个 case,按 design §2.1 继承表设置初始 `TerrainProperties`(可手写完整 `new TerrainProperties(...)` 而不是 `with`,根据 `TerrainProperties` 实际类型决定)
- [ ] 单测:遍历 `Enum.GetValues<BattleCellData.TerrainType>()` 全 30 项,断言 `GetTerrainProperties(t)` 不命中默认兜底分支(用 sentinel:把兜底分支的 TerrainName 改成 `"__UNHANDLED__"`,断言不出现)

> **验证**:`dotnet build BladeHexFrontend.csproj` 通过 + 新单测 `BattleCellData_GetTerrainProperties_All30Values_HasCase` 通过

### T2. `BattleMapGenerator.MapOverworldToBattle` 改为 21 case 全函数

- [ ] **R5#1, #2, #3** 把 `MapOverworldToBattle` 的 14 case + Plains 兜底改成 21 case 显式映射;`River → River`(不再 → ShallowWater),其他 8 个新值加上(Jungle/Taiga/Bog/Wasteland/Rocky/MountainSnow/Ice)
- [ ] 兜底分支改为 `Fallback(t)`:`GD.PushError("[BattleMapGenerator] 未识别的大地图地形: {t}")` + return Plains
- [ ] 单测:遍历 `Enum.GetValues<HexOverworldTile.TerrainType>()` 全 21 项调用 `MapOverworldToBattle`,断言每一项返回值不是"Plains 兜底"(对真值映射,Plains 输入 → Plains 输出是合法的;通过用 sentinel 测试 fallback 分支不被命中)

> **依赖**:T1 必须先完成(否则 21 case 中 River/Jungle 等枚举不存在编译失败)
> **验证**:单测 `MapOverworldToBattle_All21Values_HasExplicitCase` 通过

### T3. `BattleContext` 增字段 `WeatherOverride` + `AttackingSide`

- [ ] **R7 design §2.2** 在 `BladeHexCore/src/Strategic/BattleContext.cs` 增字段:
  - `public string? WeatherOverride = null;`(取值 `"clear"/"rain"/"snow"/"sandstorm"` 或 null)
  - `public BattleSide AttackingSide = BattleSide.Player;`
- [ ] 在 `BattleContext` 文件内或同名命名空间增 enum:`public enum BattleSide { Player, Enemy }`
- [ ] `CreateFromEncounter` 工厂方法保持向后兼容(新字段都有默认值)

> **验证**:`dotnet build` 通过 + 现有 96 单测仍 96 passed

### T4. `POIScaleProfile` 增 `SamplingRingCount` 字段

- [ ] **R1#2, R9#4** 在 `BladeHexCore/src/Strategic/Scale/POIScale.cs` 的 `POIScaleProfile` struct 增字段 `public int SamplingRingCount { get; init; }`
- [ ] 更新 ctor 签名 + 4 处 `Get(POIScale)` 内的 `new`:Tiny=1, Small=1, Medium=2, Large=3
- [ ] 单测:`POIScaleTable_AllScales_HasSamplingRingCount`,断言 4 档分别返回 1/1/2/3

> **验证**:`dotnet build` 通过 + 新单测通过

### T5. `BattleContext.CreateFromEncounter` 野外路径派生 `ApproachDirection`

- [ ] **R13#4** 当前只在 `poi != null` 时计算;扩展到野外:若 `poi == null` 且 `attacker != null && defender != null`,取 `attacker.GridPos - defender.GridPos`
- [ ] 双方都为 null 或坐标重合 → 保持 `null`(让 GenerateZones 走 q 轴 fallback)
- [ ] 单测:`CreateFromEncounter_WildAttackerVsDefender_PopulatesDirection`

> **验证**:`dotnet build` + 新单测通过

---

## Phase 2 — 采样 / 投影抽离(依赖 T1-T5)

### T6. 抽离 `OverworldSampler` 静态类

- [ ] **R1, R6, R8** 新建 `BladeHexCore/src/Map/Generation/OverworldSampler.cs`,实现 `Sample(BattleContext, HexOverworldGrid?, int samplingRingCount) → SampleSet`
- [ ] 在 `OverworldSampler.cs` 内部定义 `SampleSet` struct(design §2.4)
- [ ] 实现 `ResolveFootprint` / `ExpandRings` 私有方法(design §3.1)
- [ ] 处理 6 条边缘情况:
  - grid == null → return `SampleSet.Empty`(R6#2)
  - DefendingPOI != null && OccupiedHexes 空 → 退化到 EncounterCoord + warning(R6#4)
  - tile 跨 chunk 返回 null → 跳过不抛(R1#5)
  - 收集后 tiles.Count == 0 → return `SampleSet.Empty`(R6#1 上层处理)
  - footprint 自动去重(用 HashSet 实现 ExpandRings)(R1#6)
  - sampleRadius = max axial distance,单 tile 时 = 0(R1#7)
- [ ] 单测覆盖:
  - `Sample_TinyPoi_K1_Returns7Tiles`
  - `Sample_LargePoi_K3_Returns49Tiles`
  - `Sample_NullGrid_ReturnsEmpty`
  - `Sample_OutOfChunkTile_SkipsNull`
  - `Sample_EmptyOccupiedHexes_FallbackEncounterCoord`
  - `Sample_RadiusComputedCorrectly`

> **依赖**:T4 完成(`SamplingRingCount` 字段存在)
> **验证**:6 个新单测全部通过 + 旧 96 单测保持

### T7. 抽离 `BattleProjection` 静态类 + axial 字典序排序

- [ ] **R2, R8#1** 新建 `BladeHexCore/src/Map/Generation/BattleProjection.cs`,实现 `Project(SampleSet, int battleHexRadius) → List<SampleProjection>`
- [ ] 在同文件定义 `SampleProjection` struct(design §2.5)
- [ ] 处理:
  - sampleRadius == 0 → 单 tile 投影到 (0,0)(R2#3)
  - water sample 用 effScale = scale × 0.6(R2#4)
  - 投影点超出 N 仍保留(R2#6)
  - **末尾按 (X, Y) 字典序稳定排序**(R8#1)
- [ ] 单测:
  - `Project_SingleTile_ReturnsOrigin`
  - `Project_WaterSampleUsesReducedScale`
  - `Project_SortedByAxialAsc`
  - `Project_SameInputBitIdentical`(跑 100 次断言列表完全一致)

> **依赖**:T6 完成(SampleSet 类型存在)
> **验证**:4 个新单测通过

---

## Phase 3 — 主流程 / 核心子模块(依赖 Phase 2)

### T8. 重构 `BattleMapGenerator.GenerateFromOverworld` 为 6 stage 流水线

- [ ] **design §3.7** 改写主流程为:
  ```
  Stage 1: var samples = OverworldSampler.Sample(...);
           if (samples.IsEmpty) return GenerateFromTemplateInternal(context);
  Stage 2: var projections = BattleProjection.Project(samples, N);
  Stage 3: terrainMap = AssignTerrain(projections, ...) (含 Voronoi / 水域 / 道路 / 桥 / 天气)
  Stage 4: StructurePlacer.Place(...)  ← T15 实现
  Stage 5: DeploymentZone.GenerateZones(...) (已存在)
  Stage 6: WriteCellsFromTerrainMap + EnsureConnectivity
  ```
- [ ] 把现有 `GenerateFromOverworld` 中的 Voronoi 逻辑抽到私有方法 `AssignLandVoronoi`
- [ ] 把现有水域逻辑抽到 `PlaceWater`(同文件,后续 T9 进一步抽离)
- [ ] 现有 `samplerCenterAxial` / `sampleRadius` 之类的内联变量删除,改用 `samples` 三元组返回值
- [ ] 集成测试:`GenerateFromOverworld_StillProducesValidMap`(任意 POI 战斗参数,断言 cells 数 = `1 + 3·N·(N+1)`、PlayerDeployment / EnemyDeployment 都非空)

> **依赖**:T2、T6、T7 完成
> **验证**:集成测试通过 + Stronghold(N=14) 不报错

### T9. 引入 `SeededRng` 替换 `GD.Randf` / `GD.Randi`

- [ ] **R8#1, design §4** 新建 `BladeHexCore/src/Map/Generation/SeededRng.cs`,实现 xorshift32:
  ```csharp
  internal sealed class SeededRng
  {
      private uint _state;
      public SeededRng(int seed) { _state = (uint)(seed == 0 ? 1 : seed); }
      public float NextFloat() { /* xorshift32 → uint → /uint.MaxValue */ }
      public int NextRange(int min, int maxExclusive) { /* */ }
  }
  ```
- [ ] 在 `GenerateFromOverworld` 主流程顶端实例化 `var rng = new SeededRng(context.Seed);`
- [ ] 替换 `ScatterSpecialFeatures` / `Voronoi 的 noise` / 其他随机调用点使用 `rng`
- [ ] 单测:`SeededRng_SameSeed_SameSequence`、`SeededRng_DifferentSeed_DifferentSequence`

> **依赖**:T8 主流程已重构
> **验证**:R8#1 byte-identical 测试 — 跑两遍同 seed,`mapData.Cells` 内容完全相同

### T10. `BridgePlacer` 实现 R11(桥从 sample 派生)

- [ ] **R11** 新建 `BladeHexCore/src/Map/Generation/BridgePlacer.cs`,实现 `Place(projections, terrainMap, mapData)`
- [ ] 处理 IsBridge 投影:
  - River → 2 cell + splash 半径 1
  - ShallowWater → 3 cell + splash 半径 2
  - DeepWater → 4 cell + splash 半径 2
- [ ] 方向解析 `ResolveBridgeDirection`:取邻近 IsRoad sample 投影点方向;若无 → 1 cell + warning(R11#5)
- [ ] 桥 cell 不进 R4 水域硬封顶(R11#7)
- [ ] 桥与 Road 重叠时优先桥(R11#8)
- [ ] 桥延展超出地图静默截断(R11#11)
- [ ] 单测:
  - `BridgePlacer_RiverBridge_Places2Cells`
  - `BridgePlacer_ShallowWaterBridge_Places3Cells`
  - `BridgePlacer_NoRoadNeighbor_Downgrades1Cell_Warns`
  - `BridgePlacer_OutOfMapBounds_Truncates`

> **依赖**:T1(Bridge 枚举存在)、T8(主流程接入点)
> **验证**:4 个新单测通过 + 集成测试 `GenerateFromOverworld_PortWithBridgeSample_HasBridgeCells`

### T11. `WeatherOverlay` 实现 R7

- [ ] **R7** 新建 `BladeHexCore/src/Map/Generation/WeatherOverlay.cs` 或在 `BattleMapGenerator` 内部加 `ApplyWeatherOverride(string weather, terrainMap, rng)` 方法
- [ ] 4 种 weather 处理:
  - `"snow"` → Plains/Grassland/Savanna 25% → Snow
  - `"sandstorm"` → 25% → Sand
  - `"rain"` → 不改地形,设 mapData.EnvironmentEvent = "rain"
  - `"clear"` 或其他 → 不动 + info 日志
- [ ] 不改写 Road / Wall / 水域 / Bridge / 据点建筑 cell(R7#2)
- [ ] EnvironmentOverride 优先于 weather override(R7#3)
- [ ] 用 `SeededRng` 选 cell(R7#5)
- [ ] 单测:
  - `WeatherOverlay_Snow_25PercentPlainsBecomeSnow`
  - `WeatherOverlay_Snow_DoesNotChangeWaterRoadStructure`
  - `WeatherOverlay_Rain_SetsEnvironmentEventOnly`
  - `WeatherOverlay_EnvironmentOverridePriority`

> **依赖**:T8(主流程)、T9(SeededRng)
> **验证**:4 个新单测通过

### T12. WaterPlacer 抽离(可选,R4 现有逻辑迁移)

- [ ] **R4** 把 `GenerateFromOverworld` 内的水域 splash + 河带连线逻辑抽到独立 `WaterPlacer.Place(...)` 静态方法
- [ ] 显式硬封顶 12% / 30%(R4#3, R4#4)
- [ ] 不在 Road / Wall / Bridge cell 上放水(R4#5, R11#7)
- [ ] 单测:
  - `WaterPlacer_HardCap12Percent`
  - `WaterPlacer_AllWaterFootprint_30PercentCap`
  - `WaterPlacer_TwoRiverSamples_Connected`(R4#6)

> **依赖**:T8 主流程已重构
> **验证**:3 个单测通过

---

## Phase 4 — POI 结构层(R12 最大块,可单独 PR)

### T13. `POIStructureRule` 数据表

- [ ] **R12#3, #4, #5** 新建 `BladeHexCore/src/Map/Generation/POIStructureRule.cs`
- [ ] 定义 `readonly struct POIStructureRule`:`MainStructure` / `MinCount` / `MaxCount` / `HasWallRing` / `HasGate` / `HasTower` / `AdditionalStructures`
- [ ] 实现 `POIStructureRuleTable.Resolve(OverworldPOI poi)`:
  - 11 种 `OverworldPOI.POIType` 全部 case(R12#3 表)
  - `Settlement` 分支按 `SettlementRaceValue` 7 个值(R12#4 表)
  - `Lair` 分支按 `LairTypeValue` 8 个值(R12#5 表)
  - 未匹配 PushError + 返回 `EmptyRule`(R12#8)
- [ ] 单测:遍历所有枚举组合断言不命中 fallback

> **依赖**:无(纯查表,可与 Phase 1-3 并行)
> **验证**:单测 `POIStructureRuleTable_AllPoiTypeRaceLairType_HasCase` 通过

### T14. `StructurePlacer` 中心 POI + 邻居 POI 放置

- [ ] **R12#1, #2, #6, #10, #11** 新建 `BladeHexCore/src/Map/Generation/StructurePlacer.cs`
- [ ] 实现 `Place(projections, terrainMap, mapData, context, rng)`:
  - 找出 sample 集合中所有 POI(`OccupyingPoiName != null` 且能从 OverworldGrid 取到 POI 资源)(R12#14)
  - 中心 POI(`DefendingPOI`)按 rule.MaxCount 全量放
  - 邻居 POI 按 `ceil(MaxCount × 0.25)` 缩量,axial 半径 ≤ 2 的局部簇(R12#10)
  - 不改写水域 / Bridge / Road / 已被改写为非自然地形的 cell(R12#6)
  - 多个邻居重叠时按反距离权重(R12#11)
  - mapData.TemplateName 设为 `"poi_<type>[_<subtype>]_with_<neighbor_summary>"`(R12#13)
- [ ] 实现 `PlaceWallRing`(R12#3 Castle + R12#4 Minotaur Settlement + R12#5 DragonLair):
  - 取 POI 中心(`DefendingPOI.CenterHex` 投影到战场中心)
  - 在距中心 axial = `N / 3` 的环上放 Rampart cell
  - 留至少 1 个 Gate cell(在朝向 ApproachDirection 的方向)
  - 角落放 Tower cell(每隔 2~3 cell 一个)
  - 内部至少 1 个 Staircase cell
- [ ] 单测:
  - `StructurePlacer_Castle_PlacesFullWallRing`
  - `StructurePlacer_Town_PlacesRuinsClusters`
  - `StructurePlacer_NeighborPoi_LimitedToLocalCluster`
  - `StructurePlacer_DoesNotOverwriteWaterBridgeRoad`

> **依赖**:T8、T13
> **验证**:4 个新单测通过 + 集成测试 `GenerateFromOverworld_Castle_HasFullWallRing`

---

## Phase 5 — 部署区 Castle 战 + 节点保护(R13 残留)

### T15. `DeploymentZone` Castle 战:墙内 vs 墙外

- [ ] **R13#11, #11a, #11b, #11c** 在 `DeploymentZone.cs` 新增 `GenerateCastleZones`:
  - 入口检查:`HasFullWallRing(mapData)` — 用 BFS 从地图边缘开始,看 Wall/Rampart 是否完全封闭一片陆地区域
  - 找到墙内陆地区域(`closedArea: HashSet<Vector2I>`)
  - **防御方**(`AttackingSide=Player → 敌方,Enemy → 玩家`)部署在 closedArea 内
  - **进攻方**部署在 closedArea 外,且距 Gate 有可通行路径(R13#11c)
  - 内部空间不足时溢出到 Wall 内侧相邻 hex(R13#11a 收尾)
- [ ] **R13#11b** 若 wall ring 不完整,降级到现有 `GenerateNormal` + 中心环 vs 外围环逻辑(R13#11),warning 日志
- [ ] **R13#12** 在 `GenerateNormal` / `GenerateCastleZones` 收尾过滤:Bridge / Gate / Tower cell 从 player + enemy 部署区中移除
- [ ] 单测:
  - `Castle_DefenderInsideWalls_AttackerOutside`
  - `Castle_AttackingSideEnemy_PlayerInsideWalls`
  - `Castle_NoFullWallRing_FallsBackToCenterRing_Warns`
  - `Deployment_BridgeGateTowerExcluded`

> **依赖**:T14(WallRing 已放置)
> **验证**:4 个新单测通过

### T16. `DeploymentZone.GenerateNormal` 节点保护(已实装但需补 R13#12)

- [ ] **R13#12** 在 `GenerateNormal` / `AssignByApproachDirection` 末尾过滤掉 Bridge/Gate/Tower cell
- [ ] 单测:`Normal_BridgeNotInDeploymentZone`

> **依赖**:T1(Bridge 枚举存在)
> **验证**:1 个新单测通过

---

## Phase 6 — 性能基线 + 文档同步

### T17. `SimulationHarness.battle_scale` 性能基线

- [ ] **R8#2** 在 `BladeHexCore/tests/Simulation/SimulationHarness.cs` 增 `RunBattleScaleBatch`:
  - 100 次循环,每次构造 `BattleContext{ Size=Stronghold, OverworldGrid=真实 grid }`
  - 用 `Stopwatch` 分段统计 Stage 1-6 耗时
  - 输出 P50 / P95 / P99
- [ ] 在 `tools/scripts/sim.ps1` 加 `-Scenario battle_scale` 支持
- [ ] 跑一次 baseline 记录到 `combat-numerics-audit.md` Wave 4 章节
- [ ] **断言**:Stronghold P95 ≤ 50 ms;若超阈值,先 fail 再优化(优化点见 design §5.3)

> **依赖**:T8-T16 全部完成(完整流水线才能测真实耗时)
> **验证**:scenario 跑通 + 输出 P95 数据

### T18. 文档同步

- [ ] 更新 `docs/21-战斗地图生成规则.md` §三.1:R5 表(21 项 1:1 映射)
- [ ] 更新 `docs/21-战斗地图生成规则.md` §三:R10 新增 9 个枚举值清单
- [ ] 更新 `docs/21-战斗地图生成规则.md` §五:R11 桥派生规则(取代旧"扫水带"算法)
- [ ] 更新 `docs/21-战斗地图生成规则.md` 新增 §六:R12 POI 结构层规则表(11 PoiType + 7 Race + 8 LairType)
- [ ] 更新 `combat-numerics-audit.md` 增 "Wave 4 — 战斗地图采样流水线" 章节,记录:
  - 9 个新枚举落地
  - OverworldSampler / BattleProjection / BridgePlacer / StructurePlacer 4 个新模块
  - R8#2 性能基线数据
  - 已知未实施约束(留作 v2)

> **依赖**:T1-T17 全部完成
> **验证**:文档 grep 不出现"_ => Plains"等被废弃的兜底逻辑描述;sim_audit 章节追加成功

---

## 验证总清单

每个 phase 完成后跑:

```powershell
# 单元测试
powershell -NoProfile -ExecutionPolicy Bypass -File tools\scripts\test.ps1 -SkipBuild -Mode unit
# 期望:旧 96 + 本特性新增 ~30 个单测,共 ~126 passed, 0 failed

# 集成测试 + 性能
powershell -NoProfile -ExecutionPolicy Bypass -File tools\scripts\sim.ps1 -SkipBuild -Battles 100 -Scenario battle_scale -Level 90
# 期望:Stronghold P95 < 50 ms

# 现有 sim 不退化
powershell -NoProfile -ExecutionPolicy Bypass -File tools\scripts\sim.ps1 -SkipBuild -Battles 30 -Seed 42 -Level 30 -Scenario combat_comp -EnableSpells
# 期望:法术阵 / 刺杀阵 / 决斗家等当前胜率 ±5pp 内
```

---

## 实施风险与应对

| 风险 | 影响 task | 应对 |
|---|---|---|
| `TerrainProperties` 不支持 `with` 表达式 | T1 | 实施前先看类型,若是 class 改为复制 ctor |
| `OverworldPOI.OccupiedHexes` 不含 CenterHex | T6, T14 | 已确认有独立 `CenterHex` 字段;StructurePlacer 用 `CenterHex` 而不假定 OccupiedHexes[0] |
| WallRing 几何形状不闭合 | T14, T15 | T15 入口先 BFS 验证;不闭合走 R13#11b fallback |
| 性能基线 P95 > 50 ms | T17 | 按 design §5.3 优化候选(KD-tree / Span / 减少分配) |
| `DeploymentZone.GenerateNormal` 已部分实装 R13 | T15, T16 | tasks 已标注:T15/T16 只补 Castle 战墙内外 + 节点保护;不重写已工作的 ApproachDirection 半平面切分 |

---

## 估时汇总

| Phase | 累计估时 |
|---|---|
| Phase 1(T1-T5)| 4 h |
| Phase 2(T6-T7)| 6 h |
| Phase 3(T8-T12)| 12 h |
| Phase 4(T13-T14)| 1 day |
| Phase 5(T15-T16)| 0.5 day |
| Phase 6(T17-T18)| 4 h |
| **合计** | **~5 工作日** |

> Phase 1 全部 task 之间无依赖可并行;Phase 4(T14)是最大块单独 PR;Phase 5 / 6 依赖前面所有 phase 完成。
