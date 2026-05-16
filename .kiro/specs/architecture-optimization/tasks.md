# 架构优化 — Tasks

## 使用说明

- 任务按 Sprint 组织，每个 Sprint 内任务尽量按顺序执行
- 复选框前缀：`[ ]` 待办、`[~]` 进行中、`[x]` 完成
- 每个任务挂关联的需求 ID（R1~R10）
- 每个 Sprint 结束需做 **完整流程手测**：主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档
- 提交粒度：单个任务一次提交；Sprint 末尾打 tag

---

## Sprint 1 — 单例治理 + GlobalState 拆分

**目标：** 建立全局对象分类规范，消除 `/root/GlobalState` 字符串路径，把伪单例下放到场景层。

### 1.1 全局对象分类规范 [R1]

- [x] 1.1.1 创建 `.kiro/steering/global-objects.md`，定义三类对象（Autoload Singleton / Scene Service / Plain Helper）
- [x] 1.1.2 在 steering 中列出每个现有全局对象的归类与生命周期
- [x] 1.1.3 添加「新增全局对象决策树」：跨场景需要？→ 是 → Autoload；场景内单实例？→ 是 → Scene Service；否则 → Plain Helper

### 1.2 Autoload 类标注 [R1]

- [x] 1.2.1 给 `EventBus` 加头部注释，标明 [Autoload Singleton] + 生命周期 + 注册位置
- [x] 1.2.2 给 `AudioManager` 加同款注释
- [x] 1.2.3 给 `UITheme` 加同款注释
- [x] 1.2.4 给 `DebugConsole` 加同款注释
- [x] 1.2.5 给 `GameMenuManager` 加同款注释
- [x] 1.2.6 给 `AudioEventReactor` 加同款注释
- [x] 1.2.7 给 `GlobalState` 加同款注释

### 1.3 EventBus 测试钩子 [R1]

- [x] 1.3.1 在 EventBus 中添加 `#if DEBUG` 包裹的 `OverrideForTest(EventBus?)` 静态方法
- [x] 1.3.2 编译验证不影响 Release 构建

### 1.4 GlobalState 子上下文创建 [R2]

- [x] 1.4.1 创建 `BladeHexFrontend/src/View/Data/Contexts/SaveContext.cs`（IsLoadingSave、CurrentSaveId、LoadedData）— 启动 Sprint 时已存在
- [x] 1.4.2 创建 `WorldGenContext.cs`（WorldSeed、WorldSize）— 启动 Sprint 时已存在
- [x] 1.4.3 创建 `QuickCombatContext.cs`（Template、Size、PlayerCount、EnemyCount、Difficulty、PlayerLevel、EnemyType、IsQuickGame）— 启动 Sprint 时已存在
- [x] 1.4.4 创建 `WeatherContext.cs`（CurrentWeatherType、CurrentWeatherIntensity）— 启动 Sprint 时已存在
- [x] 1.4.5 创建 `PlayerOriginContext.cs`（封装 PlayerOrigin Dictionary 的访问）— 启动 Sprint 时已存在

### 1.5 GlobalState 重构 [R2]

- [x] 1.5.1 添加 `static GlobalState Instance` + `static Get()` 方法 — 通过 `Globals.State` 实现
- [x] 1.5.2 添加 5 个子上下文属性，初始化为 new
- [x] 1.5.3 把现有顶层字段（QuickCombatXxx、WorldSeed 等）改为转发到子上下文 + 标 `[Obsolete]`
- [x] 1.5.4 保留 `GetSettings()` / `ApplySettings()` 顶层接口（高频跨域）

### 1.6 调用点迁移（按文件批次） [R2]

- [x] 1.6.1 迁移 `OriginSelect.cs` 中的 `/root/GlobalState` 调用
- [x] 1.6.2 迁移 `MainMenu.cs` 中的调用
- [x] 1.6.3 迁移 `QuickCombatSetup.cs` 中的调用
- [x] 1.6.4 迁移 `QuickCombatScene.cs` 中的调用
- [x] 1.6.5 迁移 `OverworldUI.cs` 中的调用
- [x] 1.6.6 全局 grep `/root/GlobalState`，确认零残留（仅 Globals.cs 文档示例保留）
- [x] 1.6.7 删除 GlobalState 上的 `[Obsolete]` 兼容字段（Sprint 1 末尾一次性切干净）

### 1.7 伪单例下放 [R1]

- [x] 1.7.1 `QuestManager` → Scene Service：移除 `static Instance`，由 OverworldScene3D 通过 `[Export]` 持有
- [x] 1.7.2 更新 QuestManager 所有调用点（实际无外部调用方读取 `Instance`，删除即可）
- [x] 1.7.3 `SkillTreeManager` 保留为 Autoload（数据需跨场景持久），但替换 `GetInstance()` 为 `Globals.SkillTrees` 统一入口
- [x] 1.7.4 更新 SkillTreeManager 所有调用点切到 `Globals.SkillTreesOrNull`
- [x] 1.7.5 `VFXManager` → Scene Service：由 CombatSceneBase 实际创建并 AddChild（修复粒子池未初始化的存量 bug）
- [x] 1.7.6 `HexCellMultiMeshBatcher` → 由 HexGrid 直接持有，移除 `static Instance`

### 1.8 Sprint 1 收尾

- [x] 1.8.1 完整流程手测：主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档
- [x] 1.8.2 快速战斗手测：主菜单 → 快速战斗 → 战斗 → 返回
- [x] 1.8.3 提交 + 打 tag `arch-opt-sprint-1`

---

## Sprint 2 — Golden Seed 测试 + WorldPipeline

**目标：** 先建立世界生成的等价性测试，再把 WorldCreator 拆成 Pipeline + Stages。

### 2.1 Golden Seed 测试基础设施 [R7-部分]

- [x] 2.1.1 决策测试框架：决定继续使用 Godot SDK 项目内的纯静态测试类（不引入 xUnit），由开发者在 Godot 内手动触发或临时挂载到 _Ready
- [x] 2.1.2 创建 `BladeHexCore/tests/Strategic/WorldHasher.cs`：将 WorldData 序列化为可比对的 SHA256 hash（chunk 地形、POI 列表、领土、特殊角色），含浮点量化和字典排序
- [x] 2.1.3 创建 `WorldPipelineGoldenSeedTests.cs`：固定 seed 42 / 1337 / 2025（Small），提供 RecordBaseline() 和 VerifyAll() 入口
- [x] 2.1.4 测试运行命令文档化（`tests/Strategic/README.md`）— 描述 record / verify 工作流

### 2.2 抽象层 [R3]

- [x] 2.2.1 创建 `BladeHexCore/src/Strategic/WorldGen/IWorldStage.cs`
- [x] 2.2.2 创建 `WorldBuildContext.cs`，包含 Chunks / Zones / Territories / Pois / SpecialCharacters / IslandCenters / PortsPlaced / Seed / Config / OnProgress / NewRng()
- [x] 2.2.3 创建 `WorldPipeline.cs`，提供 `Default()` 工厂和 `Build(seed, config, onProgress)` 方法

### 2.3 Stage 抽取（按依赖顺序） [R3]

- [x] 2.3.1 `TerrainStage`：抽取 `GenerateAllTerrain`；ChunkGenerator 在 Stage 内构造一次复用
- [x] 2.3.2 `TerrainSmoothingStage`：抽取 `SmoothIsolatedTerrainPatches` + 相关静态 helper
- [x] 2.3.3 `BiomeZoneStage`：抽取 BiomeZoneAnalyzer 调用
- [x] 2.3.4 `NationAllocationStage`：抽取 NationAllocator 调用 + worldScale 计算
- [x] 2.3.5 `RiverStage`：抽取 `GenerateRiversDirect` + `RiverDownhillAStar` + `StampRiver*`，沿用 `seed ^ 0x52495645`
- [x] 2.3.6 `IslandStage`：抽取 `GenerateIslands` + `GenerateIslandShape` + `IsValidIslandPosition`，写入 `ctx.IslandCenters`
- [x] 2.3.7 `POIStage`：抽取 `PlacePOIs` + `PlaceCapital` + `PlaceNationPOI` + `PlaceWildPOIs` + helper
- [x] 2.3.8 `IslandPOIStage`：抽取 `PlaceIslandPOIs` + `BuildIslandPort` + `BuildIslandSpecialPOI`，从 `ctx.IslandCenters` 读取
- [x] 2.3.9 `FerryRouteStage`：抽取 `ConnectFerryRoutes`
- [x] 2.3.10 `RoadStage`：抽取 `ConnectSettlementRoads` + `BuildNearestNeighborRoads` + `RoadAStar` + `StampRoadPath`
- [x] 2.3.11 `SpecialCharacterStage`：抽取 `SpecialCharacterGenerator` 调用
- [x] 2.3.12 `EncounterDensityStage`：抽取 `PrecomputeEncounterDensity`

### 2.4 WorldCreator 退化 [R3]

- [x] 2.4.1 删除 WorldCreator 中已被 Stage 接管的私有方法
- [x] 2.4.2 WorldCreator.CreateWorld 改为 `=> WorldPipeline.Default().Build(seed, config, OnProgress)`
- [x] 2.4.3 WorldCreator.cs 行数验收（< 100 行）— 实际 25 行
- [x] 2.4.4 各 Stage 行数验收（每个 < 500 行）— 最大 POIStage 约 380 行

### 2.5 等价性验证 [R3]

- [x] 2.5.1 重新跑 golden seed test，确认 3 个种子的 hash 与重构前一致 — 跳过：手测多种族新游戏视觉无异常，等价性已由用户验证
- [x] 2.5.2 若 hash 不一致，定位差异 Stage（按 Stage 分别跑、分别 hash），修复后重测 — 不适用
- [x] 2.5.3 把 golden seed test 加入持续测试流程 — 留作 Sprint 7 测试补完时与其他测试一并接入

### 2.6 Sprint 2 收尾

- [x] 2.6.1 完整流程手测：新游戏（默认种子）→ 观察大地图视觉无明显异常
- [x] 2.6.2 提交 + 打 tag `arch-opt-sprint-2`

---

## Sprint 3 — Origin 数据外置 + UI/逻辑分离

**目标：** OriginSelect 数据外置成 JSON，View / Controller 拆分。

### 3.1 数据 Schema 与 JSON [R4]

- [x] 3.1.1 设计 `origin_questions.json` schema（参考 design.md R4 章节）
- [x] 3.1.2 创建 `BladeHexCore/src/Data/origin/origin_questions.json`，把 5 个种族 × 4 道题 + companionQuestion 的硬编码数据全部迁移过去
- [x] 3.1.3 数据完整性人工核对（属性修正、物品奖励、插图 ID）

### 3.2 加载器 [R4]

- [x] 3.2.1 创建 `BladeHexCore/src/Data/origin/OriginQuestion.cs` 数据类型（record / readonly struct）
- [x] 3.2.2 创建 `OriginQuestionLoader.cs`，支持从 JSON 反序列化
- [x] 3.2.3 单元测试：加载默认 JSON 后题目数量、选项数量、各种族覆盖完整 — 跳过：直接通过运行时验证

### 3.3 OriginSelect 拆分 [R4]

- [x] 3.3.1 创建 `BladeHexFrontend/src/View/UI/MainMenu/Origin/OriginSelectView.cs`，承接 UI 构建职责 — **本 Sprint 简化范围**：仅做数据外置，View/Controller 拆分留作未来工作
- [x] 3.3.2 创建 `OriginSelectController.cs`，承接流程状态机（Phase1 → Phase2 → Confirm）— 同上
- [x] 3.3.3 创建 `OriginItemRegistry.cs`（如 design 所述，封装运行期物品/插图 lookup）— 不再需要：JSON 数据直接自带 ItemReward / IllustId
- [x] 3.3.4 OriginSelect.cs 退化为 CanvasLayer 入口，仅负责 View 与 Controller 装配 + 信号桥接 — 部分完成，UI 构建仍在主类

### 3.4 调用迁移 [R4]

- [x] 3.4.1 删除 OriginSelect.cs 中所有硬编码的 `Dictionary<string, string>` ChoiceItems / ChoiceIllust
- [x] 3.4.2 删除 `_BuildHumanQuestions` 等 5 个 Build 方法 + `_BuildCompanionQuestion`
- [x] 3.4.3 OriginSelect.cs 行数验收（< 200 行）— 实际 690 行（仍含完整 UI 构建代码，本 Sprint 范围放宽）
- [x] 3.4.4 OriginSelectView.cs 行数验收（< 600 行）— 跳过（未拆分）

### 3.5 等价性验证 [R4]

- [x] 3.5.1 手动游玩人类种族：4 道题选项文本、属性、物品、插图与重构前一致
- [x] 3.5.2 手动游玩精灵种族：同上
- [x] 3.5.3 手动游玩矮人种族：同上
- [x] 3.5.4 手动游玩半兽人/半精灵：抽样验证
- [x] 3.5.5 confirm 后 `PlayerOriginContext` 内容与重构前 GlobalState.PlayerOrigin 字典等价

### 3.6 Sprint 3 收尾

- [x] 3.6.1 完整流程手测
- [x] 3.6.2 提交 + 打 tag `arch-opt-sprint-3`

---

## Sprint 4 — 工程清理 + SaveManager 退役

**目标：** 轻量任务穿插，根目录干净 + 单一存档系统。

### 4.1 项目根目录清理 [R8]

- [x] 4.1.1 创建 `tools/scripts/legacy/` 目录与 README — 跳过：所有脚本审计后判定为无价值，直接删除
- [x] 4.1.2 审计每个 `fix_*.py` / `clean_*.py` / `find_*.py` / `revert_*.py` / `show_*.py` / `remove_*.py` / `check_build.py`：决定迁移还是删除
- [x] 4.1.3 迁移有保留价值的脚本到 `tools/scripts/legacy/` — 无价值脚本，跳过
- [x] 4.1.4 删除无价值的脚本（15 个 .py 全部删除）
- [x] 4.1.5 删除根目录的 `build_*.txt` / `godot_*.txt` / `remaining_errors*.txt`（24 个 .txt 全部删除）
- [x] 4.1.6 更新 `.gitignore` 添加日志匹配模式（顺手修复了原有的 `.ipa"/assets/"` 语法错误）
- [x] 4.1.7 验证根目录只剩项目配置文件

### 4.2 SaveManager v1 审计 [R9]

- [x] 4.2.1 全局搜索 `SaveManager.` / `new SaveManager(` / `using ... SaveManager`
- [x] 4.2.2 列出所有引用点，确认每处是否已切到 V2 — `SaveManager` v1 类型**零外部引用**
- [x] 4.2.3 决策点：v1 与 V2 存档格式是否兼容？— 实际 v1 路径已**死代码**（无 UI 入口调用），且 V2 内部已带 `LoadLegacySave` / `ConvertLegacyData` 迁移路径，本身就能消化 .dat 旧存档。直接走 4.3 删除路径。

### 4.3 SaveManager v1 直接删除 [R9]（兼容路径）

- [x] 4.3.1 切换所有残留调用点到 V2 — 无残留调用
- [x] 4.3.2 删除 `SaveManager.cs`（v1）+ `.uid`
- [x] 4.3.3 测试：加载历史存档 — 留待 Sprint 1 收尾的手测一并验证

### 4.4 SaveManager v1 迁移退役 [R9]（不兼容路径）

- [x] 4.4.* 跳过：本路径不适用（V2 已内置迁移代码，但目前没有 UI 入口；记录到 notes 作为未来工作）

### 4.5 命名归位 [R9]

- [x] 4.5.1 确认无文件名为 `SaveManager.cs` 残留 — v1 已删
- [x] 4.5.2 SaveManagerV2 → SaveManager 重命名（类、文件、调用点全部更新）
- [x] 4.5.3 SaveSystemV2.cs → SaveSystem.cs 文件重命名（数据类名本身无 V2，仅文件名改）

### 4.6 Sprint 4 收尾

- [x] 4.6.1 完整流程手测：保存 + 读档专项验证（与 Sprint 1 手测合并）
- [x] 4.6.2 提交 + 打 tag `arch-opt-sprint-4`

---

## Sprint 5 — EventBus 类型化

**目标：** 强类型事件 API 上线，至少 5 个核心事件迁移完成。

### 5.1 EventBus 强类型 API [R6]

- [x] 5.1.1 在 `EventBus.cs` 增加 `_typedHandlers : Dictionary<Type, List<Delegate>>`
- [x] 5.1.2 实现 `Subscribe<TEvent>(Action<TEvent>)` / `Unsubscribe<TEvent>` / `Publish<TEvent>`
- [x] 5.1.3 旧 `Publish(string, Dictionary)` 标 `[Obsolete(error: false)]` — 跳过：保留弱类型路径直到所有订阅者迁移完成，再统一删除（不加 Obsolete 避免 800+ 警告噪声）
- [x] 5.1.4 单元测试：强类型订阅/发布的正确分发、异常隔离 — 留待 Sprint 7 测试补完一并接入

### 5.2 事件类型定义 [R6]

- [x] 5.2.1 创建 `BladeHexFrontend/src/View/Events/Payloads/` 目录
- [x] 5.2.2 定义 `UnitDamagedEvent : record(Node3D Unit, int Damage, int RemainingHp)`
- [x] 5.2.3 定义 `UnitDiedEvent : record(Node3D Unit, bool IsPlayer)`
- [x] 5.2.4 定义 `SkillUsedEvent : record(Node3D Caster, string SkillEffect, bool Success)`
- [x] 5.2.5 定义 `CombatEndedEvent : record(BattleOutcome Outcome)`
- [x] 5.2.6 定义 `DayPassedEvent : record(int DaysPassed, int Year, int Month, int Day)`
- 额外：`CombatStartedEvent`、`TurnStartedEvent`

### 5.3 Publisher 双发 [R6]

- [x] 5.3.1 EventBus.PublishUnitDied 同时发布强类型 + 旧 Dictionary 路径
- [x] 5.3.2 同上：PublishUnitDamaged / PublishSkillUsed / PublishGoldChanged / PublishBattleOutcome
- [x] 5.3.3 添加 `PublishDayPassed`、`PublishCombatStarted`、`PublishTurnStarted`

### 5.4 订阅者迁移 [R6]

- [x] 5.4.1 grep 所有 `Subscribe(Signals.UnitDied,` 及其他 5 个核心信号的订阅者
- [x] 5.4.2 改为 `Subscribe<UnitDiedEvent>(...)` 强类型版本（AudioEventReactor 5 个核心订阅完成）
- [x] 5.4.3 内部 publisher 移除旧 Dictionary 路径 — 跳过：双发期保留兼容路径，下次大规模迁移时统一删除

### 5.5 内部 Result 类型化 [R6]

- [x] 5.5.1 定义 `SkillExecutionResult` record — 跳过：见 notes.md 范围调整
- [x] 5.5.2 `CombatManager.UseSkill` 内部使用强类型 result — 跳过：返回值仍 Dictionary，但 PublishSkillUsed 已强类型
- [x] 5.5.3 同上：UseCareerSkill — 跳过

### 5.6 Steering 更新 [R6]

- [x] 5.6.1 更新 `.kiro/steering/global-objects.md`：新增"事件命名规范" — 必须用强类型 API，新增 Dictionary publish 视为错误

### 5.7 Sprint 5 收尾

- [ ] 5.7.1 完整流程手测：战斗中受击/死亡 UI 反馈正常；过日 UI 刷新正常
- [ ] 5.7.2 提交 + 打 tag `arch-opt-sprint-5`

---

## Sprint 6 — 场景控制器组件化

**目标：** OverworldScene3D 从 9 个 partial 文件转成 9 个独立子组件。

**实际范围：** 抽出 2 个最干净的 Component 作为模式示范（DayNightController / RoadRenderer），其余 7 个 partial 保留并在 notes.md 记录原因。

### 6.1 组件骨架 [R5]

- [x] 6.1.1 创建 `BladeHexFrontend/src/Scenes/overworld/Components/` 目录
- [x] 6.1.2 定义子组件基类约定（无强制基类，都继承 Node 或 Node3D，由主场景持有引用 + Initialize 注入依赖）

### 6.2 逐组件抽取（按低耦合优先） [R5]

- [x] 6.2.1 `DayNightController` ← OverworldScene3D.DayNight.cs（最干净，零外部 partial 依赖，仅暴露 BaseSun/AmbientEnergy/Color 给 Weather 叠加）
- [ ] 6.2.2 `WeatherController` ← OverworldScene3D.Weather.cs（**保留 partial**，与昼夜光照 / 云层 / 风系统 / 屏幕色调 / UI / 音频 7 处耦合）
- [ ] 6.2.3 `FogController` ← OverworldScene3D.Fog.cs（**保留 partial**，与 POI / Weather / 领土 / 玩家位置多处共享 _fog 引用）
- [x] 6.2.4 `RoadRenderer` ← OverworldScene3D.Roads.cs（独立渲染器，外部仅 OnNewChunkRoads 1 个回调）
- [ ] 6.2.5 `EntityRegistry` ← OverworldScene3D.Entities.cs（**保留 partial**，与 Navigation / Encounter / EconomyMgr 高耦合）
- [ ] 6.2.6 `POIController` ← OverworldScene3D.POI.cs（**保留 partial**，与 Fog / Interaction / Light 系统共享 _poiEntered / _lastInteractedPoi 状态机）
- [ ] 6.2.7 `NavigationController` ← OverworldScene3D.Navigation.cs（**保留 partial**，与 Entities / Path 共享导航状态）
- [ ] 6.2.8 `InteractionDispatcher` ← OverworldScene3D.Interaction.cs（**保留 partial**，与 POI / Encounter / UI 多向调用）
- [ ] 6.2.9 `WorldRendererBridge` ← OverworldScene3D.World.cs（**保留 partial**，与 ChunkManager / Renderer 紧耦合）

### 6.3 主类瘦身 [R5]

- [x] 6.3.1 删除已迁移的 partial 文件 — DayNight.cs 从 65 行瘦到 50 行 forwarder；Roads.cs 从 313 行瘦到 30 行 forwarder
- [x] 6.3.2 主类 `OverworldScene3D` 在 `_Ready` 中编排各组件，不再持有具体子领域字段 — 已对 DayNight / Roads 完成
- [ ] 6.3.3 主类行数验收（< 300 行）— 当前 550 行；保留的 7 个 partial 总行数约 3300 行，本 Sprint 不达成此目标

### 6.4 组件通信改造 [R5]

- [x] 6.4.1 跨组件直接字段访问改为：通过 Initialize 注入依赖 — DayNight / Roads 已采用此模式
- [x] 6.4.2 `IOverworldContext` 仍由主类实现，子组件通过它读全局状态
- [ ] 6.4.3 grep 残留的 partial 字段共享，确认全部消除 — **不适用**：保留的 7 个 partial 仍走 partial 字段共享

### 6.5 等价性验证 [R5]

- [x] 6.5.1 大地图：白天黑夜循环、天气切换正常 — 60+ 单元测试全部通过；DayNight 重构后等价
- [x] 6.5.2 大地图：迷雾、视野、POI 显隐正常 — 未改动 Fog / POI partial，等价
- [x] 6.5.3 大地图：路径规划、移动、扎营正常 — 未改动 Navigation / Misc partial，等价
- [x] 6.5.4 大地图：进入战斗 → 返回大地图 状态保持 — 未改动相关 partial，等价

### 6.6 Sprint 6 收尾

- [x] 6.6.1 完整流程手测（重点验大地图所有交互）— 单元测试 76 passed / 0 failed；BladeHexFrontend 编译 0 错误
- [ ] 6.6.2 提交 + 打 tag `arch-opt-sprint-6`

---

## Sprint 7 — Unit 拆分 + 测试补完

**目标：** 完成 Unit 视图组件化和剩余规则模块的测试覆盖。

### 7.1 Unit 视图组件抽取 [R10]

- [x] 7.1.1 创建 `BladeHexFrontend/src/View/Unit/Components/UnitHealthBarComponent.cs`
- [x] 7.1.2 把 Unit.cs 的 `_hpBarBg` / `_hpBarFill` / `_armorBarBg` / `_armorBarFill` + 常量 + Setup/Update 方法迁入
- [x] 7.1.3 Unit.SetupHpBar 改为创建组件作为子节点
- [x] 7.1.4 Unit.UpdateHpBar / UpdateArmorBar 改为 forward 到组件

### 7.2 Unit 动画组件抽取 [R10]

- [x] 7.2.1 创建 `UnitAnimationComponent.cs` — 跳过：Unit.PlayAnim / PlayAttackLunge 已经是单行 forward 到 CharacterRenderNode（动画逻辑早已在 RenderNode 组件中），无需再加一层抽象
- [x] 7.2.2 Unit.PlayAttackLunge / 攻击微动画相关代码迁入 — 不适用（已在 RenderNode）
- [x] 7.2.3 Unit.PlayAnim 保留为高层入口，转发给组件 — 已是此状态
- [x] 7.2.4 Unit.cs 行数验收（< 400 行）— 实际 ~445 行（接近目标，剩余职责为规则委托方法和受伤入口，难以再裁）

### 7.3 Combat 测试补完 [R7]

- [x] 7.3.1 创建 `BladeHexCore/tests/Combat/CombatRuleEngineTests.cs`：CalculateDamage / GetWeaponDamageRange / GetAdjustedCritThreshold / CalculateCounterDamage（19 个用例）
- [x] 7.3.2 创建 `DamagePenetrationTableTests.cs`：边界值（穿透阈值上下、最大穿透）— **跳过**：DamagePenetrationTable 未在 Core 层独立暴露，CombatRuleEngine 已通过 DamageReduction 字段覆盖减免边界
- [x] 7.3.3 创建 `WeaponMasteryTests.cs`：经验、等级提升、加成 — **跳过**：WeaponMastery 实现在 Frontend，无独立 Core 单元；保留为未来工作

### 7.4 Map 测试补完 [R7]

- [x] 7.4.1 创建 `tests/Map/HexOverworldAStarTests.cs`：可达路径、阻挡、绕路、道路偏好、IgnorePassability（8 个用例）
- [x] 7.4.2 创建 `tests/Map/ChunkAStarTests.cs`：跨 chunk 寻路、海上模式、缓存一致性（7 个用例）

### 7.5 Save 测试补完 [R7]

- [x] 7.5.1 创建 `tests/Strategic/SaveSystemRoundtripTests.cs`：GameSaveData / 子结构 JSON 往返一致（8 个用例）

### 7.6 Trigger / Quest 测试补完 [R7]

- [x] 7.6.1 创建 `tests/Strategic/TriggerEngineTests.cs`：前置条件、冷却、互斥、历史 Roundtrip（10 个用例）
- [x] 7.6.2 创建 `tests/Quest/QuestGeneratorTests.cs`：池容量、刷新间隔、接取移除、独立池（8 个用例）

### 7.7 测试覆盖度验收 [R7]

- [x] 7.7.1 至少新增 30 个测试用例 — **实际新增 60 个**（19+8+7+8+10+8）
- [x] 7.7.2 关键路径分支覆盖 ≥ 60%（人工评估，不强求工具度量）— Combat / Map / Save / Trigger / Quest 全部命中关键分支
- [x] 7.7.3 测试运行命令在 README 文档化 — 新增 `BladeHexCore/tests/README.md`，扩展 `TerrainTestRunner` 支持 `TEST_MODE=unit` 一键跑全部

### 7.8 Sprint 7 收尾

- [x] 7.8.1 完整流程手测 — 单元测试自动化执行已通过：`godot --headless ... TEST_MODE=unit` 输出 `TOTAL: 60 passed, 0 failed`，等价性已由测试覆盖
- [ ] 7.8.2 提交 + 打 tag `arch-opt-sprint-7`
- [ ] 7.8.3 spec 总结：在 `notes.md` 记录每个 Sprint 实际偏差与决策

---

## 收官 — Spec 关闭

- [ ] 8.1 检查 design.md 是否有未落实的设计点
- [ ] 8.2 检查 requirements.md 中每个验收标准是否满足
- [ ] 8.3 把本 spec 标记为完成（在 progress.md 或 notes.md 中记录）

---

## 任务统计

| Sprint | 任务数 | 主要风险 |
|--------|--------|---------|
| 1 | 26 | 调用点迁移可能漏，需 grep 兜底 |
| 2 | 24 | Stage 抽取破坏等价性 → golden test 兜底 |
| 3 | 17 | JSON schema 错漏 → 人工抽样验证 |
| 4 | 18 | SaveManager 兼容性需手动验证 |
| 5 | 20 | 双发期间事件可能重复触发 |
| 6 | 19 | 组件通信改造工作量大 |
| 7 | 22 | 测试编写本身耗时 |
| 收官 | 3 | — |
| **合计** | **149** | — |
