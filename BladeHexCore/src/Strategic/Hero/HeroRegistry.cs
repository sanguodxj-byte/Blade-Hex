using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Strategic.Hero;

public class HeroRegistry
{
    private static readonly Random _rng = new();
    private readonly Dictionary<string, HeroData> _heroes = new();
    private readonly Dictionary<string, int> _deadHeroes = new();
    private int _nextHeroSeq = 1;

    public IEnumerable<HeroData> AllHeroes => _heroes.Values;

    public HeroData Create(string factionId, string displayName, string familyName, OverworldPOI.LordPersonality personality, int currentDay, HeroRelationMatrix? relations = null)
    {
        var heroId = $"hero_{factionId}_{_nextHeroSeq++}";
        var hero = new HeroData
        {
            HeroId = heroId,
            DisplayName = displayName,
            FamilyName = familyName,
            FactionId = factionId,
            Personality = personality,
            Birthday = 2000 - (20 + _rng.Next(30)), // 随机 20 到 50 岁
            Background = $"来自 {factionId} 势力的 {familyName} 家族的著名领主。",
            BoundPoiName = "",
            State = CapturedState.Free,
            CurrentEntityName = displayName
        };
        _heroes[heroId] = hero;

        if (relations != null)
        {
            foreach (var other in _heroes.Values)
            {
                if (other.HeroId != heroId && other.FactionId == factionId && other.FamilyName == familyName)
                {
                    relations.Set(heroId, other.HeroId, 20);
                }
            }
        }

        return hero;
    }

    public HeroData? Get(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return null;
        return _heroes.TryGetValue(heroId, out var hero) ? hero : null;
    }

    public List<HeroData> GetByFaction(string factionId)
    {
        return _heroes.Values.Where(h => h.FactionId == factionId).ToList();
    }

    public List<HeroData> GetByFamily(string familyName, string factionId)
    {
        return _heroes.Values.Where(h => h.FamilyName == familyName && h.FactionId == factionId).ToList();
    }

    public void Remove(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        _heroes.Remove(heroId);
        _deadHeroes.Remove(heroId);
    }

    public void MarkDead(string heroId, int day)
    {
        if (string.IsNullOrEmpty(heroId)) return;
        _deadHeroes[heroId] = day;
        if (_heroes.TryGetValue(heroId, out var hero))
        {
            hero.CurrentEntityName = "";
        }
    }

    public bool IsDead(string heroId)
    {
        return _deadHeroes.ContainsKey(heroId);
    }

    public int GetDeathDay(string heroId)
    {
        return _deadHeroes.TryGetValue(heroId, out var day) ? day : -1;
    }

    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        data["next_hero_seq"] = _nextHeroSeq;

        var heroesData = new Godot.Collections.Dictionary();
        foreach (var kvp in _heroes)
        {
            heroesData[kvp.Key] = kvp.Value.Serialize();
        }
        data["heroes"] = heroesData;

        var deadData = new Godot.Collections.Dictionary();
        foreach (var kvp in _deadHeroes)
        {
            deadData[kvp.Key] = kvp.Value;
        }
        data["dead_heroes"] = deadData;

        return data;
    }

    public void PopulateFromEntities(List<OverworldEntity> entities, int currentDay, HeroRelationMatrix? relations = null)
    {
        foreach (var entity in entities)
        {
            if (entity.IsNamedCharacter && string.IsNullOrEmpty(entity.HeroId))
            {
                var hero = Create(entity.Faction, entity.EntityName, entity.FamilyName, entity.LordPersonalityValue, currentDay, relations);
                entity.HeroId = hero.HeroId;
                hero.CurrentEntityName = entity.EntityName;
                hero.BoundPoiName = entity.BoundPoiName;
            }
        }
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        _heroes.Clear();
        _deadHeroes.Clear();
        if (data == null) return;

        if (data.ContainsKey("next_hero_seq"))
        {
            _nextHeroSeq = data["next_hero_seq"].AsInt32();
        }

        if (data.ContainsKey("heroes"))
        {
            var heroesData = data["heroes"].AsGodotDictionary();
            foreach (var key in heroesData.Keys)
            {
                var heroId = key.AsString();
                var heroDict = heroesData[key].AsGodotDictionary();
                _heroes[heroId] = HeroData.Deserialize(heroDict);
            }
        }

        if (data.ContainsKey("dead_heroes"))
        {
            var deadData = data["dead_heroes"].AsGodotDictionary();
            foreach (var key in deadData.Keys)
            {
                _deadHeroes[key.AsString()] = deadData[key].AsInt32();
            }
        }
    }
}
