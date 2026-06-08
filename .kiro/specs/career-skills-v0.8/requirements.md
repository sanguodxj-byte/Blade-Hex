# Career Skills v0.8 — Requirements

## 背景

> **本文档为快速模型的执行规范**——每个 task 设计为 30~60 分钟可完成。

v0.8 版本统一了 63 个职业专属技能的设计：

1. **每场战斗 1 次**（OncePerBattle）
2. **全部主动技能**（无被动）
3. **AP 消耗 = 释放时清空当前剩余 AP**（释放即结束本回合）
4. **强度按属性数缩放**：1属=×1.0, 2属=×1.25, 3属=×1.5, 4属=×1.75, 5属=×2.0, 6属=×2.5
5. **持续效果通过 buff 实现**：原本的"被动"全部改为"释放后挂限时 buff"
6. **buff 持续回合 = 基线 × 倍率**（最低 1）

设计文档：`Blade&Hex/docs/职业专属技能.md`
数据权威源：`Blade&Hex/BladeHexCore/src/SkillTree/CareerSkillRegistry.cs`

## 重要约束（v0.8.1 新增）

> **绝对禁止任何"血量/伤害/恢复/临时HP"的硬编码常数**

游戏后期单位 HP 是前期的 5-10 倍：
- Lv.1 单位 HP ≈ 11
- Lv.30 单位 HP ≈ 70
- Lv.50 单位 HP ≈ 110+

如果用 `1d6` 这种硬编码的伤害骰子，前期一击致命、后期挠痒痒。

### 数值规范

**禁止**：
- `caster.CurrentHp += 5;`（硬编码 +5 治疗）
- `damage = 1d6 + 2;`（硬编码骰子）
- `tempHp = 10;`（硬编码临时HP）
- `if (target.CurrentHp <= 30)`（硬编码 HP 阈值）

**允许**：
- 百分比表达：`heal = caster.MaxHp * 0.25f`
- 属性修正缩放：`tempHp = Mod(CON) * Level / 2`
- 武器骰子：`damage = caster.Weapon.DamageDice` (使用 caster 武器伤害)
- 等级骰子：`damage = LevelDice(Lv) = max(1, Lv/4) d6`
- 比例阈值：`if (target.CurrentHp <= target.MaxHp * 0.3f)`（百分比 ≠ 硬编码）

### 缩放工具函数（Phase A 前置）

`Blade&Hex/BladeHexCore/src/Combat/CombatScalingMath.cs` 应包含：

```csharp
public static class CombatScalingMath
{
    /// <summary>等级骰子: max(1, Lv/4) d6 — 用于法术/技能直接伤害</summary>
    public static (int count, int sides) GetLevelDice(int level, int sides = 6)
        => (System.Math.Max(1, level / 4), sides);

    /// <summary>百分比 HP 转绝对值</summary>
    public static int PercentOfMaxHp(BattleUnitModel model, float percent)
        => System.Math.Max(1, (int)(model.GetMaxHp() * percent));

    /// <summary>百分比 Mana 转绝对值</summary>
    public static int PercentOfMaxMana(UnitData data, float percent)
        => System.Math.Max(1, (int)(CombatStats.GetMaxMana(data) * percent));

    /// <summary>属性修正型加值（用于 buff Modifier 的动态值）: 例如临时 HP = Mod(CON) × Level/2</summary>
    public static int StatModXLevel(int statScore, int level, float multiplier = 1.0f)
        => (int)System.Math.Ceiling(RPGRuleEngine.GetStatModifier(statScore) * level * multiplier);

    /// <summary>武器伤害骰应用（用于"再做一次正常近战攻击"的伤害基础）</summary>
    public static (int count, int sides) GetWeaponDice(Unit caster, int sidesFallback = 6)
    {
        var w = caster.Model.GetMainHand() as WeaponData;
        if (w != null) return (w.DamageDiceCount, w.DamageDiceSides);
        return (1, sidesFallback);
    }
}
```

### 在 effectParams 中的占位符

`CareerSkillRegistry` 使用以下"语义化"键名替代硬编码：

| 旧（禁止） | 新（推荐） |
|---|---|
| `heal_amount: 10` | `heal_max_hp_percent: 0.25` |
| `damage_dice_count: 2, damage_dice_sides: 6`（除非武器伤害骰） | `damage_level_dice: true`（让 ExecXxx 用 LevelDice）|
| `temp_hp: 15` | `temp_hp_con_mult_level: 0.5`（= Mod(CON)×Lv×0.5）|
| `dot_dice_count: 1, dot_dice_sides: 6` | `dot_level_dice: true` 或 `dot_max_hp_percent: 0.05` |
| `aoe_damage_dice: "2d8"` | `aoe_damage_level_dice: true`（直接用 LevelDice 缩放）|
| `low_hp_threshold: 30` | `low_hp_threshold: 0.3`（百分比，不变）✅ |

> **注**：燃烧、流血等 BuffRegistry 中既有的 `OnTick.DiceCount/Sides` 字段也要按级别缩放——具体由 `BuffTurnHooks` 在 tick 时根据 buff Source 单位等级动态计算（Phase E 接入）。在 BuffRegistry 注册时仍可写"基线骰子"（如 1d6），但运行时应该乘以 `max(1, Source.Level/4)` 倍。

### 验收标准

每个 ExecXxx 函数 + Registry 条目都不应出现：
- 字面量整数 ≥ 4 用于 HP/伤害/治疗（除非是骰子面数 6/8/10/12 等）
- 字面量浮点数用于"绝对值"（百分比 0.x 例外）

---

## 已完成（基线）

- ✅ 设计文档 `职业专属技能.md` 重写完毕
- ✅ `CareerSkillRegistry.cs` 全部 63 条技能改为 `Active + OncePerBattle`，注入 `tier_multiplier` / `attribute_count` / `consume_all_ap` 字段
- ✅ `CareerSkillExecutor.cs` 顶部 AP 检查改为"AP ≥ 1 即可释放"，释放后 `ConsumeAp(CurrentAp) + HasActed = true`
- ✅ 删除了 Kill Shot 的"未中返还 AP"特例
- ✅ Core + Frontend 两个 csproj 都已编译通过

## 待办（按依赖顺序）

### Phase Z — 数值规范前置任务（v0.8.1 新增）

**Z0**: 创建 `CombatScalingMath` 工具类，包含 GetLevelDice / PercentOfMaxHp / PercentOfMaxMana / StatModXLevel / GetWeaponDice
**Z1**: 把 `职业专属技能.md` 文档中所有硬编码 HP/伤害/治疗值改写为比例/级别形式
**Z2**: 把 `CareerSkillRegistry.cs` 中所有 `effectParams` 里的硬编码 HP/伤害/治疗值改为占位符语义键

### Phase A — Buff 注册（前置条件）

新设计中所有"被动"都改为通过 buff 实现，需要把 v0.8 文档里出现的所有 `buff_id` 在 `BuffRegistry` 注册一遍。

**约 30 个新 buff**，全部集中在 `BladeHexCore/src/Combat/Buff/BuffRegistry.cs` 里。

### Phase B — Executor 补全（27 个旧"被动"技能改为主动）

`CareerSkillExecutor.cs` 中目前缺失约 27 个 `ExecXxx` 函数，default 分支会拒绝它们。需要新增这些函数，全部走"释放即给单位挂 buff"的统一模式。

### Phase C — Executor 倍率应用（属性数加强落地）

现有 27 个 `ExecXxx` 内部还在用硬编码数值。需要批量改为读 `effectParams["tier_multiplier"]` 并应用到伤害骰、持续回合、加值、范围。同时按 Z 阶段的"非硬编码"规范，把 HP/伤害值改为百分比/等级骰。

### Phase D — AI 决策

`AIController` 让敌方也会用大招，每场 1 次。

### Phase E — 战斗集成

修改 `CombatResolver` / `BattleUnitModel` / `BuffTurnHooks` 等代码，让新的 buff_id 与新的 stat key 被对应攻击/伤害/移动管线读取，并让 OnTick 骰子按 Source 等级缩放。

---

## 任务列表

每个 task 见 `tasks.md`。
