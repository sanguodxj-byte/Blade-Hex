using BladeHex.Strategic;
using Godot;

namespace BladeHex.Strategic.Hero;

public class HeroData
{
    public string HeroId { get; set; } = "";                          // "hero_<faction>_<seq>"
    public string DisplayName { get; set; } = "";                     // 显示名 (含 Title + GivenName)
    public string FamilyName { get; set; } = "";                      // 家族姓
    public string FactionId { get; set; } = "";
    public OverworldPOI.LordPersonality Personality { get; set; } = OverworldPOI.LordPersonality.Balanced;
    public int Birthday { get; set; }                                // 出生年
    public string Background { get; set; } = "";                      // 背景描述
    public string BoundPoiName { get; set; } = "";                    // 原本绑定的 POI

    // 持久化的状态
    public CapturedState State { get; set; } = CapturedState.Free;
    public string CaptorHeroId { get; set; } = "";                    // 谁俘虏了我
    public string PrisonPoiName { get; set; } = "";                   // 关押地
    public int CapturedDay { get; set; } = 0;
    public int RansomGold { get; set; } = 0;                          // 赎金估值

    // 与 OverworldEntity 的弱关联
    public string CurrentEntityName { get; set; } = "";               // 当前对应的 entity

    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            { "hero_id", HeroId },
            { "display_name", DisplayName },
            { "family_name", FamilyName },
            { "faction_id", FactionId },
            { "personality", (int)Personality },
            { "birthday", Birthday },
            { "background", Background },
            { "bound_poi_name", BoundPoiName },
            { "state", (int)State },
            { "captor_hero_id", CaptorHeroId },
            { "prison_poi_name", PrisonPoiName },
            { "captured_day", CapturedDay },
            { "ransom_gold", RansomGold },
            { "current_entity_name", CurrentEntityName }
        };
    }

    public static HeroData Deserialize(Godot.Collections.Dictionary data)
    {
        if (data == null) return new HeroData();
        return new HeroData
        {
            HeroId = data.ContainsKey("hero_id") ? data["hero_id"].AsString() : "",
            DisplayName = data.ContainsKey("display_name") ? data["display_name"].AsString() : "",
            FamilyName = data.ContainsKey("family_name") ? data["family_name"].AsString() : "",
            FactionId = data.ContainsKey("faction_id") ? data["faction_id"].AsString() : "",
            Personality = data.ContainsKey("personality") ? (OverworldPOI.LordPersonality)data["personality"].AsInt32() : OverworldPOI.LordPersonality.Balanced,
            Birthday = data.ContainsKey("birthday") ? data["birthday"].AsInt32() : 0,
            Background = data.ContainsKey("background") ? data["background"].AsString() : "",
            BoundPoiName = data.ContainsKey("bound_poi_name") ? data["bound_poi_name"].AsString() : "",
            State = data.ContainsKey("state") ? (CapturedState)data["state"].AsInt32() : CapturedState.Free,
            CaptorHeroId = data.ContainsKey("captor_hero_id") ? data["captor_hero_id"].AsString() : "",
            PrisonPoiName = data.ContainsKey("prison_poi_name") ? data["prison_poi_name"].AsString() : "",
            CapturedDay = data.ContainsKey("captured_day") ? data["captured_day"].AsInt32() : 0,
            RansomGold = data.ContainsKey("ransom_gold") ? data["ransom_gold"].AsInt32() : 0,
            CurrentEntityName = data.ContainsKey("current_entity_name") ? data["current_entity_name"].AsString() : ""
        };
    }
}
