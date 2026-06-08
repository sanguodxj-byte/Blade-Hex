// CombatStats.cs
// 战斗属性计算 — 纯静态类，所有战斗数值公式的唯一真相源
// 从 Unit.cs (Frontend) 中提取全部计算逻辑，无任何 Godot Node 依赖
// 对应策划案 03-战术战斗系统
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Combat.Buff;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 战斗属性计算静态类
/// 所有战斗数值公式的 SINGLE SOURCE OF TRUTH
/// 不继承任何 Godot 类型，不引用任何 Frontend 命名空间
/// </summary>
public static class CombatStats
{
    // ========================================
    // 属性修正
    // ========================================

    /// <summary>属性修正 = floor(sqrt(score / 2))</summary>
    public static int GetStatModifier(int score) =>
        RPGRuleEngine.GetStatModifier(score);

    public static int GetEffectiveStr(UnitData? data) =>
        (data?.Str ?? 10) + (data?.Runtime?.SkillTree?.GetStrBonus() ?? 0);

    public static int GetEffectiveDex(UnitData? data) =>
        (data?.Dex ?? 10) + (data?.Runtime?.SkillTree?.GetDexBonus() ?? 0);

    public static int GetEffectiveCon(UnitData? data) =>
        (data?.Con ?? 10) + (data?.Runtime?.SkillTree?.GetConBonus() ?? 0);

    public static int GetEffectiveInt(UnitData? data) =>
        (data?.Intel ?? 10) + (data?.Runtime?.SkillTree?.GetIntBonus() ?? 0);

    public static int GetEffectiveWis(UnitData? data) =>
        (data?.Wis ?? 10) + (data?.Runtime?.SkillTree?.GetWisBonus() ?? 0);

    public static int GetEffectiveCha(UnitData? data) =>
        (data?.Cha ?? 10) + (data?.Runtime?.SkillTree?.GetChaBonus() ?? 0);

    public static int GetSaveBonus(UnitData? data)
    {
        if (data == null) return 0;

        int bonus = data.Runtime?.SkillTree?.GetAllSaveBonus() ?? 0;
        bonus += SkillTreeKeystoneResolver.GetRoyalPresenceSaveBonus(data);
        bonus += GetBuffStatBonus(data, "save_bonus");

        if (data.Runtime?.ActiveStatusEffects != null)
        {
            foreach (var effect in data.Runtime.ActiveStatusEffects)
            {
                if (effect.StatModifiers.ContainsKey("save_bonus"))
                    bonus += (int)effect.StatModifiers["save_bonus"];
                if (effect.StatModifiers.ContainsKey("save_bonus_dice"))
                    bonus += (int)effect.StatModifiers["save_bonus_dice"];
            }
        }

        return bonus;
    }

    // ========================================
    // HP 计算
    // ========================================

    /// <summary>
    /// 最大 HP = 基础HP + CON修正 × 等级 + 装备HP加成 + 饰品HP加成 + 能力百分比加成
    /// </summary>
    public static int GetMaxHp(UnitData data)
    {
        if (data == null) return 1;
        int hp = RPGRuleEngine.CalculateMaxHp(data.BaseMaxHp, GetEffectiveCon(data), data.Level);
        hp += data.GetEquipmentHpBonus();
        hp += data.AccessoryHpBonus;

        // 矮人韧性：生命值 + 20%
        if (data.Race != null && Array.IndexOf(data.Race.RacialTraits, "dwarven_resilience") >= 0)
        {
            hp = (int)(hp * 1.20f);
        }

        // 装备能力组件：HP 百分比加成（如 extra_hp_percent）
        float hpMultBonus = BladeHex.Combat.Abilities.UnitAbilities.GetTotalMaxHpMultiplierBonus(data);
        if (hpMultBonus > 0f)
            hp = (int)(hp * (1f + hpMultBonus));

        return SkillTreeKeystoneResolver.ApplyMaxHp(data, Math.Max(1, hp));
    }

    // ========================================
    // AP 计算
    // ========================================

    /// <summary>
    /// 确保 AP 已初始化 — 回合开始时调用，若 AP 未初始化则设为最大值
    /// 注意：此方法有副作用（可能修改 runtime.CurrentAp）
    /// </summary>
    public static void EnsureApInitialized(UnitData data, UnitRuntimeState runtime)
    {
        if (runtime.CurrentAp <= 0 && !runtime.HasMoved && !runtime.HasActed)
            runtime.CurrentAp = GetMaxAp(data);
    }

    /// <summary>读取当前 AP（无副作用）</summary>
    public static float GetAp(UnitRuntimeState runtime) => runtime.CurrentAp;

    /// <summary>最大 AP = 基础AP + DEX修正 + (CON修正 / 2) - 护甲AP惩罚</summary>
    public static int GetMaxAp(UnitData data)
    {
        if (data == null) return 12;
        int maxAp = RPGRuleEngine.CalculateMaxAp(data.BaseAp, GetEffectiveDex(data), GetEffectiveCon(data));
        return Math.Max(1, maxAp - GetArmorApPenalty(data));
    }

    /// <summary>护甲 AP 惩罚 = armor + shield 的 ApPenalty 总和（v0.6: 头盔不算入 AP 惩罚）</summary>
    public static int GetArmorApPenalty(UnitData data)
    {
        if (data == null) return 0;
        int penalty = 0;
        if (data.Armor != null) penalty += data.Armor.ApPenalty;
        if (data.Shield != null) penalty += data.Shield.ApPenalty;
        // 头盔不再扣 AP — 重盔已通过 AC/MaxDex 限制成本
        return penalty;
    }

    /// <summary>
    /// 最大 Mana (v0.6 10.0): 10 + INT + floor(Level/2) + floor(WIS/4) + NodeManaMax
    /// 2026-05-17 修订：WIS 提供 Mana 上限加成 floor(WIS/4)。
    /// 节点 mana_max 加成自动累入（CharacterSkillTree.GetManaMaxBonus）。
    /// </summary>
    public static int GetMaxMana(UnitData data)
    {
        if (data == null) return 0;
        int intel = GetEffectiveInt(data);
        int wis = GetEffectiveWis(data);
        int baseMana = 10 + intel + data.Level / 2 + wis / 4;
        int nodeMana = data.Runtime?.SkillTree?.GetManaMaxBonus() ?? 0;
        return SkillTreeKeystoneResolver.ApplyMaxMana(data, Math.Max(0, baseMana + nodeMana));
    }

    /// <summary>
    /// Mana 战斗内每回合恢复量 (v0.6 10.0 修订): floor(WIS/8) + floor(INT/12) + NodeManaRegen
    /// 让 WIS 系成为续航型，INT 主属性也提供少量 mana regen 让法师能持久输出。
    /// 节点 mana_regen 加成自动累入。
    /// </summary>
    public static int GetManaRegen(UnitData data)
    {
        if (data == null) return 0;
        int nodeRegen = data.Runtime?.SkillTree?.GetManaRegenBonus() ?? 0;
        return GetEffectiveWis(data) / 8 + GetEffectiveInt(data) / 12 + nodeRegen;
    }

    /// <summary>
    /// 角色是否满足施法装备限制 (v0.6 10.0):
    /// 1) 不能装备盾牌
    /// 2) 只能穿戴 Cloth 类护甲（DR ≤ 3 的 Light，即布衣 / 法师长袍）
    /// 3) 主手必须装备法术媒介（IsCatalyst）
    /// </summary>
    public static bool CanCastSpells(UnitData data)
    {
        if (data == null) return false;
        if (data.Shield != null) return false;
        if (data.Armor != null
            && (data.Armor.armorType != ArmorData.ArmorType.Light || data.Armor.DrThreshold > 3))
            return false;
        var mainHand = data.PrimaryMainHand;
        if (mainHand == null) return false;
        if (mainHand is not WeaponData w) return false;
        return w.IsCatalyst;
    }

    // ========================================
    // 暴击系统
    // ========================================

    /// <summary>
    /// 暴击阈值 (v0.6): WISCritTier = floor(sqrt(max(0, WIS-14) / 4))
    /// CritThreshold = 20 - WISCritTier + buff（负值=暴击阈值降低，正值=更难暴击）
    /// </summary>
    public static int GetCritThreshold(UnitData data)
    {
        if (data == null) return 20;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, GetEffectiveWis(data) - 14) / 4.0));
        int currentValue = 20 - wisCritTier;
        // v0.8 E1: buff 修正暴击阈值（支持 Override + FlatBonus）
        var critMod = Buff.BuffSystem.ResolveStatModifiers(data, "crit_threshold");
        if (critMod.OverrideValue.HasValue)
            return Math.Clamp((int)critMod.OverrideValue.Value, 2, 20);
        int critResult = currentValue + (int)critMod.FlatBonus;
        return Math.Clamp(critResult, 2, 20);
    }

    /// <summary>
    /// 暴击倍率 (v0.6): CritMultiplier = 2.0 + WISCritTier × 0.1
    /// 重型武器 Lv.5+ 精通: 最终暴击倍率 ×1.2 (v0.6 6.9)
    /// </summary>
    public static float GetCritMultiplier(UnitData data)
    {
        if (data == null) return 2.0f;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, GetEffectiveWis(data) - 14) / 4.0));
        float baseMultiplier = 2.0f + wisCritTier * 0.1f;

        // v0.7 重型武器 Lv.7+ → ×1.2（原 Lv.5 阈值按比例上调）
        var weapon = GetMainHand(data, data.Runtime.UsingPrimaryWeapon) as WeaponData;
        if (weapon != null && weapon.Weight == WeaponData.WeightCategory.Heavy)
        {
            int masteryLevel = data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            if (masteryLevel >= 7) baseMultiplier *= 1.2f;
        }
        return baseMultiplier;
    }

    /// <summary>暴击受伤倍率 = max(0.2, 1.0 - WISCritTier * 0.1)</summary>
    public static float GetCritDamageTakenMultiplier(UnitData data)
    {
        if (data == null) return 1.0f;
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, GetEffectiveWis(data) - 14) / 4.0));
        return Math.Max(0.2f, 1.0f - wisCritTier * 0.1f);
    }

    // ========================================
    // AC 与 DR
    // ========================================

    /// <summary>
    /// 基础 AC (v0.6) = 10 + DEX修正(受MaxDexBonus限制) + floor(sqrt(ArmorDR)) + floor(sqrt(ShieldDR))
    /// 护甲不再有独立AcBonus，AC完全来自sqrt(DR)
    /// </summary>
    public static int GetAc(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 8;
        int ac = data.BaseAc;

        // DEX 修正（受护甲 MaxDexBonus 限制）
        int dexAc = GetStatModifier(GetEffectiveDex(data));
        if (data.Armor != null && data.Armor.MaxDexBonus < 99 && data.Armor.CurrentArmorPoints > 0)
            dexAc = Math.Min(dexAc, data.Armor.MaxDexBonus);

        // 护甲 AC = floor(sqrt(ArmorDR))（装甲损毁后失效）
        int armorDrAc = 0;
        int armorAcBonus = 0;
        if (data.Armor != null && data.Armor.CurrentArmorPoints > 0)
        {
            armorDrAc = (int)Mathf.Floor(Mathf.Sqrt(data.Armor.DrThreshold));
            armorAcBonus = data.Armor.GetTotalAcBonus();
        }

        // 盾牌 AC = floor(sqrt(ShieldDR))（盾牌损毁后失效）
        // v0.7: 盾牌存在独立字段 data.Shield，不在副手武器槽（PrimaryOffHand）。
        // 历史 bug: 之前用 GetOffHand 读，导致 GetAc / GetMaxDr 漏算盾牌。
        int shieldDrAc = 0;
        int shieldAcBonus = 0;
        if (data.Shield != null && data.Shield.armorType == ArmorData.ArmorType.Shield
            && data.Shield.CurrentArmorPoints > 0)
        {
            shieldDrAc = (int)Mathf.Floor(Mathf.Sqrt(data.Shield.DrThreshold));
            shieldAcBonus = data.Shield.GetTotalAcBonus();
        }

        int totalAc = ac + dexAc + armorDrAc + shieldDrAc + armorAcBonus + shieldAcBonus
            + GetBuffStatBonus(data, "ac") + GetBuffStatBonus(data, "ac_bonus");

        // 矮人韧性：AC + 1
        if (data.Race != null && Array.IndexOf(data.Race.RacialTraits, "dwarven_resilience") >= 0)
        {
            totalAc += 1;
        }

        return SkillTreeKeystoneResolver.ApplyAc(data, totalAc);
    }

    /// <summary>
    /// 有效 AC = 基础 AC + 被动技能加成 + 防御姿态加值
    /// passiveAcBonus 由调用方从 Frontend 提取传入
    /// </summary>
    public static int GetEffectiveAc(UnitData data, bool usingPrimaryWeapon, bool isDefending, int passiveAcBonus)
    {
        int ac = GetAc(data, usingPrimaryWeapon);
        ac += passiveAcBonus;
        if (isDefending) ac += 2;
        return ac;
    }

    /// <summary>所有防具的剩余装甲值总和</summary>
    public static int GetTotalCurrentArmorPoints(UnitData data)
    {
        int total = 0;
        if (data?.Armor != null) total += data.Armor.CurrentArmorPoints;
        if (data?.Shield != null) total += data.Shield.CurrentArmorPoints;
        if (data?.Helmet != null) total += data.Helmet.CurrentArmorPoints;
        return total;
    }

    /// <summary>当前 DR 值（不低于 0）</summary>
    public static int GetDr(UnitData data) =>
        data != null ? Math.Max(0, data.CurrentDr) : 0;

    /// <summary>DR 穿透阈值 = max(armorDrThreshold, naturalDrThreshold) + buff</summary>
    public static int GetDrThreshold(UnitData data)
    {
        if (data == null || data.CurrentDr <= 0) return 0;
        int threshold = 0;
        if (data.Armor != null) threshold = Math.Max(threshold, data.Armor.DrThreshold);
        if (data.NaturalDrThreshold > 0) threshold = Math.Max(threshold, data.NaturalDrThreshold);
        // v0.8 E1: buff 修正 DR 阈值（支持 Override + FlatBonus）
        var drMod = Buff.BuffSystem.ResolveStatModifiers(data, "dr_threshold");
        if (drMod.OverrideValue.HasValue)
            return Math.Max(0, (int)drMod.OverrideValue.Value);
        int drResult = threshold + (int)drMod.FlatBonus;
        return Math.Max(0, drResult);
    }

    /// <summary>最大 DR = NaturalDr + ArmorDr + ShieldDr</summary>
    public static int GetMaxDr(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 0;
        int dr = data.NaturalDr;
        if (data.Armor != null) dr += data.Armor.DrThreshold;

        // v0.7: 盾牌从独立字段 data.Shield 读，不走副手武器槽。
        if (data.Shield != null && data.Shield.armorType == ArmorData.ArmorType.Shield)
            dr += data.Shield.DrThreshold;

        return dr;
    }

    /// <summary>初始化 DR（战斗开始时调用）</summary>
    public static void InitDr(UnitData data, bool usingPrimaryWeapon = true)
    {
        if (data != null)
        {
            data.MaxDr = GetMaxDr(data, usingPrimaryWeapon);
            data.CurrentDr = data.MaxDr;
        }
    }

    /// <summary>承受 DR 伤害，返回实际扣除的 DR 值</summary>
    public static int TakeDrDamage(UnitData data, int amount)
    {
        if (data == null || data.CurrentDr <= 0) return 0;
        int actual = Math.Min(amount, data.CurrentDr);
        data.CurrentDr -= actual;
        return actual;
    }

    // ========================================
    // 武器槽位
    // ========================================

    /// <summary>获取主手武器</summary>
    public static ItemData? GetMainHand(UnitData data, bool usingPrimaryWeapon) =>
        usingPrimaryWeapon ? data?.PrimaryMainHand : data?.SecondaryMainHand;

    /// <summary>获取副手物品</summary>
    public static ItemData? GetOffHand(UnitData data, bool usingPrimaryWeapon) =>
        usingPrimaryWeapon ? data?.PrimaryOffHand : data?.SecondaryOffHand;

    // ========================================
    // 攻击与伤害
    // ========================================

    /// <summary>
    /// 攻击加值 = 武器精通命中加成 + 武器命中修正 (v0.6 4.1，不再使用等级专精加值)
    /// + 轻型武器 Lv.7+ +1 命中 (v0.7 武器精通 15 级体系，原 Lv.5 阈值按比例上调)
    /// </summary>
    public static int GetAttackBonus(UnitData data, bool usingPrimaryWeapon)
    {
        if (data == null) return 0;
        var weapon = GetMainHand(data, usingPrimaryWeapon) as WeaponData;

        // 武器精通命中加成 = floor(MasteryLevel / 2)（v0.7 15 级体系）
        // Lv.2→+1, Lv.4→+2, Lv.6→+3, ..., Lv.14→+7, Lv.15→+7
        int masteryHitBonus = 0;
        int lightLv7Bonus = 0;
        int elfWeaponBonus = 0;
        if (weapon != null)
        {
            int masteryLevel = data.WeaponMastery.GetLevelBySubtype(weapon.Subtype);
            masteryHitBonus = masteryLevel / 2;
            // v0.7 轻型武器 Lv.7+ 命中 +1（仅命中，不影响暴击阈值）
            if (masteryLevel >= 7 && weapon.Weight == WeaponData.WeightCategory.Light)
                lightLv7Bonus = 1;

            // 精灵技艺：剑类和弓类命中 +1
            if (data.Race != null && Array.IndexOf(data.Race.RacialTraits, "elf_weapon_proficiency") >= 0)
            {
                if (weapon.Subtype == WeaponData.WeaponSubtype.ArmingSword 
                    || weapon.Subtype == WeaponData.WeaponSubtype.Greatsword 
                    || weapon.IsBow)
                {
                    elfWeaponBonus = 1;
                }
            }
        }

        // 武器自身命中修正
        int weaponHitBonus = 0;
        if (weapon?.Subtype != null)
            weaponHitBonus = WeaponRegistry.GetConfig(weapon.Subtype).HitBonus;

        return masteryHitBonus + weaponHitBonus + lightLv7Bonus + elfWeaponBonus + GetBuffStatBonus(data, "attack_bonus");
    }

    /// <summary>
    /// 掷骰伤害
    /// 返回 Dictionary: dice, multiplier, str_bonus_pct, mastery_bonus_pct, total, text, weapon_subtype
    /// </summary>
    public static Godot.Collections.Dictionary RollDamage(UnitData data, bool usingPrimaryWeapon)
    {
        var weapon = GetMainHand(data, usingPrimaryWeapon) as WeaponData;
        int dmgDice = 0;
        string dText = "徒手(1-3)";

        // v0.6 §6.5: 武器伤害骰由武器面板（含 tier 缩放）决定，
        // 不再加"等级追加骰"（v0.5 旧机制 GetDamageDiceCount(level) 已废弃）。
        // 等级伤害成长走武器精通（每级 +10%）+ 装备 tier 升级。

        // 骰子结果
        if (weapon != null)
        {
            for (int i = 0; i < weapon.DamageDiceCount; i++)
                dmgDice += GD.RandRange(1, weapon.DamageDiceSides);
            int wMin = weapon.DamageDiceCount;
            int wMax = weapon.DamageDiceCount * weapon.DamageDiceSides;
            dText = $"{wMin}-{wMax}";
        }
        else
        {
            // 徒手/天生武器攻击：非类人怪物根据等级决定基础天生武器骰
            if (data != null && data.enemyType != UnitData.EnemyType.Humanoid)
            {
                // 天生武器伤害骰 = 1 + 等级/8。比如 8级是 2d6，24级是 4d6，48级是 7d6。
                int diceCount = 1 + data.Level / 8;
                int diceSides = 6;
                dmgDice = 0;
                for (int i = 0; i < diceCount; i++) dmgDice += GD.RandRange(1, diceSides);
                dText = $"{diceCount}d{diceSides}";
            }
            else
            {
                dmgDice = GD.RandRange(1, 3);
                dText = "徒手(1-3)";
            }
        }

        // 百分比乘法加成体系
        // STR加成: floor(sqrt(STR)) × 10%
        int strMod = data != null ? (int)Mathf.Floor(Mathf.Sqrt(GetEffectiveStr(data))) : 0;
        float strBonus = strMod * 0.1f;

        // 武器精通加成: 精通等级 × 10%
        int masteryLevel = (weapon != null && data != null)
            ? data.WeaponMastery.GetLevelBySubtype(weapon.Subtype)
            : 0;
        float masteryBonus = masteryLevel * 0.1f;

        float multiplier = 1.0f + strBonus + masteryBonus;

        // 精灵技艺：剑类和弓类伤害修正 x1.1
        if (data != null && Array.IndexOf(data.Race?.RacialTraits ?? Array.Empty<string>(), "elf_weapon_proficiency") >= 0)
        {
            if (weapon != null && (weapon.Subtype == WeaponData.WeaponSubtype.ArmingSword 
                || weapon.Subtype == WeaponData.WeaponSubtype.Greatsword 
                || weapon.IsBow))
            {
                multiplier *= 1.1f;
            }
        }

        // 半兽人狂暴：血量低于 50% 时伤害加 20%
        if (data != null && Array.IndexOf(data.Race?.RacialTraits ?? Array.Empty<string>(), "rage") >= 0)
        {
            int maxHp = GetMaxHp(data);
            if (maxHp > 0 && data.Runtime.CurrentHp * 2 < maxHp)
            {
                multiplier *= 1.2f;
            }
        }

        int totalDmg = Math.Max(1, (int)(dmgDice * multiplier));

        // v0.8: buff damage 平加（来自职业技能大招的临时伤害加成）
        totalDmg += GetBuffStatBonus(data, "damage");

        return new Godot.Collections.Dictionary
        {
            { "dice", dmgDice },
            { "multiplier", multiplier },
            { "str_bonus_pct", (int)(strBonus * 100) },
            { "mastery_bonus_pct", (int)(masteryBonus * 100) },
            { "total", totalDmg },
            { "text", $"{dText}×{multiplier:F1}({(int)(multiplier * 100)}%)" },
            { "weapon_subtype", weapon?.Subtype.ToString() ?? "Unarmed" }
        };
    }



    // ========================================
    // 先攻计算
    // ========================================

    /// <summary>
    /// 先攻修正值 = DEX_mod + BaseInitiative
    /// 实际先攻 = d20 + GetInitiativeModifier()
    /// </summary>
    public static int GetInitiativeModifier(UnitData data)
    {
        if (data == null) return 0;
        return GetStatModifier(GetEffectiveDex(data)) + data.BaseInitiative;
    }

    // ========================================
    // 移动
    // ========================================

    /// <summary>移动范围 = 基础 + 装备加成 + 饰品加成 + 坐骑加成 + 技能盘 speed 节点 + buff</summary>
    public static int GetMoveRange(UnitData data)
    {
        if (data == null) return 4;
        int move = data.BaseMoveRange;
        move += data.GetEquipmentMoveBonus();
        move += data.AccessoryMoveBonus;
        if (data.Mount != null) move += data.Mount.SpeedBonus;
        move += GetBuffStatBonus(data, "speed");
        return SkillTreeKeystoneResolver.ApplyMoveRange(data, move);
    }

    // ============================================================
    // Buff 系统集成
    // ============================================================

    /// <summary>
    /// 从 BuffSystem 查询指定属性的 Base 层加值(整数)。
    /// 用于 GetAc / GetAttackBonus / GetMoveRange 等简单加法属性。
    /// 对于需要完整多乘区的属性(如 damage)，平加部分仍走这里，
    /// 百分比乘区(Increased/More/FinalMult)由伤害管线末端的
    /// BuffSystem.ResolveMultiplier 折算进 DamageInput.FinalMultiplier。
    /// </summary>
    private static int GetBuffStatBonus(UnitData? data, string stat)
    {
        if (data == null) return 0;
        var result = Buff.BuffSystem.ResolveStatModifiers(data, stat);
        // 只取 FlatBonus(Base 层平加值)。
        // 百分比乘区(Increased/More/FinalMult)在伤害管线末端折算(见 CombatResolver
        // 的 DamageInput.FinalMultiplier 调用 BuffSystem.ResolveMultiplier)，不在此处计入，
        // 避免把"+25% 伤害"错误地当成"+0.25 平伤"。
        return (int)result.FlatBonus;
    }
}
