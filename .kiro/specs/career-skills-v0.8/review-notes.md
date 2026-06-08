# Career Skills v0.8 — Review Notes

> 这是一份**滚动 review 备忘录**，记录每个 Phase 完成后发现的问题与决策。
> 后续 task 执行者请优先看这里再开工。

## 2026-05-27 review (Z 阶段完成后)

### ✅ Z 阶段产出物

- `BladeHexCore/src/Combat/CombatScalingMath.cs` 已建立，4 个核心 helper（GetLevelDice / PercentOfMaxHp / PercentOfMaxMana / StatModXLevel + GetWeaponDice）符合规范
- `CareerSkillRegistry.cs` 中绝大多数硬编码 HP/伤害值已被替换
- `职业专属技能.md` 文档已重写

### ⚠️ Review 后又修订的字段（共 9 处）

| 技能 | 旧字段 | 新字段 |
|---|---|---|
| juggernaut_unstoppable | `temp_hp_con_multiplier: 4` | `temp_hp_max_hp_percent: 0.20` |
| spellsword_rune_imbue | `fire_dice_count/sides: 1/6` + `burn_dice_count/sides: 1/6` | `fire_level_dice + fire_dice_count_mult: 1` + `burn_level_dice + burn_dice_count_mult: 1` |
| arcanearcher_homing_shot | `arcane_dice_count/sides: 1/8` | `arcane_level_dice + arcane_dice_count_mult: 1` |
| ironweaver_rune_barricade | `barricade_hp_con_multiplier: 8` + `destroy_aoe_dice_count/sides: 2/6` | `barricade_hp_max_hp_percent: 0.5` + `destroy_aoe_level_dice + destroy_aoe_dice_count_mult: 2` |
| skullcrusher_crush_weakpoint | `lost_hp_damage_per_10pct: 1` | `lost_hp_damage_dice_per_10pct: 1` + `lost_hp_damage_level_dice: true` |
| crusader_arcane_charge | `trail_heal_dice_count/sides: 1/6` | `trail_heal_max_hp_percent: 0.05` |
| ironbulwark_martyrs_guard | `temp_hp_cha_multiplier: 3` | `temp_hp_max_hp_percent: 0.15` |
| stonesaint_stone_body | `expire_aoe_dice_count/sides: 2/8` | `expire_aoe_level_dice + expire_aoe_dice_count_mult: 2` |
| tempestlord_inferno_surge | `fire_dice_count/sides: 3/6` | `fire_level_dice + fire_dice_count_mult: 3` |
| twilight_walker_stride | `low_hp_regen_dice_count/sides: 1/6` | `low_hp_regen_max_hp_percent: 0.05` |

### Phase B/C 执行者注意事项（取数公式标准）

ExecXxx 函数读取这些键时，统一用以下转换：

#### 临时 HP / 治疗

```csharp
// 旧（禁止）
int tempHp = (int)p["temp_hp_con_multiplier"] * Mod(caster.Con);

// 新（标准）
float pct = p.GetValueOrDefault("temp_hp_max_hp_percent", 0.0f).AsSingle();
int tempHp = CombatScalingMath.PercentOfMaxHp(caster.Model, pct);
```

#### 等级骰伤害

```csharp
// 旧（禁止）
int diceCount = (int)p["fire_dice_count"];
int diceSides = (int)p["fire_dice_sides"];
int dmg = RollDice(diceCount, diceSides);

// 新（标准）
if (p.GetValueOrDefault("fire_level_dice", false).AsBool()) {
    int countMult = p.GetValueOrDefault("fire_dice_count_mult", 1).AsInt32();
    var (count, sides) = CombatScalingMath.GetLevelDice(caster.Data.Level);
    int dmg = RollDice(count * countMult, sides);
    // tier 缩放在最外层乘
    dmg = (int)(dmg * GetTier(caster));
}
```

#### 召唤物 HP（Rune Barricade）

```csharp
float pct = p.GetValueOrDefault("barricade_hp_max_hp_percent", 0.5f).AsSingle();
int barricadeHp = CombatScalingMath.PercentOfMaxHp(caster.Model, pct);
```

#### 持续治疗（trail / regen）

```csharp
// 每次触发（单位经过格子 / 回合开始）
float pct = p.GetValueOrDefault("trail_heal_max_hp_percent", 0.05f).AsSingle();
int heal = CombatScalingMath.PercentOfMaxHp(beneficiary.Model, pct);
beneficiary.Heal(heal);
```

#### 等级骰 + 数量倍率（如 inferno_surge 的 3×LevelDice）

```csharp
if (p.GetValueOrDefault("fire_level_dice", false).AsBool()) {
    int countMult = p.GetValueOrDefault("fire_dice_count_mult", 1).AsInt32();
    var (count, sides) = CombatScalingMath.GetLevelDice(caster.Data.Level);
    // count 是单个 LevelDice 的骰子数；这里再乘 countMult（3 → 3 个 LevelDice 单位）
    int totalCount = count * countMult;
    int dmg = RollDice(totalCount, sides);
}
```

### Phase A 执行者注意事项（BuffRegistry）

注册新 buff 时，**OnTick 仍可写"基线骰子"（如 1d6）作为占位**，运行时由 `BuffTurnHooks` 在 Source 单位上下文中按等级缩放（`max(1, Source.Level/4)` 倍骰子数）。Phase E 才会接入这个缩放。

如果 buff 是"有 Source 单位的"（如燃烧来自施法者），在 Apply 时把 `SourceUnitId` 传入，否则缩放无法按 Source 等级算。

### 不需要缩放的字段（保留绝对值）

- **士气类**：`morale_target_penalty: 25` / `morale_ally_bonus: 18` 等——士气在 -60~+40 固定范围，不应缩放 ✅
- **AP 类**：`ap_recovery_on_kill: 5` / `kill_ap_recovery: 5`——AP 在 10~20 固定范围 ✅
- **DR 阈值修正**：`dr_threshold_bonus: 5` / `dr_threshold_reduction: 3`——DR 阈值是绝对值 3~18，不缩放 ✅
- **暴击阈值修正**：`crit_threshold_reduction: 3` / `crit_threshold_override: 12`——暴击阈值在 13~20 固定范围 ✅
- **AC 修正**：`ac_bonus: 4` / `ac_reduction: 1`——AC 是 d20 检定的对照值，不缩放 ✅
- **法术 DC 修正**：`spell_dc_bonus: 3` / `dc_per_hp_lost_10pct: 1`——同上 ✅

### Phase F 加入的原因

CareerSkillResolver.Has* 系列方法目前检查"角色是否拥有职业称号"——v0.8 设计下应改为检查"buff 是否激活"。详见 `tasks.md` Phase F。
