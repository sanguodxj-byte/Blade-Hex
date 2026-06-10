using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Combat;

internal readonly struct DamageResolutionInput
{
    public int Amount { get; init; }
    public WeaponData.DamageType DamageType { get; init; }
    public int NaturalRoll { get; init; }
    public WeaponData.WeightCategory WeaponWeight { get; init; }
    public WeaponMastery? AttackerMastery { get; init; }
    public WeaponData.WeaponSubtype WeaponSubtype { get; init; }
    public int StrPenBonus { get; init; }
    public bool MediumLv5Mastery { get; init; }
    public bool IsRanged { get; init; }
    public bool AllowDamageRedirect { get; init; }
}

internal static class DamageResolutionPipeline
{
    public static DamageResult Resolve(BattleUnitModel defender, in DamageResolutionInput input)
    {
        if (input.Amount <= 0)
            return new DamageResult { RemainingHp = defender.CurrentHp };

        UnitData data = defender.Data;
        int amount = input.Amount;

        var shield = ApplyRangedShieldAbsorption(data, amount, input.IsRanged);
        amount = shield.AmountAfterShield;

        bool isPhysical = IsPhysical(input.DamageType);
        amount = ApplyIronWall(data, amount, isPhysical);

        bool noArmor = data.Armor == null && data.NaturalDr <= 0;
        bool isPenetrated = ResolvePenetration(data, noArmor, input.NaturalRoll, input.StrPenBonus);

        var split = SplitDamage(data, amount, noArmor, isPenetrated, input);
        bool armorBroken = ApplyDrDamage(data, split.DrDamage);

        var hp = ApplyHpDamage(defender, split.HpDamage, input.IsRanged, isPhysical, input.AllowDamageRedirect);
        var mastery = ApplyMasteryXp(input.AttackerMastery, input.WeaponSubtype, hp.HpDamage + split.DrDamage);
        int reflectDamage = ResolveReflectDamage(defender, hp.HpDamage, split.DrDamage);

        return new DamageResult
        {
            IsPenetrated = isPenetrated,
            HpDamage = hp.HpDamage,
            DrDamage = split.DrDamage,
            ArmorBroken = armorBroken,
            KilledUnit = defender.CurrentHp <= 0,
            RemainingHp = defender.CurrentHp,
            MasteryLeveledUp = mastery.LeveledUp,
            MasteryNewLevel = mastery.NewLevel,
            ReflectDamageToAttacker = reflectDamage,
            ShieldAbsorbed = shield.ShieldAbsorbed,
            ShieldBroken = shield.ShieldBroken,
            RedirectedHpDamage = hp.RedirectedHpDamage,
            RedirectedToUnitId = hp.RedirectedToUnitId,
        };
    }

    private static ShieldStage ApplyRangedShieldAbsorption(UnitData data, int amount, bool isRanged)
    {
        int shieldAbsorbed = 0;
        bool shieldBroken = false;

        if (isRanged && data.Shield != null
            && data.Shield.RangedDamageMultiplier < 1.0f
            && data.Shield.CurrentArmorPoints > 0)
        {
            int reduced = (int)(amount * data.Shield.RangedDamageMultiplier);
            int absorbed = amount - reduced;
            int actualAbsorb = System.Math.Min(absorbed, data.Shield.CurrentArmorPoints);
            data.Shield.CurrentArmorPoints -= actualAbsorb;
            shieldAbsorbed = actualAbsorb;
            if (data.Shield.CurrentArmorPoints <= 0)
            {
                shieldBroken = true;
                data.Shield = null;
            }
            amount = reduced + (absorbed - actualAbsorb);
        }

        return new ShieldStage(amount, shieldAbsorbed, shieldBroken);
    }

    private static int ApplyIronWall(UnitData data, int amount, bool isPhysical)
    {
        if (isPhysical && data.Runtime.SkillTree != null && data.Runtime.SkillTree.HasSkillEffect("iron_wall"))
            return System.Math.Max(1, amount - 3);

        return amount;
    }

    private static bool ResolvePenetration(UnitData data, bool noArmor, int naturalRoll, int strPenBonus)
    {
        int armorDrThreshold = CombatStats.GetDrThreshold(data);
        int penRoll = noArmor ? 20 : CombatRandom.RollD20();
        bool isPenetrated = noArmor
            || penRoll == 20
            || (penRoll + strPenBonus) >= armorDrThreshold;

        if (naturalRoll == 20)
            isPenetrated = true;

        return isPenetrated;
    }

    private static DamageSplit SplitDamage(
        UnitData data,
        int amount,
        bool noArmor,
        bool isPenetrated,
        in DamageResolutionInput input)
    {
        if (noArmor)
            return new DamageSplit(amount, 0);

        var coef = DamagePenetrationTable.Lookup(input.DamageType, input.WeaponWeight);
        int hpDamage;
        int drDamage;

        if (isPenetrated)
        {
            hpDamage = System.Math.Max(1, (int)(amount * coef.HpRatioPenetrated));
            drDamage = coef.DrRatioPenetrated > 0f
                ? System.Math.Max(1, (int)(amount * coef.DrRatioPenetrated))
                : 0;
        }
        else
        {
            hpDamage = coef.HpRatioBlocked > 0f
                ? System.Math.Max(1, (int)(amount * coef.HpRatioBlocked))
                : 0;
            drDamage = coef.DrRatioBlocked > 0f
                ? System.Math.Max(1, (int)(amount * coef.DrRatioBlocked))
                : 0;
        }

        if (isPenetrated && input.DamageType == WeaponData.DamageType.Crush && data.CurrentDr <= 0)
            hpDamage = (int)(hpDamage * 1.5f);

        if (input.MediumLv5Mastery && drDamage > 0)
            drDamage = (int)(drDamage * 1.2f);

        return new DamageSplit(hpDamage, drDamage);
    }

    private static bool ApplyDrDamage(UnitData data, int drDamage)
    {
        if (drDamage <= 0)
            return false;

        CombatStats.TakeDrDamage(data, drDamage);

        if (data.Armor != null)
        {
            data.Armor.CurrentArmorPoints = System.Math.Max(0, data.Armor.CurrentArmorPoints - drDamage);
            if (data.Armor.CurrentArmorPoints <= 0)
            {
                data.Armor = null;
                return true;
            }
        }
        else if (data.NaturalDr > 0)
        {
            data.NaturalDr = System.Math.Max(0, data.NaturalDr - drDamage);
        }

        return false;
    }

    private static HpStage ApplyHpDamage(
        BattleUnitModel defender,
        int hpDamage,
        bool isRanged,
        bool isPhysical,
        bool allowDamageRedirect)
    {
        UnitData data = defender.Data;

        if (hpDamage > 0 && data.Runtime.BuffTempHp > 0)
        {
            int absorbedByTemp = System.Math.Min(data.Runtime.BuffTempHp, hpDamage);
            data.Runtime.BuffTempHp -= absorbedByTemp;
            hpDamage -= absorbedByTemp;
        }

        if (hpDamage > 0 && data.Runtime.SkillTree?.HasSkillEffect("unyielding") == true)
        {
            int maxHp = CombatStats.GetMaxHp(data);
            if (maxHp > 0 && (float)defender.CurrentHp / maxHp < 0.25f)
                hpDamage = System.Math.Max(1, (int)(hpDamage * 0.5f));
        }

        if (hpDamage > 0)
            hpDamage = SkillTreeKeystoneResolver.ApplyIncomingHpDamage(data, hpDamage, !isRanged, isRanged, isPhysical);

        int redirectedHpDamage = 0;
        long redirectedToUnitId = 0;
        if (hpDamage > 0)
        {
            var hpHook = Buff.BuffDamageHooks.ApplyBeforeHpDamage(data, hpDamage, allowDamageRedirect);
            hpDamage = hpHook.HpDamage;
            redirectedHpDamage = hpHook.RedirectedHpDamage;
            redirectedToUnitId = hpHook.RedirectedToUnitId;
        }

        hpDamage = SkillTreeKeystoneResolver.ApplyBeforeDeath(data, defender.CurrentHp, hpDamage);
        hpDamage = Buff.BuffDamageHooks.ApplyBeforeDeath(data, defender.CurrentHp, hpDamage);
        if (hpDamage > 0)
        {
            bool hasDeathImmunity = Buff.BuffModifierReader.FirstBuffWithTruthy(data, "death_immunity") != null;
            defender.CurrentHp = hasDeathImmunity
                ? System.Math.Max(1, defender.CurrentHp - hpDamage)
                : System.Math.Max(0, defender.CurrentHp - hpDamage);
        }

        return new HpStage(hpDamage, redirectedHpDamage, redirectedToUnitId);
    }

    private static MasteryStage ApplyMasteryXp(
        WeaponMastery? attackerMastery,
        WeaponData.WeaponSubtype weaponSubtype,
        int totalDealt)
    {
        if (attackerMastery == null || totalDealt <= 0 || weaponSubtype == WeaponData.WeaponSubtype.Unarmed)
            return new MasteryStage(false, 0);

        bool leveledUp = attackerMastery.AddDamageXp(weaponSubtype, totalDealt);
        int newLevel = leveledUp ? attackerMastery.GetLevelBySubtype(weaponSubtype) : 0;
        return new MasteryStage(leveledUp, newLevel);
    }

    private static int ResolveReflectDamage(BattleUnitModel defender, int hpDamage, int drDamage)
    {
        if (hpDamage <= 0)
            return 0;

        var ctx = new BladeHex.Combat.Abilities.TakeDamageContext
        {
            Attacker = null!,
            Defender = defender,
            HpDamageTaken = hpDamage,
            DrDamageTaken = drDamage,
        };
        foreach (var ab in BladeHex.Combat.Abilities.UnitAbilities.GetAll(defender.Data))
            ab.OnTakeDamage(ctx);

        return ctx.ReflectDamage;
    }

    private static bool IsPhysical(WeaponData.DamageType damageType)
    {
        return damageType == WeaponData.DamageType.Slash
            || damageType == WeaponData.DamageType.Pierce
            || damageType == WeaponData.DamageType.Crush;
    }

    private readonly record struct ShieldStage(int AmountAfterShield, int ShieldAbsorbed, bool ShieldBroken);
    private readonly record struct DamageSplit(int HpDamage, int DrDamage);
    private readonly record struct HpStage(int HpDamage, int RedirectedHpDamage, long RedirectedToUnitId);
    private readonly record struct MasteryStage(bool LeveledUp, int NewLevel);
}
