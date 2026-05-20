# 战斗数值体系 · 文档/代码差异审计

> 审计日期：2026-05-17
> 范围：`docs/战斗数值体系.md`（v1，旧版）+ `docs/Blade_Hex_战斗数值系统_修订版_v0.6_含节点系统.md`（v0.6，最新）
> 代码位置：`BladeHexCore/src/Combat/`、`BladeHexCore/src/Data/RPGRuleEngine.cs`、`BladeHexFrontend/src/View/Combat/`
>
> **2026-05-17 修订状态**：Wave 1 + Wave 2 全部修复已落地，详见末尾"实装跟踪"章节

## 总览

代码当前实现的是 **v1 + 部分 v0.6** 的混合体。v0.6 文档（2026-05-11-v0.6）做了一次深度修订，引入节点系统、独立穿透判定、盾牌耐久、远程减免、AP 归一化等新机制。代码大部分仍是 v1 模型。

下面分两类盘点：
- **代码已实现/比文档更细**：可以反向更新文档
- **文档已设计但代码未实现**：是真正的 TODO

---

## A. 代码 ≥ 文档（代码做得更细致或不同）

### A.1 暴击系统（v0.6 已实装）
- **文档 v1**：暴击阈值固定 = 20，暴击倍率固定 = 2.0×（士气可调）
- **文档 v0.6**：`WISCritTier = floor(sqrt(max(0, WIS-14)/4))`，阈值 = `20 - WISCritTier`，最低 14；倍率 = `2.0 + WISCritTier × 0.1`
- **代码**（`CombatStats.GetCritThreshold/CritMultiplier`）：✅ **完全按 v0.6 实现**，floor 14。
  - 但 `CombatRuleEngine.GetAdjustedCritThreshold` 仍保留旧的"高士气降低暴击阈值"逻辑，跟 v0.6 的 WIS 暴击曲线**重叠了**——这是一个潜在 bug，需要决定是否保留士气暴击加成。

### A.2 暴击受伤减免（v0.6 已实装）
- **文档 v1**：未提及
- **文档 v0.6**：未明确写公式（只有 11.5 节点 critical_rate 不影响阈值）
- **代码**（`CombatStats.GetCritDamageTakenMultiplier`）：✅ 防御方按 WIS 减少受暴击伤害（`max(0.2, 1.0 - WISCritTier × 0.1)`）—— 代码做得更细，文档应补
  - 注：这是一条**隐性规则**，文档 v0.6 没写但代码已实装

### A.3 装备 AC（DR 平方根）
- **文档 v1**：`AC = 10 + Mod(DEX) + sqrt(DR) + 盾牌AC`
- **文档 v0.6**：与 v1 一致 + `+NodeAC + AuraAC + TemporaryAC`
- **代码**（`CombatStats.GetAc`）：✅ 实装 DR 平方根，且盾牌 AC 也用平方根。**盾牌耐久=0 时自动失效**（与 v0.6 5.3 一致），优于 v1。

### A.4 状态效果实例化系统
- **文档**：只列了"中毒"、"流血"等名字
- **代码**（`UnitRuntimeState.StatusEffectInstance`）：完整实装了 `Duration`、`StatModifiers`、`TickDamageCount/Sides/Type`、`SaveToRemove/SaveDc`、`RemovesEffects`、`BreaksOnAttack`、`CanSpread` 等字段——比文档丰富得多。

### A.5 LOS 命中惩罚（你刚要求加的）
- **文档**：v1 是二元 LOS（被阻挡=不能打）；v0.6 8.5 提了"跨单位射击惩罚"`-2 / unit, 上限 -6`
- **代码**：✅ 实装了**累加路径惩罚**（地形 -4 / 半掩体 -2 / 单位 -2），高地越过免地形惩罚。**比文档更通用**。
  - 文档 v0.6 应补：地形掩体也走同一套累加，不只是"单位"
  - 文档 v0.6 应补：高地攻击者越过低位地形惩罚作废

### A.6 武器精通命中加值
- **文档 v0.6** 4.2：`MasteryHitBonus = floor(MasteryLevel / 3)`（最大 +3）
- **代码**（`CombatStats.GetAttackBonus`）：✅ 完全按公式实装。✅ 而且**不再使用等级专精加成**（v0.6 12.4 强制规定）

### A.7 武器精通伤害加值
- **文档 v0.6** 6.8：每级 +10%，Lv.10 +100%
- **代码**（`CombatStats.RollDamage` 第 294 行）：✅ `masteryBonus = masteryLevel * 0.1f` 完全一致

---

## B. 文档已设计但代码未实现

### B.1 ⚠️ 高优先：穿透判定独立 d20（v0.6 6.3）
- **文档 v0.6** 6.3：穿透使用**独立的 d20**，公式 `d20_Pen + WeaponPen + STRPenBonus ≥ ArmorDR`
- **代码**：`BattleUnitModel.ApplyDamage` 第 171 行 `bool isPenetrated = noArmor || (naturalRoll >= armorDrThreshold) || (naturalRoll == 20)`
  - **复用了命中判定的 d20**，没有独立穿透骰
  - **WeaponPen 字段都没有**（武器表 v0.6 7.1-7.6 给所有武器加了 +0~+5 穿透修正）
  - **STRPenBonus 也没有**（v0.6 6.3：`floor(sqrt(STR/4))`）
- **影响**：穿透判定整个公式与文档严重偏差。文档 v0.6 12.6 把"穿透必须独立掷骰"列为强制约束。

### B.2 ⚠️ 高优先：盾牌耐久 + 远程减免（v0.6 5.3 + 6.2）
- **文档 v0.6** 5.3：盾牌有独立耐久 `ShieldPoints = ShieldDR × 10`，破坏后 `Data.Shield = null`
- **文档 v0.6** 6.2：远程攻击命中盾牌时按盾牌系数减免
  - 轻木盾 ×0.5
  - 步兵重盾 ×0.35
  - 军团塔盾 ×0.25
- **代码**：盾牌走 `ArmorData.CurrentArmorPoints`（与身体甲共用同一字段），已经能损毁。但**远程减免系数完全没实装**。
- **影响**：文档为远程战斗设计的"盾兵反箭"机制完全不生效。

### B.3 ⚠️ 高优先：HP 公式（v0.6 2.2）
- **文档 v0.6** 2.2：
  ```
  BaseHP = 10（固定）
  CON_HP_Bonus = floor(sqrt(CON / 4))
  MaxHP = 10 + CON_HP_Bonus × Level
  ```
  低 CON 缓慢成长，避免后期 TTK 过长。
- **代码**（`RPGRuleEngine.CalculateMaxHp`）：`baseHp + Mod(CON) × Level`，其中 `Mod(CON) = floor(sqrt(CON/2))`
  - `data.BaseMaxHp` 不固定 10（默认 10 但 enemy 生成时改写）
  - 用的是 `Mod(CON)` 而非 `CON_HP_Bonus`，前者增长快
- **影响**：120 级单位 HP 偏高。这就是为什么 lvl 120 enemy 能轻松 300+ HP。

### B.3.5 ⚠️ 中优先：AP 容量公式（v0.6 3.1）
- **文档 v0.6**：`MaxAP = 12 + Mod(DEX) + floor(Mod(CON)/2) - ArmorAPPenalty - ShieldAPPenalty`
- **代码**（`RPGRuleEngine.CalculateMaxAp` + `CombatStats.GetArmorApPenalty`）：✅ 主公式正确。但 ApPenalty 把 **armor + shield + helmet** 三件相加。**头盔 AP 惩罚是文档没规定的**，多扣了。

### B.4 ⚠️ 中优先：技能动作限制（v0.6 3.3）
- **文档 v0.6**：每回合最多 1 次**非 Spell 主动技能**；Spell 不计入但需 Mana + Magic Focus + 不能持盾 + 只能穿布甲
- **代码**：完全没实装
  - `Runtime.NonSpellSkillUsedThisTurn` 字段存在，**没 enforce**
  - 主动技能没有"是否 Spell"的标签（`SkillRegistry` 没 `is_spell` / `mana_cost` 字段）
  - 装备限制（Magic Focus、禁盾、布甲）没检查
- **影响**：当前任何角色一回合可以连发多个主动技能，AP 够就行——核心系统失衡。

### B.5 ⚠️ 中优先：Mana 系统（v0.6 10.0）
- **文档 v0.6**：`MaxMana = 10 + INT + floor(Level/2) + NodeManaMax`
- **代码**：`UnitData.CurrentMana` 字段存在，**MaxMana 没公式实装**
  - 初始化只写在 `CharacterGenerator.GenerateCharacter`：`CurrentMana = 10 + GetStatModifier(Intel) * 2`
  - 用的是 `Mod(INT)*2` 而非 `INT + level/2`
  - 没有"Mana 不足不能施法"的检查
- **影响**：法师循环没有资源约束。

### B.6 ⚠️ 中优先：节点平伤 AP 归一化（v0.6 11.4）
- **文档 v0.6** 11.4.1：`EffectiveNodeWeaponDamage = NodeWeaponDamage × WeaponAP / 4`
- **文档 v0.6** 11.4.2：法术 AOE 副目标 ×0.5
- **代码**（`CharacterSkillTree.GetMeleeDamageBonus` 等）：✅ 节点累加值能取出来。**但 AP 归一化没实装**——节点提供的 +1 melee_damage 直接加到每次攻击伤害上，不管武器是 2 AP 匕首还是 8 AP 重弩。
- **影响**：低 AP 多段武器从节点平伤获利过度，违反 v0.6 设计意图。

### B.7 ⚠️ 中优先：节点暴击率独立追加（v0.6 11.5）
- **文档 v0.6** 11.5：`critical_rate` 不降低 d20 阈值，而是独立追加暴击概率（命中后再掷一次）
- **代码**（`CharacterSkillTree.GetCriticalRateBonus`）：✅ 字段存在
  - `CombatRuleEngine.RollAttack` 不读它，只看 `CritThreshold`
  - 一些技能（如旧 STR 技能 `s05` "狂战士之怒" StatBonuses=critical_rate +0.05）的加成**完全没生效**
- **影响**：技能盘里所有 `critical_rate` 节点是死字段。

### B.8 ⚠️ 中优先：`NodeAC` / `NodeAllSave` 等加成（v0.6 11.6）
- **文档 v0.6** 5.1 / 11.6：AC 应该 += `NodeAC`（不受 MaxDex 限制）
- **代码**（`CombatStats.GetAc` / `GetEffectiveAc`）：
  - `GetEffectiveAc` 接收 `passiveAcBonus` 参数，**理论上**调用方可以传入 `tree.GetAcBonus()`
  - 实际：`HeadlessCombatLoop` 会传，但 Frontend 的 `CombatResolver` **没传**——live 战斗里节点 AC 不生效
- **影响**：Frontend 战斗中节点 AC 完全不算。同样问题影响 NodeAllSave。

### B.9 ⚠️ 中优先：包围加成（v0.6 8.2 + v1 5.2）
- **文档**：周围 1/2/3/4+ 友军 → 命中 +0/+1/+2/+3，目标 AC 0/0/-1/-2，4+ 还 +10% 伤害
- **代码**：`FacingSystem.GetSurroundingBonus` 完全实装了
  - **CombatResolver 完全不调用它**——计算出来没传到 `AttackInput`
- **影响**：包围加成是死代码。

### B.10 ⚠️ 中优先：伤势惩罚（v0.6 2.3）
- **文档**：>50% HP 0；25-50% -1；<25% -2（全检定）
- **代码**：`RPGRuleEngine.GetWoundPenalty` 实装了
  - **CombatResolver / CombatRuleEngine 不调用它**
- **影响**：伤势惩罚完全不进入命中检定。

### B.11 ⚠️ 中优先：士气崩溃 / 溃逃（v1 第 9 节）
- **文档**：≤-40 自动跳过回合；≤-60 AI 强制接管向地图边缘移动
- **代码**：`MoraleSystem.CheckRout` 存在，但 `MoraleEffects` 仅返回 `CritBonus / FumbleRate / AcModifier`，**HitBonus 是 0**——文档说高昂应 +2 命中，代码没给。
- **影响**：士气在战斗中影响很弱，主要靠 fumble rate。

### B.12 ⚠️ 低优先：v0.6 武器表数值差异
- **文档 v1** vs **v0.6** 7.1-7.6：v0.6 大幅上调武器骰（巨剑 2d6→3d6，巨斧 1d12→2d10，标枪 1d6→1d8…）
- **JSON `weapons_*.json`**：**用的是 v1 数值**
- **影响**：高级武器 DPS 偏低。文档 v0.6 整张武器表都没同步。

### B.13 ⚠️ 低优先：v0.6 Lv.5 武器重量分支（6.9）
- **文档 v0.6** 6.9：
  - 轻型 Lv.5 → 命中 +1（不影响暴击）
  - 中型 Lv.5 → 装甲伤害 ×1.2
  - 重型 Lv.5 → 暴击倍率 ×1.2
- **代码**：完全没实装。`WeaponMastery` 只追踪经验和等级，没分支效果。

### B.14 ⚠️ 低优先：技能盘节点修订表（v0.6 11.8）
v0.6 对约 30 个技能节点做了 nerf / 限制（嗜血单回合一次、不屈对 HP×0.5 不影响护甲、不朽之躯每场战斗一次等）。代码全部按原始版执行，**v0.6 修订完全没落地**。

### B.15 ⚠️ 低优先：行动经济限制（v0.6 11.7）
- **文档 v0.6** 11.7：每回合最多 1 次额外行动；额外行动不能再产额外行动；Time Warp 一回合一次；Command 不能指定本回合已获额外行动的单位
- **代码**：`Runtime.ExtraActionsThisTurn`、`TimeWarpUsedThisTurn` 字段存在，**enforce 没做**

### B.16 ⚠️ 低优先：跨单位射击防御模式额外惩罚（v0.6 8.5）
- **文档 v0.6**：被穿过的单位若处于防御模式或持塔盾，惩罚 -2 → -3
- **代码**：`LosCore.GetPathPenalty` 用统一 `UnitInPathPenalty=2`，没识别"防御模式 / 塔盾"

---

## C. 文档与代码概念冲突

### C.1 暴击阈值同时受 WIS 和士气影响
- **代码** `CombatStats.GetCritThreshold` 用 WIS（v0.6）；
- 但 `CombatResolver` 调 `CombatRuleEngine.GetAdjustedCritThreshold(critThreshold, moraleCritBonus)` 又减一次（旧规则）
- **决议**：文档 v0.6 没说士气影响暴击；要么 v0.6 11.x 补一条"高昂士气进一步降低 1 阈值"，要么代码移除士气暴击叠加

### C.2 装备 HP 加成
- **文档 v0.6** 2.2：`MaxHP = 10 + CON_HP_Bonus × Level + NodeMaxHP + TemporaryHPMaxBonus`
  - **没有"装备 HP 加成"**
- **代码** `CombatStats.GetMaxHp`：`hp += data.GetEquipmentHpBonus() + data.AccessoryHpBonus + 能力百分比加成`
- **决议**：代码实现了文档没列的 3 项 HP 来源。文档 v0.6 应补，或代码应删（取决于设计意图）

### C.3 BaseHP
- **文档 v0.6** 12.8：BaseHP 必须固定 10
- **代码**：`UnitData.BaseMaxHp` 是 `[Export] int` 默认 10，但 `CharacterGenerator` 可以改（NPC 有时被改成 20+）
- **决议**：要么把 BaseMaxHp 锁成只读 10，要么文档放宽

### C.4 攻击加值的范围
- **文档 v0.6** 4.1：命中 = `d20 + MasteryHitBonus + 武器修正 + NodeHitBonus + 位置 + 士气 + 伤势 + 射线`
- **代码** `CombatStats.GetAttackBonus`：只返回 `MasteryHitBonus + WeaponHitBonus`
- 其他项分散在 `CombatResolver` 各处临时拼装，且 `NodeHitBonus` / `WoundPenalty` 没接

---

## 推荐的修复顺序

按"实装成本 vs 系统影响"排：

| 优先级 | 项目 | 估时 | 备注 |
|---|---|---|---|
| P0 | B.1 独立穿透 d20 + WeaponPen + STRPenBonus | 半天 | 已是 v0.6 强制约束（12.6） |
| P0 | B.3 HP 公式改 v0.6 | 1h | 影响所有现存单位的 HP，要重测 sim |
| P0 | C.1 决定士气暴击是否保留 | 30min | 单点决策 |
| P1 | B.4 + B.5 技能动作限制 + Mana 公式 | 1 天 | 需要给 SkillRegistry 加 `is_spell` / `mana_cost` 字段 |
| P1 | B.2 盾牌远程减免 | 半天 | 需要在 `ArmorData` 加 `RangedDamageMultiplier` 字段 |
| P1 | B.7 节点暴击率独立追加 | 1h | `CombatRuleEngine.RollAttack` 加一个 `BonusCritChance` 字段 |
| P1 | B.8 / B.9 / B.10 包围/伤势/节点 AC 接入 CombatResolver | 半天 | 已有计算，缺接线 |
| P2 | B.6 节点平伤 AP 归一化 | 1h | |
| P2 | B.13 武器重量 Lv.5 分支 | 半天 | |
| P2 | B.12 v0.6 武器表迁移 JSON | 1h | 数据替换 |
| P3 | B.14 节点修订表 | 1 天 | 30 个节点逐个修 |
| P3 | B.15 行动经济硬限制 | 半天 | |
| P3 | B.16 防御模式塔盾路径惩罚 | 30min | |

---

## 当前 sim 实测影响

简化模型下，这些"代码未实现"的项目对 sim 数值的影响：

- **HP 公式偏差**：lvl 120 enemy `BaseMaxHp=20` + `Mod(CON)=4` × 120 ≈ 500 HP（应为 10 + `floor(sqrt(15/4))`=1 × 120 = 130），约 4 倍偏差
- **穿透不独立**：自然 1 也可能因为 d20 ≥ DR 阈值而穿透，违反"自然 1 必定未命中"原则
- **节点 critical_rate 失效**：技能盘 8+ 节点的暴击率加成完全是花瓶
- **包围/伤势不进算**：4 人围殴一个垂死敌人没有任何加成

要不要按 P0 先把这三个修了？还是先生成 v0.6 文档与代码的双向 diff 列表，让你逐项决定？



---

## 实装跟踪 (2026-05-17)

### Wave 1 — 关键修复（已完成 ✅）

| 项目 | 文件 | 状态 |
|---|---|---|
| C.1 删除士气暴击叠加 | `CombatResolver.cs` | ✅ |
| B.3.5 头盔不扣 AP | `CombatStats.GetArmorApPenalty` | ✅ |
| B.3 HP 公式 v0.6 严格版 | `RPGRuleEngine.CalculateMaxHp` + `CharacterGenerator` 3 处 | ✅ |
| B.7 节点 critical_rate 独立追加 | `CombatRuleEngine.AttackInput.BonusCritChance` | ✅ |
| B.8 / B.9 / B.10 包围/伤势/节点 AC 接线 | `CombatResolver.cs` + `Unit.GetEffectiveAc` | ✅ |
| Sim 验证 | 96 tests passed; lvl 5 winrate 0.555, avg 4.77 rounds | ✅ |

### Wave 2 — 文档高分歧实装 + 节点修订（已完成 ✅）

| 项目 | 文件 | 状态 |
|---|---|---|
| B.1 独立穿透 d20 + STRPenBonus（**WeaponPen 已废弃**）| `BattleUnitModel.ApplyDamage`, `WeaponData`, `ItemDataLoader` | ✅ |
| B.2 盾牌远程减免（5 种盾） | `ArmorData.RangedDamageMultiplier`, `armors.json`, `CombatResolver`, `HeadlessCombatLoop` | ✅ |
| B.4 + B.5 法术装备限制 + Mana 公式 | `CombatStats.CanCastSpells/GetMaxMana`, `SkillRegistry.IsSpell/GetManaCost`, `CombatManager.UseSkill` | ✅ |
| B.6 节点平伤 AP 归一化 | `CombatResolver`, `HeadlessCombatLoop`, `PassiveSkillResolver.GetPassiveRangedDamageBonus` | ✅ |
| B.12 v0.6 武器表数值 | `WeaponRegistry` (Greatsword 3d6, Maul 3d8, Greatclub 2d12 等) | ✅ |
| B.13 武器 Lv.5 重量分支（轻+1命中/中×1.2装甲伤/重×1.2暴击） | `CombatStats.GetAttackBonus/GetCritMultiplier`, `BattleUnitModel.ApplyDamage` | ✅ |
| B.14 节点修订 — `str_b06 嗜血`（4 AP 池） | `MeleeSkillHandlers.Bloodthirst` | ✅ |
| B.14 节点修订 — `str_b03 旋风斩`（节点平伤每目标 50%） | `MeleeSkillHandlers.Whirlwind` + `CombatResolver.nodePassiveScale` | ✅ |
| B.14 节点修订 — `str_b08 血腥漩涡`（吸血上限 3d6） | `MeleeSkillHandlers.BloodVortex` | ✅ |
| B.14 节点修订 — `dex_b03 连珠箭`（3 支 -2 命中，节点平伤每支 50%） | `RangedSkillHandlers.MultiShot` | ✅ |
| B.14 节点修订 — `con_b03 不屈`（HP <25% HP 伤害 ×0.5） | `BattleUnitModel.ApplyDamage` | ✅ |
| B.14 节点修订 — `con_b04 生命之盾`（每场 1 次） | `SupportSkillHandlers.LifeShield` + `Runtime.LifeShieldUsedThisCombat` | ✅ |
| B.14 节点修订 — `con_b05 铁壁`（-3 单包最低 1） | `BattleUnitModel.ApplyDamage` | ✅ |
| B.14 节点修订 — `con_b07 生命之环`（每场 1 次，1d10+CON_HP_Bonus） | `SupportSkillHandlers.LifeCircle` + `Runtime.LifeCircleUsedThisCombat` | ✅ |
| B.14 节点修订 — `cha_b09 指挥`（不能指定本回合已有额外行动） | `SupportSkillHandlers.Command` | ✅ |
| B.14 节点修订 — `cha_b10 英雄号召`（每场 1 次） | `SupportSkillHandlers.HeroicCall` + `Runtime.HeroicCallUsedThisCombat` | ✅ |
| B.14 节点修订 — `wis_b03 复活`（每场 1 次） | `SupportSkillHandlers.Resurrect` + `Runtime.ResurrectUsedThisCombat` | ✅ |
| B.15 行动经济硬限制（每回合 1 次非 Spell + 1 次额外行动） | `CombatManager.UseSkill`, `UnitRegistry.ResetActions` | ✅ |
| 战斗开始重置每场战斗标记 | `CombatManager.StartCombat` | ✅ |
| Unit.SkillTree setter 镜像到 Runtime（让 BattleUnitModel 能读节点） | `Unit.cs` | ✅ |

### Sim 数据对比（200 battles, seed 42）

| Level | Player Winrate | Avg Rounds | Avg Player Dmg | Avg Enemy Dmg | Hit Rate |
|---|---|---|---|---|---|
| 5  | 0.555 | 4.77 | 9.4  | 15.2  | 0.656 |
| 60 | 0.590 | 6.93 | 45.8 | 74.5  | 0.265 |
| 120 | 0.690 | 9.10 | 67.1 | 140.0 | 0.115 |

— 全 96 单测通过；战斗节奏与 v0.6 设计意图一致（高级别 TTK 拉长但未失控）。

### 仍未实装（低优先级，留作后续）

- **B.11 士气崩溃 / 高昂命中加成**：`MoraleEffects.HitBonus` 仍为 0，文档说高昂应 +2 命中
- **B.16 防御模式 / 塔盾路径惩罚 -3**：`LosCore.GetPathPenalty` 用统一 `UnitInPathPenalty=2`，未识别防御 / 塔盾
- **B.14 部分剩余节点**：约 15-20 个节点修订（cha_b03 同名光环不叠、cha_b12 复仇誓言 +15%、wis_ks01 神之手禁伤害等）已记录到 v0.6 文档但代码尚未硬约束，运行中按原节点效果执行
- **dex_b13 流星箭雨**：v0.6 11.8 限制最多 4 个目标 + 不能暴击 — 当前实现按邻格扫荡，未限 4 目标也未禁暴击
- **str_b05 暴击大师**：v0.6 11.8 改为基础倍率 3.0×（不与重型 Lv.5 ×1.2 重复叠加），代码当前是 `critical_x3` 旗标返回 ×3，但与重型 Lv.5 的 ×1.2 叠加规则未硬约束


### Wave 3 — WIS 系刺客重设计（已完成 ✅）

**背景**：原 WIS 系是治疗/牧师主题（30+ 个治疗节点），与"WIS = 暴击/刺客主属性 + 法力续航"的核心定位完全不符。Wave 3 把整个 WIS 区域改为暴击/刺杀方向，治疗主题全部迁移到法表生命系。

| 项目 | 文件 | 状态 |
|---|---|---|
| `BuildWisRegion` 全部重写（暴击/刺杀/法力主题） | `BladeHexCore/src/SkillTree/SkillTreeData.cs` | ✅ |
| 新加 `mana_regen` 节点统计访问器 `GetManaRegenBonus` | `CharacterSkillTree.cs` | ✅ |
| `CombatStats.GetMaxMana/GetManaRegen` 接入节点 mana_max/mana_regen | `CombatStats.cs` | ✅ |
| 新加 `head_shot/mana_surge/assassinate` 主动 effect 注册 + handler 实装 | `SkillRegistry.cs`, `SkillEffectExecutor.cs`, `Skills/AssassinSkillHandlers.cs` | ✅ |
| `lethal_focus` 被动（HP <30% 敌人 +2 命中 +10% 暴击）接入 sim | `HeadlessCombatLoop.ResolveAttack` | ✅ |
| `deathblow_focus` 被动（击杀后下次攻击 +20% 伤 +10% 暴击）接入 sim | `HeadlessCombatLoop.ResolveAttack` | ✅ |
| `assassin_instinct` keystone（暴击 ×2.5、+5% 暴击）接入 sim | `HeadlessCombatLoop.ResolveAttack` | ✅ |
| Sim AI 新主动技能（head_shot/assassinate/mana_surge）使用逻辑 | `HeadlessCombatLoop.TryUseActiveSkill` | ✅ |
| `Runtime` 新加 `ManaSurgeUsedThisCombat / AssassinateUsedThisCombat / HeadShotPendingTurns / DeathblowFocusPendingTurns` | `UnitRuntimeState.cs` | ✅ |
| v0.6 §11.8 WIS 修订表完整重写 | `Blade_Hex_战斗数值系统_修订版_v0.6_含节点系统.md` | ✅ |

### WIS 节点设计映射（保持 ID 兼容）

| ID | 旧名 / 旧效果 | 新名 / 新效果 |
|---|---|---|
| wis_s01 | 治愈之心（heal_amount +1） | 暴击直觉（critical_rate +0.02）|
| wis_s02 | 虔诚信仰（mana_max +2）| 法力觉醒（mana_max +3）|
| wis_s03 | 净化之触（wis_check +1） | 影刃训练（critical_rate +0.03） |
| wis_s05 | 大治愈术 | 影刃大师（critical_rate +0.05, mana_max +5） |
| wis_s06 | 灵魂庇护（ac +1） | 心灵专注（mana_max +3, mana_regen +1） |
| wis_s07 | 驱散邪恶（wis_check +2） | 法力洪流（mana_max +5, mana_regen +1） |
| wis_s08 | 坚韧灵魂（all_save +1）| 暴击精通（critical_rate +0.03） |
| wis_s09 | 洞察之力（wis_check +1）| 致命专注（critical_rate +0.02, wis_check +1） |
| wis_s10 | 信仰之盾 | 致命之眼（critical_rate +0.05） |
| wis_s11 | 先知之眼（wis_check +3） | 智慧之眼（保留 wis_check +3，作为暗杀前置）|
| wis_s12 | 奥术护盾 | 完美专注（critical_rate +0.05, mana_regen +1） |
| wis_s13 | 自然之怒（spell_damage +1）| 致命爆击（critical_rate +0.05） |
| wis_s14 | 荆棘之环 | 暴击本能（critical_rate +0.03） |
| wis_b01 | 基础治疗（basic_heal）| 法力涌动（mana_surge：每场 1 次回满 Mana）|
| wis_b02 | 群体治疗（group_heal） | 爆头突袭（head_shot：必定暴击 ×1.5 伤）|
| wis_b03 | 守护祝福（保留，贤者跨界用）| 守护祝福（保留，stub）|
| wis_b04 | 净化之焰（purifying_flame） | 致命猎杀（lethal_focus：被动 HP<30% +2 命中 +10% 暴击）|
| wis_b05 | 净化领域（保留，贤者跨界用） | 净化领域（保留，stub）|
| wis_b06 | 守护之灵（guardian_spirit） | 影刃涂毒（poison_blade，复用 DEX handler） |
| wis_b07 | 神谕（oracle）| 暗杀（assassinate：HP<30% 斩杀，每场 1 次）|
| wis_b08 | 元素风暴（elemental_storm） | 影遁（stealth，复用 DEX handler） |
| wis_b09 | 灵魂守护（违反"无复活"约束 — 已废除）| 死灵之锋（deathblow_focus：击杀后下次攻击 +20% +10% 暴击） |
| wis_ks01 | 生命精通（life_mastery）| 刺客本能（assassin_instinct：暴击倍率 ×2.5，禁重甲/盾）|

### Sim 数据对比（30 battles, seed 42, combat_comp scenario）

| Level | 刺杀阵 | 圣战阵 | 纯战阵 | 纯重战 | 纯坦阵 | 法术阵 |
|---|---|---|---|---|---|---|
| 30 | **55.9%** | 48.7% | 67.4% | 66.2% | 92.1% | 31.8% |
| 90 | **44.4%** | 34.9% | 60.5% | 80.8% | 91.5% | 42.3% |

- **lvl 30**：刺杀阵从 ~40% 提升至 55.9%（+16pp），符合"WIS 单点 = 刺客可用"目标
- **lvl 90**：刺杀阵 44%，仍弱于纯坦/重战，但已稳定在中游。后续可考虑：
  - `head_shot` 调高伤害倍率（×1.5 → ×2.0）
  - `assassinate` 阈值放宽（<30% → <40%）
  - 增加 wis_b04 致命猎杀的 +命中（+2 → +3）

### 已知约束未硬实装（标记后续 Wave 4）

- `assassin_instinct` keystone 限制（禁重甲/禁盾）只在文档约束，代码层未阻止装备
- `head_shot` 当前直接结算"必中 ×1.5 伤害"，未通过 `HeadShotPendingTurns` buff 形式延迟到下一次攻击（简化以方便 sim 验证；UI 流程实装时可走 buff 路径）
- `deathblow_focus` sim 简化：不区分 spell 击杀还是武器击杀；按击杀通用触发
- WIS 节点改造完成，但 v0.6 文档"§4.4 修订 6（WIS 暴击对所有武器，法术不能暴击）"已落实于核心规则，sim ResolveAttack 路径只走武器攻击因此天然符合该约束
