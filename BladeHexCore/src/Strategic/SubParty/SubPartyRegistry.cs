using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.SubParty;

public class SubPartyRegistry
{
    private readonly Dictionary<string, SubParty> _subParties = new();
    private int _nextSubPartySeq = 1;

    public SubParty Create(string leaderName, Vector2 pos)
    {
        var subPartyId = $"subparty_{_nextSubPartySeq++}";
        var subParty = new SubParty
        {
            SubPartyId = subPartyId,
            LeaderUnitName = leaderName,
            Position = pos,
            Task = SubPartyTask.Idle
        };
        _subParties[subPartyId] = subParty;
        return subParty;
    }

    public SubParty? Get(string subPartyId)
    {
        if (string.IsNullOrEmpty(subPartyId)) return null;
        return _subParties.TryGetValue(subPartyId, out var sp) ? sp : null;
    }

    public List<SubParty> GetAll()
    {
        return _subParties.Values.ToList();
    }

    public void Remove(string subPartyId)
    {
        if (string.IsNullOrEmpty(subPartyId)) return;
        _subParties.Remove(subPartyId);
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        data["next_subparty_seq"] = _nextSubPartySeq;

        var listData = new Godot.Collections.Array();
        foreach (var sp in _subParties.Values)
        {
            listData.Add(sp.Serialize());
        }
        data["subparties"] = listData;
        return data;
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        _subParties.Clear();
        if (data == null) return;

        if (data.ContainsKey("next_subparty_seq"))
        {
            _nextSubPartySeq = data["next_subparty_seq"].AsInt32();
        }

        if (data.ContainsKey("subparties"))
        {
            var listData = data["subparties"].AsGodotArray();
            foreach (var item in listData)
            {
                var spDict = item.AsGodotDictionary();
                var sp = SubParty.Deserialize(spDict);
                _subParties[sp.SubPartyId] = sp;
            }
        }
    }
}
