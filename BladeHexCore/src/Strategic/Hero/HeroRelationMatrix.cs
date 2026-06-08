using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Strategic.Hero;

public class HeroRelationMatrix
{
    private readonly Dictionary<string, int> _values = new();

    private string GetKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) < 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    public int Get(string heroAId, string heroBId)
    {
        if (heroAId == heroBId) return 0;
        var key = GetKey(heroAId, heroBId);
        return _values.TryGetValue(key, out var val) ? val : 0;
    }

    public void Set(string heroAId, string heroBId, int value)
    {
        if (heroAId == heroBId) return;
        var key = GetKey(heroAId, heroBId);
        int clamped = Math.Clamp(value, -100, 100);
        if (clamped == 0)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = clamped;
        }
    }

    public void Adjust(string heroAId, string heroBId, int delta)
    {
        if (heroAId == heroBId) return;
        int current = Get(heroAId, heroBId);
        Set(heroAId, heroBId, current + delta);
    }

    public IEnumerable<(string otherId, int value)> GetAllRelations(string heroId)
    {
        var result = new List<(string otherId, int value)>();
        foreach (var kvp in _values)
        {
            var parts = kvp.Key.Split('|');
            if (parts.Length == 2)
            {
                if (parts[0] == heroId)
                {
                    result.Add((parts[1], kvp.Value));
                }
                else if (parts[1] == heroId)
                {
                    result.Add((parts[0], kvp.Value));
                }
            }
        }
        return result;
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var kvp in _values)
        {
            if (kvp.Value != 0)
            {
                data[kvp.Key] = kvp.Value;
            }
        }
        return data;
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        _values.Clear();
        if (data == null) return;
        foreach (var key in data.Keys)
        {
            _values[key.AsString()] = data[key].AsInt32();
        }
    }
}
