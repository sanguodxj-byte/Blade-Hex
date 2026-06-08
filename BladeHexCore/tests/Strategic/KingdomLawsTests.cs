// KingdomLawsTests.cs
// 王国法律测试
using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Strategic.Kingdom;

namespace BladeHex.Tests.Strategic;

public static class KingdomLawsTests
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
        yield return Run(nameof(Conscription_Major_BoostsRecruit15x), Conscription_Major_BoostsRecruit15x);
        yield return Run(nameof(TaxRate_Low_Reduces25Percent), TaxRate_Low_Reduces25Percent);
        yield return Run(nameof(TaxRate_High_Increases25Percent), TaxRate_High_Increases25Percent);
        yield return Run(nameof(Trade_Embargo_HalvesIncome), Trade_Embargo_HalvesIncome);
        yield return Run(nameof(Laws_Serialization_Roundtrip), Laws_Serialization_Roundtrip);
        yield return Run(nameof(Laws_Clone_CreatesCopy), Laws_Clone_CreatesCopy);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"异常: {ex.Message}");
        }
    }

    private static (bool, string) Conscription_Major_BoostsRecruit15x()
    {
        float mult = KingdomLawEffects.GetRecruitMultiplier(ConscriptionLaw.Major);
        if (Math.Abs(mult - 1.5f) > 0.01f)
            return (false, $"预期1.5，实际{mult}");

        return (true, "");
    }

    private static (bool, string) TaxRate_Low_Reduces25Percent()
    {
        float mult = KingdomLawEffects.GetIncomeMultiplier(TaxLaw.Low);
        if (Math.Abs(mult - 0.75f) > 0.01f)
            return (false, $"预期0.75，实际{mult}");

        return (true, "");
    }

    private static (bool, string) TaxRate_High_Increases25Percent()
    {
        float mult = KingdomLawEffects.GetIncomeMultiplier(TaxLaw.High);
        if (Math.Abs(mult - 1.25f) > 0.01f)
            return (false, $"预期1.25，实际{mult}");

        return (true, "");
    }

    private static (bool, string) Trade_Embargo_HalvesIncome()
    {
        float mult = KingdomLawEffects.GetTradeMultiplier(TradeLaw.Embargo);
        if (Math.Abs(mult - 0.5f) > 0.01f)
            return (false, $"预期0.5，实际{mult}");

        return (true, "");
    }

    private static (bool, string) Laws_Serialization_Roundtrip()
    {
        var laws = new KingdomLaws
        {
            Conscription = ConscriptionLaw.Major,
            TaxRate = TaxLaw.High,
            Religion = ReligionLaw.StateReligion,
            Trade = TradeLaw.Embargo
        };

        var data = laws.Serialize();
        var newLaws = KingdomLaws.Deserialize(data);

        if (newLaws.Conscription != ConscriptionLaw.Major)
            return (false, "Conscription 不一致");
        if (newLaws.TaxRate != TaxLaw.High)
            return (false, "TaxRate 不一致");
        if (newLaws.Religion != ReligionLaw.StateReligion)
            return (false, "Religion 不一致");
        if (newLaws.Trade != TradeLaw.Embargo)
            return (false, "Trade 不一致");

        return (true, "");
    }

    private static (bool, string) Laws_Clone_CreatesCopy()
    {
        var laws = new KingdomLaws
        {
            Conscription = ConscriptionLaw.Aristocracy,
            TaxRate = TaxLaw.Low
        };

        var clone = laws.Clone();
        clone.Conscription = ConscriptionLaw.Standard;
        clone.TaxRate = TaxLaw.High;

        // 原始不应被修改
        if (laws.Conscription != ConscriptionLaw.Aristocracy)
            return (false, "克隆修改了原始对象");
        if (laws.TaxRate != TaxLaw.Low)
            return (false, "克隆修改了原始对象");

        return (true, "");
    }
}
