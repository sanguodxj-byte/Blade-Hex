# Career Skills v0.8 — Design

## 目标

把 63 个职业大招的代码层全部落地，与文档保持一致。

## 总体架构

```
┌──────────────────────────────────────────────────────────────────┐
│  CareerSkillRegistry (✅ 已完成)                                 │
│   - 63 条技能配置，全 Active + OncePerBattle                     │
│   - 每条注入 tier_multiplier / attribute_count / consume_all_ap  │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ caster.GetCareerSkill().EffectId
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│  CareerSkillExecutor.ExecuteCareerSkill (✅ 主入口已改造)        │
│   - AP ≥ 1 即可释放                                              │
│   - 释放后: ConsumeAp(CurrentAp) + HasActed = true               │
│   - switch (skill.EffectId) → 各 ExecXxx                         │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ effectParams + tier_multiplier
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│  ExecXxx 函数 (⚠️ 需要补全 + 倍率应用)                           │
│   - 即时效果(伤害/治疗/位移/挂状态)                              │
│   - 给单位挂 buff(BuffSystem.Apply / ApplyDirect)                │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ buff_id
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│  BuffRegistry (⚠️ 需要新注册 ~30 个 buff)                        │
│   - 每个 buff_id 都要在这里 Register 模板                        │
│   - 有 Modifiers 的（AC/伤害/移动）走 StatModifier               │
│   - 有 OnTick 的（DOT/HOT）走 TickEffect                         │
│   - 有触发器的（反击/受伤回血）走 BuffTrigger                    │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ ActiveBuffs[]
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│  CombatResolver / BuffDamageHooks / BuffAttackHooks              │
│   (⚠️ 部分新 stat key 需要在 ResolveStatModifiers 调用方接入)    │
└──────────────────────────────────────────────────────────────────┘
```

## 倍率应用规范

每个 `ExecXxx` 函数开头必须读取 `tier_multiplier`：

```csharp
private static void ExecXxx(Unit caster, ..., Godot.Collections.Dictionary result)
{
    var skill = caster.GetCareerSkill()!;
    float tier = skill.EffectParams.GetValueOrDefault("tier_multiplier", 1.0f).AsSingle();
    int attrCount = skill.EffectParams.GetValueOrDefault("attribute_count", 1).AsInt32();

    // 关键数值用 Scale 函数缩放
    int duration = ScaleDuration(BASE_DURATION, tier);
    int damageBonus = ScaleInt(BASE_DAMAGE_BONUS, tier);
    int diceCount = ScaleDice(BASE_DICE_COUNT, tier);
    // ...
}

// 统一 helper（建议放在 CareerSkillExecutor 文件顶部 partial class 中）
private static int ScaleDuration(int baseDuration, float tier)
    => System.Math.Max(1, (int)System.Math.Floor(baseDuration * tier));

private static int ScaleInt(int baseValue, float tier)
    => (int)System.Math.Ceiling(baseValue * tier);

private static int ScaleDice(int baseDice, float tier)
    => System.Math.Max(1, (int)System.Math.Floor(baseDice * tier));

private static float ScaleFloat(float baseValue, float tier)
    => baseValue * tier;
```

**不缩放的字段**：范围（aura_range / charge_distance）—— 否则 6 属性技能会覆盖整个战场。

## Buff 注册模板

所有职业技能新挂的 buff 命名规则：`buff_id = `（与 docs / Registry 中的 effectParams["buff_id"] 完全对应）。

### 通用模板

```csharp
Register("<buff_id>", new BuffInstance
{
    Id = "<buff_id>",
    Name = "<显示名>",
    Description = "<UI 描述>",
    IconId = "<选一个现成图标>",  // IconBless / IconShield / IconFire / IconPoison / IconSlash / IconDark / IconStun / IconHoly / IconLightning / IconCrush / IconPierce / IconIce / IconFreeze
    IsNegative = false,           // 正面 false / 负面 true
    Duration = -1,                // 由 Apply 调用方注入实际值
    Tags = new[] { "buff", "career" },
    Modifiers = new() {
        new StatModifier { Stat = "ac",            Layer = ModifierLayer.Base, Value = 3 },
        new StatModifier { Stat = "damage",        Layer = ModifierLayer.More, Value = 0.30f },
        // ...
    },
    OnTick = null,        // 仅 DOT/HOT 才需要
    Triggers = new() { /* OnTakeDamage / OnDealDamage / OnHit 等 */ },
});
```

### Stat key 字典（供 Modifier 使用）

只列本次 v0.8 用得到的：

| Stat 名 | 含义 | Layer | 已被战斗管线读取？ |
|---|---|---|---|
| `ac` | 闪避 | Base | ✅ CombatStats.GetEffectiveAc |
| `attack_bonus` | 攻击命中加值 | Base | ✅ |
| `damage` | 伤害百分比 | Increased / More | ✅ DamageCalcPipeline |
| `damage_reduction_flat` | 平减伤 | Base | ✅ BuffDamageHooks |
| `damage_reduction_percent` | 百分比减伤 | Base | ✅ BuffDamageHooks |
| `damage_taken_final_mult` | 受伤倍率 | FinalMult | ✅ |
| `temp_hp_amount` | 临时 HP（每点 = 一点护盾） | Base | ✅ AbsorbWithTempHp |
| `speed` | 移动力 | Base | ⚠️ 需在 MoveCommand 接入 |
| `dr_threshold` | 装甲阈值 | Base | ⚠️ 需在 CombatStats.GetDrThreshold 接入 |
| `crit_threshold` | 暴击阈值 | Base | ⚠️ 需在 CombatStats.GetCritThreshold 接入 |
| `crit_damage_mult` | 暴击倍率 | More | ⚠️ 需在 CombatStats.GetCritMultiplier 接入 |
| `move_ap_reduction` | 每格移动 AP 减免 | Base | ⚠️ 需在 MoveCommand 接入 |
| `save_bonus` | 全部豁免加值 | Base | ⚠️ 需在 RPGRuleEngine.MakeSave 调用方接入 |
| `morale_floor` | 士气下限 | Override | ⚠️ 需在 MoraleSystem.ChangeMorale 接入 |
| `immune_fear` | 免疫恐惧 | Override(=1) | ⚠️ 需在 fear buff 应用前检查 |
| `immune_negative` | 免疫所有负面 | Override(=1) | ⚠️ 需在 BuffSystem.Apply 检查 |
| `cannot_act` | 完全不能行动 | Override(=1) | ⚠️ 需在 InputController/AI 检查 |
| `cannot_move` | 不能移动 | Override(=1) | ⚠️ 需在 MoveCommand 检查 |

⚠️ 标记的 stat 在 Phase E 才需要接入战斗管线。Phase A/B/C 只要求注册到 BuffRegistry 并在 Modifier 中正确填写即可——即使现在没人读，buff 仍然能挂上身体，UI 能显示。

### Triggers / OnTick / Override 字段

复杂 buff（如反击型、临时 HP 型、致死保护型）需要写 `Triggers` 或 `OnTick`。Phase A 暂不实现复杂 trigger 逻辑，只要把字段填对，BuffSystem.FireTriggers 会自动调度。

## Phase 划分（按依赖顺序）

| Phase | 内容 | 输出 | 依赖 |
|---|---|---|---|
| A | 注册全部 ~30 个 buff 到 BuffRegistry | BuffRegistry.cs | - |
| B | 补全 27 个缺失的 ExecXxx 函数 | CareerSkillExecutor.cs | A |
| C | 改造已有 27 个 ExecXxx 应用倍率 | CareerSkillExecutor.cs | A, B |
| D | AI 决策（让 NPC 也用大招） | AIController.cs | B + C |
| E | 战斗管线接入（消费新 stat key） | 多文件 | A |

每个 Phase 内部按职业属性数（1→6）拆分，让快速模型可以并行处理。

## 验收标准（共同）

每个 task 完成后：

1. `dotnet build Blade&Hex/BladeHexCore/BladeHexCore.csproj` 通过
2. `dotnet build Blade&Hex/BladeHexFrontend/BladeHexFrontend.csproj` 通过
3. 0 errors（既有警告无视）
4. 修改文件不超出该 task 范围
