// FamilyRegistry.cs
// 家族注册表 — 管理所有家族的生命周期
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic.Hero;

/// <summary>
/// 家族注册表 — 管理所有家族的创建、查询和继承
/// </summary>
public class FamilyRegistry
{
    private readonly Dictionary<string, FamilyData> _families = new();

    /// <summary>获取所有家族</summary>
    public IEnumerable<FamilyData> AllFamilies => _families.Values;

    /// <summary>获取家族数量</summary>
    public int Count => _families.Count;

    /// <summary>创建新家族</summary>
    public FamilyData Create(string familyName, string factionId, string patriarchHeroId, List<string> memberHeroIds, int foundedDay)
    {
        var family = new FamilyData
        {
            FamilyName = familyName,
            FactionId = factionId,
            PatriarchHeroId = patriarchHeroId,
            MemberHeroIds = new List<string>(memberHeroIds),
            FoundedDay = foundedDay
        };
        _families[familyName] = family;
        return family;
    }

    /// <summary>获取家族（按姓氏）</summary>
    public FamilyData? Get(string familyName)
    {
        return _families.TryGetValue(familyName, out var family) ? family : null;
    }

    /// <summary>获取某势力的所有家族</summary>
    public List<FamilyData> GetByFaction(string factionId)
    {
        return _families.Values.Where(f => f.FactionId == factionId).ToList();
    }

    /// <summary>添加成员到家族</summary>
    public void AddMember(string familyName, string heroId)
    {
        if (_families.TryGetValue(familyName, out var family))
        {
            if (!family.MemberHeroIds.Contains(heroId))
                family.MemberHeroIds.Add(heroId);
        }
    }

    /// <summary>从家族移除成员</summary>
    public void RemoveMember(string familyName, string heroId)
    {
        if (_families.TryGetValue(familyName, out var family))
        {
            family.MemberHeroIds.Remove(heroId);

            // 如果家族首领被移除，自动晋升新首领
            if (family.PatriarchHeroId == heroId)
            {
                family.PatriarchHeroId = "";
            }
        }
    }

    /// <summary>
    /// 家族首领死亡时的自动晋升逻辑
    /// </summary>
    public void OnPatriarchDied(string heroId, HeroRegistry heroRegistry, int currentDay)
    {
        var family = _families.Values.FirstOrDefault(f => f.PatriarchHeroId == heroId);
        if (family == null) return;

        // 从存活成员中选择最高等级者晋升
        var aliveMembers = family.MemberHeroIds
            .Where(id => id != heroId)
            .Select(id => heroRegistry.Get(id))
            .Where(h => h != null && h.State == CapturedState.Free)
            .ToList();

        if (aliveMembers.Count == 0)
        {
            // 家族无存活成员，移除家族
            _families.Remove(family.FamilyName);
            GD.Print($"[FamilyRegistry] 家族 {family.FamilyName} 因无存活成员而消亡");
            return;
        }

        // 选择最高等级者（简化：用 CombatPower 代理）
        var newPatriarch = aliveMembers.OrderByDescending(h => h!.Birthday).First(); // 年龄最小 = 等级最高（近似）
        family.PatriarchHeroId = newPatriarch!.HeroId;
        GD.Print($"[FamilyRegistry] 家族 {family.FamilyName} 新首领: {newPatriarch.DisplayName}");
    }

    /// <summary>
    /// 检查英雄是否为某家族首领
    /// </summary>
    public bool IsPatriarch(string heroId)
    {
        return _families.Values.Any(f => f.PatriarchHeroId == heroId);
    }

    /// <summary>序列化</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        var dict = new Godot.Collections.Dictionary();
        foreach (var (name, family) in _families)
            dict[name] = family.Serialize();
        return dict;
    }

    /// <summary>反序列化</summary>
    public static FamilyRegistry Deserialize(Godot.Collections.Dictionary data)
    {
        var registry = new FamilyRegistry();
        foreach (var key in data.Keys)
        {
            var familyData = (Godot.Collections.Dictionary)data[key];
            var family = FamilyData.Deserialize(familyData);
            registry._families[family.FamilyName] = family;
        }
        return registry;
    }
}
