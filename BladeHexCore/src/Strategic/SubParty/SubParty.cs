using System.Collections.Generic;
using Godot;
using BladeHex.Data;

namespace BladeHex.Strategic.SubParty;

public enum SubPartyTask
{
    Idle,
    EscortCaravan,      // 护送商队
    PatrolRegion,       // 巡逻指定 POI 周围
    HuntBandits,        // 剿匪
    Garrison,           // 驻防一个 POI
}

public class SubParty
{
    public string SubPartyId { get; set; } = "";                  // "subparty_<seq>"
    public string LeaderUnitName { get; set; } = "";              // Companion 的 UnitData.UnitName
    public List<UnitData> Members { get; set; } = new();          // 跟随的兵
    public Vector2 Position { get; set; }
    public SubPartyTask Task { get; set; } = SubPartyTask.Idle;
    public string TargetPoiName { get; set; } = "";               // 任务目标
    public int TaskStartDay { get; set; } = 0;
    public OverworldEntity? OverworldEntityRef { get; set; }     // Companion 在大地图的实体表示

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary
        {
            { "subparty_id", SubPartyId },
            { "leader_unit_name", LeaderUnitName },
            { "position_x", Position.X },
            { "position_y", Position.Y },
            { "task", (int)Task },
            { "target_poi_name", TargetPoiName },
            { "task_start_day", TaskStartDay }
        };

        var membersArray = new Godot.Collections.Array();
        foreach (var m in Members)
        {
            if (m == null) continue;
            var memberDict = new Godot.Collections.Dictionary
            {
                ["unit_name"] = m.UnitName,
                ["level"] = m.Level,
                ["str"] = m.Str,
                ["dex"] = m.Dex,
                ["con"] = m.Con,
                ["intel"] = m.Intel,
                ["wis"] = m.Wis,
                ["cha"] = m.Cha,
                ["base_max_hp"] = m.BaseMaxHp,
                ["base_ac"] = m.BaseAc,
                ["current_hp"] = m.Runtime.CurrentHp > 0 ? m.Runtime.CurrentHp : m.BaseMaxHp,
                ["xp"] = m.Xp,
                ["race_id"] = m.Race != null ? (int)m.Race.raceId : 0,
                ["is_leader"] = false,
                ["portrait_id"] = m.PortraitId,
                ["sprite_frames_id"] = m.SpriteFramesId,
                ["is_wounded"] = m.IsWounded,
            };
            membersArray.Add(memberDict);
        }
        data["members"] = membersArray;

        return data;
    }

    public static SubParty Deserialize(Godot.Collections.Dictionary data)
    {
        if (data == null) return new SubParty();

        var subParty = new SubParty
        {
            SubPartyId = data.ContainsKey("subparty_id") ? data["subparty_id"].AsString() : "",
            LeaderUnitName = data.ContainsKey("leader_unit_name") ? data["leader_unit_name"].AsString() : "",
            Position = new Vector2(
                data.ContainsKey("position_x") ? data["position_x"].AsSingle() : 0f,
                data.ContainsKey("position_y") ? data["position_y"].AsSingle() : 0f
            ),
            Task = data.ContainsKey("task") ? (SubPartyTask)data["task"].AsInt32() : SubPartyTask.Idle,
            TargetPoiName = data.ContainsKey("target_poi_name") ? data["target_poi_name"].AsString() : "",
            TaskStartDay = data.ContainsKey("task_start_day") ? data["task_start_day"].AsInt32() : 0
        };

        if (data.ContainsKey("members"))
        {
            var membersArray = data["members"].AsGodotArray();
            foreach (var memberVar in membersArray)
            {
                var memberDict = memberVar.AsGodotDictionary();
                var unit = new UnitData();
                unit.UnitName = memberDict.ContainsKey("unit_name") ? memberDict["unit_name"].AsString() : "未知";
                unit.Level = memberDict.ContainsKey("level") ? memberDict["level"].AsInt32() : 1;
                unit.Str = memberDict.ContainsKey("str") ? memberDict["str"].AsInt32() : 10;
                unit.Dex = memberDict.ContainsKey("dex") ? memberDict["dex"].AsInt32() : 10;
                unit.Con = memberDict.ContainsKey("con") ? memberDict["con"].AsInt32() : 10;
                unit.Intel = memberDict.ContainsKey("intel") ? memberDict["intel"].AsInt32() : 10;
                unit.Wis = memberDict.ContainsKey("wis") ? memberDict["wis"].AsInt32() : 10;
                unit.Cha = memberDict.ContainsKey("cha") ? memberDict["cha"].AsInt32() : 10;
                unit.BaseMaxHp = memberDict.ContainsKey("base_max_hp") ? memberDict["base_max_hp"].AsInt32() : 10;
                unit.BaseAc = memberDict.ContainsKey("base_ac") ? memberDict["base_ac"].AsInt32() : 8;
                unit.Xp = memberDict.ContainsKey("xp") ? memberDict["xp"].AsInt32() : 0;
                unit.PortraitId = memberDict.ContainsKey("portrait_id") ? memberDict["portrait_id"].AsString() : "";
                unit.SpriteFramesId = memberDict.ContainsKey("sprite_frames_id") ? memberDict["sprite_frames_id"].AsString() : "";
                unit.IsWounded = memberDict.ContainsKey("is_wounded") && memberDict["is_wounded"].AsBool();

                int raceId = memberDict.ContainsKey("race_id") ? memberDict["race_id"].AsInt32() : 0;
                unit.Race = RaceData.GetRaceById((RaceData.Race)raceId);

                int currentHp = memberDict.ContainsKey("current_hp") ? memberDict["current_hp"].AsInt32() : unit.BaseMaxHp;
                unit.Runtime.CurrentHp = currentHp;

                subParty.Members.Add(unit);
            }
        }

        return subParty;
    }
}
