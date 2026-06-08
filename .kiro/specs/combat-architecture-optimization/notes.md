# 战斗架构优化 — Notes

> 运行时笔记：记录决策日志、偏差、待讨论事项。

## 2026-05-26 — Spec 创建

### 现状盘点确认

- `CombatSceneBase.cs` 实测 2303 行（比用户报告中 2215 行略多，可能近期有变更未计入）。
- `CombatResolver.cs` 实测 518 行（比用户报告 498 行略多）。
- `AIController.cs` 实测 558 行（比用户报告 543 行略多）。
- `CareerSkillExecutor.cs` 实测 875 行（比用户报告 858 行多 17 行）。
- `PassiveSkillResolver.cs` 实测 607 行（比用户报告 585 行多 22 行）。
- `LuaSkillBridge.cs` 实测 631 行（与用户报告一致）。
- `HeadlessCombatLoop.cs` 实测 1202 行（与用户报告一致）。

### 观察到的细节

1. **CombatSceneBase.cs** 字段数量超过 40+，`_Ready()` 方法超过 200 行。`OnCellClicked` 含 6 段独立分支，`OnActionSelected` 含 switch 嵌套 if。
2. **CombatManager.cs** 中 `ChangeState` 已标注 deprecated。
3. **CombatScene.cs** `SpawnHardcodedPlayer` / `SpawnHardcodedEnemies` 直接 new `UnitData` 字面量（典型的 PrototypeData/CharacterGenerator 分叉）。
4. **HeadlessCombatLoop** 顶部明确声明了 4 条 Limitations，与 Frontend 的 `CombatResolver.ResolveAttack` 有结构性偏差（无 LOS/侧翼/冲锋/法术/状态/士气）。

---

## 待讨论事项

### 🔴 1. CombatSceneBase 拆分的 .tscn 文件同步

- CombatSceneBase 是 `Node3D` 基类，子类通过 `partial` 在代码层面拆分。**组件化需要同步更新 .tscn 场景文件。**
- **风险**：Godot 编辑器不会自动跟踪组件提取。提取新组件后，必须手动在 .tscn 中 `add_child` 或用 `[Export]` 挂载。
- **建议**：每提取一个 Component，就在 .tscn 中对应添加一次，不要最后统一挂载。

### 🔴 2. SkillExecutionResult 的 Godot.Dictionary 兼容期

- 旧 API 被路径 `CombatManager → LuaSkillBridge → LuaScriptEngine` 引用，Lua 返回值也是 Dictionary。
- **建议**：兼容期至少保留到 C7（技能执行器收编）完成，不要提前移除 `ToDictionary()`。

### 🟡 3. AIPlanner 纯函数的 CombatStateSnapshot 构建成本

- `CombatStateSnapshot` 需要从 Godot Node 中提取纯数据结构，每回合 AI 决策时需要遍历所有 Unit。
- **量化**：假设 10 个单位，每 AI 回合需要 snapshot 约 10 个 Unit × 30 个字段 = 300 个字段拷贝。在 C# 中这个成本可忽略。
- **建议**：先用 `record struct` 做 snapshot，避免 GC 压力。

### 🟡 4. HeadlessCombatLoop 的 LOS/Facing 开关是否值得

- Headless 的核心用途是 AI 调参（跑大量模拟），打开 LOS/Facing 后模拟会变慢（几何计算）。
- **权衡**：如果 AI 调参只看胜率分布（粗略），不开 LOS 也可以接受；如果需要"跟玩家体验一致"才开。
- **建议**：SimulationConfig 默认 `EnableLineOfSight = false`，但在 CombatRuleEngine 的 golden seed test 中强制打开，确保代码路径正确。

### 🟡 5. CareerSkillExecutor 的 31 个 case 到注册表的映射

- `ExecuteCareerSkill` 的 switch 中每个 case 调用一个私有方法（如 `ExecArmorBreak`）。
- 迁移方案：每个 case 对应一个 `CareerSkillHandler : ISkillEffectHandler`，把私有方法变为 handler 的 `Execute`。
- **风险**：31 个 handler 类会产生大量小文件。建议按"单属性/双属性/三属性"等分组放同一个文件（`CareerSkillHandlers.cs` partial class）。

### 🟢 6. `DamageResolutionParityTest` 的位置

- 当前在 `BladeHexCore/tests/Combat/` 下，是 Frontend 和 Core 之间的等价性保障。
- C5（修正流水线）和 C6（Core 回收）改动后，此测试可能会失败——这是预期内的信号。
- **建议**：C5/C6 的 PR 中务必更新此测试，不能跳过。

---

## 实施状态

### T1.1 — CombatCameraController 提取（In Progress）

- **2026-05-26**:
  - 创建了 `CombatCameraController.cs` 独立组件（~180 行），封装了：
    - 相机初始化和配置
    - 缩放控制（滚轮/Web）
    - WASD 移动
    - 镜头聚焦（Tween）
    - 小地图视野同步
    - 位置限制
  - 在 `CombatSceneBase.cs` 中：
    - 添加了 `[Export] public CombatCameraController CameraCtrl` 字段
    - 恢复了 `_camera` 原字段（保留旧代码兼容）
    - 在 `ComputeBattlefieldBounds()` 中同步初始化 `CameraCtrl`
    - 旧相机逻辑完全保留，不破坏现有行为
  - **桥接状态**：`CombatCameraController` 是独立的、可用的组件，但 `CombatSceneBase` 仍在使用旧的 `_camera` 逻辑。后续 Sprint（T1.2+ T1.3...）将逐步把调用从旧逻辑迁移到新组件。

---

## 决策日志

| 日期 | 主题 | 决策 |
|------|------|------|
| 2026-05-26 | 阶段划分 | 按 ROI 分三阶段，阶段 1 完成后 CombatSceneBase + CombatManager 将瘦下一半 |
| 2026-05-26 | EventBus 强类型 | 复用 architecture-optimization R6 的 EventBus 类型化设计，不需要重新设计 |
| 2026-05-26 | 兼容期 | 旧 Dictionary API 标记 [Obsolete] 但不移除，直到 C7 完成 |
| 2026-05-26 | 命令系统升级 | AI 走 CommandHistory 管道与 C3（技能类型化）有交叉依赖，建议在 C3 后做 C4 |

---

## 外部引用

- `architecture-optimization/requirements.md` — R6（事件总线类型化）
- `architecture-optimization/design.md` — R6 的 EventBus 强类型 API 设计
- `combat-build-balance-2026-05-17.md` — 数值设计（本 spec 不改数值）
- `combat-numerics-audit.md` — 数值审计（本 spec 不改数值）
