# Blade & Hex 设计文档与代码实际实现差异核对报告

> **核对时间**：2026-06-06
> **比对源**：`docs/` 目录下所有策划案设计与 C# 代码核心逻辑层（`BladeHexCore/src/`）

本报告逐一遍历了项目的核心系统，记录了在代码审计过程中发现的**文档陈旧、参数偏离、机制未实装、以及概念混淆**等差异，并为后续的游戏平衡性调整和版本对齐提供了具体的重构/订正建议。

---

## 1. 核心属性修正系统 (RPG 规则层)

### 1.1 策划案文档设定
- **`docs/02-RPG系统.md`**：设计了类似 DND 5e 的固定属性修正表。例如，属性值为 10-11 时修正值为 `+0`，12-13 为 `+1`，14-15 为 `+2`... 而属性值 1 时修正值为 `-5`。
- **`docs/Blade_Hex_战斗数值系统_修订版_v0.6_含节点系统.md`**：定义了新的平方根属性修正公式：
  $$\text{Modifier} = \lfloor\sqrt{\text{Stat} / 2}\rfloor$$
  并根据该公式列出了与之对应的无负数修正表（例如 3-7 修正为 `+1`，8-17 修正为 `+2`，18-31 修正为 `+3`）。

### 1.2 代码实际实现
- **`RPGRuleEngine.cs`**（以及 `CombatStats.cs`）：
  ```csharp
  public static int GetStatModifier(int score) =>
      (int)Mathf.Floor(Mathf.Sqrt(score / 2.0f));
  ```
  代码完全采用了平方根计算公式，未包含任何负数修正处理。

### 1.3 差异与冲突分析
- `02-RPG系统.md` 中引用的 DND +/-5 体系表格已完全过时，与实际核心代码逻辑以及 v0.6 物理战斗数值系统文档存在严重冲突。这会导致查阅旧文档的开发人员对属性的实际价值产生严重的误判。

### 1.4 修复与同步建议
- **修改文档**：重写 `02-RPG系统.md` 中的属性修正章节，移除过时的 DND 属性修正表格，将其统一对齐为最新的 `floor(sqrt(score / 2))` 修正逻辑。

---

## 2. 最大生命值 (Max HP) 计算与升级成长

### 2.1 策划案文档设定
- **`docs/02-RPG系统.md`**：规定“所有角色基础 HP 为 10，CON 修正由生成器预先计入 `baseMaxHp`，代码中不再重复乘以 CON”。

### 2.2 代码实际实现
- **`RPGRuleEngine.cs`** 与 **`CombatStats.cs`**：
  在升级 HP 计算中，公式实现为：
  `BaseHP (10) + conHpBonus * Level + 装备HP + 饰品HP`
  其中：
  `conHpBonus = floor(sqrt(CON / 4))`。
  也就是说，升级成长部分明确乘以了等级 `Level`。

### 2.3 差异与冲突分析
- `02-RPG系统.md` 的描述与实际代码不匹配。实际代码（以及 v0.6 数值系统文档）在生命值升级成长上均乘以了 `Level`，这表明“不再重复乘以 CON”仅指初建卡时的处理，但升级生命值依然遵循了 CON 的等级加成。

### 2.4 修复与同步建议
- **修改文档**：订正 `02-RPG系统.md` 中的 HP 计算章节，明确写出 `10 + conHpBonus * Level` 的升级公式，保持文档的连贯与准确。

---

## 3. AP (行动点) 容量公式与默认值冲突

### 3.1 策划案文档设定
- **`docs/02-RPG系统.md`**：AP 公式为 `AP = 基础AP(4) + floor(sqrt((DEX + CON) / 8))`。1级角色通常是 5 AP，中后期一般是 6-7 AP，极后期最多 8 AP。

### 3.2 代码实际实现
- **`RPGRuleEngine.cs`** 与 **`CombatStats.cs`**：
  AP 计算实际代码实现为：
  ```csharp
  int maxAp = RPGRuleEngine.CalculateMaxAp(data.BaseAp, GetEffectiveDex(data), GetEffectiveCon(data));
  // 扣除护甲与盾牌的 AP 惩罚后返回
  ```
  在 `RPGRuleEngine.CalculateMaxAp` 中：
  `return Mathf.Max(1, baseAp + dexMod + (int)Mathf.Floor(conMod / 2.0f));`
  这里 `data.BaseAp` 作为导出字段在 `UnitData.cs` 中被定义，其默认值被设为了 **12**。

### 3.3 差异与冲突分析
- **极其严重的数值偏离**：由于代码中的基础 AP (`BaseAp`) 默认值为 12，且公式中将 `DEX修正` 和 `CON修正` 直接平加到了基础值上，导致 1 级角色实际拥有高达 **15** 左右的 AP。
- 这和策划案规划的“1级角色 5 AP，后期最多 8 AP”存在巨大出入。因为动作 AP 消耗（移动 1 AP/格，普攻 2-4 AP，切换武器 2 AP 等）是基于 5-8 AP 的低容量尺度设计的。若角色拥有 15 AP，将可以在单回合内执行数次移动和多段攻击，使得战术阻碍与借机攻击的卡位效果失去意义，战斗平衡完全失控。

### 3.4 修复与同步建议
- **处理状态**：已解决。经团队确认，代码中基础 AP = 12 且使用平加修正的计算公式为正确设定。
- **修改文档**：已重写 `02-RPG系统.md` 中的行动点（AP）系统章节，使公式与数据对齐代码逻辑。

---

## 4. AC (闪避) 与 DR (装甲) 计算公式分歧

### 4.1 策划案文档设定
- **`docs/06-装备与物品.md`**（及 **`docs/02-RPG系统.md`**）：
  明确指出：“护甲不参与 AC 计算。护甲对防御的唯一影响是：通过 max_dex_bonus 限制 DEX 对 AC 的贡献上限；提供 DR 装甲耐久和穿透阈值”。AC 公式只受 `DEX 修正`、`盾牌 AC 加成` 和 `技能盘/饰品` 影响。

### 4.2 代码实际实现
- **`CombatStats.cs`** 的 `GetAc` 方法：
  ```csharp
  public static int GetAc(UnitData data, bool usingPrimaryWeapon)
  {
      if (data == null) return 8;
      int ac = data.BaseAc; // 默认为 8

      // DEX 修正（受护甲 MaxDex 限制）
      int dexAc = GetStatModifier(GetEffectiveDex(data));
      if (data.Armor != null && data.Armor.MaxDexBonus < 99 && data.Armor.CurrentArmorPoints > 0)
          dexAc = Math.Min(dexAc, data.Armor.MaxDexBonus);

      // 护甲 AC = floor(sqrt(ArmorDR))（装甲损毁后失效）
      int armorDrAc = 0;
      if (data.Armor != null && data.Armor.CurrentArmorPoints > 0)
          armorDrAc = (int)Mathf.Floor(Mathf.Sqrt(data.Armor.DrThreshold));

      // 盾牌 AC = floor(sqrt(ShieldDR))
      int shieldDrAc = 0;
      if (data.Shield != null && data.Shield.armorType == ArmorData.ArmorType.Shield
          && data.Shield.CurrentArmorPoints > 0)
          shieldDrAc = (int)Mathf.Floor(Mathf.Sqrt(data.Shield.DrThreshold));

      return ac + dexAc + armorDrAc + shieldDrAc + GetBuffStatBonus(data, "ac");
  }
  ```

### 4.3 差异与冲突分析
1. **核心概念冲突**：实际代码在计算闪避 AC 时，强行将护甲 DR 阈值的平方根 `floor(sqrt(ArmorDR))` 算入了 AC。这完全推翻了策划案“闪避 (能不能躲开) 与 装甲 (打中后抗伤) 体系分离”的原则。穿重甲的角色本应难闪避（DEX上限限制为 0），但因为代码加上了 `floor(sqrt(ArmorDR))`，导致其基础闪避 AC 反而得到了提升，使得“铁罐子”不仅肉还能闪。
2. **AC 基础值偏差**：策划案规定的 AC 基础值为 10，而 `UnitData.cs` 导出的 `BaseAc` 默认值为 8。
3. **装备词缀 AC 丢失**：护甲和盾牌在配置中的 `AcBonus` 在 `GetAc` 中被完全忽略（没有进行加算），这导致防具本身的闪避属性失效；但在 UI 生成（`ArmorData.cs` 的 `GetArmorDescription`）中，描述文本仍会按照 `AcBonus` 拼装 AC 数据，导致 UI 描述与战斗实际执行的 AC 不一致。

### 4.4 修复与同步建议
- **处理状态**：已解决。
- **修改代码**：在 `CombatStats.GetAc` 中引入了对防具与盾牌的 `GetTotalAcBonus()`（含词缀 AC 奖励）的加算，解决了词缀失效且与 UI 描述不一致的 bug。
- **修改文档**：保留代码中现有的“装甲强度折算偏折 AC”（`floor(sqrt(ArmorDR))`）以及基础 AC = 8 的设计，更新了 `06-装备与物品.md` 和 `02-RPG系统.md` 里的 AC 计算章节。

---

## 5. 盾牌耐久与损毁机制在代码中缺失

### 5.1 策划案文档设定
- **`docs/06-装备与物品.md`**：盾牌拥有独立的装甲耐久，计算公式为 `ShieldPoints = ShieldDR × 10`。
- 当盾牌耐久 `CurrentShieldPoints` $\leq 0$ 时，系统应执行 `Data.Shield = null` 将盾牌从单位数据中移除，使其失去盾牌的 AC 加成和 AP 惩罚。

### 5.2 代码实际实现
- **`BattleUnitModel.cs`** 里的 `ApplyDamage` 方法：
  代码在计算和应用 DR 损耗时，仅仅扣减了身体护甲的 `Data.Armor.CurrentArmorPoints`，即使在 `CombatStats.TakeDrDamage` 中扣减了角色的总 `CurrentDr`，也完全没有扣减盾牌 `Data.Shield.CurrentArmorPoints` 值的逻辑，更没有将毁坏的 `Data.Shield` 设为 null 的逻辑。

### 5.3 差异与冲突分析
- 盾牌的耐久扣减和战斗碎裂机制在代码中完全缺失。在当前的战斗中，盾牌相当于拥有**无限生命且永远不会破损**，这严重偏离了策划案中木盾易被击碎的设定。

### 5.4 修复与同步建议
- **修改代码**：在 `ApplyDamage` 中引入对 `Data.Shield` 剩余耐久的计算，当盾牌受到伤害磨损归零时，执行 `Data.Shield = null` 并清除对应的 AC/AP 惩罚。

---

## 6. 盾牌远程伤害拦截机制缺失

### 6.1 策划案文档设定
- **`docs/Blade_Hex_战斗数值系统_修订版_v0.6_含节点系统.md`** (§6.2)：
  当远程攻击命中持盾目标且来自防护弧时，伤害在计算穿甲前优先进入盾牌阶段。
  远程伤害应先乘以盾牌减免系数（木盾 ×0.50，重盾 ×0.35，塔盾 ×0.25），然后再扣除 `CurrentShieldPoints`（盾牌耐久）。
  **若盾牌未碎，本次远程攻击不会继续对身体护甲或 HP 造成伤害**。

### 6.2 代码实际实现
- **`BattleUnitModel.cs`** 的 `ApplyDamage`：
  代码在解算伤害时，直接根据武器类型和穿透判定进行 HP 和 DR 伤害分流，然后直接作用于 HP 和身体护甲。完全未实装“远程攻击时先判定盾牌弧，进行伤害乘数扣减，并完全由盾牌吸收伤害”的这一整套拦截管道。

### 6.3 差异与冲突分析
- 盾牌的“防远程拦截与吸收”这一核心战术功能在当前代码中完全未实装，导致盾牌在面对远程箭矢时起不到应有的保护作用（伤害会直接分流给身体防具和 HP）。

### 6.4 修复与同步建议
- **修改代码**：在 `ApplyDamage` 方法入口处，针对伤害来源为远程（且目标拥有盾牌且属于防护弧）的情况，加入分流和折减逻辑，将折减后的伤害优先扣减至盾牌耐久，若未破盾则提前返回结算结果（防具与 HP 免伤）。

---

## 7. 物理伤害公式与 Nd20 等级额外伤害骰

### 7.1 策划案文档设定
- **`docs/02-RPG系统.md`**：仍保留有“基础伤害 = 武器骰(XdY) + 等级额外骰(Nd20) ... 等级额外骰 = min(6, 1 + floor((等级-1)/20))”这一伤害机制。

### 7.2 代码实际实现
- **`CombatStats.cs`** 的 `RollDamage` 方法：
  根据 v0.6 物理战斗体系修订，等级伤害成长已改由武器精通的百分比乘数（每级 +10%）和装备阶级（Tier）升级来承担。等级额外骰被彻底废弃，代码内无任何该追加骰的掷骰计算。
- **`RPGRuleEngine.cs`**：
  代码实际逻辑虽已废除此机制，但静态类中依然残留了 `GetDamageDiceCount` 和 `RollNd20` 这两个陈旧工具方法。

### 7.3 差异与冲突分析
- `02-RPG系统.md` 未能及时与 v0.6 物理大改版后的设定同步，导致残留了大量的 Nd20 追加骰说明。同时，代码的伤害公式已变更为纯百分比乘数体系：`武器骰 * (1 + 力量加成% + 武器精通%)`，这与旧文档中写的加法体系（`武器骰 + 修正值`）存在本质区别。

### 7.4 修复与同步建议
- **修改文档**：清理 `02-RPG系统.md` 中的 Nd20 等级额外骰叙述，将伤害公式修改为最新的百分比乘数加成体系。
- **修改代码**：重构并移除 `RPGRuleEngine.cs` 中残留的 `GetDamageDiceCount` 与 `RollNd20` 废弃方法，保持代码干净整洁。

---

## 8. 武器精通命中加成公式不符

### 8.1 策划案文档设定
- **`docs/Blade_Hex_战斗数值系统_修订版_v0.6_含节点系统.md`**：
  武器精通为 10 级上限体系，命中加成公式规定为：
  `MasteryHitBonus = floor(MasteryLevel / 3)`
  即最大加成仅为 +3（Lv.9-10 时）。

### 8.2 代码实际实现
- **`CombatStats.cs`** 里的 `GetAttackBonus` 方法：
  ```csharp
  // 武器精通命中加成 = floor(MasteryLevel / 2)（v0.7 15 级体系）
  masteryHitBonus = masteryLevel / 2;
  ```
  代码实际上支持的是 15 级精通上限，且每两级即可获得 +1 命中（最高加到 +7 命中）。

### 8.3 差异与冲突分析
- 属于版本不对齐差异。代码实际上在开发中演进到了 v0.7 版的 15 级精通上限体系，而 v0.6 策划数值文档仍采用的是旧版的 10 级精通上限和每 3 级 +1 命中的慢速加成公式。

### 8.4 修复与同步建议
- **修改文档**：撰写或更新策划案，将武器精通系统的上限从 10 级扩展到 15 级，并同步修正 `MasteryHitBonus = floor(MasteryLevel / 2)` 公式。

---

## 9. 全局声望知名度 vs 势力友好度

### 9.1 策划案文档设定
- **`docs/18-声望系统.md`**：声望是玩家的“全局影响力指标”，值在 0~800+ 范围，用于驱动阶段晋升（如无名之辈、名扬四方、活着的传说等）。
- **`docs/.kiro/specs/poi-secondary-panels/requirements.md`**：提出通过 `EconomyManager.Reputation` 属性进行声望值的读写。

### 9.2 代码实际实现
- **`EconomyManager.cs`** 中并未实现任何全局 `Reputation` 属性。
- 项目的声望系统仅由 **`ReputationTracker.cs`** 承载。而在 `ReputationTracker` 中，实现的是**各个国家势力独立的友好度（-100 到 +100）**，包含了城镇准入、招募倍率和价格折扣的加成。

### 9.3 差异与冲突分析
- **核心概念偏离**：策划案规划的“全局声望知名度”系统在代码中实际未实装，导致依赖全局声望进阶的剧情晋升事件无法触发。代码将“国家势力关系友好度”命名为了 `Reputation`，从而占用了声望这一命名，造成了概念的重合与系统缺失。

### 9.4 修复与同步建议
- **重构方案**：在 `EconomyManager` 或 `GlobalState` 中新增一个代表玩家全局声望的字段（例如命名为 `GlobalRenown`，范围 0-800+），将其与势力关系 `ReputationTracker` 隔离。让任务和战斗能够同时为势力友好度与全局声望知名度提供加成，从而支持晋升事件。

---

## 10. 复活术废除在文档中的残留

### 10.1 策划案文档设定
- **`docs/07-法术系统.md`**：在 5 环法术列表中列有 `复活术`（“复活1个死亡队友(HP50%)”，冷却为“战斗结束前不可再用”）。

### 10.2 代码实际实现
- 根据 **`v0.6 战斗数值系统`** 的增补，项目确立了**“全游戏完全没有复活机制”**的核心约束，HP 归零即永久死亡，任何法术、道具都不能让人复活。
- C# 代码核心逻辑（如法表系统、技能解析等）已废除了 `resurrect` 和 `wis_b03` 等任何复活逻辑。

### 10.3 差异与冲突分析
- `07-法术系统.md` 属于遗留文档，未能与最新的游戏“永久阵亡/无复活”机制相统一。

### 10.4 修复与同步建议
- **修改文档**：修改 `07-法术系统.md`，将 `复活术` 从 5 环法术表中移除，替换为其他生命系法术（如超强群体治疗或群体再生等），确保文档与核心逻辑保持完全一致。

---

## 11. 半精灵属性修正缺失规则支持

### 11.1 策划案文档设定
- **`docs/05-角色与职业.md`** 与 **`docs/12-种族与招募.md`**：
  半精灵（Half-Elf）的属性修正规定为：`CHA +2，并自选两项属性 +1`。

### 11.2 代码实际实现
- **`races.json`** 中半精灵的配置：
  `"str_mod": 0, "dex_mod": 0, "con_mod": 0, "int_mod": 0, "wis_mod": 0, "cha_mod": 2`
- **`CharacterGenerator.cs`**：
  属性生成时只简单将 `races.json` 中的各属性修正值累加。没有实现任何针对半精灵“自选两项属性 +1”的动态点数分配或配置支持。

### 11.3 差异与冲突分析
- 属性数值丢失。代码实现仅赋予了半精灵魅力属性的额外 +2，由于未实装“自选两项属性 +1”的动态分配规则，使得随机生成的半精灵角色在基础属性总和上比策划案中少掉了 2 个属性点。

### 11.4 修复与同步建议
- **修改代码**：在 `CharacterGenerator.AllocateAttrs` 或 `_GenerateCharacterWithWeights` 中，加入针对 `HalfElf` 种族的特判，随机或根据倾向加权为另外两个主属性增加 +1 修正。

---

## 12. 招募价格系数与友好度溢价脱钩

### 12.1 策划案文档设定
- **`docs/12-种族与招募.md`**：
  设计了招募价格计算公式：
  $$\text{最终价格} = \text{基础价格} \times \text{种族系数} \times \text{关系系数}$$
  其中：
  - 种族系数：同族 = 1.0，外族 = 1.5 ~ 2.0
  - 关系系数：同盟 = 0.8，友好 = 0.9，中立 = 1.0，冷淡 = 1.3，敌对不可招募

### 12.2 代码实际实现
- **`RecruitPool.cs`** 刷新招募单位的价格：
  `Cost = RecruitPricingService.GetRecruitCost(unit, poiTier, prosperity) + rng.Next(10)`
- **`RecruitService.cs`** 招募单位的扣款逻辑：
  只简单读取了池中的 `recruit.Cost` 并执行 `SpendGold`，在刷新价格和执行扣款中均没有对目标招募单位的种族匹配（同族 vs 外族）做价格缩放判定，也没有与国家/势力友好度关系系数进行相乘。

### 12.3 差异与冲突分析
- 招募价格机制缩水。游戏内的招募价格只与角色等级、携带装备总值、聚落阶层和繁荣度挂钩，完全与玩家的种族和势力好感度脱钩，导致策划案中的“友好度省钱、外族高溢价”等核心战术考量未能产生任何实际作用。

### 12.4 修复与同步建议
- **修改代码**：在 `RecruitPool.cs` 刷新或 `RecruitService.Recruit` 结算时，获取玩家队伍的主种族（队长）及目标聚落的势力好感等级，根据公式引入种族系数和关系系数计算最终扣除价格。

---

## 13. 职业称号判定器命名与映射偏差

### 13.1 策划案文档设定
- **`docs/05-角色与职业.md`**：
  定义了 64 种基于技能盘大节点涉足属性的职业称号。其中：
  - 单 WIS（感知）职业称号为 **`牧师`**（治愈与生命的守望者）。
  - STR+WIS 复合职业称号为 **`圣骑士`**。
  - STR+CON+WIS 复合职业称号为 **`神殿骑士`**。
  - STR+WIS+CHA 复合职业称号为 **`圣战者`**。
  - CON+WIS+CHA 复合职业称号为 **`圣盾使`**。
  - STR+CON+WIS+CHA 复合职业称号为 **`铁壁圣骑`**。

### 13.2 代码实际实现
- **`ClassTitleResolver.cs`** 的称号字典匹配表中：
  - `FlagWis`（单 WIS）被指定为了 **`刺客`**。
  - `FlagStr | FlagWis` 被指定为了 **`守护骑士`**。
  - `FlagStr | FlagCon | FlagWis` 被指定为了 **`磐石骑士`**。
  - `FlagStr | FlagWis | FlagCha` 被指定为了 **`战术大师`**。
  - `FlagCon | FlagWis | FlagCha` 被指定为了 **`铁壁守护`**。
  - `FlagStr | FlagCon | FlagWis | FlagCha` 被指定为了 **`磐石守护`**。

### 13.3 差异与冲突分析
- 称号与角色技能属性严重错位。WIS（感知/信仰）在技能盘上包含的是治疗、再生、祈福等核心大节点，但在游戏内点亮这些节点会被称为“刺客”，严重违背了治愈系的背景设定。同理，圣骑士/神殿骑士等圣誓系称号在代码中被替换为了守护骑士、磐石骑士等，失去了圣法术的信标特色。

### 13.4 修复与同步建议
- **修改代码**：在 `ClassTitleResolver.cs` 的 `EnsureTable()` 中修正这些中文字典的键值对，使“刺客”变更为“牧师”，“守护骑士”变更为“圣骑士”等，使其与策划案的称号名及设定完全一致。
