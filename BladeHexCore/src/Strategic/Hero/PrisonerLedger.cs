using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.Hero;

public class PrisonerLedger
{
    private readonly Dictionary<string, List<string>> _poiPrisoners = new();

    public void Imprison(string heroId, string poiName)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        Release(heroId); // 先从其他地方释放

        if (string.IsNullOrEmpty(poiName)) return;

        if (!_poiPrisoners.TryGetValue(poiName, out var list))
        {
            list = new List<string>();
            _poiPrisoners[poiName] = list;
        }
        if (!list.Contains(heroId))
        {
            list.Add(heroId);
        }
    }

    public void Release(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        foreach (var kvp in _poiPrisoners.Values)
        {
            if (kvp.Contains(heroId))
            {
                kvp.Remove(heroId);
            }
        }
    }

    public List<string> GetPrisonersAt(string poiName)
    {
        if (string.IsNullOrEmpty(poiName)) return new List<string>();
        return _poiPrisoners.TryGetValue(poiName, out var list) ? new List<string>(list) : new List<string>();
    }

    public List<string> GetAllPrisoners()
    {
        var all = new List<string>();
        foreach (var list in _poiPrisoners.Values)
        {
            all.AddRange(list);
        }
        return all.Distinct().ToList();
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var kvp in _poiPrisoners)
        {
            if (kvp.Value.Count > 0)
            {
                var arr = new Godot.Collections.Array();
                foreach (var id in kvp.Value)
                {
                    arr.Add(id);
                }
                data[kvp.Key] = arr;
            }
        }
        return data;
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        _poiPrisoners.Clear();
        if (data == null) return;
        foreach (var key in data.Keys)
        {
            var poiName = key.AsString();
            var arr = data[key].AsGodotArray();
            var list = new List<string>();
            foreach (var item in arr)
            {
                list.Add(item.AsString());
            }
            _poiPrisoners[poiName] = list;
        }
    }
}
