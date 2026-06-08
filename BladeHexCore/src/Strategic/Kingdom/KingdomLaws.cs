// KingdomLaws.cs
// 王国法律数据模型
using Godot;

namespace BladeHex.Strategic.Kingdom;

/// <summary>
/// 王国法律 — 影响经济、招募、外交
/// </summary>
public class KingdomLaws
{
    /// <summary>征兵权</summary>
    public ConscriptionLaw Conscription { get; set; } = ConscriptionLaw.Standard;

    /// <summary>税率</summary>
    public TaxLaw TaxRate { get; set; } = TaxLaw.Medium;

    /// <summary>宗教宽容</summary>
    public ReligionLaw Religion { get; set; } = ReligionLaw.Tolerant;

    /// <summary>贸易特权</summary>
    public TradeLaw Trade { get; set; } = TradeLaw.Free;

    /// <summary>序列化</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            { "conscription", (int)Conscription },
            { "tax_rate", (int)TaxRate },
            { "religion", (int)Religion },
            { "trade", (int)Trade }
        };
    }

    /// <summary>反序列化</summary>
    public static KingdomLaws Deserialize(Godot.Collections.Dictionary data)
    {
        return new KingdomLaws
        {
            Conscription = data.ContainsKey("conscription") ? (ConscriptionLaw)data["conscription"].AsInt32() : ConscriptionLaw.Standard,
            TaxRate = data.ContainsKey("tax_rate") ? (TaxLaw)data["tax_rate"].AsInt32() : TaxLaw.Medium,
            Religion = data.ContainsKey("religion") ? (ReligionLaw)data["religion"].AsInt32() : ReligionLaw.Tolerant,
            Trade = data.ContainsKey("trade") ? (TradeLaw)data["trade"].AsInt32() : TradeLaw.Free
        };
    }

    /// <summary>深拷贝</summary>
    public KingdomLaws Clone()
    {
        return new KingdomLaws
        {
            Conscription = this.Conscription,
            TaxRate = this.TaxRate,
            Religion = this.Religion,
            Trade = this.Trade
        };
    }
}
