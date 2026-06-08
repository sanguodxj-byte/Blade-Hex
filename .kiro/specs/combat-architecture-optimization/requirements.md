# 战斗架构优化 — Requirements

## 背景

Blade&Hex 战斗子系统长期演进中积累了结构性技术债：

- **CombatSceneBase.cs 上帝类**（2303 行）：承担 95% 战斗交互，包含环境/相机/小地图/单位放置/视野/高亮/部署/回合 UI/输入/技能/移动/攻击/法术/物品/AI 收尾/结算面板。
- **死亡/移除单位的 SSOT 不统一**：同一件事有四五个入口，幂等保护散落在各处。
- **技能/法术结果仍是弱类型 Dictionary**：`UseSkill` / `UseCareerSkill` 返回 `Godot.Collections.Dictionary`，调用方靠字符串键解析，场景/CombatManager/SkillEffectExecutor 各自处理一部分。
- **AIController 既是规划又是执行又是表现**：543 行内既要决策、又要调度寻路、又要播动画、又要写日志。
- **CombatResolver 修正项与规则解算混在一起**：498 行的 `ResolveAttack` 里手工累加 9 个修正项。
- **Headless 与 Frontend 战斗规则不完全对齐**：Headless 明确放弃 LOS/侧翼/冲锋/法术/状态/士气，AI 调参报告与玩家体验存在结构性偏差。

本 spec 按 ROI 分三阶段推进，每阶段行为不变、可独立 PR。

## 总体原则

- **行为不变**：所有改动保持已有玩法、数值、UI 行为完全一致。
- **小步快跑**：每个需求独立可交付、可回退，避免大爆炸式重构。
- **测试先行**：涉及战斗规则变动的改动必须先补回归测试。
- **不引入新框架**：不引入 DI 容器、不切换 ECS。
- **保留 Core 不依赖 Godot 渲染的既有禁令**。

## 范围

✅ **In Scope**
- CombatSceneBase 组件化拆分
- 单位死亡 SSOT 统一
- 技能结果类型化
- AI 三段式分离（Planner / Translator / Presenter）
- CombatResolver 修正项流水线化
- LOS/Facing/Morale 纯逻辑部分迁入 Core
- Career/Passive/Lua 技能执行器收编
- 死代码与冗余兼容路径清理

❌ **Out of Scope**
- 战斗数值重平衡
- 新技能/新职业设计
- 渲染管线、Shader、视觉效果改造
- 多人/网络同步

## 阶段划分

| 阶段 | 主题 | 关联需求 |
|------|------|----------|
| 1 🔴 必做 | CombatSceneBase 拆分 + 死亡 SSOT + 技能类型化 | C1, C2, C3 |
| 2 🟡 强烈建议 | AI 三段分离 + 修正流水线 + Core 回收 LOS/Facing/Morale | C4, C5, C6 |
| 3 🟠 完整 | 技能执行器收编 + 清理 | C7, C8 |

---

## C1 — CombatSceneBase 上帝类拆分

### 用户故事

作为开发者，我希望 CombatSceneBase 拆成可独立装配的 Node 组件，每个组件只负责一个子领域，主类只负责生命周期编排。

### 现状

- `CombatSceneBase.cs` 2303 行，`OnCellClicked` / `OnActionSelected` 都是数百行 switch，`HandleAttack` 一条龙耦合（调 CombatResolver → 读 `result["hit"]` 字符串 → 播伤害数字 → 改 UI → 移除单位 → 更新战力条）。
- Sprint 6 的 OverworldScene3D 已成功拆成 9 个 partial 再转组件，本需求借鉴同一模式。

### 验收标准

1. WHEN CombatSceneBase 被拆分时 THEN 它 SHALL 退化为仅负责组件装配和生命周期编排的主类，目标 < 300 行。

2. WHEN 组件被定义 THEN 它们 SHALL 至少包含以下独立 Node 组件：
   - `CombatCameraController`：相机、AABB、UI insets
   - `CombatInputController`：`OnCellClicked` / `OnCellRightClicked` / 长按检视 / 部署点击
   - `CombatHighlightController`：移动范围 / 攻击范围 / hover / 攻击叠加层
   - `CombatDeploymentController`：部署阶段、确认按钮
   - `CombatActionDispatcher`：`OnActionSelected` 的巨大 switch → 命令分派
   - `CombatResultPresenter`：`OnCombatEndedInternal`、结算面板、BGM 切换

3. WHEN 组件间通信 THEN 首选 `EventBus` 强类型事件，禁止跨组件直接访问私有字段。

4. WHEN 组件依赖 SceneTree 类型时 THEN 通过 `[Export]` 在 .tscn 场景文件中显式装配，主类只负责编排顺序。

### 完成定义

- CombatSceneBase.cs < 300 行
- 6 个及以上独立 Node 组件存在
- 主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档 全流程手测通过

---

## C2 — 单位死亡 SSOT 统一

### 用户故事

作为开发者，我希望"单位从战场死亡并清理"有且只有一个正确入口，消除四处散落的幂等保护。

### 现状

场景里存在 4~5 处不同的"清场逻辑"：
- `HandleAttack` 走 `_combatUi.RemoveEnemy` + `cell.Occupant = null`
- `CheckAndResolveUnitDeaths` 又一份
- AI 路径走 `OnUnitKilled` 适配器
- `CombatManager.HandleUnitKilled` 又一份
- `UnitRegistry` 还监听 `TreeExited` 自动清

### 验收标准

1. WHEN BattleUnitModel.ApplyDamage 导致 HP 归零 THEN 规则层 SHALL 只负责标记死亡状态，不直接触发战场清理。

2. WHEN 单位需要从战场清理时 THEN 唯一入口 SHALL 是 `CombatManager.HandleUnitKilled`：
   - 移除格子占用
   - 刷 UI
   - 刷战力条
   - 维护 InitiativeQueue
   - 播 SFX
   - 发布 `UnitDiedEvent` 供订阅者监听

3. WHEN 订阅者需要响应单位死亡时 THEN 它们 SHALL 通过 `EventBus.Publish(new UnitDiedEvent(...))` 订阅，禁止直接 `cell.Occupant = null` / `_combatUi.RemoveEnemy(...)`。

4. WHEN 清理完成 THEN 先攻队列、单位注册表、格子占用 SHALL 同时达到一致状态（原子性）。

### 完成定义

- 全局搜索 `cell.Occupant = null` 仅在 `CombatManager.HandleUnitKilled` 内出现
- `TreeExited` 清场逻辑移除或转发到统一入口
- 至少手动触发 3 次单位死亡（玩家/敌方/群攻）验证一致性

---

## C3 — 技能结果类型化

### 用户故事

作为开发者，我希望 `UseSkill` / `UseCareerSkill` 返回强类型结果，而不是靠字符串键从 Dictionary 里读字段。

### 现状

- `UseSkill` / `UseCareerSkill` 返回 `Godot.Collections.Dictionary`
- 调用方靠字符串键（`"success"` / `"results"` / `"status_effects"` / `"type" == "teleport"` / `"destination"`）解析
- `CombatManager.ProcessTeleportResults` / `ApplyStatusEffects` + `CombatSceneBase.CheckAndResolveUnitDeaths` 三处字符串解析
- Sprint 5 已显式跳过 `SkillExecutionResult`

### 验收标准

1. WHEN 技能执行返回结果时 THEN 新的内部 API SHALL 引入 `SkillExecutionResult record + SkillSubResult` 多态：
   - `DamageEvent`
   - `TeleportEvent`
   - `StatusEffectApplication`
   - `ResultText`

2. WHEN 旧 API 仍被调用时 THEN 保留 `Godot.Collections.Dictionary` 入口作为 `[Obsolete]` 适配器（`ToDictionary()` 方法），确保存量代码不中断。

3. WHEN SkillExecutionResult 被消费时 THEN `CombatManager.ProcessTeleportResults` / `ApplyStatusEffects` / `CheckAndResolveUnitDeaths` 三处字符串解析 SHALL 被强类型分支替代。

4. WHEN 类型化结果引入后 THEN 它 SHALL 成为后续 Headless 接入技能、Lua 桥调用的前置条件。

### 完成定义

- `SkillExecutionResult` 及子类型定义完成
- 旧 Dictionary 标记 `[Obsolete]`
- 至少 5 个技能路径（近战/远程/法术/职业技能/Lua）返回类型化结果
- 全量手测：每个技能类型触发一次，验证行为一致

---

## C4 — AI 三段式分离

### 用户故事

作为开发者，我希望 AI 决策是纯函数（可在 Headless 跑），与动画/日志/Godot 渲染完全解耦。

### 现状

- `AIController` 543 行：既要决策 (`DecideActionForUnit`)、又要调度寻路 (`PrepareMoveThenAttack` / `TrimPathToBudget`)、又要播动画 (`ExecuteAttack` 直接调 `_attackAnimator.PlayAttack`)、又要写日志、扣 AP、刷 UI。

### 验收标准

1. WHEN AI 决策被拆分时 THEN 它 SHALL 产出以下三个独立类：
   - `AIPlanner`：纯函数，接收 `CombatState` + `Unit` 返回 `AIAction`，0 依赖 Godot
   - `AICommandTranslator`：把 `AIAction` 翻译成 `ICommand`，入 `CommandHistory`
   - `AIPresenter`：订阅命令执行结果，负责动画/日志/镜头跟随

2. WHEN 命令系统升级后 THEN 玩家和 AI SHALL 共用同一条 Command 管道（`ICommand`），悔棋/录像/Headless 几乎免费拿到。

3. WHEN AIPlanner 是纯函数时 THEN 它 SHALL 能在 `BladeHexCore` 层直接单元测试，无需启动 Godot。

4. WHEN AIPresenter 订阅命令结果时 THEN 它 SHALL 通过 `EventBus` 接收事件，不直接持有 `CombatAttackAnimator` 引用。

### 完成定义

- `AIController.cs` 被拆成 3 个及以上独立类
- AI 决策可在纯 C# 单元测试中运行
- 战斗 AI 行为与拆分前手动对比一致

---

## C5 — CombatResolver 修正项流水线化

### 用户故事

作为开发者，我希望新增攻击修正项时不需要修改 `CombatResolver.ResolveAttack` 主方法。

### 现状

- `CombatResolver.ResolveAttack` 498 行内手工累加：高地优势 / 士气 / 视线惩罚 / 高度差 / 渡河 / 包夹 / 包围 / 伤势惩罚 / 节点暴击率
- 修正项加到第 9 个，下一项怎么挂不知道

### 验收标准

1. WHEN 修正项被抽取时 THEN 它们 SHALL 变为 `IAttackModifier` 列表，每个修正器实现 `Apply(AttackContext ctx, ref AttackInput input)`。

2. WHEN CombatResolver 被重构时 THEN 它 SHALL 只做装配：
   ```csharp
   var input = new AttackInput { ... };
   foreach (var mod in _modifiers) mod.Apply(ctx, ref input);
   var roll = CombatRuleEngine.RollAttack(input);
   ```

3. WHEN 新增修正项时 THEN 开发者 SHALL 只需：新建类实现 `IAttackModifier` → 在装配列表注册 → 写单测。

4. WHEN 修正器被实现时 THEN 每个 SHALL 单独可测，输入输出明确。

### 完成定义

- `IAttackModifier` 接口定义完成
- 至少 5 个已有修正项迁移到独立类
- `CombatResolver.ResolveAttack` 主方法 < 100 行
- 新增修正项的单测模板可用

---

## C6 — LOS/Facing/Morale 核心层回收

### 用户故事

作为开发者，我希望 Headless 模拟器能用上与 Frontend 同样的 LOS/侧翼/士气规则，让 AI 调参报告贴近实战。

### 现状

- `HeadlessCombatLoop` 顶部明确写"No line-of-sight / flanking / charge / spells / status / morale"
- `LineOfSight` / `FacingSystem` / `MoraleSystem` 中纯几何+纯数值部分仍可迁入 Core
- 它们大部分只用 `Vector2I` 和 `HexCell` 数据

### 验收标准

1. WHEN LOS 纯逻辑被迁移时 THEN `LosCore` 已存在（无 Node），`LineOfSight`（Frontend）SHALL 退化为 thin adapter 调用 `LosCore`。

2. WHEN FacingSystem 纯几何被迁移时 THEN 方向计算 / 侧翼判定逻辑 SHALL 进入 Core（`FacingCore`），Frontend 保留可视化叠加层。

3. WHEN MoraleSystem 纯数值被迁移时 THEN 士气效果计算（HitBonus / FumbleRate 等）SHALL 进入 Core（`MoraleCore`），Frontend 保留 UI 表现。

4. WHEN Core 层回收完成后 THEN `HeadlessCombatLoop` SHALL 可选择性打开包夹/掩体/士气开关，让模拟器输出贴近实战。

### 完成定义

- `LosCore`、`FacingCore`、`MoraleCore` 在 Core 层可用
- Frontend adapter 层 < 50 行
- Headless 模拟结果与 Frontend 至少 3 个场景对比通过

---

## C7 — 技能执行器收编

### 用户故事

作为开发者，我希望 CareerSkillExecutor / SkillEffectExecutor / PassiveSkillResolver / LuaSkillBridge 共用同一套调度路径，而不是各自走不同入口。

### 现状

- `CareerSkillExecutor`（858 行）和 `SkillEffectExecutor` 是重复的两条调度路径
- `PassiveSkillResolver`（585 行）以 static 方法直查 SkillTree，调用点散落在 CombatResolver 多处
- `LuaSkillBridge`（630 行）直接读 SkillTree 字段

### 验收标准

1. WHEN 主动技能被调度时 THEN `CareerSkillExecutor` 和 `SkillEffectExecutor` SHALL 合并到一个 `ISkillEffectHandler` 接口注册表（按 `effectId` 路由）。

2. WHEN 被动技能被查询时 THEN `PassiveSkillResolver` 的查询接口 SHALL 折叠成 `Unit.GetPassiveModifiers()` 一个聚合调用，返回 `PassiveModifierSet`。

3. WHEN Lua 桥调用技能时 THEN `LuaSkillBridge` SHALL 只调"已注册的 effect handler"，不再直接读 SkillTree 字段。

4. WHEN 收编完成后 THEN 技能效果有 60+ 种，但它们 SHALL 统一通过 `ISkillEffectHandler` 路由，不再有并行入口。

### 完成定义

- `ISkillEffectHandler` 接口 + 注册表上线
- 至少 10 个技能迁移到新注册表
- `CareerSkillExecutor` 和 `SkillEffectExecutor` 的重复路径消除
- Lua 桥不再直接读 SkillTree

---

## C8 — 轻量清理

### 用户故事

作为开发者，我希望删除死代码、统一 fallback、把战利品生成规则从场景文件移到 LootTable。

### 清理项

1. **删除 `UpdateFov`**：注释自承"无视野机制：所有格子和单位永久可见"，但每次调用仍遍历所有 cell 和敌方单位。
2. **删除 `CombatManager.ChangeState`**：Deprecated，确认无调用点后移除。
3. **`SpawnHardcodedPlayer` / `SpawnHardcodedEnemies`**：改成调用 `CharacterGenerator` + `EquipmentGenerator`，移除场景里的 `UnitData` 字面量。
4. **`CombatScene.GenerateLoot` 抽进 `LootTable`**：目前 CombatScene 与 LootTable 各持半套掉落规则，合并到 `LootTable` 统一。
5. **删除 `OnAiDone` 与 `OnAiSingleUnitDone` 的冗余并存**：确认只留一个。

### 验收标准

1. WHEN 死代码被删除时 THEN 编译 SHALL 通过，运行期无异常。
2. WHEN fallback 被统一时 THEN 游戏 SHALL 仍能从零开始正常生成单位。
3. WHEN 掉落规则被合并后 THEN `LootTable` SHALL 成为战利品生成的唯一入口。

### 完成定义

- 上述 5 个清理项全部完成
- 编译通过，运行期无警告（`[Obsolete]` 除外）
- 手动验证：战斗结束 → 战利品面板正常显示

---

## 跨需求约束

- **不破坏 Core 渲染禁令**：任何改动不得让 `BladeHexCore/**` 引入 `Texture2D`、`Material`、`Mesh`、`Node3D` 等渲染类型。
- **每个需求独立 PR/提交**：避免大爆炸合并。
- **每阶段结束做一次完整流程手测**：主菜单 → 新游戏 → 大地图 → 战斗 → 保存 → 读档。
