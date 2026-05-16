# 架构优化 — Requirements

## 背景

Blade&Hex 项目当前由 BladeHexCore（纯 C# 数据/规则层）和 BladeHexFrontend（Godot/视图层）组成，约 338 个 .cs 文件、3.4MB 源码。整体分层意图清晰，但在长期演进中积累了若干结构性问题，导致：

- 模块间通过单例和字符串路径强耦合，单元测试困难
- 部分核心类职责过重（WorldCreator、OriginSelect、Unit），单文件维护成本高
- 事件/技能等跨层通信使用弱类型 `Godot.Collections.Dictionary`，易出错
- 关键规则模块（战斗、寻路、存档）测试覆盖薄弱，重构信心不足
- 工程根目录散落十余个一次性 Python 修复脚本，污染版本库

本 spec 的目标是在不改变游戏外部行为（gameplay parity）的前提下，逐步收紧架构，让后续功能开发更顺畅。

## 总体原则

- **行为不变**：所有改动必须保持已有玩法、存档、UI 行为完全一致
- **小步快跑**：每个需求独立可交付、可回退，避免一次性大爆炸式重构
- **测试先行**：高风险改动（战斗规则、存档）必须先补回归测试再动结构
- **不引入新框架**：不引入 DI 容器、不引入 ECS 框架、不切换游戏引擎
- **保留分层**：维持 Core 不依赖 Godot 渲染类型的既有 steering 规则

## 范围

✅ **In Scope**
- C# 代码组织、依赖关系、命名空间、单例与服务边界
- 跨层通信的类型化与解耦
- 关键规则模块的回归测试
- 工程根目录清理

❌ **Out of Scope**
- 游戏内容设计（数值、技能、剧情）
- 渲染管线、Shader、视觉效果改造
- 多人/网络同步
- 性能优化（除非是上述结构改动的副产品）
- 切换到 ECS 或其他范式

## 阶段划分

按风险与依赖排序，前阶段是后阶段的地基：

| 阶段 | 主题 | 关联需求 |
|------|------|----------|
| 1 🔴 必做 | 单例治理 + 状态访问统一 | R1, R2 |
| 2 🟡 强烈建议 | 上帝类拆分（数据外置 + Pipeline） | R3, R4 |
| 3 🟠 大范围 | 场景组件化 + 事件类型化 | R5, R6 |
| 4 🟢 完整 | 测试覆盖 + 工程清理 | R7, R8, R9, R10 |

---

## R1 — 单例治理与减量

### 用户故事
作为开发者，我希望项目中的全局可访问对象有清晰的分类和访问方式，以便在新增功能时知道该建什么类、放在哪里、怎么调用。

### 现状
项目中至少存在以下静态 `Instance` 单例：`EventBus`、`AudioManager`、`UITheme`、`QuestManager`、`VFXManager`、`DebugConsole`、`HexCellMultiMeshBatcher`、`GameMenuManager`、`SkillTreeManager`，以及通过 `/root/GlobalState` 路径访问的 `GlobalState`。这些单例没有统一分类，部分本应是有限作用域的服务（如 QuestManager、SkillTreeManager）也被当作全局单例。

### 验收标准

1. WHEN 开发者查阅 steering 文档 THEN 项目 SHALL 提供一份明确的「全局对象分类指引」，区分：
   - **Autoload Singleton**（Godot 启动期注册，跨场景持久）
   - **Scene Service**（场景内单实例，场景结束即销毁）
   - **Plain Helper**（无状态工具类，纯静态方法）

2. WHEN 一个类被识别为「场景级」职责（如 QuestManager） THEN 它 SHALL 改造为 Scene Service，由所属场景持有，不暴露 `static Instance`。

3. WHEN 一个类必须保留 `static Instance`（EventBus、AudioManager、SkillTreeManager 等真正跨场景的） THEN 该 Instance 字段 SHALL 标注其单例类别和生命周期注释，并通过 `Globals.XxxName` 访问，禁止暴露 `GetInstance()` 等不一致的访问 API。

4. WHEN 单元测试需要替换某个服务（如 EventBus） THEN 该服务 SHALL 提供可注入的接口或可重置的静态方法，使测试无需启动 Godot 场景树。

5. IF 改动涉及现有 Autoload 注册 THEN `project.godot` 的 `[autoload]` 段 SHALL 同步更新，并在 PR 描述中列明。

### 完成定义
- 新建 `.kiro/steering/global-objects.md`，列出每个全局对象的类别、作用域、访问方式
- 至少 2 个伪单例（建议 QuestManager、SkillTreeManager）改造为 Scene Service
- 现有 `static Instance` 类完成分类注释

---

## R2 — 跨场景状态访问统一

### 用户故事
作为开发者，我希望访问 GlobalState 时不需要每次都写 `GetNode<GlobalState>("/root/GlobalState")`，并且不希望 GlobalState 变成什么都往里塞的"全局垃圾桶"。

### 现状
代码中至少 7 处直接使用字符串路径 `/root/GlobalState`（OriginSelect、QuickCombatSetup、QuickCombatScene、MainMenu、OverworldUI 等），且 GlobalState 同时持有：存档加载状态、世界生成参数、快速战斗配置、天气状态、玩家出身数据、设置接口。职责混杂。

### 验收标准

1. WHEN 业务代码需要访问全局状态 THEN 该代码 SHALL 通过 `GlobalState.Get()` 静态方法或注入获取，而非硬编码 `GetNode<GlobalState>("/root/GlobalState")`。

2. WHEN 全局状态被进一步组织 THEN 它 SHALL 按职责拆分为可识别的上下文对象：
   - `SaveContext`：存档加载状态、当前 SaveId
   - `WorldGenContext`：世界种子、世界大小
   - `QuickCombatContext`：快速战斗配置
   - `WeatherContext`：当前天气快照
   - `PlayerOriginContext`：出身选择数据
   
   GlobalState 作为这些上下文的聚合根存在，不直接持有原始字段。

3. WHEN 拆分完成 THEN 既有外部调用点 SHALL 通过适配器或迁移路径继续工作，分批切换。

4. WHEN 全局状态被新增字段 THEN 新增 SHALL 落入对应的子上下文，禁止直接加在 GlobalState 顶层。

### 完成定义
- 全部 `/root/GlobalState` 字符串路径消除
- GlobalState 顶层字段数量减少至少 50%
- 子上下文有独立单元测试

---

## R3 — 世界生成器职责拆分

### 用户故事
作为开发者，我希望 WorldCreator 在新增"世界生成阶段"（如海岛、河流、道路）时只需新增一个独立类，而不是在 1900+ 行的单文件里再加一组方法。

### 现状
`BladeHexCore/src/Strategic/WorldCreator.cs` 单文件约 1900 行，包含 30+ 个方法，覆盖：地形生成、平滑、河流、海岛、POI 放置、首都/国家 POI、野外 POI、铁路连接、渡船、遭遇密度计算、坐标工具等。所有逻辑挤在一个类里，方法之间通过 `chunks` 字典共享状态。

### 验收标准

1. WHEN WorldCreator 被重构 THEN 它 SHALL 退化为一个 Pipeline 协调者，仅负责：
   - 创建初始 `WorldBuildContext`
   - 按顺序调用各阶段
   - 返回最终 `WorldData`

2. WHEN 世界生成阶段被拆分 THEN 它 SHALL 至少产出以下独立类（每个 < 500 行）：
   - `TerrainStage`（GenerateAllTerrain + Smooth）
   - `RiverStage`（GenerateRivers + Stamp）
   - `IslandStage`（Islands + IslandPOI + FerryRoutes）
   - `POIStage`（Capital + Nation + Wild）
   - `RoadStage`（ConnectSettlements + RoadAStar + Stamp）
   - `EncounterDensityStage`

3. WHEN 各阶段被实现 THEN 它们 SHALL 通过显式的 `IWorldStage` 接口通信，输入输出明确，禁止共享可变全局状态。

4. WHEN 重构完成 THEN 同一种子生成的世界 SHALL 与重构前完全一致（确定性回归测试覆盖）。

### 完成定义
- WorldCreator.cs < 200 行
- 各 Stage 类独立可测
- 至少 3 个种子的世界生成回归测试通过

---

## R4 — 起源选择系统数据视图分离

### 用户故事
作为内容设计者，我希望可以在不改 C# 代码的情况下增删起源问答、调整属性加成、替换插图与物品奖励。

### 现状
`OriginSelect.cs` 单文件 1500+ 行，包含：UI 构建（数百行 Button/Label 创建）、5 个种族 × 4 道题硬编码问答数据、选项 → 物品映射表、选项 → 插图映射表、伙伴选项分支逻辑、音频接入、属性结算。

### 验收标准

1. WHEN 起源问答数据被外置 THEN 它 SHALL 存储为 JSON 资源（建议 `BladeHexCore/src/Data/origin/origin_questions.json`），结构包含：
   - 种族标识
   - 问题列表（文本 + 选项）
   - 每个选项的属性修正、物品奖励、插图 ID、伙伴标记

2. WHEN 起源选择 UI 被改造 THEN 业务代码 SHALL 通过 `OriginQuestionLoader` 读取 JSON，硬编码字典从 .cs 中消失。

3. WHEN UI 与逻辑分离 THEN OriginSelect SHALL 拆为：
   - `OriginSelectView`（UI 构建、控件管理，< 600 行）
   - `OriginSelectController`（流程控制、选择应用、状态保存）

4. WHEN 改造完成 THEN 玩家体验（问答顺序、选项文本、最终属性、插图、物品）SHALL 与改造前完全一致。

### 完成定义
- `origin_questions.json` 创建并通过 schema 校验
- OriginSelect.cs < 600 行
- 至少手动游玩 3 个种族验证一致性

---

## R5 — 场景控制器组件化

### 用户故事
作为开发者，我希望大型场景控制器（OverworldScene3D）由可独立装配的组件构成，每个组件可独立测试和替换。

### 现状
`OverworldScene3D` 通过 `partial class` 拆成 9 个文件（DayNight、Entities、Fog、Interaction、Misc、Navigation、POI、Roads、Weather、World）。物理上拆了文件，逻辑上仍是一个类——所有字段共享、`this` 互访、生命周期纠缠。CombatScene 也有类似情况。

### 验收标准

1. WHEN OverworldScene3D 被重构 THEN 现有 partial 文件 SHALL 转换为独立的 `Node` 子组件，每个组件：
   - 单独继承 Node 或 Node3D
   - 通过 `[Export]` 暴露依赖
   - 通过场景树挂载或 OverworldScene3D 编排创建

2. WHEN 组件拆分完成 THEN OverworldScene3D 主类 SHALL 仅负责组件编排和高层流程，不再持有具体子领域字段。

3. WHEN 组件之间通信 THEN 它们 SHALL 通过 `EventBus` 或显式的接口引用，禁止跨组件直接访问私有字段。

4. WHEN 改造完成 THEN 主菜单 → 出身选择 → 大地图 → 进入战斗 → 返回大地图 → 保存读档全流程 SHALL 与改造前行为一致。

### 完成定义
- OverworldScene3D 主文件 < 300 行
- 至少 5 个分领域作为独立组件存在
- 关键流程手动验证通过

---

## R6 — 事件总线类型化

### 用户故事
作为开发者，我希望订阅 `unit_died` 事件时由编译器告诉我数据字段是什么类型，而不是靠记忆从 `Godot.Collections.Dictionary` 里读 `"unit"` 字符串键。

### 现状
`EventBus.Publish` 使用 `Godot.Collections.Dictionary` 作为载荷，订阅者通过字符串键 `data["unit"]` 取值。技能结算、伤害计算等也大量返回 Dictionary。键拼错只能运行时发现。

### 验收标准

1. WHEN 事件被发布 THEN EventBus SHALL 同时支持：
   - **强类型路径**：`Publish<TEvent>(TEvent ev)` + `Subscribe<TEvent>(Action<TEvent>)`
   - **兼容路径**：保留现有 `Publish(string, Dictionary)` 直到全部迁移

2. WHEN 强类型事件被定义 THEN 它们 SHALL 是不可变的 `record` 或 `readonly struct`，放在 `BladeHex.Events.Payloads` 命名空间。

3. WHEN 既有事件被迁移 THEN 至少以下 5 个核心事件 SHALL 完成强类型化：
   - `UnitDamagedEvent`
   - `UnitDiedEvent`
   - `SkillUsedEvent`
   - `CombatEndedEvent`
   - `DayPassedEvent`

4. WHEN 内部技能/战斗结算返回 Dictionary THEN 它们 SHALL 引入对应的强类型 result 类型作为新的内部 API，Dictionary 仅在边界（例如旧订阅者兼容）保留。

### 完成定义
- 强类型 API 上线，至少 5 个事件迁移完成
- 旧 Dictionary API 标 `[Obsolete]`，但仍可工作
- 新增事件必须用强类型 API（在 steering 中明确）

---

## R7 — 关键模块测试覆盖

### 用户故事
作为开发者，我希望在重构核心规则模块（伤害结算、寻路、存档）时，由测试告诉我有没有改坏。

### 现状
仅找到 2 个测试文件：`TerrainGenerationTest`、`DamageResolutionParityTest`。CombatRuleEngine、HexOverworldAStar、SaveSystemV2、QuestGenerator、TriggerEngine 等关键模块无测试覆盖。

### 验收标准

1. WHEN 项目内补充测试 THEN 它 SHALL 至少覆盖以下模块的关键路径：
   - **CombatRuleEngine**：命中、暴击、护甲穿透、武器精通加成
   - **HexOverworldAStar / ChunkAStar**：可达路径、阻挡、地形代价
   - **SaveSystemV2**：序列化-反序列化往返一致
   - **TriggerEngine**：触发条件求值、历史去重
   - **WorldCreator/各 Stage**（与 R3 协同）

2. WHEN 测试被组织 THEN 它们 SHALL 放在 `BladeHexCore/tests/` 下，按模块划分目录，命名 `XxxTest.cs`。

3. WHEN 测试运行 THEN 它们 SHALL 不依赖 Godot 场景树（纯 C# 单元测试），可在 CI 中独立执行。

### 完成定义
- 至少 30 个新测试用例
- 关键路径分支覆盖 ≥ 60%
- 测试运行命令在 README 或 steering 中文档化

---

## R8 — 项目根目录清理

### 用户故事
作为开发者，我希望仓库根目录只有真正需要常驻的文件，一次性的修复脚本和构建日志不应该污染版本库。

### 现状
`Blade&Hex/` 根目录下存在 10+ 个 Python 一次性脚本（`fix_all_errors.py`、`clean_all_gd_refs.py`、`check_build.py` 等）和 15+ 个构建日志文本（`build_check.txt`、`build_core.txt`、`build_err.txt` 等）。

### 验收标准

1. WHEN 一次性脚本被处理 THEN 它们 SHALL 统一移入 `tools/scripts/legacy/` 子目录，并附 README 说明各脚本的用途和最后使用日期。

2. WHEN 临时脚本不再有用 THEN 它们 SHALL 直接从仓库删除（git history 仍可追溯）。

3. WHEN 构建日志文本被处理 THEN 它们 SHALL 全部删除，并将匹配模式（`build_*.txt`、`godot_*.txt`、`remaining_errors*.txt`）加入 `.gitignore`。

### 完成定义
- 根目录只剩项目配置（`.csproj`、`.sln`、`project.godot`、`icon.svg` 等）
- `.gitignore` 更新

---

## R9 — SaveManager v1 退役

### 用户故事
作为开发者，我希望项目中只存在一个存档系统，避免新代码不知道该用 SaveManager 还是 SaveManagerV2。

### 现状
`BladeHexFrontend/src/View/Data/` 同时存在 `SaveManager.cs` 和 `SaveManagerV2.cs`。

### 验收标准

1. WHEN SaveManager v1 被审计 THEN 它 SHALL 确认所有调用方已切换到 V2，无残留引用。

2. WHEN v1 被移除 THEN 旧版存档文件 SHALL 仍可被 V2 加载（通过迁移代码或确认格式兼容）。

3. WHEN 移除完成 THEN 项目中仅存在 SaveManagerV2，且建议改名为 `SaveManager`（原 v1 文件已不存在）。

### 完成定义
- SaveManager.cs 删除
- 至少手动验证一次旧存档加载

---

## R10 — Unit 视图组件化

### 用户故事
作为开发者，我希望 Unit 类只负责 Unit 自身的状态和规则委托，不应同时负责 HP 条、装甲条、点击区域、动画播放等视觉细节。

### 现状
`Unit.cs` 当前同时持有：核心战斗状态（HP/AP/位置）、规则委托（GetMaxHp/GetAc 等）、HP 条 4 个 Sprite3D 字段及其更新逻辑、装甲条字段及其更新逻辑、攻击微动画、点击碰撞区域。

### 验收标准

1. WHEN Unit 被拆分 THEN HP 条与装甲条 SHALL 抽离为独立组件 `UnitHealthBarComponent : Node3D`，作为 Unit 的子节点挂载。

2. WHEN 攻击动画被抽离 THEN 攻击突进 / 受击反馈 SHALL 由 `UnitAnimationComponent` 处理。

3. WHEN 改造完成 THEN Unit.cs 的字段数量 SHALL 减少至少 30%，且类长度 < 400 行。

4. WHEN 改造完成 THEN 既有战斗中的视觉表现 SHALL 与改造前一致。

### 完成定义
- Unit.cs < 400 行
- 至少 2 个视图组件独立存在
- 战斗手动验证视觉一致

---

## 跨需求约束

- **不破坏 Core 渲染禁令**：任何改动不得让 `BladeHexCore/**` 引入 `Texture2D`、`Material`、`Mesh`、`Node3D` 等渲染类型（保留既有 `core-no-render-types.md` steering）
- **每个需求独立 PR/提交**：避免大爆炸合并
- **每阶段结束做一次完整流程手测**：主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档
