using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.Army;

public class ArmyRegistry
{
    private readonly Dictionary<string, Army> _armies = new();
    private readonly Random _random = new();

    public Army Create(OverworldEntity marshal, string targetPoi, int currentDay)
    {
        string faction = marshal.Faction;
        string armyId = $"army_{faction}_{currentDay}_{_random.Next(1000, 9999)}";
        var army = new Army
        {
            ArmyId = armyId,
            Faction = faction,
            Marshal = marshal,
            TargetPoiName = targetPoi,
            State = ArmyState.Forming,
            FormedDay = currentDay,
            RallyPoint = marshal.Position
        };
        marshal.IsMarshal = true;
        marshal.ArmyId = armyId;
        if (!army.Members.Contains(marshal))
        {
            army.Members.Add(marshal);
        }
        _armies[armyId] = army;
        return army;
    }

    public Army? GetByLord(OverworldEntity lord)
    {
        if (lord == null) return null;
        if (!string.IsNullOrEmpty(lord.ArmyId))
        {
            if (_armies.TryGetValue(lord.ArmyId, out var army))
            {
                return army;
            }
        }
        // 兜底扫描
        return _armies.Values.FirstOrDefault(a => a.Members.Contains(lord));
    }

    public Army? Get(string armyId)
    {
        if (string.IsNullOrEmpty(armyId)) return null;
        _armies.TryGetValue(armyId, out var army);
        return army;
    }

    public IEnumerable<Army> All() => _armies.Values;

    public IEnumerable<Army> ByFaction(string factionId)
        => _armies.Values.Where(a => a.Faction == factionId);

    public void Remove(string armyId)
    {
        if (string.IsNullOrEmpty(armyId)) return;
        if (_armies.TryGetValue(armyId, out var army))
        {
            foreach (var m in army.Members)
            {
                m.ArmyId = "";
                m.IsMarshal = false;
            }
            _armies.Remove(armyId);
        }
    }

    public void Clear()
    {
        foreach (var army in _armies.Values)
        {
            foreach (var m in army.Members)
            {
                m.ArmyId = "";
                m.IsMarshal = false;
            }
        }
        _armies.Clear();
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        var list = new Godot.Collections.Array();
        foreach (var kvp in _armies)
        {
            var armyData = new Godot.Collections.Dictionary
            {
                { "army_id", kvp.Value.ArmyId },
                { "faction", kvp.Value.Faction },
                { "marshal_name", kvp.Value.Marshal?.EntityName ?? "" },
                { "target_poi", kvp.Value.TargetPoiName },
                { "state", (int)kvp.Value.State },
                { "formed_day", kvp.Value.FormedDay },
                { "rally_x", kvp.Value.RallyPoint.X },
                { "rally_y", kvp.Value.RallyPoint.Y }
            };
            var membersArray = new Godot.Collections.Array();
            foreach (var m in kvp.Value.Members)
            {
                membersArray.Add(m.EntityName);
            }
            armyData.Add("members", membersArray);
            list.Add(armyData);
        }
        data.Add("armies", list);
        return data;
    }

    public void Deserialize(Godot.Collections.Dictionary data, List<OverworldEntity> entities)
    {
        _armies.Clear();
        if (data == null || !data.ContainsKey("armies")) return;
        var list = data["armies"].AsGodotArray();
        foreach (var item in list)
        {
            var armyData = item.AsGodotDictionary();
            var army = new Army
            {
                ArmyId = armyData["army_id"].AsString(),
                Faction = armyData["faction"].AsString(),
                TargetPoiName = armyData["target_poi"].AsString(),
                State = (ArmyState)armyData["state"].AsInt32(),
                FormedDay = armyData["formed_day"].AsInt32(),
                RallyPoint = new Vector2(
                    armyData.ContainsKey("rally_x") ? armyData["rally_x"].AsSingle() : 0f,
                    armyData.ContainsKey("rally_y") ? armyData["rally_y"].AsSingle() : 0f
                )
            };
            string marshalName = armyData["marshal_name"].AsString();
            army.Marshal = entities.FirstOrDefault(e => e.EntityName == marshalName);
            if (army.Marshal != null)
            {
                army.Marshal.IsMarshal = true;
                army.Marshal.ArmyId = army.ArmyId;
            }
            if (armyData.ContainsKey("members"))
            {
                var membersArray = armyData["members"].AsGodotArray();
                foreach (var memberNameVal in membersArray)
                {
                    string mName = memberNameVal.AsString();
                    var member = entities.FirstOrDefault(e => e.EntityName == mName);
                    if (member != null)
                    {
                        if (!army.Members.Contains(member))
                        {
                            army.Members.Add(member);
                        }
                        member.ArmyId = army.ArmyId;
                    }
                }
            }
            _armies[army.ArmyId] = army;
        }
    }
}
