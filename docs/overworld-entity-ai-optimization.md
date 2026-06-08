## Blade&Hex 大地图实体 AI 架构优化建议

### 一、状态机层级化重构

当前的 AIState 是一个 11 值的扁平枚举，所有状态转换逻辑散布在 DailyDecisionProcessor、BattleResolver、SiegeProcessor 中。领主军队尤其严重，DecideLordArmy 方法超过 120 行、嵌套 4 层 if-else，且 Army 元帅/跟随者分支直接跳过了 DecideLordArmy 的主流程，导致状态流转路径难以追踪。

建议引入两级状态机：第一层为宏观状态（Roaming、War、Army），第二层为子状态（Idle、Patrolling、Chasing 等）。实现方式可以用一个 `IStateHandler` 接口配合 Dictionary 分派，不需要引入外部状态机库。这样每个宏观状态内部维护自己的子状态转换表，DecideLordArmy 的复杂度可以拆解为三个独立方法。

### 二、DailyDecisionProcessor 职责拆分

当前 DailyDecisionProcessor.ProcessDailyDecisions 同时承担了三件事：调用 OnDayPassed、按类型分派决策、清理死亡/过期实体。建议将死亡/过期清理移到调用方（OverworldEntityManager.OnDayPassed），让 ProcessDailyDecisions 只负责纯粹的决策分派。这样 ProcessDailyDecisions 的入参可以从 `List<OverworldEntity>` 改为 `IEnumerable<OverworldEntity>`，避免隐式的列表修改副作用。

### 三、随机数引擎注入

当前所有处理器（DailyDecisionProcessor、EncounterEntitySpawner、OverworldAIResolver）各自持有 `static readonly Random`，这有两个问题：一是全局共享 Random 在多线程场景下不安全（虽然当前 Godot 主线程模式不受影响，但 headless 模拟测试可能并行跑），二是无法复现特定的随机序列用于调试。

建议统一通过构造函数或 SetSeed 方法注入随机数实例。OverworldAIResolver 的 `_random` 尤其需要处理，因为它是 `static class`，无法注入实例字段，建议改为接受 `Random` 参数的方法重载，或在测试时使用固定种子。

### 四、BattleResolver 视野检测与决策解耦

CheckVisionDetection 当前在 BattleResolver.ProcessEntityInteractions 中与战斗结算混合执行。这意味着"感知"和"行动"耦合在同一个循环里。建议将视野检测结果收集为一个 `List<VisionEvent>` 中间产物，然后由 DailyDecisionProcessor 在下一轮决策时消费这些事件。这样感知和行动的时序关系更清晰，也便于日志记录和调试回放。

### 五、追击路径优化

EncounterEntitySpawner.BuildChasePath 使用 200px 步长的直线采样加 60 度偏转绕行，在复杂地形（如 U 型山谷）下容易出现卡墙或反复横跳。建议引入"有限前瞻 A*"——只搜索前方 5-8 个节点的局部 A*，计算量远小于全路径 A*，但比直线采样更可靠地处理凹形障碍。

### 六、LOD 阈值配置化

EntityLodController 的 5000/5500px 阈值和 EncounterEntitySpawner 的 2500px 收容距离均为硬编码常量。不同性能的设备和不同规模的地图可能需要不同阈值。建议提取为 `OverworldPerformanceConfig` 数据类，从项目设置或存档中读取，方便调优。

### 七、OverworldAIResolver 返回类型改进

ResolveBattle/ResolveSiege/ResolveRaid 返回 `Godot.Collections.Dictionary`，调用方需要字符串键访问和类型转换。建议定义 `BattleResult` 结构体或 record，包含 AttackerWon、AttackerDestroyed、DefenderDestroyed、AttackerLosses、DefenderLosses 等字段。这能消除拼写错误风险并提高 IDE 补全体验。在当前双工程架构下，Core 层定义结构体是零成本的。

### 八、测试覆盖率缺口

现有测试覆盖了 SpatialIndex（18 用例）、ArmySystem（22 用例）、WarSystem（32 用例），但以下关键子系统缺乏独立测试：

- DailyDecisionProcessor 各实体类型行为（冒险者巡逻、掠夺队生命周期、商队往返、怪物领地、领主多层决策）
- BattleResolver 的 CheckVisionDetection 追击/逃跑阈值逻辑
- EncounterEntitySpawner 的生成条件、类型权重、追击 AI
- SiegeProcessor 的围攻天数结算、回援距离检查、招募恢复
- MovementProcessor 的路径到达回调和追击加速

下方新增的 OverworldEntityAITests.cs 覆盖了以上全部缺口。
