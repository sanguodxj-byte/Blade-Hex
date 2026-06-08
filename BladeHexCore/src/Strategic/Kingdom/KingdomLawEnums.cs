// KingdomLawEnums.cs
// 王国法律枚举定义
namespace BladeHex.Strategic.Kingdom;

/// <summary>征兵权法律</summary>
public enum ConscriptionLaw
{
    /// <summary>常规征兵 — 基础招募率</summary>
    Standard,
    /// <summary>全民动员 — 招募率 ×1.5，但忠诚度 -10</summary>
    Major,
    /// <summary>贵族独占 — 招募率 ×0.7，但只产生 Lord 级单位</summary>
    Aristocracy
}

/// <summary>税率法律</summary>
public enum TaxLaw
{
    /// <summary>低税 (15%) — 收入 ×0.75，忠诚度 +10，繁荣度 +5/月</summary>
    Low,
    /// <summary>中税 (20%) — 基础收入</summary>
    Medium,
    /// <summary>高税 (25%) — 收入 ×1.25，忠诚度 -10，造反风险 +30%</summary>
    High
}

/// <summary>宗教宽容法律</summary>
public enum ReligionLaw
{
    /// <summary>宗教宽容 — 多元共存</summary>
    Tolerant,
    /// <summary>国教制 — 同教派 +20 关系，异教派 -20</summary>
    StateReligion,
    /// <summary>宗教迫害 — 同教派 +10，异教派 -30</summary>
    Persecution
}

/// <summary>贸易特权法律</summary>
public enum TradeLaw
{
    /// <summary>自由贸易 — 基础贸易</summary>
    Free,
    /// <summary>保护主义 — 本国市场收入 ×1.2，但与他国贸易 -10%</summary>
    Protected,
    /// <summary>禁运 — 经济收入 ×0.5，但战争中敌国经济 -20%</summary>
    Embargo
}
