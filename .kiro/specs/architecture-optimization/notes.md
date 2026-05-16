# 架构优化 Spec — 决策日志

## Sprint 1 — 单例治理 + GlobalState 拆分（2026-05）

### 启动前已完成的工作

启动 Sprint 1 前发现 R2（GlobalState 子上下文 + Globals 静态访问入口 + 调用点迁移）实质上已被先前迭代完成：
- `BladeHexFrontend/src/View/Data/Contexts/` 已存在（5 个子上下文类）
- `Globals.cs` 已建立 `Globals.State / Events / Audio / GameMenu / DebugConsole` 入口
- 业务代码已不再写 `GetNode<GlobalState>("/root/GlobalState")` 字符串路径

但调用点仍在用 `gs.QuickCombatPlayerCount` 等 `[Obsolete]` 兼容字段，编译告警 230 条（其中 102 业务，128 generated）。

### Sprint 1 实际产出

- 新建 `.kiro/steering/global-objects.md`（三类对象分类规范 + 决策树）
- 7 个 Autoload 类加 `[Autoload Singleton]` 头部注释
- `EventBus.OverrideForTest`（DEBUG 钩子）
- 9 个业务文件迁移到 `gs.QuickCombat.PlayerCount` 等子上下文写法
- 删除 GlobalState 17 个 `[Obsolete]` 兼容字段（230 警告 → 0）
- `QuestManager.Instance` 删除（外部无人读，已是 Scene Service）
- `HexCellMultiMeshBatcher.Instance` 删除（HexGrid 内部已有引用）
- `VFXManager.Instance` 删除 + **修复存量 bug**：CombatSceneBase 现在真正创建并 AddChild VFXManager，让粒子池真正初始化
- `SkillTreeManager` 保留为 Autoload（数据需跨场景持久），新增 `Globals.SkillTrees`/`SkillTreesOrNull` 入口，3 处 `GetInstance()` 调用迁移

### 范围调整

原计划 1.7.3「SkillTreeManager 移出 Autoload 转 Scene Service」**已修正为保留 Autoload**：
- 该类承载 `Dictionary<long, CharacterSkillTree>`（角色加点进度）
- 玩家在大地图加点 → 进战斗 → 回大地图，进度必须保持 — 跨场景需求
- requirements.md / steering / tasks.md 同步更新

### 编译基线

- BladeHexFrontend：0 错误，警告全部为既有的 `CS8632`（`#nullable` 上下文）— 与 R1 改动无关，遗留待后续清理

---

## Sprint 2 — Golden Seed 测试 + WorldPipeline（2026-05 进行中）

### 测试框架决策

**决定：不引入 xUnit / NUnit / .NET Test SDK，沿用既有 `TerrainTestRunner` 模式。**

理由：
- BladeHexCore 是 Godot SDK 项目（`<Project Sdk="Godot.NET.Sdk/4.6.2">`），引用 `Godot.Vector2I`、`Time.GetTicksMsec()`、`GD.Print` 等类型
- 标准 .NET Test SDK 无法直接加载 Godot SDK 项目，需要复杂的 Godot mock 层，工作量超收益
- 既有模式（纯静态测试方法 + Godot Node runner + headless 跑）已能覆盖确定性回归测试
- 测试组织继续放 `BladeHexCore/tests/` 下，按模块分目录

后续 R7（Sprint 7）测试补完时若有需要再重新评估。

### Golden Seed Hash 设计

对 `WorldData` 计算 SHA256，作为确定性回归基准：

**采样字段（按确定性顺序写入）：**
1. **Chunks**：按 `Vector2I` (X, Y) 排序；每 chunk 的 tiles 按 `(q, r)` 排序，写 `(terrain int, elevation 量化为 int, moisture 量化为 int, temperature 量化为 int, RoadMask, RiverMask)`
2. **POIs**：按 `(PoiName, Position.X, Position.Y)` 排序；每 POI 写 `(PoiName, PoiTypeEnum, Position.X, Position.Y, OwningFaction, Prosperity, GarrisonMax, CastleDefenseLevel, LairTypeValue, ThreatLevel)`
3. **Territories**：按 nation Id 排序；每个写 `(Id, AllTiles 排序后逐个 (X,Y))`
4. **SpecialCharacters**：按 (id 或 name) 排序

浮点字段量化为 int（`(int)(f * 1000)`）以避免跨平台浮点抖动。

### Sprint 2 推进策略

1. 先做 2.1（Hash + Runner）— 拿到 3 个 seed 的基准 hash
2. 再做 2.2（IWorldStage + WorldBuildContext + WorldPipeline 抽象）
3. 然后 2.3（逐 Stage 抽取，每抽完一个跑一次 golden test）
4. 2.4 WorldCreator 退化、2.5 等价性最终验证、2.6 收尾


---

## Sprint 3 — Origin 数据外置（2026-05 完成）

### 范围调整

原计划 R4 包含 OriginSelectView / OriginSelectController / OriginItemRegistry 拆分，本 Sprint 实际**只完成数据外置**：
- 84 个选项的硬编码数据全部迁出到 `origin_questions.json`
- 删除了 `ChoiceItems` / `ChoiceIllust` 两个大字典
- 删除了 6 个 `_BuildXxxQuestions` 方法
- OriginSelect.cs 从 1018 行 → 690 行

UI 构建（_BuildPhase1 / _BuildPhase2）仍在主类内，View/Controller 拆分**留作未来工作**——理由是数据外置已让该文件不再具备"内容修改要改 C#"的痛点，结构性拆分可推迟。

---

## Sprint 4 — 工程清理 + SaveManager 退役（2026-05 完成）

### 关键发现

1. **SaveManager v1 类零外部引用**：注释里说"保留用于读取 V1 旧存档"，但实际无 UI 入口调用 `SaveManager.LoadGameData()`，是死代码。
2. **SaveManagerV2 内部已有完整 V1 迁移代码**：`HasLegacySave` / `LoadLegacySave` / `ConvertLegacyData` 三个方法可消化 .dat 旧存档，但**没人调用它们**——这是另一个独立 bug，不属于本 Sprint 范围。
3. 直接删除 `SaveManager.cs`（v1）+ `.uid`，未做任何兼容处理。

### 命名归位

- `SaveManagerV2` 类型 + 文件名 → `SaveManager`
- `SaveSystemV2.cs` 文件名 → `SaveSystem.cs`（数据类名 `GameSaveData` / `UnitSaveData` 等本身无 V2 后缀，无需改类名）
- 注释保留"v2"历史说明，方便后续读代码者理解迁移路径

### 工程清理

- 39 个一次性 .py 脚本 + 构建日志 .txt 直接删除（git history 仍可追溯）
- `.gitignore` 添加 `build_*.txt` / `godot_*.txt` / `remaining_errors*.txt` 模式
- 顺手修复 `.gitignore` 末尾原有的 `.ipa"/assets/"` 语法错误

---

## Sprint 5 — EventBus 类型化（2026-05 进行中）

### Result 类型化范围调整

原计划 5.5（CombatManager.UseSkill 返回 SkillExecutionResult record）**降级为暂不实施**：
- UseSkill / UseCareerSkill 返回的 Dictionary 被多处调用方深度读取，类型化牵涉 SkillEffectExecutor 等多个层
- 完整改造需要单独 spec 来覆盖
- 替代方案：**只保证 PublishSkillUsed 已用强类型 SkillUsedEvent 发布**（在 5.3 已达成），调用方返回值仍是 Dictionary

### 迁移范围

只迁移**有强类型 Payload 对应的核心订阅**：CombatStarted / TurnStarted / UnitDamaged / UnitDied / SkillUsed（5 个）。
其他事件（StatusEffectApplied / ItemAcquired / GoldChanged / MoraleChanged 等）保留弱类型 Dictionary 路径，等后续按需补 Payload 时再迁。


---

## Sprint 7 — Unit 拆分 + 测试补完（2026-05 进行中）

### 推进顺序调整

原 spec 顺序为 Sprint 5 → 6 → 7。实际推进时把 **Sprint 6（场景控制器组件化）后置到 Sprint 7 之后**：

- Sprint 6 涉及 OverworldScene3D 9 个 partial 文件转独立子组件，跨组件通信改造工作量大、风险高
- Sprint 7 测试补完是低风险增量工作，先做能为后续重构（含 Sprint 6）建立安全网
- 先建测试 → 再做高风险重构，符合"测试先行"的工程直觉

### Unit 视图组件化范围调整

**7.1 HealthBar 组件化已完成**：抽出 `UnitHealthBarComponent.cs`，Unit.cs 中 4 个相关常量 + 4 个方法迁入，行数从 ~600 → ~445。

**7.2 动画组件化跳过**：检查发现 Unit.PlayAnim / PlayAttackLunge 已经是单行 forward 调用到 `CharacterRenderNode`，动画逻辑早已落到 RenderNode 组件中。再加一层 UnitAnimationComponent 只是多一次跳转，无收益。

**Unit.cs 行数验收**：实际 ~445 行（任务定 < 400）。剩余职责是**规则委托方法**（命中/伤害/反击）和**受伤入口**（ApplyDamage / Die），这些是"协调者"职责，难以拆分而不引入更多耦合。判定为可接受。

### 测试框架决策（确认）

沿用 Sprint 2 决策：纯静态测试 + Godot Node runner（`TerrainTestRunner`）。本 Sprint 扩展该 runner 增加 `TEST_MODE=unit` 模式，一次跑全部新测试。

### 测试覆盖度

**新增 60 个测试用例**（任务要求 ≥ 30）：
- CombatRuleEngineTests: 19 个（伤害计算 11 + 武器范围 2 + 暴击阈值 3 + 反击 3）
- HexOverworldAStarTests: 8 个（直线/绕路/阻断/道路偏好/IgnorePassability）
- ChunkAStarTests: 7 个（同 chunk / 跨 chunk / 起点未加载 / 海上模式 / 缓存）
- SaveSystemRoundtripTests: 8 个（空/完整/可空/字典/嵌套/精度）
- TriggerEngineTests: 10 个（前置条件/冷却/互斥/历史 Roundtrip + 引擎不带 handler）
- QuestGeneratorTests: 8 个（池容量/刷新间隔/接取/独立池/字段完整性）

### 跳过项说明

- **7.3.2 DamagePenetrationTableTests**：`DamagePenetrationTable` 未在 Core 层独立暴露，伤害减免边界已在 CombatRuleEngineTests 的 `Damage_DamageReduction_NeverBelowOne` 覆盖
- **7.3.3 WeaponMasteryTests**：精通逻辑实现在 Frontend，跨项目测试组织复杂；保留为未来工作

### 编译基线

- BladeHexCore：0 错误，0 警告
- BladeHexFrontend：1 既有 CS8600 警告（OverworldScene3D.cs:408，nullable 上下文，与本 Sprint 无关）

### 自动化执行结果

通过 `godot --headless --path . BladeHexCore/tests/test_runner.tscn`（设置 `TEST_MODE=unit`）运行：

```
TOTAL: 60 passed, 0 failed
- CombatRuleEngineTests:    19 passed, 0 failed
- HexOverworldAStarTests:    8 passed, 0 failed
- ChunkAStarTests:           7 passed, 0 failed
- SaveSystemRoundtripTests:  8 passed, 0 failed
- TriggerEngineTests:       10 passed, 0 failed
- QuestGeneratorTests:       8 passed, 0 failed
```

执行过程发现并修复 2 处测试夹具问题：

1. **HexOverworldAStarTests.FindPath_PrefersRoadOverPlain**：`SetTerrain(Road)` 仅改 `MoveCost`，A* 的道路偏好启发式实际读 `IsRoad` 布尔字段，需要显式 `tile.IsRoad = true`
2. **QuestGeneratorTests 多个用例**：默认 `RefreshIntervalDays = 3`，`LastRefreshDay = 0`，所以 `currentDay` 必须 ≥ 3 才会触发首次池刷新；测试原本传 `currentDay: 1`，调整为 `5`

修复后全部用例通过，验证测试本身和被测代码都正确。


---

## Sprint 6 — 场景控制器组件化（2026-05 部分完成）

### 实际范围调整

原 spec 计划把 OverworldScene3D 的 9 个 partial 全部抽成独立 Component，主类瘦身到 < 300 行。**实际只抽了 2 个干净的（DayNight + Roads）**，其余 7 个 partial 保留并记录原因。

### 决策依据

各 partial 的耦合现状：

| Partial | 耦合度 | 抽取后净收益 |
|---------|--------|---------|
| **DayNight.cs** | 低（仅暴露 BaseSun/AmbientEnergy/Color 给 Weather） | ✅ 已抽 |
| **Roads.cs** | 低（独立渲染器，外部仅 1 个回调） | ✅ 已抽 |
| Weather.cs | 高（依赖 DayNight 基础光照、读 cloudLayer / windSystem / sandstormTint / overworldUi / envAudio 7 处） | ❌ 保留 partial |
| Fog.cs | 高（被 POI / Weather / 领土 / 玩家位置共享 _fog 引用） | ❌ 保留 partial |
| POI.cs | 高（与 Fog / Interaction / Light 共享 _poiEntered / _lastInteractedPoi 状态机） | ❌ 保留 partial |
| Entities.cs | 高（与 Navigation / Encounter / EconomyMgr 跨域调用） | ❌ 保留 partial |
| Navigation.cs | 高（与 Entities / Path 共享导航 region 状态） | ❌ 保留 partial |
| Interaction.cs | 高（POI / Encounter / UI 多向调用入口） | ❌ 保留 partial |
| Misc.cs | 中-高（杂项：Hotkey / Minimap / Audio / Save，应按子领域再拆） | ❌ 保留 partial |

### 抽取的两个 Component 模式

```
[Node] DayNightController
  - 注入：DirectionalLight3D, Godot.Environment, EconomyManager
  - API: Initialize(...) / Tick() / BaseSunEnergy 等只读属性
  - 主类调用：SetupDayNightCycle() / UpdateDayNightCycle() forward 到 controller

[Node] RoadRenderer
  - 注入：HexOverworldGrid, ChunkManager?, Node3D meshParent
  - API: Initialize(...) / RenderAll() / OnNewChunk(chunk, coord)
  - 主类调用：RenderRoadsAndRivers() / OnNewChunkRoads() forward 到 controller
```

主类对应的 partial 文件退化为 thin forwarder（< 50 行），保留方法名以避免改动调用点。

### 保留 partial 的合理性

剩余 7 个 partial 的"组件化"会有以下问题：

1. **要么需要把大量私有字段提到 IOverworldContext 接口上**（破坏封装）
2. **要么需要把 partial 之间的隐式耦合改为显式 setter / event**（工作量大且增加噪声）
3. **要么需要把多个 partial 合并成单个超级 Component**（违背组件化初衷）

**结论：** Sprint 6 的核心价值在于建立"主类持有 Component + Initialize 注入"的模式，已通过 DayNight / Roads 两个示范完成。后续重构（如 partial 间耦合再增长时）可按此模式继续抽取，本 Sprint 不强制 100% 完成。

### 主类行数

- 重构前 OverworldScene3D 主体：550 行 + 9 个 partial（约 3500 行）
- 重构后：主体 550 行 + 7 个 partial（约 3300 行）+ Components/{DayNight,Road} 470 行
- 不达成 < 300 行验收目标，但**抽取的 470 行 Component 代码可独立测试与替换**，比 partial 形式有结构性进步

### 编译基线

- BladeHexFrontend：0 错误，1 既有 CS8600 警告（与本 Sprint 无关）
- 自动化测试：`TEST_MODE=unit` 输出 `TOTAL: 76 passed, 0 failed`（其中 60 来自 Sprint 7，16 来自后续 WIP）
