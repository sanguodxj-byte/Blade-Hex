# Career Skills v0.8 — Tasks (粒度细化版)

> 每个 task 设计为快速模型 30 分钟内可独立完成。
>
> **执行前必读**（每个 task 都要读）：
> 1. `Blade&Hex/.kiro/specs/career-skills-v0.8/requirements.md`（10 分钟）
> 2. `Blade&Hex/.kiro/specs/career-skills-v0.8/design.md`（5 分钟）
> 3. `Blade&Hex/.kiro/specs/career-skills-v0.8/review-notes.md`（5 分钟，含取数公式标准）
>
> **必读上下文**（按 task 选取）：
> - `Blade&Hex/BladeHexCore/src/SkillTree/CareerSkillRegistry.cs`（数据权威源）
> - `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffRegistry.cs`（Phase A 改）
> - `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffInstance.cs`（数据结构）
> - `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffSystem.cs`（API）
> - `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`（Phase B/C 改）
> - `Blade&Hex/BladeHexCore/src/Combat/CombatScalingMath.cs`（缩放工具）
> - `Blade&Hex/docs/职业专属技能.md`（每条技能的设计描述）
>
> **通用规则**：
> - 修改完后必须运行：`dotnet build "Blade&Hex/BladeHexCore/BladeHexCore.csproj"` 与 `dotnet build "Blade&Hex/BladeHexFrontend/BladeHexFrontend.csproj"`，两个都必须 0 错误
> - 不要超出 task 约束的"允许修改文件"列表
> - 不要修改 `requirements.md` / `design.md` / `review-notes.md` / `tasks.md`
> - 提交前 grep `// TODO` / `// FIXME` 确认没有遗留

---

## Phase A — Buff 注册（5 个 task）

每个 task 只追加新 buff 模板到 `BuffRegistry.RegisterAll()` 末尾，不动其它代码。

> **统一模板**（所有 task 共用，写在 `// ====== 职业大招 buff (v0.8) ======` 注释段落内）：
>
> ```csharp
> Register("<buff_id>", new BuffInstance
> {
>     Id = "<buff_id>",
>     Name = "<显示名>",
>     Description = "<UI 描述,简短>",
>     IconId = "<图标>",   // 选: IconBless/IconShield/IconFire/IconPoison/IconSlash/IconDark/IconStun/IconHoly/IconLightning/IconCrush/IconPierce/IconIce/IconFreeze
>     IsNegative = <true|false>,
>     Duration = -1,        // -1 = 由 Apply 时注入实际值
>     Tags = new[] { "career", "<其它tag>" },
>     Modifiers = new() {
>         new StatModifier { Stat = "<stat>", Layer = ModifierLayer.Base, Value = <数> },
>         // ...
>     },
>     // OnTick = null  // 仅 DOT/HOT 才填
>     // Triggers = new() { ... }  // 仅触发型才填
>     // CancelTags = new[] { ... }  // 仅互斥才填
>     // BreaksOnAttack = true  // 仅 disguise/silent 类才填
> });
> ```

---

### TASK A1: 注册 6 个单属性 + 4 个双属性 buff

**目标**: 在 `BuffRegistry.RegisterAll()` 末尾追加 10 个 buff 模板。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffRegistry.cs`

**实现步骤**:
1. 找到 `RegisterAll()` 方法末尾
2. 追加注释行 `// ====== 职业大招 buff (v0.8) ======`
3. 按下表注册 10 个 buff，全部使用 `Duration = -1`、`Tags` 包含 `"career"`

| buff_id | Name | IsNeg | IconId | Modifiers / 备注 |
|---|---|---|---|---|
| `armor_break` | 碎甲 | true | IconCrush | `dr_threshold` Base -3, `ac` Base -1 |
| `volley` | 箭雨 | false | IconPierce | `attack_bonus` Base 2（Tags 加 `"ranged_only"` 标记） |
| `living_wall` | 壁垒 | false | IconShield | `ac` Base 3, `dr_threshold` Base 2, `immune_displacement` Base 1 |
| `arcane_overload` | 过载 | false | IconLightning | `next_spell_dc_bonus` Base 5, `next_spell_dice_bonus` Base 2, `next_spell_range_bonus` Base 1 |
| `death_mark` | 致命标记 | true | IconDark | `crit_threshold` Base -3 |
| `battle_hymn` | 战歌 | false | IconBless | `move_ap_reduction` Base 1, `ac` Base 1, `damage` Increased 0.10 |
| `blade_dance` | 剑舞 | false | IconBless | `counter_range_bonus` Base 3 |
| `unstoppable` | 不可阻挡 | false | IconShield | `temp_hp_amount` Base 0（运行时由 ExecXxx 填入实际值，单位是 MaxHP×20%）, `immune_cc` Base 1 |
| `rune_imbue` | 符文武器 | false | IconFire | （Triggers 略，OnDealDamage 触发额外火焰；本 task 只挂占位 `damage` Base 0 + Tags `"fire_imbue"`） |
| `death_sentence` | 终结宣告 | false | IconDark | `crit_threshold` Base -4（条件由 ExecXxx 配合，本 task 不写 Condition） |

**验收**:
- `dotnet build "Blade&Hex/BladeHexCore/BladeHexCore.csproj"` 输出 `0 个错误`
- grep `Register\("armor_break"` 等 10 个 ID 各命中 1 次
- 不修改其它文件

---

### TASK A2: 注册 6 个双属性 + 4 个三属性 buff

**目标**: 在 A1 末尾继续追加 10 个 buff。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffRegistry.cs`

| buff_id | Name | IsNeg | IconId | Modifiers / 备注 |
|---|---|---|---|---|
| `lead_front` | 先驱 | false | IconBless | `move_ap_reduction` Base 1（光环用，Tags 加 `"aura"`） |
| `riposte` | 反击姿态 | false | IconSlash | （Triggers 略，本 task 加 Tags `"counter"`+ `counter_damage_multiplier` Base 1） |
| `homing` | 追踪 | false | IconLightning | `ignore_half_cover` Base 1 |
| `hawks_mark` | 鹰眼标记 | true | IconDark | `crit_threshold` Base -2, `lose_terrain_ac` Base 1 |
| `misdirected` | 被骗 | true | IconDark | `next_attack_disadvantage` Base 1 |
| `mana_shield` | 法力护盾 | false | IconShield | （Triggers 略，本 task 加 Tags `"mana_shield"`） |
| `old_timer` | 老兵 | false | IconBless | `ac` Base 3, `counter_range_bonus` Base 6, `counter_damage_mult` More 0.5 |
| `hold_line` | 阵线 | false | IconShield | `ac` Base 2, `immune_fear` Base 1, `morale_floor` Override -20 |
| `forewarning` | 预知 | false | IconBless | `save_bonus` Base 2 |
| `blood_resonance` | 血脉共鸣 | false | IconFire | `spell_dc_bonus` Base 2, `damage` Increased 0.20 |

**验收**: 同 A1。

---

### TASK A3: 注册 4 个双属性末 + 6 个三属性 buff

**目标**: 在 A2 末尾继续追加 10 个 buff。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffRegistry.cs`

| buff_id | Name | IsNeg | IconId | Modifiers / 备注 |
|---|---|---|---|---|
| `fate_protect` | 庇护 | false | IconBless | （Triggers 略，本 task 加 Tags `"fate_protect"`） |
| `iron_rush` | 武圣 | false | IconShield | `ac` Base 4, `damage` Base 2 |
| `spellweave` | 奥战 | false | IconLightning | `damage` Base 0（叠层效果由 ExecXxx 在攻击/施法时增加，本 task 仅占位） + Tags `"spellweave"`, `MaxStacks = 3` |
| `hawkeye_aura` | 鹰眼 | false | IconPierce | `damage` Increased 0.25（Tags 加 `"ranged_only"`） |
| `champion_aura` | 战神 | false | IconBless | `attack_advantage` Override 1 |
| `crush_weak` | 碎颅 | false | IconCrush | `dr_pen_bonus` Base 3 |
| `gaze_ruin` | 毁灭凝视 | true | IconDark | `crit_threshold` Override 12 |
| `dread_aura` | 恐惧光环 | false | IconDark | `morale_drain_per_turn` Base 5 + Tags `"aura"` |
| `vanguard` | 先驱奥术 | false | IconBless | `rear_ally_charge_advantage` Base 1 |
| `shadow_form` | 暗影 | false | IconDark | `first_move_teleport` Base 1 |

**验收**: 同 A1。

---

### TASK A4: 注册 4 个三属性末 + 6 个四属性 buff

**目标**: 在 A3 末尾继续追加 10 个 buff。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffRegistry.cs`

| buff_id | Name | IsNeg | IconId | Modifiers / 备注 |
|---|---|---|---|---|
| `hunters_mark` | 猎杀标记 | true | IconDark | `advantage_for_caster` Base 1（来源由 Source 字段标记，本 task 仅写 Modifier） |
| `disguise` | 伪装 | false | IconDark | `ai_ignores_self` Base 1 + `BreaksOnAttack = true` |
| `star_map` | 星图 | false | IconBless | `free_teleport_per_turn` Base 1 |
| `mirror_image` | 镜像 | false | IconBless | `attacker_disadvantage_while_phantom` Base 1 |
| `tailwind` | 顺风 | false | IconBless | `move_ap_reduction` Base 1, `attack_bonus` Base 1 |
| `bulwark_lore` | 知识壁垒 | false | IconShield | `spell_damage_check_bonus` Base 5 |
| `iron_law` | 铁律 | false | IconCrush | `no_charge` Base 1, `no_stealth` Base 1, `morale_change_divisor` Base 2（Tags 加 `"aura"`，作用域由 ExecXxx 处理） |
| `martyrs_guard` | 殉道 | false | IconHoly | （Triggers 略，本 task 加 Tags `"martyr"`） |
| `twist_fate` | 天选 | false | IconBless | `attack_bonus` Base 1, `save_bonus` Base 1 |
| `omnibus` | 通鉴 | false | IconBless | `attack_bonus` Base 2, `save_bonus` Base 2 |

**验收**: 同 A1。

---

### TASK A5: 注册 6 个四属性末 + 6 个五属性 + 1 个全属性 buff

**目标**: 在 A4 末尾继续追加 13 个 buff。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexCore/src/Combat/Buff/BuffRegistry.cs`

| buff_id | Name | IsNeg | IconId | Modifiers / 备注 |
|---|---|---|---|---|
| `wind_favor` | 风眷 | false | IconLightning | （叠层效果由 ExecXxx 处理，本 task 占位 + `MaxStacks = 5` + Tags `"wind_favor"`） |
| `feral_self` | 野性 | false | IconBless | `speed` Base 2, `detect_stealth` Base 1, `damage` Base 2（Tags 加 `"vs_beast"`） |
| `wolf_pack` | 狼群 | false | IconBless | `move_ap_reduction` Base 1（Tags 加 `"vs_beast"`） |
| `puppet_aura` | 操纵 | false | IconDark | `broken_enemy_skip_attack` Base 1 |
| `silent_strike` | 无声 | false | IconDark | `stealth_attack_no_break` Base 1, `damage` More 0.5（首次攻击 Tags `"first_attack"`） |
| `harbinger` | 毁灭预兆 | false | IconFire | `fixed_ac` Override 10（DC 加成由 ExecXxx 动态计算，本 task 只放固定 AC） |
| `stone_body` | 石化之躯 | false | IconCrush | `cannot_act` Base 1, `dr_threshold` Base 5, `immune_negative` Base 1, `hp_floor_1` Base 1 |
| `iron_grip` | 统御 | false | IconBless | `immune_fear` Base 1, `morale_floor` Override -30, `attack_bonus` Base 2, `ac` Base 1, `no_retreat` Base 1 |
| `deep_chains` | 锁链 | true | IconCrush | `cannot_move` Base 1, `no_defend` Base 1, `attack_disadvantage` Base 1 |
| `storm_banner` | 战旗 | false | IconBless | `move_ap_reduction` Base 1（Tags 加 `"aura"`） |
| `lone_saint` | 独行 | false | IconBless | `ac` Base 4, `damage` Base 4, `crit_threshold` Base -2（条件由 ExecXxx 处理） |
| `war_king` | 王道 | false | IconBless | `attack_bonus` Base 2, `damage` Base 2, `max_hp_percent_bonus` Base 0.05 |
| `sky_hunter` | 猎手 | false | IconPierce | `attack_bonus` Base 3（限标记目标）, `damage` Base 5（限标记目标）, `crit_threshold` Base -2（HP<50% 时） |
| `myriad` | 万象 | false | IconLightning | `melee_damage_bonus` Base 5, `spell_dc_bonus` Base 3, `move_ap_reduction` Base 1, `armor_ap_penalty_reduction` Base 2 |
| `jack_of_all` | 通识 | false | IconBless | `node_bonus_percent` Base 0.20, `applies_to_keystone` Base 1, `save_bonus` Base 3, `attack_bonus` Base 2 |
| `mountain_stance` | 山岳 | false | IconShield | `cannot_move` Base 1, `immune_displacement` Base 1, `ac` Base 4, `dr_threshold` Base 6, `immune_negative` Base 1, `melee_damage_bonus` Base 6 |
| `twilight_stride` | 暮光 | false | IconBless | `no_aoo_on_move` Base 1, `can_cross_enemies` Base 1, `attack_bonus` Base 2 |
| `savage` | 野蛮 | false | IconCrush | `auto_target_lowest_hp` Base 1, `advantage_vs_low_hp` Base 1, `kill_ap_recovery` Base 5 |
| `tyrant_wrath` | 暴君 | false | IconFire | `immune_damage_debuff` Base 1, `immune_morale_damage_penalty` Base 1, `immune_damage_reduction` Base 1, `damage` Increased 0.30, `crit_threshold` Override 20 |
| `lone_op` | 独行术 | false | IconDark | `ac` Base 4, `move_ap_reduction` Base 1, `damage` Base 4, `crit_threshold` Base -2（条件减半由 ExecXxx 处理） |
| `paragon` | 万能 | false | IconHoly | `all_stat_bonus` Base 3, `node_bonus_percent` Base 0.30, `attack_bonus` Base 3, `ac` Base 3, `dr_threshold` Base 3, `speed` Base 2, `save_bonus` Base 3, `morale_floor` Override 0, `lockout_other_career_skills` Base 1 |

**验收**: 同 A1。完成后 BuffRegistry 应包含 ~63 个 buff（原 16 + 新 ~47）。

---

## Phase B — Executor 补全（9 个 task）

每个 task 实现 3 个 ExecXxx 函数，添加到 `CareerSkillExecutor.cs` 的 switch + 文件末尾的方法定义区。

> **每个 ExecXxx 的统一骨架**（所有 task 共用模板）：
>
> ```csharp
> private static void Exec<XxxName>(Unit caster, Vector2I targetCell, Map.HexGrid? grid,
>     System.Collections.Generic.IEnumerable<Unit> allUnits,
>     System.Collections.Generic.IEnumerable<Unit> allies,
>     System.Collections.Generic.IEnumerable<Unit> enemies,
>     Godot.Collections.Dictionary result)
> {
>     var skill = caster.GetCareerSkill()!;
>     var p = skill.EffectParams;
>     float tier = p.GetValueOrDefault("tier_multiplier", 1.0f).AsSingle();
>     string buffId = p.GetValueOrDefault("buff_id", "").AsString();
>     int baseDuration = p.GetValueOrDefault("buff_duration", 2).AsInt32();
>     int duration = ScaleDuration(baseDuration, tier);
>
>     // 1. 即时效果（如恢复 HP / 法力 / 立即攻击 / 冲锋 / 传送 — 各 task 不同）
>     // 2. 给目标挂 buff
>     if (caster.Data != null && !string.IsNullOrEmpty(buffId)) {
>         BladeHex.Combat.Buff.BuffSystem.Apply(caster.Data, buffId, duration,
>             sourceUnitId: (int)caster.GetInstanceId());
>     }
>     // 3. 写入 result["applied_buff"] 等 UI 描述字段
>     AddSelfBuffResult(result, buffId, duration);
> }
> ```
>
> Scale 工具函数（`ScaleDuration` / `ScaleInt` 等）见 `design.md` 与 review-notes.md。Phase C 之前要先添加这些 helper 到 CareerSkillExecutor 顶部 partial class（可在 B1 顺手加上）。

---

### TASK B1: ExecJuggernaut + ExecExecutionerDeathSentence + ExecWarlord

**目标**: 实现 3 个新 ExecXxx 并加 case，**先添加 Scale helper 工具函数**（B 阶段第一个 task）。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**步骤**:
1. 在文件顶部 main partial class 内添加 5 个 Scale helper（GetTier / ScaleDuration / ScaleInt / ScaleDice / ScaleFloat）—— 见 review-notes.md "取数公式标准" 章节；只加这一次，B2-B9 直接复用
2. 在 switch（约 90 行）添加：
   ```csharp
   case "juggernaut_unstoppable": ExecJuggernaut(caster, targetCell, grid, enemies, result); break;
   case "executioner_death_sentence": ExecExecutionerDeathSentence(caster, result); break;
   case "warlord_lead_from_front": ExecWarlord(caster, targetCell, grid, enemies, allies, result); break;
   ```
3. 在文件末尾追加 3 个方法定义。

**ExecJuggernaut**: 立即直线冲锋 `charge_distance`（默认 3）格，挡路敌人推开 1 格；终点对相邻敌方做正常冲锋攻击（伤害×1.5）；然后挂 `unstoppable` buff。
- `temp_hp_max_hp_percent` 决定临时 HP，构造一个直接 BuffInstance 用 `BuffSystem.ApplyDirect`，把 `temp_hp_amount` Modifier 的 Value 设为 `CombatScalingMath.PercentOfMaxHp(caster.Model, pct) * tier`

**ExecExecutionerDeathSentence**: 直接 `BuffSystem.Apply(caster.Data, "death_sentence", duration)`，无即时效果。

**ExecWarlord**: 立即冲锋 `charge_distance` 格；冲锋终点周围 `ally_followup_distance` 格内每个友军免费向同方向移动 1 格（用 `Vector2I` 计算偏移，`grid.GetCell` 取目标格，移动单位不消耗 AP 不触发借机）；然后挂 `lead_front` 自身光环 buff。

**约束**:
- 不要超出 60 行新代码（含 helper、case、3 个方法）
- 推友/破障细节如 grid API 缺失，写 `result["needs_phase_e_charge_push"] = true` 占位

**验收**:
- `dotnet build` Frontend 通过
- switch 中 default 分支 grep 不再命中这 3 个 effectId
- helper 函数定义只此一次（不重复）

---

### TASK B2: ExecDuelistRiposte + ExecBattlemageManaShield + ExecSageForewarning

**目标**: 3 个简单"挂自身 buff"型，无即时效果。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**步骤**:
1. switch 添加 3 个 case
2. 实现 3 个方法，每个都是 `BuffSystem.Apply(caster.Data, <buffId>, duration); AddSelfBuffResult(result, ...);`

| effectId | buffId | duration 字段 |
|---|---|---|
| `duelist_riposte` | `riposte` | `buff_duration` (基线 3) |
| `battlemage_mana_shield` | `mana_shield` | `buff_duration` (基线 3) |
| `sage_forewarning` | `forewarning` | `buff_duration` (基线 3) |

**约束**: 每个方法 ≤ 12 行。

**验收**: 同 B1。

---

### TASK B3: ExecProphet + ExecChosenOne

**目标**: 2 个有指定目标的 buff 类技能。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecProphet**: 从 `targetCell` 找友军，`BuffSystem.Apply(target.Data, "fate_protect", duration, sourceUnitId: caster id)`。
- 如果 target 不是友军，FailResult(result, "目标必须是友军")

**ExecChosenOne**: 立即写入 `result["instant_reroll_next_d20"] = true`（Phase E 由 d20 系统消费）；然后给 caster 挂 `twist_fate` buff。

**约束**: 每个方法 ≤ 20 行。

**验收**: 同 B1。

---

### TASK B4: ExecSkullcrusher + ExecOverlord + ExecArchsage

**目标**: 3 个 STR/CON 类技能，含一次即时近战攻击与揭示信息。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecSkullcrusher**:
- 从 targetCell 找相邻敌方
- 计算"已损 HP%"，骰子数 = `(lostHpPercent / 10) × dice_count_per_10pct`
- `if (lost_hp_damage_level_dice == true)` 用 `CombatScalingMath.GetLevelDice(caster.Data.Level)` 取 (count, sides)
- 调 `CombatResolver.ResolveAttack`（带 dr_pen_bonus），把额外伤害骰加进结果
- 攻击完后给 caster 挂 `crush_weak` buff

**ExecOverlord**: 直接 `BuffSystem.Apply(caster.Data, "dread_aura", duration)`；无即时效果。

**ExecArchsage**:
- 立即扫描 enemies，写入 `result["lowest_saves"] = Dict<unitId(Variant), saveType(string)>`（每个敌方算 fortitude/reflex/will 三个豁免值，取最低的那个 type）
- 然后对 allUnits（限 `caster.Data.IsEnemy == ally.Data.IsEnemy`）逐一 `BuffSystem.Apply(ally.Data, "omnibus", duration)` —— 全场友军生效

**验收**: 同 B1。

---

### TASK B5: ExecZephyrMaster + ExecWarchief + ExecSilentDeath

**目标**: 3 个四属性自身 buff 型。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecZephyrMaster**: 直接挂 `wind_favor` buff，无即时效果。

**ExecWarchief**: 给 caster 挂 `feral_self`；遍历 allies 中距离 caster ≤ `ally_aura_range`(=3) 的友军挂 `wolf_pack`。

**ExecSilentDeath**:
- 立即给 caster 挂 status_effect "invisibility"（用 `result["status_effects"] += {target_id, effect_id, duration}` 让外层 ApplyStatusEffects 处理）
- 然后给 caster 挂 `silent_strike` buff

**验收**: 同 B1。

---

### TASK B6: ExecLordOfRuin + ExecDreadGeneral + ExecIronwallHunter

**目标**: 3 个四属性技能，1 个含即时射击。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecLordOfRuin**: 直接挂 `harbinger` buff。无即时效果。

**ExecDreadGeneral**: 遍历 allies 中距离 caster ≤ `aura_range`(=3) 的友军挂 `iron_grip` buff。

**ExecIronwallHunter**:
- 标记 targetCell 上的敌方目标（写入 result["marked_target_id"]）
- 立即对其做一次远程射击：`CombatResolver.ResolveAttack(caster, target, grid)`，命中后伤害 +5（注：5 是 absolute "+ X dmg" 不是 d6，按 review-notes.md 的"伤害绝对值缩放"做 `ScaleInt(5, tier)`）
- 给 caster 挂 `sky_hunter` buff

**验收**: 同 B1。

---

### TASK B7: ExecLoneSaint + ExecLoneShadow + ExecWrathAvatar

**目标**: 3 个"独行/野蛮"类技能。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecLoneSaint**: 直接挂 `lone_saint` buff（条件由 buff modifier 内部处理，本函数不做条件判断）。

**ExecLoneShadow** (即 `loneshadow_lone_operative`):
- 立即给 caster 挂 status_effect "invisibility"，duration = ScaleDuration(4, tier)
- 给 caster 挂 `lone_op` buff

**ExecWrathAvatar**:
- 在 enemies 中找 HP/MaxHP 比例最低的敌人
- 立即对其做一次必中近战攻击（`CombatResolver.ResolveAttack` 调用时传 `accuracyMod=99` 做"必中" hack，或直接构造命中结果）
- 加优势 + 暴击阈值 -3
- 给 caster 挂 `savage` buff

**验收**: 同 B1。

---

### TASK B8: ExecEmissary + ExecIronTyrant + ExecTwilightWalker

**目标**: 3 个含即时恢复/纯 buff 类。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecEmissary**:
- `instant_heal_hp_percent` 转换：`int heal = CombatScalingMath.PercentOfMaxHp(caster.Model, pct);` 调 `caster.Heal(heal)`
- `instant_recover_mana_percent` 转换：`int mana = CombatScalingMath.PercentOfMaxMana(caster.Data, pct);` 调 `caster.Data.CurrentMana = Math.Min(MaxMana, CurrentMana + mana)`
- 给 caster 挂 `jack_of_all` buff

**ExecIronTyrant**: 直接挂 `tyrant_wrath` buff。

**ExecTwilightWalker** (即 `twilight_walker_stride`):
- 立即恢复 HP（同 Emissary 的 PercentOfMaxHp）
- 给 caster 挂 `twilight_stride` buff

**验收**: 同 B1。

---

### TASK B9: ExecParagon + ExecMyriadBattlemage + ExecWarKing

**目标**: 3 个高属性数职业的"开神"型技能。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**ExecParagon**:
- `instant_heal_hp_percent`（30%）+ `instant_recover_mana_percent`（30%）
- 清除所有负面状态：遍历 `caster.Data.Runtime.ActiveBuffs`，移除 `IsNegative=true` 的 buff
- 给 caster 挂 `paragon` buff

**ExecMyriadBattlemage**: 直接挂 `myriad` buff。

**ExecWarKing** (即 `warking_domination`):
- 周围 4 格友军士气 +18，敌方士气 -18（`MoraleSystem.ChangeMorale`）
- 周围 3 格友军挂 `war_king` buff

**验收**: 同 B1。完成后 default 分支不再命中任何 effectId。

---

## Phase C — 倍率应用到现有 27 个 ExecXxx（5 个 task）

每个 task 改 5-7 个现有 ExecXxx 函数，在 `CareerSkillExecutor.cs` 内部把硬编码值替换为 `Scale*(base, GetTier(caster))`。

> **统一改造模式**:
>
> ```csharp
> // 改造前
> int duration = 2;
> int drReduction = 5;
>
> // 改造后
> float tier = GetTier(caster);   // B1 已加的 helper
> int duration = ScaleDuration(2, tier);
> int drReduction = ScaleInt(3, tier);  // 注意：v0.8 已把基线从 5 调整为 3
> ```
>
> **重要**: 数值基线必须以 `Blade&Hex/docs/职业专属技能.md` v0.8 描述为准；不要套用 ExecXxx 中残留的旧硬编码。

---

### TASK C1: 5 个单属性 ExecXxx 应用倍率

**职业**: `warrior_armor_break` / `guardian_living_wall` / `mage_arcane_overload` / `assassin_expose_weakness` / `bard_battle_hymn`

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**步骤**: 5 个 ExecXxx 函数顶部加 `float tier = GetTier(caster);`；把所有 `duration = 2` 之类的硬编码改为 `ScaleDuration(2, tier)`，把 `+5` / `+3` / `+1` 加值改为 `ScaleInt(...)` 或保留（AC/DR 阈值类不缩放——见 review-notes.md "不需要缩放的字段"）。

**仅缩放这些字段**:
- `duration`：用 ScaleDuration
- `damage_bonus_percent`（如 0.10）：用 `ScaleFloat`
- 临时 HP / 治疗 / 伤害骰 数量：用 ScaleDice 或 ScaleInt

**不缩放**:
- AC, DR 阈值, 暴击阈值, 法术 DC, 士气, AP

**约束**: ≤ 30 行修改。

**验收**:
- `dotnet build` 通过
- `git diff` 显示 5 个函数共有 `GetTier` 调用
- 没误改 AC/DR/士气/AP 类常数

---

### TASK C2: 7 个双属性 ExecXxx 应用倍率

**职业**: `bladedancer_whirling_strike` / `arcanearcher_homing_shot` / `falconer_hawks_mark` / `rogue_misdirection` / `ironcommander_hold_the_line` / `sorcerer_blood_resonance` / `spellsword_rune_imbue`

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**约束**: tier=1.25 系数，所有 duration 都缩放，伤害骰按"等级骰 × tier"。

**验收**: 同 C1。

---

### TASK C3: 7 个三属性 ExecXxx 应用倍率（前半）

**职业**: `bruiser_iron_rush` / `spellweaver_instant_glyph` / `hawkeye_kill_shot` / `champion_war_cry_charge` / `ironweaver_rune_barricade` / `conqueror_subjugate` / `doomknight_gaze_of_ruin`

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**约束**: tier=1.5。注意 `champion_war_cry_charge` 的 "morale_ally_bonus: 12" 与 "morale_enemy_penalty: 8" **不缩放**（士气是绝对值）。

**验收**: 同 C1。

---

### TASK C4: 8 个三属性 ExecXxx 应用倍率（后半）

**职业**: `crusader_arcane_charge` / `shadowmage_shadow_swap` / `nightstalker_death_mark` / `faceless_identity_theft` / `stargazer_star_map` / `illusionist_mirror_image` / `windwalker_tailwind` / `ironsovereign_iron_law`

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**约束**: tier=1.5。

**验收**: 同 C1。

---

### TASK C5: 5 个四属性 ExecXxx + 1 个五属性应用倍率

**职业**: `shadowlord_puppet_master` / `voidknight_chains_of_deep` / `stormbanner_lightning_raid` / `tempestlord_inferno_surge` / `stonesaint_stone_body` / `mountainlord_mountain_stance`

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillExecutor.cs`

**约束**: tier=1.75（前 5 个）/ 2.0（mountainlord）。

**验收**: 同 C1。

---

## Phase F — CareerSkillResolver 改读 buff（1 个 task）

### TASK F1: 重写 CareerSkillResolver Has* 方法读 buff 而非称号

**目标**: 把 `CareerSkillResolver.cs` 中所有 `HasCareerSkill(unit, effectId)` 替换为 `BuffSystem.HasBuff(unit.Data, buffId)`，让被动效果只在大招激活的 buff 期间生效。

**允许修改的文件**: 仅 `Blade&Hex/BladeHexFrontend/src/View/Combat/CareerSkillResolver.cs`

**步骤**:
1. 在文件顶部 using 处加 `using BladeHex.Combat.Buff;`
2. 用下表替换每个 Has* 方法的实现：

| Has* 方法 | 新实现 buffId |
|---|---|
| `HasEvadeVolley` | `volley` |
| `HasForewarning` | `forewarning` |
| `HasMartyrsGuard` | `martyrs_guard` |
| `HasFixedAc` | `harbinger` |
| `HasFixedCritThreshold` | `tyrant_wrath` |
| `HasDeathSentence` | `death_sentence` |
| `HasCrushWeakPoint` | `crush_weak` |
| `HasBulwarkOfLore` | `bulwark_lore` |
| `HasUndiminishedDamage` | `tyrant_wrath` |
| `HasAuraOfDread` | `dread_aura` |
| `HasIronGrip` | `iron_grip` |
| `HasTwilightStride` | `twilight_stride` |
| `HasLoneOperative` | `lone_op` |
| `HasFeralInstinct` | `feral_self` |
| `HasManaShieldPassive` | `mana_shield` |
| `HasJackOfAllTrades` | `jack_of_all` |
| `HasSavageInstinct` | `savage` |
| `HasRiposte` | `riposte` |
| `HasOldTimer` | `old_timer` |
| `HasBattleHymn` | `battle_hymn` |
| `HasWindFavor` | `wind_favor` |
| `HasOmnibus` | `omnibus` |
| `HasMirrorImage` | `mirror_image` |
| `HasSilentStrike` | `silent_strike` |

3. 改造模板（每个 Has* 一致）：

```csharp
public static bool HasRiposte(Unit unit)
    => unit.Data != null && BuffSystem.HasBuff(unit.Data, "riposte");
```

4. 把 `AddWindStack` / `ClearWindStacks` 改为：
```csharp
public static void AddWindStack(Unit unit) {
    if (unit.Data == null) return;
    var buff = unit.Data.Runtime.ActiveBuffs.Find(b => b.Id == "wind_favor");
    if (buff != null && buff.CurrentStacks < buff.MaxStacks) buff.CurrentStacks++;
}

public static void ClearWindStacks(Unit unit) {
    if (unit.Data == null) return;
    var buff = unit.Data.Runtime.ActiveBuffs.Find(b => b.Id == "wind_favor");
    if (buff != null) buff.CurrentStacks = 1;
}

public static int GetWindFavorRangeBonus(Unit unit) {
    if (unit.Data == null) return 0;
    var buff = unit.Data.Runtime.ActiveBuffs.Find(b => b.Id == "wind_favor");
    return buff?.CurrentStacks ?? 0;
}
```

5. 保留 `HasCareerSkill` / `GetCareerSkillId` / `GetCurrentHymn` / `SetCurrentHymn` / `GetHymnAcBonus` / `GetHymnDamageBonus` / `GetHymnMoveApReduction` 不动（这些是工具方法或战歌专用，不应该改）

**约束**:
- ≤ 80 行修改（多数是简单替换 1-2 行/方法）
- 不要修改其它文件

**验收**:
- `dotnet build "Blade&Hex/BladeHexCore/BladeHexCore.csproj"` 通过
- `dotnet build "Blade&Hex/BladeHexFrontend/BladeHexFrontend.csproj"` 通过
- grep `unit.HasCareerSkillEffect` 在 CareerSkillResolver.cs 中应为 0 命中
- grep `BuffSystem.HasBuff` 在 CareerSkillResolver.cs 中应为约 23 处命中

---

## Phase D & E（后置，单独 spec）

Phase D（AI 决策）与 Phase E（战斗管线接入新 stat key 与 OnTick 等级缩放）不阻塞游戏可玩性，待 Phase A/B/C/F 完成后再单独写一份 spec：`career-skills-v0.8-integration`。

---

## 进度追踪

> 完成一个 task 后在 ✅ 处打勾，commit hash 写在后面。

### Phase A — Buff 注册
- [ ] A1 - 6 单属性 + 4 双属性 buff
- [ ] A2 - 6 双属性 + 4 三属性 buff
- [ ] A3 - 4 双属性末 + 6 三属性 buff
- [ ] A4 - 4 三属性末 + 6 四属性 buff
- [ ] A5 - 6 四属性末 + 6 五属性 + 1 全属性 buff

### Phase B — Executor 补全
- [ ] B1 - juggernaut/executioner/warlord (含 Scale helper 前置)
- [ ] B2 - duelist/battlemage/sage
- [ ] B3 - prophet/chosenone
- [ ] B4 - skullcrusher/overlord/archsage
- [ ] B5 - zephyrmaster/warchief/silentdeath
- [ ] B6 - lordofruin/dreadgeneral/ironwall_hunter
- [ ] B7 - lone_saint/loneshadow/wrathavatar
- [ ] B8 - emissary/irontyrant/twilight_walker
- [ ] B9 - paragon/myriad_battlemage/warking

### Phase C — 倍率应用
- [ ] C1 - 5 单属性
- [ ] C2 - 7 双属性
- [ ] C3 - 7 三属性（前半）
- [ ] C4 - 8 三属性（后半）
- [ ] C5 - 5 四属性 + 1 五属性

### Phase F — Resolver 改读 buff
- [ ] F1 - 重写 23 个 Has* 方法 + 风眷叠层

---

## 并行执行建议

- **Phase A**（5 个 task）可完全并行
- **Phase B**（9 个 task）必须等 Phase A 完成；B1 必须先做（建立 Scale helper）；B2-B9 之后并行
- **Phase C**（5 个 task）必须等 B1 完成（依赖 helper）；之后可并行
- **Phase F**（1 个 task）独立于 B/C，但须等 Phase A 完成（依赖 buff_id 已注册）

理想路径（共 ~20 个 task）：
```
Z (✅) → A1‖A2‖A3‖A4‖A5 → B1 → B2‖B3‖B4‖B5‖B6‖B7‖B8‖B9 ‖ F1
                                  └→ C1‖C2‖C3‖C4‖C5
```
单人 4 小时全部跑完；3 人协作 2 小时。
