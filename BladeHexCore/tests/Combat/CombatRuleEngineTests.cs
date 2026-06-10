// CombatRuleEngineTests.cs
// 战斗规则引擎纯函数测试 — 服务于架构优化 spec R7。
//
// 设计原则：
//   - 纯静态测试，不依赖 Godot 场景树或测试框架
//   - 每个 Test_xxx 方法返回 (bool ok, string description)
//   - RunAll() 聚合结果供 Runner 调用
//
// 覆盖关键路径：
//   - CalculateDamage（基础伤害、擦伤减半、暴击倍率、偷袭、被动加成、包夹、冲锋、减免、最终倍率）
//   - GetWeaponDamageRange（武器伤害范围预览）
//   - GetAdjustedCritThreshold（士气暴击修正）
//   - CalculateCounterDamage（反击伤害）
using System.Collections.Generic;
using BladeHex.Combat.Buff;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Combat.Tests;

public static class CombatRuleEngineTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(Damage_Base_NoModifiers), Damage_Base_NoModifiers);
        yield return Run(nameof(Damage_Graze_HalvesDamage), Damage_Graze_HalvesDamage);
        yield return Run(nameof(Damage_Critical_DoublesByMultiplier), Damage_Critical_DoublesByMultiplier);
        yield return Run(nameof(Damage_Critical_AppliesDefenderReduction), Damage_Critical_AppliesDefenderReduction);
        yield return Run(nameof(Damage_SneakAddsFlat), Damage_SneakAddsFlat);
        yield return Run(nameof(Damage_PassiveMeleeBonus_Applied), Damage_PassiveMeleeBonus_Applied);
        yield return Run(nameof(Damage_PassiveMeleeMultiplier_OnlyMelee), Damage_PassiveMeleeMultiplier_OnlyMelee);
        yield return Run(nameof(Damage_FlankMultiplier_Applied), Damage_FlankMultiplier_Applied);
        yield return Run(nameof(Damage_ChargeMultiplier_Applied), Damage_ChargeMultiplier_Applied);
        yield return Run(nameof(Damage_DamageReduction_NeverBelowOne), Damage_DamageReduction_NeverBelowOne);
        yield return Run(nameof(Damage_FinalMultiplier_AoOHalves), Damage_FinalMultiplier_AoOHalves);
        yield return Run(nameof(DamageResolution_RangedShieldAbsorbsBeforeHp), DamageResolution_RangedShieldAbsorbsBeforeHp);
        yield return Run(nameof(DamageResolution_CrushCanBreakArmor), DamageResolution_CrushCanBreakArmor);
        yield return Run(nameof(Range_d6Plus2_ReturnsMinMaxAvg), Range_d6Plus2_ReturnsMinMaxAvg);
        yield return Run(nameof(Range_NeverZero), Range_NeverZero);
        yield return Run(nameof(Counter_FullDirection_FullDamage), Counter_FullDirection_FullDamage);
        yield return Run(nameof(Counter_HalfDirection_HalvesDamage), Counter_HalfDirection_HalvesDamage);
        yield return Run(nameof(Counter_ZeroDirection_NoDamage), Counter_ZeroDirection_NoDamage);
        yield return Run(nameof(Buff_ResolveResult_BaseOnly), Buff_ResolveResult_BaseOnly);
        yield return Run(nameof(Buff_ResolveResult_BaseAndIncreased), Buff_ResolveResult_BaseAndIncreased);
        yield return Run(nameof(Buff_ResolveResult_BaseAndIncreasedAndMore), Buff_ResolveResult_BaseAndIncreasedAndMore);
        yield return Run(nameof(Buff_ResolveResult_FullMultiplicative), Buff_ResolveResult_FullMultiplicative);
        yield return Run(nameof(Buff_ResolveResult_OverrideValue), Buff_ResolveResult_OverrideValue);
        yield return Run(nameof(Buff_AcBonusAlias_AffectsCombatStatsAc), Buff_AcBonusAlias_AffectsCombatStatsAc);
        yield return Run(nameof(Buff_DamageTaken_AppliesIncomingMultiplier), Buff_DamageTaken_AppliesIncomingMultiplier);
        yield return Run(nameof(Buff_CritTaken_CanIncreaseCriticalDamage), Buff_CritTaken_CanIncreaseCriticalDamage);
        yield return Run(nameof(Buff_ApplyDirect_RespectsAgnosticCommand), Buff_ApplyDirect_RespectsAgnosticCommand);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (System.Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    // ========================================
    // Damage 基础
    // ========================================

    private static (bool, string) Damage_Base_NoModifiers()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 10, $"expected 10, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_Graze_HalvesDamage()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.IsGraze = true;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 5, $"expected 5 (10/2), got {result.FinalDamage}");
    }

    private static (bool, string) Damage_Critical_DoublesByMultiplier()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.IsCritical = true;
        input.CritMultiplier = 2;
        input.CritDamageTakenMultiplier = 1.0f;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 20, $"expected 20 (10*2), got {result.FinalDamage}");
    }

    private static (bool, string) Damage_Critical_AppliesDefenderReduction()
    {
        // 暴击 ×2，但防御方暴击受伤倍率 0.5（高 WIS）
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.IsCritical = true;
        input.CritMultiplier = 2;
        input.CritDamageTakenMultiplier = 0.5f;
        var result = CombatRuleEngine.CalculateDamage(in input);
        // 10 * 2 = 20 → 20 * 0.5 = 10
        return Expect(result.FinalDamage == 10, $"expected 10, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_SneakAddsFlat()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.SneakDamage = 5;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 15, $"expected 15, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_PassiveMeleeBonus_Applied()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.IsMelee = true;
        input.PassiveMeleeBonus = 3;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 13, $"expected 13, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_PassiveMeleeMultiplier_OnlyMelee()
    {
        // 远程攻击不应触发 melee 倍率
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.IsMelee = false;
        input.PassiveMeleeBonus = 5; // 不应生效
        input.PassiveMeleeMultiplier = 2.0f; // 不应生效
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 10, $"expected 10 (no melee bonus), got {result.FinalDamage}");
    }

    private static (bool, string) Damage_FlankMultiplier_Applied()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.FlankMultiplier = 1.5f;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 15, $"expected 15, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_ChargeMultiplier_Applied()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.ChargeMultiplier = 1.5f;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 15, $"expected 15, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_DamageReduction_NeverBelowOne()
    {
        // 减免量 > 伤害本身时，应该保留至少 1 点
        var input = MakeBaseDamageInput(baseDamage: 5);
        input.DamageReduction = 100;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage >= 1, $"expected >= 1, got {result.FinalDamage}");
    }

    private static (bool, string) Damage_FinalMultiplier_AoOHalves()
    {
        // AoO 0.5 倍率
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.FinalMultiplier = 0.5f;
        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 5, $"expected 5, got {result.FinalDamage}");
    }

    private static (bool, string) DamageResolution_RangedShieldAbsorbsBeforeHp()
    {
        var unit = MakeUnit(currentHp: 100);
        unit.Shield = new ArmorData
        {
            armorType = ArmorData.ArmorType.Shield,
            DrThreshold = 8,
            CurrentArmorPoints = 100,
            MaxArmorPoints = 100,
            RangedDamageMultiplier = 0.5f,
        };

        var model = new BattleUnitModel(unit) { CurrentHp = 100 };
        var result = model.ApplyDamage(
            DamageSource.WeaponAttack,
            20,
            WeaponData.DamageType.Pierce,
            naturalRoll: 20,
            isRanged: true);

        bool ok = result.ShieldAbsorbed == 10
            && result.HpDamage == 10
            && model.CurrentHp == 90
            && unit.Shield?.CurrentArmorPoints == 90;

        return Expect(ok,
            $"expected shield=10 hpDamage=10 hp=90 shieldAp=90; got shield={result.ShieldAbsorbed} hpDamage={result.HpDamage} hp={model.CurrentHp} shieldAp={unit.Shield?.CurrentArmorPoints ?? -1}");
    }

    private static (bool, string) DamageResolution_CrushCanBreakArmor()
    {
        var unit = MakeUnit(currentHp: 100);
        unit.Armor = new ArmorData
        {
            armorType = ArmorData.ArmorType.Heavy,
            DrThreshold = 10,
            CurrentArmorPoints = 5,
            MaxArmorPoints = 5,
        };
        unit.CurrentDr = 10;

        var model = new BattleUnitModel(unit) { CurrentHp = 100 };
        var result = model.ApplyDamage(
            DamageSource.WeaponAttack,
            20,
            WeaponData.DamageType.Crush,
            naturalRoll: 20,
            weaponWeight: WeaponData.WeightCategory.Heavy);

        bool ok = result.ArmorBroken
            && unit.Armor == null
            && result.HpDamage == 6
            && result.DrDamage == 14
            && model.CurrentHp == 94;

        return Expect(ok,
            $"expected broken=true hpDamage=6 drDamage=14 hp=94; got broken={result.ArmorBroken} armorNull={unit.Armor == null} hpDamage={result.HpDamage} drDamage={result.DrDamage} hp={model.CurrentHp}");
    }

    // ========================================
    // 武器伤害范围
    // ========================================

    private static (bool, string) Range_d6Plus2_ReturnsMinMaxAvg()
    {
        // 1d6+2: min=1+2=3, max=6+2=8, avg=3+2=5 (round avg = (1+6)/2*1+2)
        var (min, max, avg) = CombatRuleEngine.GetWeaponDamageRange(diceCount: 1, diceSides: 6, statMod: 2);
        if (min != 3) return (false, $"min: expected 3, got {min}");
        if (max != 8) return (false, $"max: expected 8, got {max}");
        // avg = 1 * (6+1) / 2 + 2 = 3 + 2 = 5
        if (avg != 5) return (false, $"avg: expected 5, got {avg}");
        return (true, "");
    }

    private static (bool, string) Range_NeverZero()
    {
        // 极端情况：1d1-100 应被钳到 1
        var (min, max, avg) = CombatRuleEngine.GetWeaponDamageRange(diceCount: 1, diceSides: 1, statMod: -100);
        bool ok = min >= 1 && max >= 1 && avg >= 1;
        return Expect(ok, $"all should be >= 1, got min={min} max={max} avg={avg}");
    }

    // ========================================
    // 反击
    // ========================================

    private static (bool, string) Counter_FullDirection_FullDamage()
    {
        // 1d6+2, dirMul=1.0 → avg(1,6)+2 = 3+2 = 5
        int dmg = CombatRuleEngine.CalculateCounterDamage(1, 6, 2, 1.0f);
        return Expect(dmg == 5, $"expected 5, got {dmg}");
    }

    private static (bool, string) Counter_HalfDirection_HalvesDamage()
    {
        // 1d6+2, dirMul=0.5 → 5*0.5 = 2.5 → (int) 2，但 Math.Max(1, _) → 2
        int dmg = CombatRuleEngine.CalculateCounterDamage(1, 6, 2, 0.5f);
        return Expect(dmg == 2, $"expected 2, got {dmg}");
    }

    private static (bool, string) Counter_ZeroDirection_NoDamage()
    {
        int dmg = CombatRuleEngine.CalculateCounterDamage(1, 6, 2, 0f);
        return Expect(dmg == 0, $"expected 0, got {dmg}");
    }

    // ========================================
    // 工具方法
    // ========================================

    private static CombatRuleEngine.DamageInput MakeBaseDamageInput(int baseDamage)
    {
        return new CombatRuleEngine.DamageInput
        {
            BaseDamage = baseDamage,
            IsGraze = false,
            IsCritical = false,
            CritMultiplier = 2,
            CritDamageTakenMultiplier = 1.0f,
            SneakDamage = 0,
            PassiveMeleeBonus = 0,
            PassiveMeleeMultiplier = 1.0f,
            IsMelee = true,
            FlankMultiplier = 1.0f,
            ChargeMultiplier = 1.0f,
            MountBonus = 0,
            DamageReduction = 0,
            FinalMultiplier = 1.0f,
        };
    }

    private static (bool ok, string msg) Expect(bool condition, string failureMsg)
    {
        return (condition, condition ? "" : failureMsg);
    }

    // ========================================
    // Buff 多乘区属性解算单元测试
    // ========================================

    private static (bool, string) Buff_ResolveResult_BaseOnly()
    {
        var result = new StatResolveResult
        {
            FlatBonus = 20
        };
        float final = result.Apply(100f);
        return Expect(System.Math.Abs(final - 120f) < 0.001f, $"expected 120, got {final}");
    }

    private static (bool, string) Buff_ResolveResult_BaseAndIncreased()
    {
        var result = new StatResolveResult
        {
            FlatBonus = 20,
            IncreasedPercent = 0.5f // +50% Increased
        };
        float final = result.Apply(100f);
        return Expect(System.Math.Abs(final - 180f) < 0.001f, $"expected 180, got {final}");
    }

    private static (bool, string) Buff_ResolveResult_BaseAndIncreasedAndMore()
    {
        var result = new StatResolveResult
        {
            FlatBonus = 20,
            IncreasedPercent = 0.5f, // +50%
            MoreMultiplier = 1.2f    // 1.2x (对应代码中的 1f + Value, 等于 MoreMultiplier 的初始 1.0f 乘以 1.2f)
        };
        // (100 + 20) * 1.5 * 1.2 = 216
        float final = result.Apply(100f);
        return Expect(System.Math.Abs(final - 216f) < 0.001f, $"expected 216, got {final}");
    }

    private static (bool, string) Buff_ResolveResult_FullMultiplicative()
    {
        var result = new StatResolveResult
        {
            FlatBonus = 20,
            IncreasedPercent = 0.5f,
            MoreMultiplier = 1.2f,
            FinalMultiplier = 0.9f // Final 0.9 倍 (90%)
        };
        // (100 + 20) * 1.5 * 1.2 * 0.9 = 194.4
        float final = result.Apply(100f);
        int finalInt = result.ApplyInt(100);
        if (System.Math.Abs(final - 194.4f) > 0.001f)
            return (false, $"float Apply expected 194.4, got {final}");
        if (finalInt != 194)
            return (false, $"int ApplyInt expected 194, got {finalInt}");
        return (true, "");
    }

    private static (bool, string) Buff_ResolveResult_OverrideValue()
    {
        var result = new StatResolveResult
        {
            FlatBonus = 500,
            IncreasedPercent = 2.0f,
            OverrideValue = 88f // Override 直接改写
        };
        float final = result.Apply(100f);
        return Expect(System.Math.Abs(final - 88f) < 0.001f, $"expected 88, got {final}");
    }

    private static (bool, string) Buff_AcBonusAlias_AffectsCombatStatsAc()
    {
        var unit = MakeUnit();
        int baseAc = CombatStats.GetAc(unit, usingPrimaryWeapon: true);
        unit.Runtime.ActiveBuffs.Add(new BuffInstance
        {
            Id = "ward_blessing_buff",
            Modifiers = new List<StatModifier> { new() { Stat = "ac_bonus", Value = 2 } },
        });

        int buffedAc = CombatStats.GetAc(unit, usingPrimaryWeapon: true);
        return Expect(buffedAc == baseAc + 2, $"expected AC {baseAc + 2}, got {buffedAc}");
    }

    private static (bool, string) Buff_DamageTaken_AppliesIncomingMultiplier()
    {
        var defender = MakeUnit(currentHp: 100);
        defender.Runtime.ActiveBuffs.Add(new BuffInstance
        {
            Id = "stance_guard",
            Modifiers = new List<StatModifier> { new() { Stat = "damage_taken", Value = -0.15f } },
        });

        var model = new BattleUnitModel(defender) { CurrentHp = 100 };
        var result = model.ApplyDamage(DamageSource.Skill, 100);
        return Expect(result.HpDamage == 85 && model.CurrentHp == 15,
            $"expected 85 hp damage and 15 hp left, got damage={result.HpDamage}, hp={model.CurrentHp}");
    }

    private static (bool, string) Buff_CritTaken_CanIncreaseCriticalDamage()
    {
        var input = MakeBaseDamageInput(baseDamage: 10);
        input.IsCritical = true;
        input.CritMultiplier = 2;
        input.CritDamageTakenMultiplier = 1.20f;

        var result = CombatRuleEngine.CalculateDamage(in input);
        return Expect(result.FinalDamage == 24, $"expected 24, got {result.FinalDamage}");
    }

    private static (bool, string) Buff_ApplyDirect_RespectsAgnosticCommand()
    {
        var treeData = new SkillTreeData();
        string agnosticNode = "";
        foreach (var node in treeData.Nodes.Values)
        {
            if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone && node.SkillEffect == "agnostic_command")
            {
                agnosticNode = node.NodeId;
                break;
            }
        }
        if (string.IsNullOrEmpty(agnosticNode))
            return (false, "missing agnostic_command node");

        var characterTree = new CharacterSkillTree();
        characterTree.Deserialize(new Godot.Collections.Dictionary
        {
            { "activated_nodes", new Godot.Collections.Array<string>(new[] { SkillTreeData.StartNodeId, agnosticNode }) },
            { "available_skill_points", 0 },
            { "character_level", 99 },
        }, treeData);

        var unit = MakeUnit();
        unit.Runtime.SkillTree = characterTree;
        BuffSystem.ApplyDirect(unit, new BuffInstance { Id = "lua_positive", IsNegative = false });

        return Expect(unit.Runtime.ActiveBuffs.Count == 0,
            $"agnostic_command should reject positive ApplyDirect buffs, got {unit.Runtime.ActiveBuffs.Count}");
    }

    private static UnitData MakeUnit(int currentHp = 20)
    {
        return new UnitData
        {
            UnitName = "combat_rule_test",
            Level = 1,
            BaseMaxHp = 20,
            BaseAc = 10,
            BaseAp = 12,
            BaseMoveRange = 4,
            Str = 10,
            Dex = 10,
            Con = 10,
            Intel = 10,
            Wis = 10,
            Cha = 10,
            Runtime = { CurrentHp = currentHp },
        };
    }
}
