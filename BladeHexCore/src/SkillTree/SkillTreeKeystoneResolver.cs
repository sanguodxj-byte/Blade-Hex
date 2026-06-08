using BladeHex.Combat;
using BladeHex.Combat.Buff;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// Runtime contract for skill-tree keystones. Keeps design effect IDs centralized.
/// </summary>
public static class SkillTreeKeystoneResolver
{
    public static readonly string[] KeystoneEffects =
    [
        "berserk_power",
        "resolute_technique",
        "blood_oath",
        "ghost_footwork",
        "acrobatics",
        "point_blank",
        "undying_body",
        "shield_bastion",
        "iron_body",
        "absolute_focus",
        "blood_magic",
        "chaos_inoculation",
        "assassin_instinct",
        "elemental_overload",
        "pain_attunement",
        "royal_presence",
        "agnostic_command",
        "martyr_oath",
    ];

    public static bool Has(UnitData? unit, string effect)
        => unit?.Runtime?.SkillTree?.HasSkillEffect(effect) == true;

    public static int ApplyMaxHp(UnitData unit, int hp)
    {
        var tree = unit.Runtime?.SkillTree;
        if (tree == null) return hp;

        hp += tree.GetHpBonus();
        hp += tree.GetCostInt("max_hp");
        if (tree.HasSkillEffect("con_p02_passive")
            && unit.Armor != null
            && unit.Armor.armorType == ArmorData.ArmorType.Heavy)
            hp += 5;
        if (tree.HasSkillEffect("ghost_footwork")) hp = ScaleFloor(hp, 0.80f);
        if (tree.HasSkillEffect("chaos_inoculation")) hp = ScaleFloor(hp, 0.50f);
        if (tree.HasSkillEffect("pain_attunement")) hp = ScaleFloor(hp, 0.75f);
        if (tree.HasSkillEffect("royal_presence")) hp = ScaleFloor(hp, 0.80f);
        return System.Math.Max(1, hp);
    }

    public static int ApplyMaxMana(UnitData unit, int mana)
        => Has(unit, "blood_magic") ? 0 : System.Math.Max(0, mana + (unit.Runtime?.SkillTree?.GetCostInt("mana_max") ?? 0));

    public static int ApplyAc(UnitData unit, int ac)
    {
        var tree = unit.Runtime?.SkillTree;
        if (tree == null) return ac;

        ac += tree.GetAcBonus() + tree.GetCostInt("ac");
        if (tree.HasSkillEffect("con_p01_passive") && unit.Shield != null)
            ac += 1;
        return ac;
    }

    public static int ApplyMoveRange(UnitData unit, int move)
    {
        var tree = unit.Runtime?.SkillTree;
        return tree == null ? System.Math.Max(1, move) : System.Math.Max(1, move + tree.GetSpeedBonus() + tree.GetCostInt("speed"));
    }

    public static int ApplyAttackBonus(UnitData attacker, int attackBonus, bool isMelee, bool isRanged, int distance)
    {
        var tree = attacker.Runtime?.SkillTree;
        if (tree == null) return attackBonus;

        attackBonus += isMelee ? tree.GetMeleeHitBonus() : tree.GetRangedHitBonus();
        if (isRanged)
        {
            if (tree.HasSkillEffect("resolute_technique")) attackBonus -= 4;
            if (tree.HasSkillEffect("point_blank") && distance > 4) attackBonus -= 2;
        }
        return attackBonus;
    }

    public static int ApplyIncomingAttackBonus(UnitData defender, int attackBonus, bool incomingRanged)
    {
        if (incomingRanged && Has(defender, "ghost_footwork"))
            return attackBonus - 2;
        return attackBonus;
    }

    public static float GetBonusCritChance(UnitData attacker)
    {
        var tree = attacker.Runtime?.SkillTree;
        if (tree == null) return 0f;

        float bonus = tree.GetCriticalRateBonus();
        bonus += tree.GetCostFloat("critical_rate");
        if (tree.HasSkillEffect("assassin_instinct")) bonus += 0.05f;
        if (tree.HasSkillEffect("pain_attunement") && IsLowHp(attacker, 0.35f)) bonus += 0.15f;
        return System.Math.Max(0f, bonus);
    }

    public static float GetBonusCritChance(UnitData attacker, WeaponData? weapon, bool isRanged, bool hasMoved)
    {
        float bonus = GetBonusCritChance(attacker);
        var tree = attacker.Runtime?.SkillTree;
        if (tree == null) return bonus;

        if (weapon != null)
        {
            var mastery = WeaponMastery.GetKeyFor(weapon.Subtype);
            if (tree.HasSkillEffect("wis_p01_passive") && mastery.Weight == WeaponData.WeightCategory.Light)
                bonus += 0.05f;
            if (tree.HasSkillEffect("wis_p07_passive")
                && mastery.Weight == WeaponData.WeightCategory.Light
                && (mastery.DamageType == WeaponData.DamageType.Slash || mastery.DamageType == WeaponData.DamageType.Pierce))
                bonus += 0.05f;
        }

        if (tree.HasSkillEffect("wis_p04_passive") && !hasMoved)
            bonus += 0.05f;
        if (tree.HasSkillEffect("wis_p08_passive") && attacker.Runtime!.SkillTreeKillCritPendingTurns > 0)
            bonus += 0.05f;

        return System.Math.Max(0f, bonus);
    }

    public static void ApplyAttackRollRules(UnitData attacker, ref CombatRuleEngine.AttackInput input, bool isMelee)
    {
        var tree = attacker.Runtime?.SkillTree;
        if (tree == null) return;

        if (isMelee && tree.HasSkillEffect("resolute_technique"))
            input.ForceHit = true;
        if (tree.HasSkillEffect("resolute_technique") || tree.HasSkillEffect("elemental_overload"))
            input.SuppressCritical = true;
        if (BuffModifierReader.HasTruthy(attacker, "force_attack_hit"))
            input.ForceHit = true;
        if (BuffModifierReader.HasTruthy(attacker, "force_attack_crit"))
        {
            input.ForceHit = true;
            input.ForceCritical = true;
        }
    }

    public static float GetDamageFinalMultiplier(UnitData attacker, bool isMelee, bool isRanged, int distance, bool isCritical)
    {
        var tree = attacker.Runtime?.SkillTree;
        if (tree == null) return 1.0f;

        float multiplier = 1.0f;
        if (isMelee && tree.HasSkillEffect("berserk_power")) multiplier *= 1.50f;
        if (isRanged && tree.HasSkillEffect("point_blank")) multiplier *= distance <= 3 ? 1.50f : 0.75f;
        if (isRanged && tree.HasSkillEffect("shield_bastion")) multiplier *= 0.50f;
        if (tree.HasSkillEffect("pain_attunement") && IsLowHp(attacker, 0.35f)) multiplier *= 1.30f;
        if (tree.HasSkillEffect("elemental_overload") && attacker.Runtime!.KeystoneRecentCritTurns > 0) multiplier *= 1.40f;
        if (isCritical && tree.HasSkillEffect("assassin_instinct")) multiplier *= 1.25f;
        return multiplier;
    }

    public static float GetDamageFinalMultiplier(
        UnitData attacker,
        WeaponData? weapon,
        bool isMelee,
        bool isRanged,
        int distance,
        bool isCritical,
        bool hasMoved)
    {
        float multiplier = GetDamageFinalMultiplier(attacker, isMelee, isRanged, distance, isCritical);
        var tree = attacker.Runtime?.SkillTree;
        if (tree == null) return multiplier;

        if (isMelee)
        {
            if (weapon != null)
            {
                var mastery = WeaponMastery.GetKeyFor(weapon.Subtype);
                if (tree.HasSkillEffect("str_p01_passive") && mastery.DamageType == WeaponData.DamageType.Slash)
                    multiplier *= 1.05f;
                if (tree.HasSkillEffect("str_p02_passive") && mastery.DamageType == WeaponData.DamageType.Crush)
                    multiplier *= 1.05f;
                if (tree.HasSkillEffect("str_p03_passive") && mastery.Weight == WeaponData.WeightCategory.Heavy)
                    multiplier *= 1.05f;
                if (tree.HasSkillEffect("str_p04_passive") && IsSwordSubtype(weapon.Subtype))
                    multiplier *= 1.05f;
            }

            if (tree.HasSkillEffect("str_p06_passive") && attacker.Runtime!.SkillTreeCritMeleeDamagePendingTurns > 0)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("str_p07_passive") && IsLowHp(attacker, 0.50f))
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("str_p08_passive"))
                multiplier *= 1.05f;
        }

        if (isRanged && weapon != null)
        {
            if (tree.HasSkillEffect("dex_p01_passive") && weapon.IsBow)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("dex_p02_passive") && weapon.IsCrossbow)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("dex_p03_passive") && weapon.IsThrowing)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("dex_p04_passive") && WeaponMastery.GetKeyFor(weapon.Subtype).Weight == WeaponData.WeightCategory.Light)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("dex_p07_passive") && !hasMoved)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("dex_p08_passive"))
                multiplier *= 1.05f;
        }

        return multiplier;
    }

    public static float GetSpellDamageFinalMultiplier(UnitData caster, WeaponData? catalyst, bool hasMoved)
    {
        var tree = caster.Runtime?.SkillTree;
        if (tree == null) return 1.0f;

        float multiplier = 1.0f;
        if (catalyst != null)
        {
            if (tree.HasSkillEffect("int_p01_passive") && catalyst.Subtype == WeaponData.WeaponSubtype.Staff)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("int_p02_passive") && catalyst.Subtype == WeaponData.WeaponSubtype.Orb)
                multiplier *= 1.05f;
            if (tree.HasSkillEffect("int_p03_passive") && catalyst.Subtype == WeaponData.WeaponSubtype.Wand)
                multiplier *= 1.05f;
        }

        if (tree.HasSkillEffect("int_p07_passive"))
            multiplier *= 1.05f;
        if (tree.HasSkillEffect("int_p08_passive") && !hasMoved)
            multiplier *= 1.05f;
        return multiplier;
    }

    public static int ApplyReceivedHealing(UnitData receiver, int amount)
    {
        if (amount <= 0) return amount;
        if (Has(receiver, "con_p06_passive"))
            return System.Math.Max(amount + 1, (int)System.Math.Ceiling(amount * 1.05f));
        return amount;
    }

    public static bool IsImmuneToBleed(UnitData unit)
        => Has(unit, "con_p06_passive");

    public static float GetShopPriceMultiplier(UnitData buyer)
        => Has(buyer, "cha_p05_passive") ? 0.95f : 1.0f;

    public static int ApplyIncomingHpDamage(UnitData defender, int hpDamage, bool incomingMelee, bool incomingRanged, bool isPhysical)
    {
        var tree = defender.Runtime?.SkillTree;
        if (tree == null || hpDamage <= 0) return hpDamage;

        if (incomingMelee && tree.HasSkillEffect("shield_bastion") && defender.Shield != null)
            hpDamage = System.Math.Max(1, (int)(hpDamage * 0.75f));
        if (isPhysical && tree.HasSkillEffect("iron_body"))
            hpDamage = System.Math.Max(1, hpDamage - 3);
        if (isPhysical && tree.HasSkillEffect("con_p04_passive"))
            hpDamage = System.Math.Max(1, (int)(hpDamage * 0.95f));
        if (tree.HasSkillEffect("con_p07_passive") && IsLowHp(defender, 0.50f))
            hpDamage = System.Math.Max(1, (int)(hpDamage * 0.95f));
        if (tree.HasSkillEffect("con_p08_passive") && !defender.Runtime!.HasMoved)
            hpDamage = System.Math.Max(1, (int)(hpDamage * 0.95f));
        if (CanDodge(defender)
            && tree.HasSkillEffect("acrobatics")
            && !IsWearingMediumOrHeavyArmor(defender)
            && CombatRandom.RandRange(0, 99) < 30)
            return 0;

        return hpDamage;
    }

    public static int ApplyBeforeDeath(UnitData defender, int currentHp, int hpDamage)
    {
        if (hpDamage <= 0 || currentHp - hpDamage > 0) return hpDamage;
        if (!Has(defender, "undying_body") || defender.Runtime.KeystoneUndyingBodyUsed) return hpDamage;

        defender.Runtime.KeystoneUndyingBodyUsed = true;
        int conMod = CombatStats.GetStatModifier(CombatStats.GetEffectiveCon(defender));
        int chance = System.Math.Clamp(35 + conMod * 5, 5, 95);
        return CombatRandom.RandRange(1, 100) <= chance
            ? System.Math.Max(0, currentHp - 1)
            : hpDamage;
    }

    public static int ApplySpellManaCost(UnitData caster, int manaCost)
        => Has(caster, "blood_magic") ? 0 : manaCost;

    public static int GetSpellHpCost(UnitData caster, int manaCost)
        => Has(caster, "blood_magic") ? System.Math.Max(0, manaCost) : 0;

    public static bool IsImmuneToNegative(UnitData unit)
        => Has(unit, "chaos_inoculation") || BuffModifierReader.HasTruthy(unit, "immune_negative");

    public static bool IsImmuneToFear(UnitData unit)
        => Has(unit, "iron_body") || Has(unit, "royal_presence") || Has(unit, "agnostic_command") || BuffModifierReader.HasTruthy(unit, "immune_fear");

    public static bool IsImmuneToMind(UnitData unit)
        => Has(unit, "agnostic_command") || BuffModifierReader.HasTruthy(unit, "immune_mind");

    public static bool CanReceivePositiveBuff(UnitData unit)
        => !Has(unit, "agnostic_command");

    public static bool CanEquipShield(UnitData unit)
        => !Has(unit, "berserk_power") && !Has(unit, "assassin_instinct");

    public static bool CanEquipMediumOrHeavyArmor(UnitData unit)
        => !Has(unit, "acrobatics") && !Has(unit, "assassin_instinct");

    public static bool CanEquipTwoHandedWeapon(UnitData unit)
        => !Has(unit, "shield_bastion");

    public static bool CanDodge(UnitData unit)
        => !Has(unit, "iron_body");

    public static bool CanRetreat(UnitData unit)
        => !Has(unit, "iron_body") && !BuffModifierReader.HasTruthy(unit, "no_retreat");

    public static bool RequiresOneSchoolSpellStudy(UnitData unit)
        => Has(unit, "absolute_focus");

    public static bool CanStudySpell(UnitData unit, SpellData spell)
    {
        if (!RequiresOneSchoolSpellStudy(unit)) return true;

        SpellData.SpellSchool? locked = null;
        foreach (var known in unit.KnownSpells)
        {
            if (known == null) continue;
            locked ??= known.spellSchool;
            if (known.spellSchool != locked.Value) return false;
        }
        return !locked.HasValue || spell.spellSchool == locked.Value;
    }

    public static bool CanActivateAbsoluteFocus(UnitData unit, out string reason)
    {
        reason = "";
        SpellData.SpellSchool? locked = null;
        foreach (var known in unit.KnownSpells)
        {
            if (known == null) continue;
            locked ??= known.spellSchool;
            if (known.spellSchool == locked.Value) continue;

            reason = "绝对专注限制：已研习多个法术学派，无法点亮。";
            return false;
        }

        return true;
    }

    public static void OnAttackResolved(UnitData attacker, bool critical)
    {
        if (critical && (Has(attacker, "elemental_overload") || Has(attacker, "str_p06_passive")))
            attacker.Runtime.KeystoneRecentCritTurns = 2;
        if (critical && Has(attacker, "str_p06_passive"))
            attacker.Runtime.SkillTreeCritMeleeDamagePendingTurns = 2;
        if (attacker.Runtime.SkillTreeKillCritPendingTurns > 0)
            attacker.Runtime.SkillTreeKillCritPendingTurns = 0;
    }

    public static void ConsumeAttackDamageTriggers(UnitData attacker, bool isMelee)
    {
        if (isMelee && attacker.Runtime.SkillTreeCritMeleeDamagePendingTurns > 0)
            attacker.Runtime.SkillTreeCritMeleeDamagePendingTurns = 0;
    }

    public static void OnEnemyKilled(UnitData attacker)
    {
        if (Has(attacker, "wis_p08_passive"))
            attacker.Runtime.SkillTreeKillCritPendingTurns = 2;
    }

    public static int ApplyBloodOathLeech(UnitData attacker, int hpDamageDealt, bool isMelee = true)
        => isMelee && hpDamageDealt > 0 && Has(attacker, "blood_oath") ? System.Math.Max(1, hpDamageDealt / 4) : 0;

    public static int ApplyBloodOathTurnStartLoss(UnitData unit)
    {
        if (!Has(unit, "blood_oath") || unit.Runtime.CurrentHp <= 1) return 0;

        int loss = System.Math.Max(1, CombatStats.GetMaxHp(unit) / 20);
        int actual = System.Math.Min(loss, unit.Runtime.CurrentHp - 1);
        unit.Runtime.CurrentHp -= actual;
        return actual;
    }

    public static int GetRoyalPresenceSaveBonus(UnitData unit)
        => Has(unit, "royal_presence") ? 2 : 0;

    public static int GetAuraRangeBonus(UnitData unit)
        => Has(unit, "agnostic_command") ? 2 : 0;

    public static bool HasMartyrOath(UnitData unit)
        => Has(unit, "martyr_oath");

    private static bool IsLowHp(UnitData unit, float threshold)
    {
        int maxHp = CombatStats.GetMaxHp(unit);
        return maxHp > 0 && unit.Runtime.CurrentHp > 0 && unit.Runtime.CurrentHp / (float)maxHp < threshold;
    }

    private static bool IsWearingMediumOrHeavyArmor(UnitData unit)
        => unit.Armor != null
            && (unit.Armor.armorType == ArmorData.ArmorType.Medium
                || unit.Armor.armorType == ArmorData.ArmorType.Heavy);

    private static bool IsSwordSubtype(WeaponData.WeaponSubtype subtype)
        => subtype is WeaponData.WeaponSubtype.ArmingSword
            or WeaponData.WeaponSubtype.Greatsword
            or WeaponData.WeaponSubtype.NomadSaber;

    private static int ScaleFloor(int value, float multiplier)
        => System.Math.Max(1, (int)(value * multiplier));
}
