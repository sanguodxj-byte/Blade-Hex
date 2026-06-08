// KingdomLawEffects.cs
// 王国法律数值效果
namespace BladeHex.Strategic.Kingdom;

/// <summary>
/// 王国法律数值效果计算
/// </summary>
public static class KingdomLawEffects
{
    /// <summary>获取收入乘数（基于税率法律）</summary>
    public static float GetIncomeMultiplier(TaxLaw taxRate)
    {
        return taxRate switch
        {
            TaxLaw.Low => 0.75f,
            TaxLaw.Medium => 1.0f,
            TaxLaw.High => 1.25f,
            _ => 1.0f
        };
    }

    /// <summary>获取忠诚度修正（基于税率法律）</summary>
    public static int GetLoyaltyModifier(TaxLaw taxRate)
    {
        return taxRate switch
        {
            TaxLaw.Low => 10,
            TaxLaw.Medium => 0,
            TaxLaw.High => -10,
            _ => 0
        };
    }

    /// <summary>获取繁荣度修正/月（基于税率法律）</summary>
    public static int GetProsperityModifier(TaxLaw taxRate)
    {
        return taxRate switch
        {
            TaxLaw.Low => 5,
            TaxLaw.Medium => 0,
            TaxLaw.High => -5,
            _ => 0
        };
    }

    /// <summary>获取招募乘数（基于征兵权法律）</summary>
    public static float GetRecruitMultiplier(ConscriptionLaw conscription)
    {
        return conscription switch
        {
            ConscriptionLaw.Standard => 1.0f,
            ConscriptionLaw.Major => 1.5f,
            ConscriptionLaw.Aristocracy => 0.7f,
            _ => 1.0f
        };
    }

    /// <summary>获取忠诚度修正（基于征兵权法律）</summary>
    public static int GetConscriptionLoyaltyModifier(ConscriptionLaw conscription)
    {
        return conscription switch
        {
            ConscriptionLaw.Standard => 0,
            ConscriptionLaw.Major => -10,
            ConscriptionLaw.Aristocracy => 5,
            _ => 0
        };
    }

    /// <summary>获取贸易乘数（基于贸易特权法律）</summary>
    public static float GetTradeMultiplier(TradeLaw trade)
    {
        return trade switch
        {
            TradeLaw.Free => 1.0f,
            TradeLaw.Protected => 1.2f,
            TradeLaw.Embargo => 0.5f,
            _ => 1.0f
        };
    }

    /// <summary>获取敌国经济影响（基于贸易特权法律，仅 Embargo 生效）</summary>
    public static float GetEnemyEconomyPenalty(TradeLaw trade)
    {
        return trade switch
        {
            TradeLaw.Embargo => 0.8f, // 敌国经济 -20%
            _ => 1.0f
        };
    }

    /// <summary>获取宗教关系修正</summary>
    public static int GetReligionRelationModifier(ReligionLaw playerLaw, bool isSameReligion)
    {
        return playerLaw switch
        {
            ReligionLaw.Tolerant => 0,
            ReligionLaw.StateReligion => isSameReligion ? 20 : -20,
            ReligionLaw.Persecution => isSameReligion ? 10 : -30,
            _ => 0
        };
    }
}
