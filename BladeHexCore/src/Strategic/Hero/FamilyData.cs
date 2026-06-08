// FamilyData.cs
// 家族数据模型 — 管理同姓领主组成的家族
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic.Hero;

/// <summary>
/// 家族数据 — 记录同姓领主组成的家族
/// </summary>
public class FamilyData
{
    /// <summary>家族姓氏</summary>
    public string FamilyName { get; set; } = "";

    /// <summary>所属势力 ID</summary>
    public string FactionId { get; set; } = "";

    /// <summary>家族首领 HeroId</summary>
    public string PatriarchHeroId { get; set; } = "";

    /// <summary>家族成员 HeroId 列表</summary>
    public List<string> MemberHeroIds { get; set; } = new();

    /// <summary>家族创立天数</summary>
    public int FoundedDay { get; set; } = 1;

    /// <summary>序列化</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        var membersArr = new Godot.Collections.Array();
        foreach (var id in MemberHeroIds)
            membersArr.Add(id);

        return new Godot.Collections.Dictionary
        {
            { "family_name", FamilyName },
            { "faction_id", FactionId },
            { "patriarch_hero_id", PatriarchHeroId },
            { "member_hero_ids", membersArr },
            { "founded_day", FoundedDay }
        };
    }

    /// <summary>反序列化</summary>
    public static FamilyData Deserialize(Godot.Collections.Dictionary data)
    {
        var family = new FamilyData
        {
            FamilyName = data.ContainsKey("family_name") ? data["family_name"].AsString() : "",
            FactionId = data.ContainsKey("faction_id") ? data["faction_id"].AsString() : "",
            PatriarchHeroId = data.ContainsKey("patriarch_hero_id") ? data["patriarch_hero_id"].AsString() : "",
            FoundedDay = data.ContainsKey("founded_day") ? data["founded_day"].AsInt32() : 1
        };

        if (data.ContainsKey("member_hero_ids"))
        {
            var arr = (Godot.Collections.Array)data["member_hero_ids"];
            foreach (var item in arr)
                family.MemberHeroIds.Add(item.AsString());
        }

        return family;
    }
}
