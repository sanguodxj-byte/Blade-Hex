# 比例尺统一 — Requirements

## 背景

当前 POI 在大地图、互动判定、战斗采样三层用各自的硬编码参数，相同 POI 类型在不同场景下表现不一致。本 spec 引入统一的 `POIScale` 4 档体系 + 可变形 footprint，把视觉、互动、战斗规模锚定到同一份数据。

## 范围

✅ **In Scope**
- `POIScale` 4 档（Tiny/Small/Medium/Large）+ `POIScaleProfile` 数据表
- `FootprintCell` / `FootprintTemplate` / `FootprintTemplateRegistry`
- `POIBattlePresetRegistry`（POI 类型 → preset + scale）
- `OverworldPOI` 增 footprint 字段；存档兼容
- `HexOverworldTile.OccupyingPoiName` + `IsPoiCenter`
- POI 互动判定改纯 hex 命中 + 鼠标 tooltip（删距离 fallback）
- 战斗地图改六边形（半径 N） + Voronoi sample 映射
- 野外遭遇统一为 Tiny footprint 路径
- 灯光 / Marker 渲染参数改用 `POIScaleProfile`
- Sim 验证（world_gen / battle_scale）

❌ **Out of Scope**
- Hex ↔ 真实公里换算（仅作设计假设，不写入运行时）
- 玩家修建/拆除 footprint cell
- 村→镇升级流程
- 战斗地图尺寸连续可变（保持 4 档离散）

## 验收原则

- **行为不变（已运行存档）**：旧存档反序列化 fallback 到 `solo` template，加载不报错
- **数据驱动**：所有尺度参数都从 `POIScaleTable` / `POIBattlePresetRegistry` 派生，禁止再加 magic number
- **测试先行**：六边形战斗地图改造前先建 sim scenario，改造后量化对比
- **可逆性**：P5（六边形战斗）保留 feature flag `BattleMap.UseHexagonalShape`，验证期可一键回退

---

## R1 — POIScale 与 ScaleProfile

### 用户故事
作为开发者，我希望 POI 的视觉/互动/战斗参数从同一份 Scale 表派生，不要每处写各自的 magic number。

### 验收标准
- 新增 `BladeHex.Strategic.POIScale` 枚举：Tiny / Small / Medium / Large
- 新增 `BladeHex.Strategic.POIScaleProfile` struct，字段：MarkerSize / LightRange / LightEnergy / BattleHexRadius / BattleSize / MinSpawnDistanceHex
- 新增 `BladeHex.Strategic.POIScaleTable.Get(POIScale)` 静态查表
- 单元测试：4 档 Get 返回值与 design.md 表格一致

## R2 — Footprint 数据结构与模板注册表

### 用户故事
作为内容设计者，我希望声明式定义 POI 形状（含地形约束），世界生成自动找合适落点。

### 验收标准
- 新增 `FootprintCellRole` 枚举（Any / CoastalDock / RiverDock / MountainSlope / ForestEdge）
- 新增 `FootprintCell` struct（Offset + Role + 可选 VisualSpriteId）
- 新增 `FootprintTemplate` 类（Name + Cells[] + RequiredCellCount）
- 新增 `FootprintTemplateRegistry`：注册 `solo` / `village_3` / `port_city_4` / `mountain_castle_5` / `town_5` / `fortress_7` / 等模板
- `TryFit(template, center, grid)` 方法：枚举 6 个 hex 旋转方向，返回第一个满足约束的方向 + 实际占用 hex 列表

## R3 — POIBattlePresetRegistry

### 用户故事
作为系统设计者，我希望 POI 类型到战斗 preset 的映射来自数据表而非 switch case，新增 POI 子类型不需要改代码。

### 验收标准
- 新增 `POIBattlePreset` record（TemplateName + Scale + FootprintTemplate + OverrideBattleSize? + DisplayName）
- 新增 `POIBattlePresetRegistry`：覆盖现有所有 POIType + 子类型
- `Resolve(poi)`: 按 (PoiTypeEnum, subType) 查表，找不到 fallback 到 (type, 0) 通配
- `OverworldPOI.GetBattleTemplateName()` 改为转发到 registry

## R4 — POI 占用 hex 标记

### 用户故事
作为玩家，我希望走到城堡/城镇任意一格都能进入交互；鼠标悬停显示 POI 信息。

### 验收标准
- `HexOverworldTile` 增字段 `OccupyingPoiName: string?` 和 `IsPoiCenter: bool`，序列化保持向后兼容（默认值即旧行为）
- 世界生成：POI 落点后调 `TryFit` 写入所有 footprint hex 的 `OccupyingPoiName`，中心格 `IsPoiCenter = true`
- `OverworldPOI` 增 `OccupiedHexes: Vector2I[]`（可计算字段，从 footprint + Position 派生）
- 旧存档反序列化：找不到 footprint 字段 → fallback 到 `solo` 模板

## R5 — 互动判定改纯 hex 命中

### 用户故事
作为玩家，走到 POI footprint 任一 hex 即触发交互，不再依赖距离阈值。

### 验收标准
- `POIController.CheckEnter()` 改为查 `tile.OccupyingPoiName`，命中即触发
- 删 `POI_ENTER_DIST` / `POI_LEAVE_DIST` 常量
- 离开判定：玩家移动到非 footprint 的 hex 即视为离开
- 鼠标悬停 footprint hex → 显示 POI tooltip（名字、类型、Scale 描述）

## R6 — 渲染 / 灯光参数从 ScaleProfile 派生

### 用户故事
作为美术，相同 Scale 的 POI 视觉表现一致；新增子类型不需要改渲染代码。

### 验收标准
- `POIController.RenderAll()`：marker size 取 `POIScaleTable.Get(poi.Scale).MarkerSize`，删除按 POIType 写死的 if-else
- `OverworldLightSystem`：删 `_poiLightConfigs` 字典，灯光 Range/Energy 取 ScaleProfile；颜色保留按 POIType 区分
- `WorldRegionRegistry.IsValidPoiPosition`：minDistance 从 ScaleProfile 取（按两 POI 的较大值）

## R7 — 战斗地图改六边形 + Voronoi 采样

### 用户故事
作为玩家，进入 POI 战斗时战斗地图能反映 POI 实际地形组成（港口含海格、山城含山坡）；战斗地图视觉是六边形而非矩形。

### 验收标准
- `BattleMapData` 增 `HexRadius: int N` 字段，废弃 W×H（保留兼容字段，运行时计算）
- `BattleMapGenerator.GenerateFromOverworld`：根据 footprint cells + 1 圈外围邻居作为 sample 集合；战斗 cell 通过 Voronoi 取最近 sample 的地形
- 野外遭遇用 Tiny 路径（玩家所在 hex + 6 邻居）
- `DeploymentZone.GenerateZones` 重写为六边形上下半弧
- `EnsureConnectivity` 适配六边形 grid
- Feature flag `BattleMap.UseHexagonalShape`（开发期默认 true，验证期可关）

## R8 — 战斗渲染层适配六边形

### 用户故事
战斗地图边界视觉是六边形/圆形，摄像机不会暴露空白角落。

### 验收标准
- `CombatScene` 摄像机 bbox 改为以 (0,0) 为中心、半径 ~ N · HexUtils.Size · 1.5 的方形外切
- 战斗 hex grid 渲染遍历 `HexUtils.GetHexagonCoords(N)` 而非 (q, r) 嵌套
- 边缘 visual fade（如雾化）覆盖六边形外缘

## R9 — Sim 验证

### 验收标准
- `SimulationHarness` 新增 `battle_scale` scenario：每档 Scale × 每个 preset 跑 N 次生成
- 输出指标：战斗 cell 总数 / 各地形比例 / 部署区可达性 / 港口水域占比 / 山城山地占比
- `world_gen` scenario 输出 footprint fallback 到 solo 的比例 / Scale 分布

---

## 阶段映射

| 阶段 | 实现 |
|------|------|
| P1 | R1 + R2 + R3 |
| P2 | R4（数据结构 + 世界生成调用 TryFit） |
| P3 | R5 |
| P4 | R6 |
| P5 | R7 |
| P6 | R8 |
| P7 | R9（sim 验证） |
| P8 | R2 补全 footprint 模板覆盖率 |
