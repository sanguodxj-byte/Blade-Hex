using System.Collections.Generic;

namespace BladeHex.Combat.Buff;

/// <summary>
/// 伤害结算明细 — 记录多乘区管线每一步的值与来源,供 UI 结算面板显示。
/// </summary>
public class DamageBreakdown
{
    // ============================================================
    // 阶段 1: 基础伤害
    // ============================================================
    public int BaseDamage;
    public List<(string source, int value)> BaseContributions = new();

    // ============================================================
    // 阶段 2: 增伤区(Increased)
    // ============================================================
    public float TotalIncreasedPercent;
    public List<(string source, float percent)> IncreasedSources = new();
    public int AfterIncreased;

    // ============================================================
    // 阶段 3: 更多伤害区(More)
    // ============================================================
    public List<(string source, float multiplier)> MoreMultipliers = new();
    public int AfterMore;

    // ============================================================
    // 阶段 4: 最终修正(FinalMult)
    // ============================================================
    public List<(string source, float multiplier)> FinalMultipliers = new();
    public int FinalDamage;

    // ============================================================
    // 阶段 5: 穿甲 / DR
    // ============================================================
    public int DrAbsorbed;
    public int ShieldAbsorbed;
    public int HpDamage;

    // ============================================================
    // 元信息
    // ============================================================
    public bool IsCritical;
    public bool IsMiss;
    public string DamageType = "";
    public string AttackerName = "";
    public string DefenderName = "";

    /// <summary>生成可读的结算文本(供日志/tooltip)</summary>
    public string ToSummaryText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"基础伤害: {BaseDamage}");
        foreach (var (src, val) in BaseContributions)
            sb.AppendLine($"  {src}: +{val}");

        if (TotalIncreasedPercent != 0)
            sb.AppendLine($"增伤: ×{1 + TotalIncreasedPercent:F2} → {AfterIncreased}");

        foreach (var (src, mult) in MoreMultipliers)
            sb.AppendLine($"更多: {src} ×{mult:F2}");
        if (MoreMultipliers.Count > 0)
            sb.AppendLine($"  → {AfterMore}");

        foreach (var (src, mult) in FinalMultipliers)
            sb.AppendLine($"最终: {src} ×{mult:F2}");

        sb.AppendLine($"最终伤害: {FinalDamage}");
        if (DrAbsorbed > 0) sb.AppendLine($"DR 吸收: {DrAbsorbed}");
        if (ShieldAbsorbed > 0) sb.AppendLine($"盾牌吸收: {ShieldAbsorbed}");
        sb.AppendLine($"实际 HP 伤害: {HpDamage}");
        return sb.ToString();
    }
}
