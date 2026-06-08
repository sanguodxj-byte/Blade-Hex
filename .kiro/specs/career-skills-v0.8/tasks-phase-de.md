# Career Skills v0.8 — Phase D + E Tasks (细粒度版)

> 每个 task 设计为快速模型 30 分钟内可独立完成。
>
> **执行前必读**：
> 1. `Blade&Hex/.kiro/specs/career-skills-v0.8/requirements.md`
> 2. `Blade&Hex/.kiro/specs/career-skills-v0.8/design.md`
> 3. `Blade&Hex/.kiro/specs/career-skills-v0.8/review-notes.md`
>
> **通用验收**：
> - `dotnet build "Blade&Hex/BladeHexCore/BladeHexCore.csproj"` 0 错误
> - `dotnet build "Blade&Hex/BladeHexFrontend/BladeHexFrontend.csproj"` 0 错误
> - 不修改 task 约束之外的文件

---

## Phase D — AI 使用职业大招（3 个 task）

> **目标**：让 AI 控制的单位每场战斗也会释放 1 次职业大招，与玩家对称。
>
> **关键约束**：
> - AI 释放大招走 `CombatManager.UseCareerSkill(unit, targetCell)`
> - 释放后 AP 清零 + HasActed = true（与玩家一致，已在 Executor 中实现）
> - AI 每场只用 1 次（`CanUseCareerSkill()` 已有计数）
> - 不需要 AI 做复杂的"最优时机"判断——简单启发式即可

---

### TASK D1: AI 决策入口

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/AI/AIController.cs`

**必读上下文**:
- `AIController.cs` 当前决策循环（grep `DecideAction` 或 `ChooseAction`）
- `CombatManager.UseCareerSkill` 签名
- `Unit.CanUseCareerSkill()` / `Unit.GetCareerSkill()`

**步骤**:
1. 在 AI 决策循环的**最顶部**（在 Move/Attack/Retreat 之前）加入：
```csharp
// v0.8: AI 职业大招决策
if (TryUseCareerSkill(unit, context))
    return; // 大招释放成功，本回合结束
```

2. 实现 `private bool TryUseCareerSkill(Unit unit, AIContext context)` 方法：
```csharp
private bool TryUseCareerSkill(Unit unit, AIContext context)
{
    if (!unit.CanUseCareerSkill()) return false;
    var skill = unit.GetCareerSkill();
    if (skill == null) return false;

    // 简单启发式：HP < 40% 或 战斗已进行 ≥ 3 回合
    bool shouldUse = unit.CurrentHp < unit.Model.GetMaxHp() * 0.4f
                  || context.CurrentRound >= 3;
    if (!shouldUse) return false;

    // 确定目标格
    Vector2I targetCell = ResolveCareerSkillTarget(unit, skill, context);

    // 释放
    var mgr = GetCombatManager(); // 或通过 context 获取
    var result = mgr.UseCareerSkill(unit, targetCell);
    return result.ContainsKey("success") && result["success"].AsBool();
}
```

3. 实现 `private Vector2I ResolveCareerSkillTarget(Unit unit, CareerSkillData skill, AIContext context)` 占位版：
```csharp
private Vector2I ResolveCareerSkillTarget(Unit unit, CareerSkillData skill, AIContext context)
{
    string target = skill.EffectParams.GetValueOrDefault("target_type", "").AsString();
    // 默认：Self 类技能用自身位置
    if (string.IsNullOrEmpty(target) || target == "Self")
        return unit.GridPos;
    // SingleEnemy：最近的敌人
    // SingleAlly：HP 最低的友军
    // LineCharge：朝最近敌人方向
    // 暂时全部返回自身位置（D3 细化）
    return unit.GridPos;
}
```

**约束**: ≤ 60 行新代码。`AIContext` / `GetCombatManager()` 等如果不存在，用现有的等价物（如 `_combatManager` 字段或 `context.Grid` 等）。

**验收**:
- `dotnet build` 通过
- AI 单位在 HP < 40% 或第 3 回合后会尝试释放大招
- 不影响现有 AI 行为（`CanUseCareerSkill` 为 false 时直接跳过）

---

### TASK D2: AI 启发式细化

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/AI/AIController.cs`

**前置**: D1 完成

**步骤**:
1. 在 `TryUseCareerSkill` 中把 `shouldUse` 的判断改为按技能类型分类：

```csharp
// 从 effectParams 推断技能类型
bool hasAttack = skill.EffectParams.ContainsKey("attack_type");
bool hasHeal = skill.EffectParams.ContainsKey("instant_heal_percent") || skill.EffectParams.ContainsKey("instant_heal_hp_percent");
bool hasDefense = skill.EffectParams.ContainsKey("ac_bonus") || skill.EffectParams.ContainsKey("immune_negative") || skill.EffectParams.ContainsKey("dr_threshold_bonus");
bool hasMorale = skill.EffectParams.ContainsKey("morale_target_penalty") || skill.EffectParams.ContainsKey("morale_ally_bonus");

bool shouldUse;
if (hasHeal || hasDefense)
    shouldUse = unit.CurrentHp < unit.Model.GetMaxHp() * 0.30f; // 自保型：HP < 30%
else if (hasAttack)
    shouldUse = context.HasLowHpEnemy(0.3f) || context.CurrentRound >= 2; // 进攻型：有残血敌人或第2回合后
else if (hasMorale)
    shouldUse = context.AlliesInDanger >= 2; // 控场型：2+ 友军危险
else
    shouldUse = context.CurrentRound >= 3; // 其他：第3回合后
```

2. 如果 `AIContext` 没有 `HasLowHpEnemy` / `AlliesInDanger`，加两个简单 helper：
```csharp
// 在 AIContext 或 AIController 内
private bool HasLowHpEnemy(float threshold) =>
    _enemies.Any(e => e.CurrentHp > 0 && (float)e.CurrentHp / e.Model.GetMaxHp() < threshold);

private int CountAlliesInDanger() =>
    _allies.Count(a => a.CurrentHp > 0 && (float)a.CurrentHp / a.Model.GetMaxHp() < 0.3f);
```

**约束**: ≤ 40 行修改。

**验收**:
- `dotnet build` 通过
- 防御型大招在 HP < 30% 时触发
- 进攻型大招在有残血敌人时触发

---

### TASK D3: AI 目标选择

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/AI/AIController.cs`

**前置**: D1 完成

**步骤**:
1. 完善 `ResolveCareerSkillTarget`，根据 JSON 中的 `target` 字段（`"Self"` / `"SingleEnemy"` / `"SingleAlly"` / `"AllAllies"` / `"AllAdjacent"` / `"LineCharge"` / `"RangedSingle"`）选择目标：

```csharp
private Vector2I ResolveCareerSkillTarget(Unit unit, CareerSkillData skill, ...)
{
    // 从 career_skill_configs.json 的 "target" 字段读取（已在 effectParams 或 skill 本身）
    // 注意：JSON 中 target 字段在 CareerSkillData 上可能没有直接映射
    // 可以从 effectParams["target_type"] 读取，或从 JSON 的 "target" 字段
    string targetType = "Self"; // 默认
    if (skill.EffectParams.ContainsKey("target_type"))
        targetType = skill.EffectParams["target_type"].AsString();
    else if (skill.EffectParams.ContainsKey("attack_type"))
        targetType = "SingleEnemy"; // 有攻击的默认打敌人

    return targetType switch
    {
        "Self" or "AllAllies" or "AllAdjacent" => unit.GridPos,
        "SingleEnemy" or "single_visible" or "adjacent_enemy" or "single_visible_enemy"
            => FindNearestEnemy(unit)?.GridPos ?? unit.GridPos,
        "SingleAlly" or "single_ally" or "single_visible_ally"
            => FindLowestHpAlly(unit)?.GridPos ?? unit.GridPos,
        "LineCharge" or "line_charge"
            => FindChargeDirection(unit),
        "RangedSingle" or "ranged_homing" or "ranged_precise"
            => FindLowestHpEnemyInLos(unit)?.GridPos ?? unit.GridPos,
        _ => unit.GridPos,
    };
}
```

2. 实现 4 个 helper（如果不存在）：
- `FindNearestEnemy(unit)` — 距离最近的存活敌人
- `FindLowestHpAlly(unit)` — HP 百分比最低的存活友军
- `FindChargeDirection(unit)` — 朝最近敌人方向延伸 3 格的终点
- `FindLowestHpEnemyInLos(unit)` — 视线内 HP 最低的敌人

**约束**: ≤ 50 行。如果 AI 已有类似 helper（如 `AITargetEvaluator`），直接复用。

**验收**:
- `dotnet build` 通过
- AI 的 SingleEnemy 技能会瞄准最近敌人
- AI 的 LineCharge 技能会朝敌人方向冲锋

---

## Phase E — 战斗管线接入新 stat key（6 个 task）

> **目标**：让 BuffRegistry 中注册的新 stat key（`dr_threshold` / `crit_threshold` / `move_ap_reduction` / `morale_floor` / `immune_fear` / `immune_negative` / `cannot_act` / `cannot_move` / `attack_bonus` / `save_bonus` / `attack_advantage` / `no_aoo_on_move` / `can_cross_enemies` / `attacker_disadvantage_while_phantom`）在战斗管线中真正生效。
>
> **统一模式**：在现有计算末尾加 `BuffSystem.ResolveStatModifiers(data, "<stat>")` 调用，把结果应用到最终值。

---

### TASK E1: dr_threshold + crit_threshold 接入

**允许修改的文件**: 仅 `Blade&Hex/BladeHexCore/src/Combat/CombatStats.cs`

**必读上下文**:
- `CombatStats.GetDrThreshold(UnitData data)` 当前实现
- `CombatStats.GetCritThreshold(UnitData data)` 当前实现
- `BuffSystem.ResolveStatModifiers(UnitData, string)` API

**步骤**:
1. 在 `GetDrThreshold` 末尾（return 前）加：
```csharp
// v0.8: buff 修正 DR 阈值
var drMod = Buff.BuffSystem.ResolveStatModifiers(data, "dr_threshold");
if (drMod.OverrideValue.HasValue)
    return System.Math.Max(0, (int)drMod.OverrideValue.Value);
int drResult = currentValue + (int)drMod.FlatBonus;
return System.Math.Max(0, drResult);
```

2. 在 `GetCritThreshold` 末尾加：
```csharp
// v0.8: buff 修正暴击阈值
var critMod = Buff.BuffSystem.ResolveStatModifiers(data, "crit_threshold");
if (critMod.OverrideValue.HasValue)
    return System.Math.Clamp((int)critMod.OverrideValue.Value, 2, 20);
int critResult = currentValue + (int)critMod.FlatBonus;
return System.Math.Clamp(critResult, 2, 20);
```

**约束**: ≤ 15 行新代码。不改变函数签名。

**验收**:
- `dotnet build` 通过
- 挂了 `armor_break` buff（dr_threshold -3）的目标 DR 阈值确实降低
- 挂了 `gaze_ruin` buff（crit_threshold Override 12）的目标暴击阈值变为 12

---

### TASK E2: move_ap_reduction 接入

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/commands/MoveCommand.cs`

**必读上下文**:
- `MoveCommand.Execute` 中 AP 消耗计算（约 `float apCost = Mathf.Max(1f, Path.Count);`）

**步骤**:
1. 在 AP 消耗计算后、`unit.ConsumeAp` 前加：
```csharp
// v0.8: buff 移动 AP 减免
if (unit.Data != null)
{
    var moveMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "move_ap_reduction");
    float reduction = moveMod.FlatBonus * Path.Count; // 每格减免 N AP
    apCost = Mathf.Max(0.5f * Path.Count, apCost - reduction); // 最低 0.5 AP/格
}
```

**约束**: ≤ 10 行。

**验收**:
- `dotnet build` 通过
- 挂了 `battle_hymn` buff（move_ap_reduction 1）的单位移动每格少花 1 AP

---

### TASK E3: morale_floor + immune_fear 接入

**允许修改的文件**:
- `Blade&Hex/BladeHexFrontend/src/View/Combat/MoraleSystem.cs`
- `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffSystem.cs`

**步骤**:
1. 在 `MoraleSystem.ChangeMorale` 中，把 `Mathf.Clamp(... MoraleMin, MoraleMax)` 改为：
```csharp
// v0.8: buff 士气下限
int floor = MoraleMin;
if (unit.Data != null)
{
    var floorMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "morale_floor");
    if (floorMod.OverrideValue.HasValue)
        floor = System.Math.Max(floor, (int)floorMod.OverrideValue.Value);
}
unit.Data.Morale = Mathf.Clamp(unit.Data.Morale + amount, floor, MoraleMax);
```

2. 在 `BuffSystem.Apply` 方法开头（互斥处理之前）加：
```csharp
// v0.8: immune_fear 检查
if (instance.Tags.Contains("fear") || instance.Id == "fear")
{
    var immuneMod = ResolveStatModifiers(target, "immune_fear");
    if (immuneMod.OverrideValue.HasValue && immuneMod.OverrideValue.Value >= 1f)
        return null; // 免疫恐惧，不施加
}
```

**约束**: ≤ 20 行。

**验收**:
- `dotnet build` 通过
- 挂了 `hold_line` buff（morale_floor -20）的单位士气不会低于 -20
- 挂了 `iron_grip` buff（immune_fear 1）的单位不会被施加 fear

---

### TASK E4: immune_negative + cannot_act + cannot_move

**允许修改的文件**:
- `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffSystem.cs`
- `Blade&Hex/BladeHexFrontend/src/View/Combat/commands/MoveCommand.cs`
- `Blade&Hex/BladeHexFrontend/src/View/Combat/AI/AIController.cs`

**步骤**:
1. `BuffSystem.Apply` 开头加（在 immune_fear 检查之后）：
```csharp
// v0.8: immune_negative 检查
if (instance.IsNegative)
{
    var immuneNeg = ResolveStatModifiers(target, "immune_negative");
    if (immuneNeg.OverrideValue.HasValue && immuneNeg.OverrideValue.Value >= 1f)
        return null;
}
```

2. `MoveCommand.Execute` 开头加：
```csharp
// v0.8: cannot_move 检查
if (unit.Data != null)
{
    var moveLock = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "cannot_move");
    if (moveLock.OverrideValue.HasValue && moveLock.OverrideValue.Value >= 1f)
        return CommandResult.Fail("无法移动");
}
```

3. `AIController` 决策循环开头加：
```csharp
// v0.8: cannot_act 检查
if (unit.Data != null)
{
    var actLock = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "cannot_act");
    if (actLock.OverrideValue.HasValue && actLock.OverrideValue.Value >= 1f)
        return; // 跳过本回合
}
```

**约束**: ≤ 20 行。

**验收**:
- `dotnet build` 通过
- 挂了 `stone_body`（immune_negative + cannot_act）的单位不会被施加负面 buff，且 AI 跳过其回合
- 挂了 `deep_chains`（cannot_move）的单位无法移动

---

### TASK E5: attack_bonus + save_bonus + attack_advantage

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CombatResolver.cs`

**必读上下文**:
- `CombatResolver.ResolveAttack` 中 `attackBonus` 的计算位置
- `hasAdvantage` / `hasDisadvantage` 的设置位置

**步骤**:
1. 在 `attackBonus` 计算后加：
```csharp
// v0.8: buff attack_bonus
if (attacker.Data != null)
{
    var atkMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(attacker.Data, "attack_bonus");
    attackBonus += (int)atkMod.FlatBonus;
}
```

2. 在 advantage/disadvantage 判断区域加：
```csharp
// v0.8: buff attack_advantage
if (attacker.Data != null)
{
    var advMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(attacker.Data, "attack_advantage");
    if (advMod.OverrideValue.HasValue && advMod.OverrideValue.Value >= 1f)
    { hasAdvantage = true; modifiers["career_advantage"] = true; }
}
```

3. 在豁免检定处（如果 `StatusEffectManager.TrySave` 或类似位置存在）加 `save_bonus`：
```csharp
// v0.8: buff save_bonus（在 MakeSave 调用前）
var saveMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "save_bonus");
int saveBonus = (int)saveMod.FlatBonus;
// 传入 MakeSave 的 modifier 中
```

**约束**: ≤ 20 行。如果豁免检定不在 CombatResolver 中，在 `StatusEffectManager.TrySave` 中加。

**验收**:
- `dotnet build` 通过
- 挂了 `omnibus` buff（attack_bonus 2）的单位命中 +2
- 挂了 `champion_aura`（attack_advantage 1）的单位攻击获得优势

---

### TASK E6: no_aoo_on_move + can_cross_enemies + attacker_disadvantage

**允许修改的文件**:
- `Blade&Hex/BladeHexFrontend/src/View/Combat/FacingSystem.cs`
- `Blade&Hex/BladeHexFrontend/src/View/Combat/commands/MoveCommand.cs`
- `Blade&Hex/BladeHexFrontend/src/View/Combat/CombatResolver.cs`

**步骤**:
1. `FacingSystem.ShouldTriggerAoo` 开头加：
```csharp
// v0.8: no_aoo_on_move 检查
if (mover.Data != null)
{
    var aooMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(mover.Data, "no_aoo_on_move");
    if (aooMod.OverrideValue.HasValue && aooMod.OverrideValue.Value >= 1f)
        return null; // 不触发借机攻击
}
```

2. `MoveCommand.Execute` 中路径验证处（检查格子是否可通过）加：
```csharp
// v0.8: can_cross_enemies 允许穿越敌方格（但不可停留）
if (unit.Data != null)
{
    var crossMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(unit.Data, "can_cross_enemies");
    if (crossMod.OverrideValue.HasValue && crossMod.OverrideValue.Value >= 1f)
        allowCrossEnemies = true; // 路径计算时允许穿越有敌人的格子
}
```
（具体实现取决于 MoveCommand 如何验证路径——如果是 HexGrid.FindPath 做的，可能需要在 FindPath 参数中加 flag）

3. `CombatResolver.ResolveAttack` 中 defender 侧加：
```csharp
// v0.8: attacker_disadvantage_while_phantom
if (defender.Data != null)
{
    var phantomMod = BladeHex.Combat.Buff.BuffSystem.ResolveStatModifiers(defender.Data, "attacker_disadvantage_while_phantom");
    if (phantomMod.OverrideValue.HasValue && phantomMod.OverrideValue.Value >= 1f)
    { hasDisadvantage = true; modifiers["phantom_disadvantage"] = true; }
}
```

**约束**: ≤ 25 行。`can_cross_enemies` 如果路径系统不好改，写 `// TODO: Phase E6 - integrate with HexGrid.FindPath` 占位。

**验收**:
- `dotnet build` 通过
- 挂了 `twilight_stride`（no_aoo_on_move）的单位移动不触发借机
- 挂了 `mirror_image`（attacker_disadvantage_while_phantom）的单位被攻击时攻击者劣势

---

## 进度追踪

- [ ] D1 - AI 决策入口
- [ ] D2 - AI 启发式细化
- [ ] D3 - AI 目标选择
- [ ] E1 - dr_threshold / crit_threshold 接入
- [ ] E2 - move_ap_reduction 接入
- [ ] E3 - morale_floor / immune_fear 接入
- [ ] E4 - immune_negative / cannot_act / cannot_move 接入
- [ ] E5 - attack_bonus / save_bonus / attack_advantage 接入
- [ ] E6 - no_aoo / can_cross / attacker_disadvantage 接入

## 并行执行建议

```
D1 → D2 ‖ D3
E1 ‖ E2 ‖ E3 ‖ E4 ‖ E5 ‖ E6  (全部并行，互不依赖)
D 与 E 之间也互不依赖
```

单人约 3 小时全部完成；3 人协作 1.5 小时。
