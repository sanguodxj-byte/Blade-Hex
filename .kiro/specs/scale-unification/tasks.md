# 比例尺统一 — Tasks

跟踪本 spec 各阶段的完成度。

## Phase 1 — 数据结构（R1 + R2 + R3）✅ DONE

- [x] T1.1 新增 `BladeHex.Strategic.POIScale` 枚举（Tiny/Small/Medium/Large）
- [x] T1.2 新增 `POIScaleProfile` struct + `POIScaleTable.Get()`
- [x] T1.3 新增 `FootprintCellRole` 枚举（5 种）
- [x] T1.4 新增 `FootprintCell` struct
- [x] T1.5 新增 `FootprintTemplate` 类 + 旋转匹配 `TryFit`
- [x] T1.6 新增 `FootprintTemplateRegistry`，注册 12 个内建模板
- [x] T1.7 新增 `POIBattlePreset` record + `POIBattlePresetRegistry`
- [x] T1.8 `HexUtils.GetHexagonCoords(N)` 助手

## Phase 2 — POI Footprint 字段 + 世界生成（R4）✅ DONE

- [x] T2.1 `OverworldPOI` 增 `Scale` / `CenterHex` / `FootprintTemplateName` / `FootprintRotation` / `OccupiedHexes`
- [x] T2.2 `OverworldPOI.RebuildOccupiedHexes()` + `ContainsHex(hex)`
- [x] T2.3 `OverworldPOI.GetBattleTemplateName()` 转发到 registry
- [x] T2.4 `OverworldPOI.Serialize()` 加 footprint 字段；`ChunkPersistence.DeserializePoi()` 兼容旧存档
- [x] T2.5 `POIFootprintApplier.Apply()` 工具方法
- [x] T2.6 `POIStage.Execute()` 调用 ApplyFootprint
- [x] T2.7 `HexOverworldTile` 增 `IsPoiCenter`；`PoiId` 用作 OccupyingPoiName

## Phase 3 — 互动判定迁移（R5）✅ DONE

- [x] T3.1 `POIController.CheckEnter` 改为纯 hex 命中（`tile.PoiId != null`）
- [x] T3.2 删除 `POI_ENTER_DIST` / `POI_LEAVE_DIST` 距离判定
- [x] T3.3 离开判定：玩家走出 footprint 即重置交互锁

## Phase 4 — 渲染/灯光参数派生（R6）✅ DONE

- [x] T4.1 `POIController.RenderAll()` 用 `POIScaleTable.Get(poi.Scale).MarkerSize`
- [x] T4.2 `POIController` 颜色按 POIType 区分（保留风格差异）
- [x] T4.3 `OverworldLightSystem._poiLightConfigs` → `PoiLightFromScale()`

## Phase 5 — 战斗 sample 集合统一（R7 部分）✅ DONE

- [x] T5.1 `BattleMapGenerator.GenerateFromOverworld` 改用 footprint sample
- [x] T5.2 野外遭遇统一为 Tiny 路径（玩家 hex + 6 邻居）
- [x] T5.3 `BattleContext.CreateFromEncounter` 从 preset 派生 Size

## Phase 6 — 战斗地图改六边形（R7 + R8）⏳ DEFERRED

> 设计文档标为最高风险，需专门分支 + feature flag。本轮 P5 已统一 sample 集合，BattleSize 派生已就绪；战斗地图实际形状/部署区/UI 摄像机重写延后到独立 spec 或独立 PR。

- [ ] T6.1 `BattleMapData.HexRadius` 字段（替代 W×H）+ `RadiusMap`
- [ ] T6.2 战斗 grid iteration 改为 `HexUtils.GetHexagonCoords(N)`
- [ ] T6.3 `DeploymentZone.GenerateZones` 重写为六边形上下半弧
- [ ] T6.4 `EnsureConnectivity` 适配六边形
- [ ] T6.5 战斗 cell 地形用 Voronoi 取最近 sample
- [ ] T6.6 `CombatScene` 摄像机 bbox 改圆形外切
- [ ] T6.7 `BattleHexGridRenderer` 适配六边形
- [ ] T6.8 战斗 UI 边缘 fade 适配六边形
- [ ] T6.9 Feature flag `BattleMap.UseHexagonalShape`

## Phase 7 — Sim 验证（R9）✅ DONE (P5 部分)

- [x] T7.1 `world_gen` scenario 输出 footprint stats（Scale 分布 / 多格 fallback 比例 / 平均 cells）
- [ ] T7.2 `battle_scale` scenario：每档 Scale × preset 战斗生成统计
- [ ] T7.3 港口战斗水域占比 / 山城山地占比验证

## Phase 8 — Footprint 模板补充（R2 完整覆盖）⏳

- [x] T8.1 12 个基础模板（solo / village_3 / port_city_4 等）
- [ ] T8.2 评估是否需要更多变体（如 small_2 / medium_4 / 不同方向的 dock 模板）
- [ ] T8.3 调优 fallback 比例（当前 35.9%，若不可接受降低 role 约束严格度或增加候选位置重试次数）

---

## 当前 sim 数据基线

```
== 多 seed (N=8, Level=1) ==
总 POI 数：1721 (~215 / 种子)
平均每 POI 占用 hex：1.80

Scale 分布：
  Tiny    :  924 (53.7%)
  Small   :  431 (25.0%)
  Medium  :  275 (16.0%)
  Large   :   91 (5.3%)

多格 POI 中 fallback 到 solo 的比例：35.9% (286/797)
```
