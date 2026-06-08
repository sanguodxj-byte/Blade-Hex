# 战斗架构优化 — Tasks

## 当前状态

已完成：
- ✅ T1.1 CombatCameraController 提取（独立组件，CombatSceneBase 通过兼容层转发）
- ✅ T3.1 SkillExecutionResult + 子类型定义
- ✅ T3.2 旧 Dictionary API 标记 [Obsolete]

已创建骨架但未迁移逻辑：
- 🔲 CombatInputController（已有键鼠输入转发，但 OnCellClicked/OnCellRightClicked 逻辑仍在 Base）
- 🔲 CombatHighlightController（已有高亮/hover 方法，但 Base 仍保留重复实现）
- 🔲 CombatDeploymentController（空壳，TODO 状态）
- 🔲 CombatActionDispatcher（空壳，TODO 状态）
- 🔲 CombatResultPresenter（空壳，TODO 状态）

CombatSceneBase 当前约 **2770 行**，目标 < 600 行。

---

## 阶段 1：CombatSceneBase 组件化拆分（C1）

### T1.2 — 迁移移动系统到 CombatMovementController

**目标**：将单位移动动画、路径跟随、AoO 检测逻辑提取为独立组件。

**涉及方法**（~200 行）：
- `MoveUnitTo(Unit, int, int, List<Vector2I>?)`
- `MoveUnitToAsync(Unit, int, int, List<Vector2I>?)`
- `MoveUnitAlongPath(Unit, List<Vector2I>)` — 含 AoO 检测 + 平滑移动
- `MoveUnitOneStep(Unit, Vector2I)` — 单格跳跃动画

**步骤**：
1. 新建 `Scenes/combat/CombatMovementController.cs`
2. 迁移上述 4 个方法，依赖通过构造注入：`HexGrid`、`CombatManager`、`CombatUI`、`CombatMinimapPanel`
3. 暴露 `async Task MoveUnitAsync(Unit, int, int, List<Vector2I>?)` 公共 API
4. CombatSceneBase 中 `MoveUnitTo` 改为转发调用
5. 删除 Base 中的原始实现

**验收**：
- 移动动画正常（单格/多格/跳跃抛物线）
- AoO 触发正常（借机攻击 → 伤害数字 → 可能中断移动）
- AI 移动正常
- 编译通过

---

### T1.3 — 填充 CombatDeploymentController

**目标**：将部署阶段全部逻辑迁移到已有的空壳组件。

**涉及方法**（~180 行）：
- `BeginDeploymentPhase()`
- `AutoPlaceUnitsAndStart()`
- `HighlightDeploymentZone()`
- `HandleDeploymentClick(HexCell)`
- `AdvanceToNextUnplacedUnit()`
- `AllUnitsDeployed()`
- `CreateDeployConfirmButton()`
- `UpdateDeployConfirmButton()`
- `OnDeployConfirmPressed()`

**涉及字段**：
- `_deploymentPhaseActive`、`_deploymentZoneCells`、`_unitsToPlace`
- `_currentDeployIndex`、`_selectedDeployUnit`、`_deployConfirmButton`

**步骤**：
1. 将上述方法和字段迁移到 `CombatDeploymentController`
2. 组件暴露 `IsActive` 属性供 Base 查询
3. 组件暴露 `event Action DeploymentCompleted` 事件
4. Base 的 `_Ready()` 中调用 `DeployCtrl.Begin(...)` 替代 `BeginDeploymentPhase()`
5. `OnCellClicked` 中的部署分支改为 `if (DeployCtrl.IsActive) { DeployCtrl.HandleClick(cell); return; }`
6. 删除 Base 中的原始实现和字段

**验收**：
- 部署区高亮正常
- 拖放单位到部署区正常
- 确认按钮出现/隐藏正常
- 自动放置（部署区不足时）正常
- 编译通过

---

### T1.4 — 填充 CombatActionDispatcher

**目标**：将 `OnActionSelected` 的巨型 switch（~230 行）迁移到已有的空壳组件。

**涉及方法**：
- `OnActionSelected(string action)` — 主分派逻辑
- `OnSpellSelected(SpellData spell)` — 法术选择回调
- `ResolveSkillTargetingInfo(string action)` — 技能瞄准信息解析
- `HighlightSkillRangeAction(string action)` — 技能范围高亮
- `IsSkillTargetCellValid(string action, HexCell cell)` — 技能目标验证
- `IsImmediateCastTargetType(string targetType)` — 立即释放判定

**步骤**：
1. 定义 `ICombatActionContext` 接口，暴露 Dispatcher 需要的状态：
   - `Unit? ActiveUnit`、`ActionMode CurrentMode`（get/set）
   - `HexGrid Grid`、`CombatManager Manager`、`CombatUI UI`
   - `SpellData? SelectedSpell`（get/set）、`string? SelectedSkillAction`（get/set）
2. CombatSceneBase 实现 `ICombatActionContext`
3. 迁移 switch 逻辑到 `CombatActionDispatcher.Dispatch(string action, ICombatActionContext ctx)`
4. 迁移辅助方法（ResolveSkillTargetingInfo 等）
5. Base 中 `OnActionSelected` 退化为一行转发

**验收**：
- 所有行动类型正常分派（move/attack/spell/item/defend/swap_weapon/end_turn/retreat/career_skill/skill_xxx/build_ladder/attack_gate）
- 法术选择面板正常
- 编译通过

---

### T1.5 — 提取 CombatHoverPreviewController

**目标**：将悬停预览逻辑（命中预览、路径折线、AOE 预览、AP 消耗预览）提取为独立组件。

**涉及方法**（~250 行）：
- `OnCellHover(HexCell cell)` — 主悬停逻辑
- `OnCellHoverExit(HexCell cell)` — 悬停退出
- `HandleSpellHover(HexCell cell)` — 施法瞄准悬浮预览
- `ShowSkillPreview(HexCell, SkillTargetingInfo)` — 技能效果预览 Tooltip
- `ClearAoePreview()` — 清除 AOE 预览
- `RefreshCurrentHover()` — 刷新当前悬停
- `OnActionHovered(string action)` — 行动按钮悬停高亮

**涉及字段**：
- `_hoverHighlightedCell`、`_attackRangeShownForHover`
- `_attackRangeOverlayCells`、`_aoePreviewCells`、`_aoePreviewCenter`

**步骤**：
1. 新建 `Scenes/combat/CombatHoverPreviewController.cs`
2. 迁移上述方法和字段
3. 依赖注入：`ICombatActionContext`（读取 ActiveUnit/ActionMode/SelectedSkill）、`HexGrid`、`CombatUI`、`CombatHighlightController`
4. 连接 HexGrid 的 CellMouseEntered/CellMouseExited 信号
5. Base 中删除原始实现

**验收**：
- 空地悬停显示移动路径折线 + AP 消耗
- 敌人悬停显示命中预览面板
- 施法瞄准模式悬停显示 AOE 范围
- 悬停退出正确清理所有预览
- 编译通过

---

### T1.6 — 填充 CombatResultPresenter

**目标**：将战斗结束处理逻辑迁移到已有的空壳组件。

**涉及方法**（~50 行）：
- `OnCombatEndedInternal(bool victory)` — 战斗结束主逻辑

**步骤**：
1. 迁移 `OnCombatEndedInternal` 到 `CombatResultPresenter.OnCombatEnded(bool victory)`
2. 组件持有 `CombatUI`、`CameraCtrl`、`AudioManager` 引用
3. Base 中保留 `HandleCombatEnd(victory)` 抽象方法调用（子类实现具体结算）
4. 删除 Base 中的原始实现

**验收**：
- 战斗胜利/失败结算面板正常
- BGM 切换正常
- 相机聚焦最后击杀正常
- 编译通过

---

### T1.7 — 提取 CombatSkillExecutor

**目标**：将技能/攻击/物品执行逻辑提取为独立组件，统一执行流程。

**涉及方法**（~400 行）：
- `HandleMove(HexCell cell)` — 移动执行
- `HandleAttack(HexCell cell)` — 攻击执行（含动画、伤害数字、死亡检测）
- `HandleSpell(HexCell cell)` — 法术/技能执行
- `HandleItem(HexCell cell)` — 物品使用
- `CheckAndResolveUnitDeaths(Dictionary skillResult)` — 死亡检测
- `ProcessSkillResultFeedback(Dictionary skillResult, Unit caster)` — 结果反馈

**步骤**：
1. 新建 `Scenes/combat/CombatSkillExecutor.cs`
2. 定义执行上下文：`_isExecutingAction` 锁、ActiveUnit、HexGrid、CombatManager 等
3. 迁移上述方法
4. Base 中 `OnCellClicked` 的攻击/移动/法术/物品分支改为调用 Executor
5. 删除 Base 中的原始实现

**验收**：
- 近战攻击正常（动画 + 伤害数字 + 死亡）
- 远程攻击正常（投射物 + 命中/未命中）
- 法术释放正常（AOE/单体/传送/Buff）
- 物品使用正常
- 并发锁正常（狂点不会重复消耗 AP）
- 编译通过

---

### T1.8 — CombatSceneBase 最终瘦身

**目标**：删除所有已迁移的字段和方法，Base 退化为编排层。

**步骤**：
1. 删除已迁移到各组件的私有字段
2. 删除已迁移的方法实现（保留转发桩或直接删除）
3. 清理 `_Ready()` 为纯编排逻辑（初始化各组件 → 连接事件 → 启动战斗）
4. 保留：
   - 抽象方法（`GenerateBattlefield`/`SpawnUnits`/`HandleCombatEnd`）
   - `ICombatSceneAdapter` 接口实现（thin adapter）
   - 组件 `[Export]` 字段
   - `RegisterAndInitUnit` / `PlaceUnitAt`（单位注册，供子类调用）
   - `OnTurnStarted` / `ExecuteAiTurnForUnit`（回合调度）
5. 验证行数 < 600 行

**验收**：
- CombatSceneBase.cs < 600 行（理想 < 400 行）
- 主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档 全流程通过
- CombatScene / QuickCombatScene 子类正常工作
- 编译通过

---

## 阶段 1 续：死亡 SSOT（C2）

### T2.1 — 统一死亡入口到 CombatManager.HandleUnitKilled

**步骤**：
1. 在 `CombatManager.HandleUnitKilled` 中实现原子性清理：
   - 幂等检查（`_deadUnits` HashSet）
   - 移除先攻队列
   - 清除格子占用（`cell.Occupant = null`）
   - 刷新 UI（移除单位图标）
   - 刷新战力条
   - 播放死亡 SFX
   - 检查战斗结束条件
2. 全局搜索 `cell.Occupant = null`，将散落的清场逻辑替换为调用 `HandleUnitKilled`
3. 全局搜索 `_combatUi.RemoveEnemy` / `RemoveAlly`，统一到 HandleUnitKilled 内部

**验收**：
- `cell.Occupant = null` 仅在 `HandleUnitKilled` 和 `MoveUnit*` 中出现
- 单次击杀/连续击杀/AOE 群杀 均正常
- 先攻队列、格子占用、UI 三者一致

---

### T2.2 — 发布 UnitDiedEvent 替代直接清场

**步骤**：
1. 定义 `UnitDiedEvent` 强类型事件（Unit, Killer, IsAlly）
2. `HandleUnitKilled` 末尾发布事件
3. 各订阅者改用事件监听：
   - CombatUI 移除单位
   - 战力条刷新
   - 小地图刷新
   - 经验/击杀统计

**验收**：
- 死亡后所有 UI 同步更新
- 无直接调用 `_combatUi.RemoveEnemy` 的散落代码

---

### T2.3 — 移除 TreeExited 自动清逻辑

**步骤**：
1. 确认 `UnitRegistry` 的 `TreeExited` 处理是否有其他依赖
2. 移除或改为转发到 `HandleUnitKilled`
3. 确保场景切换时不触发重复清理

**验收**：
- 场景切换无异常
- 无重复清理日志

---

## 阶段 1 续：技能结果类型化（C3）

### T3.3 — 迁移 ProcessTeleportResults 到类型化

**步骤**：
1. 在 `CombatManager.ProcessTeleportResults` 中，从 `SkillExecutionResult.SubResults` 读取 `TeleportEvent`
2. 替换原有的 `result["type"] == "teleport"` 字符串解析
3. 保留 fallback 路径处理旧 Dictionary（兼容期）

**验收**：
- 传送技能（闪现/瞬移）正常工作
- 传送后格子占用正确更新

---

### T3.4 — 迁移 ApplyStatusEffects 到类型化

**步骤**：
1. 从 `SkillExecutionResult.SubResults` 读取 `StatusEffectApplication`
2. 替换原有的 `result["status_effects"]` 字符串解析
3. 保留 fallback 路径

**验收**：
- 状态效果正常挂载（中毒/灼烧/减速等）
- Buff 图标正常显示

---

### T3.5 — 迁移 CheckAndResolveUnitDeaths 到类型化

**步骤**：
1. 从 `SkillExecutionResult.SubResults` 读取 `DamageEvent`
2. 用 `DamageEvent.WasKillingBlow` 驱动死亡清理
3. 调用统一的 `CombatManager.HandleUnitKilled`（依赖 T2.1）
4. 替换原有的字符串解析

**验收**：
- 技能造成的击杀正常触发死亡流程
- 群体技能多目标死亡正常

---

## 阶段 2：AI 三段分离（C4）

### T4.1 — 提取 AIPlanner（纯函数）

**步骤**：
1. 新建 `View/Combat/AI/AIPlanner.cs`
2. 定义 `CombatStateSnapshot`（纯数据，无 Godot 依赖）
3. 提取 `AIController.DecideActionForUnit` → `AIPlanner.Decide(Unit, CombatStateSnapshot)`
4. 返回 `AIAction` record（MoveAction/AttackAction/UseSkillAction/WaitAction）
5. 各 `AIStrategy*` 策略类改为接收 snapshot 而非 Node 引用

**验收**：
- AI 行为与拆分前一致（对比 AIBehaviorRegressionTests）
- AIPlanner 可在纯 C# 单元测试中运行

---

### T4.2 — 提取 AICommandTranslator

**步骤**：
1. 新建 `View/Combat/AI/AICommandTranslator.cs`
2. `AIAction` → `ICommand` 映射（MoveCommand/AttackCommand/UseSkillCommand/WaitCommand）
3. 命令入栈 `CommandHistory`

**验收**：
- AI 行动正确入栈
- CommandHistory 可回放

---

### T4.3 — 提取 AIPresenter

**步骤**：
1. 新建 `View/Combat/AI/AIPresenter.cs`
2. 订阅命令执行结果事件
3. 负责：动画播放、日志输出、镜头跟随
4. 移除 AIController 中直接调用动画/UI 的代码

**验收**：
- AI 行动动画正常播放
- AI 行动日志正常输出
- 镜头跟随 AI 单位正常

---

### T4.4 — AI 走 CommandHistory 管道

**步骤**：
1. AIController 的执行流程改为：Planner → Translator → Execute(ICommand) → Presenter
2. 移除 AIController 中直接修改 UI/扣 AP 的代码
3. 玩家和 AI 共用同一条 Command 管道

**验收**：
- AI 全回合正常执行
- 悔棋功能（如有）可回退 AI 行动

---

## 阶段 2 续：修正流水线（C5）

### T5.1 — 定义 IAttackModifier + AttackContext/Input

**步骤**：
1. 新建 `Core/src/Combat/Modifiers/IAttackModifier.cs`
2. 定义 `AttackContext`（Attacker/Defender/Grid/IsCharge/IsAoo）
3. 定义 `AttackInput`（AttackBonus/Advantage/Disadvantage/AppliedModifiers）

**验收**：
- 接口定义完成，编译通过

---

### T5.2 — 逐个迁移 9 个修正项到独立类

**步骤**：
1. 每个修正项一个类，实现 `IAttackModifier`：
   - `HighGroundModifier`
   - `ChargeModifier`
   - `MoraleModifier`
   - `CoverModifier`（LOS 惩罚）
   - `FlankingModifier`
   - `HeightDifferenceModifier`
   - `RiverCrossingModifier`
   - `EncirclementModifier`
   - `NodeCritModifier`（被动技能暴击率）
2. 每个 modifier 附带单元测试

**验收**：
- 9 个独立类存在
- 每个有对应单测
- 编译通过

---

### T5.3 — 重构 CombatResolver.ResolveAttack 主方法

**步骤**：
1. `ResolveAttack` 改为装配模式：构建 AttackContext → 遍历 modifiers → 调用 RollAttack
2. 主方法 < 100 行
3. 保留 `DamageResolutionParityTest` 通过

**验收**：
- `ResolveAttack` < 100 行
- 所有攻击场景（高地/冲锋/侧翼/包围/掩体）结果一致
- ParityTest 通过

---

## 阶段 2 续：Core 回收 LOS/Facing/Morale（C6）

### T6.1 — LosCore 扩展 / LineOfSight 变薄

**步骤**：
1. 验证 `LosCore` 当前接口是否覆盖 `LineOfSight` 的所有纯逻辑
2. 补充缺失的方法到 `LosCore`
3. `LineOfSight`（Frontend）退化为 thin adapter

**验收**：
- Frontend `LineOfSight` < 50 行
- LOS 计算结果与重构前一致

---

### T6.2 — 新增 FacingCore / FacingSystem 变薄

**步骤**：
1. 新建 `Core/src/Combat/FacingCore.cs`
2. 迁移方向计算 / 侧翼判定纯几何逻辑
3. Frontend `FacingSystem` 退化为 thin adapter + 可视化叠加层

**验收**：
- 侧翼判定结果一致
- Frontend adapter < 50 行

---

### T6.3 — 新增 MoraleCore / MoraleSystem 变薄

**步骤**：
1. 新建 `Core/src/Combat/MoraleCore.cs`
2. 迁移士气效果计算（HitBonus/FumbleRate 等）
3. Frontend `MoraleSystem` 退化为 thin adapter + UI 表现

**验收**：
- 士气效果计算结果一致
- Frontend adapter < 50 行

---

### T6.4 — HeadlessCombatLoop 打开可选开关

**步骤**：
1. `SimulationConfig` 新增 `EnableLineOfSight` / `EnableFlanking` / `EnableMorale`
2. `HeadlessCombatLoop` 条件调用 Core 层方法
3. 默认关闭（保持模拟速度），golden seed test 中强制打开

**验收**：
- 模拟结果与 Frontend 至少 3 个场景对比通过
- 默认模式性能无退化

---

## 阶段 3：技能执行器收编（C7）

### T7.1 — 定义 ISkillEffectHandler + 注册表

**步骤**：
1. 新建 `ISkillEffectHandler.cs`（`string EffectId` + `Execute(ctx)`）
2. 新建 `SkillHandlerRegistry.cs`（按 effectId 路由）

**验收**：
- 接口和注册表编译通过

---

### T7.2 — 合并 CareerSkillExecutor → 注册表

**步骤**：
1. 将 31 个 case 拆分为独立 handler 类（按属性分组放同一文件）
2. 注册到 `SkillHandlerRegistry`
3. `CareerSkillExecutor` 退化为路由调用

**验收**：
- 所有职业技能正常
- `CareerSkillExecutor` < 100 行

---

### T7.3 — 折叠 PassiveSkillResolver 到 Unit.GetPassiveModifiers()

**步骤**：
1. 新增 `Unit.GetPassiveModifiers()` 聚合方法
2. `PassiveSkillResolver` 改为内部实现
3. 调用点统一使用 `unit.GetPassiveModifiers()`

**验收**：
- 被动技能加成一致
- 散落的 `PassiveSkillResolver.Query*` 调用消除

---

### T7.4 — LuaSkillBridge 只调注册表

**步骤**：
1. `LuaSkillBridge` 不再直接读 SkillTree 字段
2. 通过 `SkillHandlerRegistry` 路由技能执行
3. Lua 返回值通过 `SkillExecutionResult` 类型化

**验收**：
- Lua 技能正常（blessing/poison_blade/time_warp 等）
- 无直接 SkillTree 字段访问

---

## 阶段 3 续：清理（C8）

### T8.1 — 删除 UpdateFov

- 删除方法 + 所有调用点
- 验收：编译通过，无运行期异常

### T8.2 — 删除 CombatManager.ChangeState

- 确认无调用点后删除
- 验收：编译通过

### T8.3 — 替换 SpawnHardcoded* 为 Generator

- `SpawnHardcodedPlayer` → 调用 `CharacterGenerator`
- `SpawnHardcodedEnemies` → 调用 `CharacterGenerator` + `EquipmentGenerator`
- 删除 UnitData 字面量
- 验收：从零开始游戏正常生成单位

### T8.4 — 合并 GenerateLoot 到 LootTable

- 提取 `CombatScene.GenerateLoot` → `LootTable.Generate`
- 删除 CombatScene 内的掉落规则
- 验收：战利品面板正常显示

### T8.5 — 合并 OnAiDone / OnAiSingleUnitDone

- 确认实际使用入口，删除冗余
- 验收：AI 回合结束正常

---

## 执行顺序与依赖

```
阶段 1（按顺序执行）：
  T1.2 (移动) ──→ T1.3 (部署) ──→ T1.4 (行动分派) ──→ T1.5 (悬停预览)
       ──→ T1.6 (结算) ──→ T1.7 (技能执行) ──→ T1.8 (瘦身)
       ──→ T2.1 → T2.2 → T2.3 (死亡 SSOT)
       ──→ T3.3 → T3.4 → T3.5 (技能类型化)

阶段 2（可并行）：
  T4.1 → T4.2 → T4.3 → T4.4 (AI 三段)
  T5.1 → T5.2 → T5.3 (修正流水线)
  T6.1 → T6.2 → T6.3 → T6.4 (Core 回收)

阶段 3：
  T7.1 → T7.2 → T7.3 → T7.4 (技能收编)
  T8.1 ~ T8.5 (清理，可穿插)
```

---

## 里程碑验收

| 里程碑 | 验收标准 |
|--------|----------|
| 阶段 1 完成 | CombatSceneBase < 600 行；死亡入口唯一；技能结果类型化 |
| 阶段 2 完成 | AI 可纯函数测试；ResolveAttack < 100 行；Headless 可选开关 |
| 阶段 3 完成 | 技能统一注册表；死代码清除；全流程手测通过 |

每阶段结束做一次完整流程手测：主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档。
