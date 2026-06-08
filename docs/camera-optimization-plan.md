## 战斗攻击时视角频繁变换 — 深度审查与优化方案

### 问题概述

战斗攻击过程中，摄像机视角会发生频繁且不必要的位置切换和缩放抖动。在一次完整的攻击动作中（从发起攻击到命中展示），摄像机最多经历 **3~4 次** 独立的位置/缩放变换操作，而在 AI 回合中多个敌方单位连续行动时，该问题会被进一步放大——一场 AI 回合可能出现 **10+ 次** 可感知的镜头跳动。

### 调用链追踪

以下是一次典型攻击的完整摄像机操作调用链：

**玩家选中单位：**
`CombatSceneBase.SelectUnit()` → `CameraCtrl.LockOnUnit(unit)` ← **第 1 次锁定**

**玩家发起攻击：**
`CombatSkillExecutor.HandleAttack()` → `CombatAttackAnimator.PlayAttack()`
→ `CameraCtrl.FrameTwoTargets(attacker, target, 0.3f)` ← **第 2 次：Tween 平移+缩放 (0.3s)**
→ 近战：`CameraCtrl.LockOnUnit(target)` ← **第 3 次：切换到目标**
→ 0.25s 后：`CameraCtrl.LockOnUnit(attacker)` ← **第 4 次：切回攻击者**

**AI 回合开始：**
`OnTurnStarted()` → `CameraCtrl.LockOnUnit(initUnit)` ← **额外锁定**
`AIController.ExecuteMove()` → `_adapter.MoveUnitTo()` → `MovementCtrl` → `CameraCtrl.LockOnUnit(unit)` ← **移动锁定**
`AIController.ExecuteAttack()` → `_attackAnimator.PlayAttack()` → 同上述 3~4 步

### 根因分析

**根因 1：PlayAttack 中无条件调用 FrameTwoTargets**

`CombatAttackAnimator.PlayAttack()` 第 50-53 行：
```csharp
if (CameraCtrl != null)
{
    await CameraCtrl.FrameTwoTargets(attacker.Position, target.Position, 0.3f);
}
```

每次攻击都会触发 `FrameTwoTargets`，即使两个单位都已经在视野内。虽然 `FrameTwoTargets` 内部有 `IsWorldPosVisible` 检查，但在 `LockOnUnit` 刚锁定完攻击者之后，目标不一定可见，所以这个检查往往通过，导致每次攻击都触发一次 Tween 动画。Tween 动画会同时改变 position 和 size，产生明显的视觉变化。

**根因 2：攻击动画中"聚焦目标→切回攻击者"的硬切换模式**

`CombatAttackAnimator.PlayMeleeAttack()` 第 73-82 行和 `PlayRangedAttack()` 第 126-139 行：
```csharp
// 近战命中后
CameraCtrl.LockOnUnit(target);            // 硬切到目标
await ScaledWait(this, 0.25f);
CameraCtrl.LockOnUnit(attacker);          // 硬切回攻击者
```

每次攻击完成后，摄像机会先硬切到目标（展示受击），0.25 秒后再硬切回攻击者。`LockOnUnit` 使用 `Lerp(0.15f)` 做平滑跟随，但从一个目标切换到另一个目标时，0.15 的插值系数在视觉上是明显的"跳切"。

**根因 3：LockOnUnit 缺乏切换抑制机制**

`CombatCameraController.LockOnUnit()` 没有做任何判断就直接设置锁定状态。同一个单位被重复锁定、或在两个单位之间快速交替锁定，都没有防抖/去重逻辑。在 AI 回合中，`OnTurnStarted` 先 `LockOnUnit(initUnit)`，紧接着 `MoveUnitAlongPath` 又 `LockOnUnit(unit)`（同一个单位），之后 `PlayAttack` 又 `FrameTwoTargets` → `LockOnUnit(target)` → `LockOnUnit(attacker)`。

**根因 4：AI 多单位连续行动时的镜头风暴**

`AIController.ExecuteEnemyTurn()` 对每个敌方单位循环执行 Move + Attack，每次 Move 触发 1 次 LockOnUnit，每次 Attack 触发 FrameTwoTargets + 2 次 LockOnUnit。3 个敌人各攻击 1 次 = 至少 3×(1+3) = **12 次** 摄像机操作。

**根因 5：FrameTwoTargets 的缩放抖动**

`FrameTwoTargets` 第 414 行：
```csharp
float targetOrtho = Mathf.Max(neededOrtho, _camera.Size); // 只放大不缩小
```

虽然注释说"只放大不缩小"，但每次 `FrameTwoTargets` 会根据攻击双方距离计算新的 `neededOrtho`。当不同攻击对的距离不同时（近战 vs 远程），缩放值会在不同级别之间跳动。而且这个缩放值不会恢复到攻击前的值。

**根因 6：Tween 与 Lerp 跟随的竞争**

`FrameTwoTargets` 使用 `Tween` 驱动 `camera.position`（0.35s Cubic InOut），而 `ApplyLockTracking` 在 `_Process` 中每帧用 `Lerp(0.15f)` 驱动 `camera.position`。如果 `FrameTwoTargets` 的 Tween 结束后紧接着触发 `LockOnUnit`，两者会产生短暂的位置竞争——Tween 刚把相机移到中点，LockOnUnit 的 Lerp 立即开始向单个目标拉扯。

---

### 优化方案

#### 方案 A：最小改动（推荐优先实施）

核心思路：在 `CombatAttackAnimator` 层面减少不必要的摄像机操作，并给 `CombatCameraController` 添加去重和防抖逻辑。改动范围小，风险低。

**A1. 给 LockOnUnit 添加去重逻辑**

文件：`CombatCameraController.cs`

在 `LockOnUnit` 方法开头判断：如果当前已经锁定在同一个单位上，直接 return。

```csharp
public void LockOnUnit(Unit unit, float orthoSize = 0f)
{
    if (unit == null || !IsInstanceValid(unit)) return;
    // 去重：已锁定同一单位且不需要改缩放时跳过
    if (_lockMode == LockMode.Unit
        && _lockUnit != null && IsInstanceValid(_lockUnit)
        && ReferenceEquals(_lockUnit, unit)
        && (orthoSize <= 0f || Mathf.IsEqualApprox(_lockOrthoSize, orthoSize)))
    {
        return;
    }
    _lockUnit = unit;
    _lockMode = LockMode.Unit;
    _lockOrthoSize = orthoSize;
}
```

预期效果：消除 `OnTurnStarted` → `MoveUnitAlongPath` 之间对同一单位的重复 LockOnUnit 调用。

**A2. 给 PlayAttack 的 FrameTwoTargets 添加距离阈值**

文件：`CombatAttackAnimator.cs`

在调用 `FrameTwoTargets` 之前，先判断攻击者和目标是否都在视野内。如果都在，跳过该步骤。

```csharp
// 镜头策略：仅当至少一方不在视野内时才框定
if (CameraCtrl != null)
{
    bool bothVisible = CameraCtrl.IsWorldPosVisible(attacker.Position)
                    && CameraCtrl.IsWorldPosVisible(target.Position);
    if (!bothVisible)
    {
        await CameraCtrl.FrameTwoTargets(attacker.Position, target.Position, 0.3f);
    }
}
```

预期效果：大部分近战攻击（攻击者和目标相邻）不再触发 FrameTwoTargets 的 Tween 动画。

**A3. 取消攻击后的"聚焦目标→切回攻击者"硬切换**

文件：`CombatAttackAnimator.cs`

将近战和远程攻击后的 LockOnUnit(target) → wait → LockOnUnit(attacker) 序列替换为更轻量的方案：不切换锁定，让摄像机保持在 FrameTwoTargets 后的位置（已经同时包含两者）。如果需要展示受击效果，可以在目标受击动画中只做短暂聚焦，而不是硬切锁定。

```csharp
// 近战 - 优化后
private async Task PlayMeleeAttack(Unit attacker, Unit target)
{
    attacker.PlayAttackLunge(target.GlobalPosition);
    await CombatSpeed.ScaledWait(this, 0.4f);

    // 不再硬切锁定到目标再切回 — FrameTwoTargets 已确保两者都在视野内
    // 如果需要展示受击，只做轻微偏移而非完整锁定
    await CombatSpeed.ScaledWait(this, 0.2f);

    // 确保攻击结束后仍然跟随攻击者
    if (CameraCtrl != null && GodotObject.IsInstanceValid(attacker) && attacker.CurrentHp > 0)
        CameraCtrl.LockOnUnit(attacker);
    else if (CameraCtrl != null)
        CameraCtrl.Unlock();
}
```

```csharp
// 远程 - 优化后
private async Task PlayRangedAttack(Unit attacker, Unit target, WeaponData weapon)
{
    // ... 投射物发射代码不变 ...
    await CombatSpeed.ScaledWait(this, travelTime);

    // 飞行结束后：不再硬切到目标再切回
    await CombatSpeed.ScaledWait(this, 0.2f);

    if (CameraCtrl != null)
    {
        if (GodotObject.IsInstanceValid(attacker) && attacker.CurrentHp > 0)
            CameraCtrl.LockOnUnit(attacker);
        else
            CameraCtrl.Unlock();
    }
}
```

预期效果：每次攻击减少 2 次 LockOnUnit 调用（切到目标 + 切回），消除最明显的视角跳动。

**A4. 恢复 FrameTwoTargets 之后的缩放值**

文件：`CombatCameraController.cs`

在 `FrameTwoTargets` 完成后，记录原始缩放值，提供一个恢复方法。或者更简单地：在攻击结束后，将缩放恢复到攻击前的值。

在 `CombatAttackAnimator.PlayAttack` 中：
```csharp
float savedOrthoSize = CameraCtrl?.OrthoSize ?? 0f;
// ... FrameTwoTargets ...
// ... 攻击动画和结算 ...
// 攻击结束后恢复缩放
if (CameraCtrl != null && savedOrthoSize > 0)
{
    // 用 Lerp 平滑恢复，而非瞬间跳回
    var tween = CameraCtrl.CreateTween();
    tween.TweenProperty(CameraCtrl.Camera, "size", savedOrthoSize, 0.3f)
        .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
}
```

预期效果：攻击结束后缩放恢复到之前的值，避免不同攻击对之间的缩放抖动累积。

---

#### 方案 B：结构优化（可在方案 A 验证通过后实施）

**B1. 引入摄像机操作队列/状态机**

当前摄像机操作是分散调用的（各处直接调 LockOnUnit/FrameTwoTargets/FocusOn），没有统一调度。引入一个轻量的操作队列，让摄像机操作按优先级排序并合并：

```csharp
public enum CameraIntent
{
    FollowUnit,       // 持续跟随某单位（低优先级）
    FrameEncounter,   // 框定攻击双方（中优先级）
    FocusImpact,      // 聚焦命中效果（高优先级，短暂）
}

public void PushIntent(CameraIntent intent, Unit? primary, Unit? secondary = null, float duration = 0f)
{
    // 如果当前已有相同或更高优先级的 intent 在执行，忽略本次
    if (_currentIntent >= intent && _intentTimer > 0) return;
    _currentIntent = intent;
    _intentTimer = duration;
    // ... 执行对应的摄像机操作
}
```

预期效果：从根本上消除重复和冲突的摄像机操作，让镜头行为可预测。

**B2. AI 回合的批量镜头策略**

AI 回合当前是每个单位独立执行 Move+Attack，每次都操作摄像机。优化为：AI 回合开始时，摄像机锁定到第一个行动单位，之后只在"切换行动单位"时才移动镜头。同一单位的 Move+Attack 过程中，摄像机保持跟随不额外切换。

在 `AIController.ExecuteEnemyTurn()` 中，在进入循环前锁定当前行动单位，循环内不再额外调用摄像机：

```csharp
// AI 回合开始
_adapter?.LockCameraOnUnit(sortedEnemies.First());

foreach (var enemy in sortedEnemies)
{
    // Move 和 Attack 不再操作摄像机
    // 只在切换到下一个单位时才更新锁定
    await ExecuteAction(action, hexGrid, combatUi);

    // 切换单位时的过渡
    var next = GetNextAliveEnemy(sortedEnemies, enemy);
    if (next != null)
        _adapter?.LockCameraOnUnit(next);
}
```

需要在 `ICombatSceneAdapter` 中添加 `LockCameraOnUnit` 方法。

预期效果：AI 回合的摄像机操作从 12+ 次降低到 N 次（N = 行动单位数）。

---

#### 方案 C：体验增强（长期）

**C1. 添加摄像机操作冷却时间**

在 `CombatCameraController` 中添加全局冷却：在上一次摄像机操作完成后 0.2 秒内，忽略新的操作请求（紧急的 FocusImpact 除外）。

**C2. 用 Smooth Follow 替代 Lerp**

将 `ApplyLockTracking` 中的 `Lerp(0.15f)` 替换为基于速度的 smooth follow，使摄像机在不同距离的切换中有统一的视觉感受：

```csharp
float followSpeed = Mathf.Clamp(
    (targetPos - _camera.Position).Length() * 2f,
    200f,   // 最小速度
    2000f   // 最大速度
);
var direction = (desiredPos - _camera.Position).Normalized();
_camera.Position += direction * followSpeed * (float)GetProcessDeltaTime();
```

**C3. 提供玩家设置项**

在设置面板中添加"战斗镜头模式"选项：跟随模式（当前行为）、固定模式（不自动跟随）、简化模式（仅切换单位时移动）。

---

### 实施优先级

| 优先级 | 改动项 | 改动文件 | 预期效果 | 风险 |
|--------|--------|---------|---------|------|
| P0 | A1 LockOnUnit 去重 | CombatCameraController.cs | 消除重复锁定 | 极低 |
| P0 | A2 FrameTwoTargets 视野检查 | CombatAttackAnimator.cs | 消除不必要的 Tween | 低 |
| P0 | A3 取消硬切聚焦序列 | CombatAttackAnimator.cs | 每次攻击减少 2 次镜头跳切 | 低 |
| P1 | A4 恢复缩放值 | CombatAttackAnimator.cs + CombatCameraController.cs | 消除缩放抖动 | 低 |
| P2 | B1 摄像机操作队列 | CombatCameraController.cs | 根本性消除冲突 | 中 |
| P2 | B2 AI 批量镜头策略 | AIController.cs + ICombatSceneAdapter.cs | AI 回合镜头操作量级下降 | 中 |
| P3 | C1/C2/C3 体验增强 | 多文件 | 整体镜头品质提升 | 低 |

### 验证方法

每个改动项完成后，通过以下流程验证：

1. 从主菜单进入 Quick Combat 场景，使用默认配置开始战斗
2. 选中玩家单位，执行普通攻击，观察镜头是否有跳切
3. 选中远程单位，攻击远处的敌人，观察缩放是否稳定
4. 等待 AI 回合，观察多个敌方单位连续行动时的镜头行为
5. 反复进行 3 轮以上完整回合（玩家+AI），确认镜头无累积偏移
6. 开启 2x 战斗速度，确认镜头在加速模式下不出现抖动
