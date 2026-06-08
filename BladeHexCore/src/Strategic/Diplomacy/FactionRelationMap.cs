using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Strategic.Diplomacy;

/// <summary>
/// 势力关系映射表 — 管理所有势力之间的关系数值、停战期（Truce）及宣战/议和冷却
/// </summary>
public class FactionRelationMap
{
    public Dictionary<string, int> RelationsInternal { get; } = new();
    private readonly Dictionary<string, int> _truceExpiryDays = new();
    private readonly Dictionary<string, int> _declareWarCooldowns = new();
    private readonly Dictionary<string, int> _proposePeaceCooldowns = new();

    private static string GetNormalizeKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) < 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    public int GetRelation(string factionA, string factionB)
    {
        if (factionA == factionB) return 100;
        var key = GetNormalizeKey(factionA, factionB);
        return RelationsInternal.TryGetValue(key, out var val) ? val : 0;
    }

    public void SetRelation(string factionA, string factionB, int value)
    {
        if (factionA == factionB) return;
        var key = GetNormalizeKey(factionA, factionB);
        RelationsInternal[key] = Math.Clamp(value, -100, 100);
    }

    public void AdjustRelation(string factionA, string factionB, int delta)
    {
        if (factionA == factionB) return;
        int current = GetRelation(factionA, factionB);
        SetRelation(factionA, factionB, current + delta);
    }

    public void SetTruce(string factionA, string factionB, int durationDays, int currentDay)
    {
        if (factionA == factionB) return;
        var key = GetNormalizeKey(factionA, factionB);
        _truceExpiryDays[key] = currentDay + durationDays;
    }

    public bool IsInTruce(string factionA, string factionB, int currentDay)
    {
        if (factionA == factionB) return false;
        var key = GetNormalizeKey(factionA, factionB);
        if (_truceExpiryDays.TryGetValue(key, out int expiryDay))
        {
            return currentDay < expiryDay;
        }
        return false;
    }

    public int GetTruceRemainingDays(string factionA, string factionB, int currentDay)
    {
        if (factionA == factionB) return 0;
        var key = GetNormalizeKey(factionA, factionB);
        if (_truceExpiryDays.TryGetValue(key, out int expiryDay))
        {
            return Math.Max(0, expiryDay - currentDay);
        }
        return 0;
    }

    public void ClearTruce(string factionA, string factionB)
    {
        var key = GetNormalizeKey(factionA, factionB);
        _truceExpiryDays.Remove(key);
    }

    public void SetDeclareWarCooldown(string fromFaction, string toFaction, int durationDays, int currentDay)
    {
        if (fromFaction == toFaction) return;
        var key = $"{fromFaction}->{toFaction}";
        _declareWarCooldowns[key] = currentDay + durationDays;
    }

    public bool IsDeclareWarInCooldown(string fromFaction, string toFaction, int currentDay)
    {
        if (fromFaction == toFaction) return false;
        var key = $"{fromFaction}->{toFaction}";
        if (_declareWarCooldowns.TryGetValue(key, out int expiryDay))
        {
            return currentDay < expiryDay;
        }
        return false;
    }

    public void SetProposePeaceCooldown(string fromFaction, string toFaction, int durationDays, int currentDay)
    {
        if (fromFaction == toFaction) return;
        var key = $"{fromFaction}->{toFaction}";
        _proposePeaceCooldowns[key] = currentDay + durationDays;
    }

    public bool IsProposePeaceInCooldown(string fromFaction, string toFaction, int currentDay)
    {
        if (fromFaction == toFaction) return false;
        var key = $"{fromFaction}->{toFaction}";
        if (_proposePeaceCooldowns.TryGetValue(key, out int expiryDay))
        {
            return currentDay < expiryDay;
        }
        return false;
    }

    public void TickTruces(int currentDay)
    {
        var toRemove = new List<string>();
        foreach (var kvp in _truceExpiryDays)
        {
            if (currentDay >= kvp.Value)
            {
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _truceExpiryDays.Remove(key);
        }
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        
        var rels = new Godot.Collections.Dictionary();
        foreach (var kvp in RelationsInternal) rels[kvp.Key] = kvp.Value;
        data["relations"] = rels;

        var truces = new Godot.Collections.Dictionary();
        foreach (var kvp in _truceExpiryDays) truces[kvp.Key] = kvp.Value;
        data["truces"] = truces;

        var warCds = new Godot.Collections.Dictionary();
        foreach (var kvp in _declareWarCooldowns) warCds[kvp.Key] = kvp.Value;
        data["war_cds"] = warCds;

        var peaceCds = new Godot.Collections.Dictionary();
        foreach (var kvp in _proposePeaceCooldowns) peaceCds[kvp.Key] = kvp.Value;
        data["peace_cds"] = peaceCds;

        return data;
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        if (data == null) return;
        
        RelationsInternal.Clear();
        if (data.ContainsKey("relations"))
        {
            var dict = data["relations"].AsGodotDictionary();
            foreach (var key in dict.Keys)
                RelationsInternal[key.AsString()] = dict[key].AsInt32();
        }

        _truceExpiryDays.Clear();
        if (data.ContainsKey("truces"))
        {
            var dict = data["truces"].AsGodotDictionary();
            foreach (var key in dict.Keys)
                _truceExpiryDays[key.AsString()] = dict[key].AsInt32();
        }

        _declareWarCooldowns.Clear();
        if (data.ContainsKey("war_cds"))
        {
            var dict = data["war_cds"].AsGodotDictionary();
            foreach (var key in dict.Keys)
                _declareWarCooldowns[key.AsString()] = dict[key].AsInt32();
        }

        _proposePeaceCooldowns.Clear();
        if (data.ContainsKey("peace_cds"))
        {
            var dict = data["peace_cds"].AsGodotDictionary();
            foreach (var key in dict.Keys)
                _proposePeaceCooldowns[key.AsString()] = dict[key].AsInt32();
        }
    }
}
