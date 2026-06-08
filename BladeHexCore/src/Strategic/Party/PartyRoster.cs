// PartyRoster.cs
// 雇佣兵团队伍名册 — 骑砍/战场兄弟核心数据模型
//
// 职责：
// - 持有玩家队伍所有成员（含队长）
// - 提供增删查改接口
// - 序列化/反序列化（存档用）
// - 战斗时提供部署单位列表
// - 战斗后接收伤亡回写
//
// 设计约束：
// - Core 层，不持渲染类型
// - UnitData 是 Godot Resource，可直接序列化
// - 队长（玩家本人）永远在 Members[0]
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Data;

/// <summary>
/// 雇佣兵团队伍名册
/// </summary>
[GlobalClass]
public partial class PartyRoster : Resource
{
    // ========================================
    // 数据
    // ========================================

    /// <summary>队伍成员列表（含队长，队长始终在 index 0）</summary>
    public List<UnitData> Members { get; private set; } = new();

    /// <summary>队伍上限（初始 6，可通过升级/声望提升）</summary>
    public int Capacity { get; set; } = 6;

    /// <summary>队长的 UnitName（用于标识，不可被移除）</summary>
    public string LeaderName { get; private set; } = "";

    // ========================================
    // 初始化
    // ========================================

    /// <summary>设置队长（必须是第一个加入的成员）</summary>
    public void SetLeader(UnitData leader)
    {
        LeaderName = leader.UnitName;
        if (Members.Count == 0)
            Members.Add(leader);
        else
            Members[0] = leader;
    }

    // ========================================
    // 增删查
    // ========================================

    /// <summary>当前人数</summary>
    public int Count => Members.Count;

    /// <summary>是否满员</summary>
    public bool IsFull => Members.Count >= Capacity;

    /// <summary>是否为空（队长也没有 = 未初始化）</summary>
    public bool IsEmpty => Members.Count == 0;

    /// <summary>
    /// 招募/加入一个新成员。满员时返回 false。
    /// </summary>
    public bool Add(UnitData unit)
    {
        if (IsFull) return false;
        if (Members.Contains(unit)) return false;
        Members.Add(unit);
        return true;
    }

    /// <summary>
    /// 移除一个成员。队长不可被移除（返回 false）。
    /// </summary>
    public bool Remove(UnitData unit)
    {
        if (unit.UnitName == LeaderName) return false;
        return Members.Remove(unit);
    }

    /// <summary>按名字移除</summary>
    public bool RemoveByName(string unitName)
    {
        if (unitName == LeaderName) return false;
        var unit = Members.FirstOrDefault(m => m.UnitName == unitName);
        if (unit == null) return false;
        return Members.Remove(unit);
    }

    /// <summary>按名字查找</summary>
    public UnitData? GetByName(string unitName)
    {
        return Members.FirstOrDefault(m => m.UnitName == unitName);
    }

    /// <summary>按索引获取</summary>
    public UnitData? GetAt(int index)
    {
        return index >= 0 && index < Members.Count ? Members[index] : null;
    }

    /// <summary>是否为队长</summary>
    public bool IsLeader(UnitData unit) => unit.UnitName == LeaderName;

    /// <summary>是否为队长（按名字）</summary>
    public bool IsLeader(string unitName) => unitName == LeaderName;

    /// <summary>队长引用</summary>
    public UnitData? Leader => Members.Count > 0 ? Members[0] : null;

    // ========================================
    // 战斗相关
    // ========================================

    /// <summary>获取可参战的成员（HP > 0 且未重伤的活人）</summary>
    public List<UnitData> GetDeployableMembers()
    {
        return Members.Where(m => GetCurrentHp(m) > 0 && !m.IsWounded).ToList();
    }

    /// <summary>获取已阵亡的成员（HP <= 0）</summary>
    public List<UnitData> GetDeadMembers()
    {
        return Members.Where(m => GetCurrentHp(m) <= 0).ToList();
    }

    /// <summary>队长是否存活</summary>
    public bool IsLeaderAlive => Leader != null && GetCurrentHp(Leader) > 0;

    /// <summary>计算名册的总战斗力（用于大地图 AI 决策）</summary>
    public float CalculateCombatPower()
    {
        var deployable = GetDeployableMembers();
        if (deployable.Count == 0) return 10.0f;
        return Math.Max(10.0f, deployable.Sum(m => m.Level) * 1.5f);
    }

    /// <summary>
    /// 应用战斗结果：更新 HP、移除阵亡者
    /// </summary>
    /// <param name="survivorHp">存活者 unitName → 战后 HP</param>
    /// <param name="deadNames">阵亡者 unitName 列表</param>
    public void ApplyBattleResult(Dictionary<string, int> survivorHp, List<string> deadNames)
    {
        // 更新存活者 HP
        foreach (var (name, hp) in survivorHp)
        {
            var unit = GetByName(name);
            if (unit != null)
                SetCurrentHp(unit, hp);
        }

        // 移除阵亡者（队长阵亡不移除，由上层判定游戏结束）
        foreach (var name in deadNames)
        {
            if (name == LeaderName) continue;
            RemoveByName(name);
        }
    }

    /// <summary>全员休息恢复 HP（每天调用）</summary>
    public void RestoreHp(int amount)
    {
        foreach (var m in Members)
        {
            int current = GetCurrentHp(m);
            int max = m.BaseMaxHp;
            SetCurrentHp(m, Math.Min(current + amount, max));
        }
    }

    // ========================================
    // HP 访问（UnitData 的 HP 存在 Runtime 里）
    // ========================================

    /// <summary>获取单位当前 HP</summary>
    public static int GetCurrentHp(UnitData unit)
    {
        if (unit.IsWounded) return 0;
        // UnitData.Runtime.CurrentHp 是战斗运行时字段
        // 大地图上用 Runtime.CurrentHp 持久化当前 HP
        return unit.Runtime.CurrentHp > 0 ? unit.Runtime.CurrentHp : unit.BaseMaxHp;
    }

    /// <summary>设置单位当前 HP</summary>
    public static void SetCurrentHp(UnitData unit, int hp)
    {
        unit.Runtime.CurrentHp = Math.Max(0, hp);
    }

    // ========================================
    // 序列化
    // ========================================

    /// <summary>序列化为 Godot Dictionary（存档用）</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        var membersArray = new Godot.Collections.Array();
        foreach (var m in Members)
        {
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
                ["current_hp"] = GetCurrentHp(m),
                ["xp"] = m.Xp,
                ["race_id"] = m.Race != null ? (int)m.Race.raceId : 0,
                ["is_leader"] = IsLeader(m),
                ["portrait_id"] = m.PortraitId,
                ["sprite_frames_id"] = m.SpriteFramesId,
                ["is_wounded"] = m.IsWounded,
            };
            membersArray.Add(memberDict);
        }

        return new Godot.Collections.Dictionary
        {
            ["leader_name"] = LeaderName,
            ["capacity"] = Capacity,
            ["members"] = membersArray,
        };
    }

    /// <summary>从 Godot Dictionary 反序列化</summary>
    public static PartyRoster Deserialize(Godot.Collections.Dictionary data)
    {
        var roster = new PartyRoster();
        roster.LeaderName = data.ContainsKey("leader_name") ? data["leader_name"].AsString() : "";
        roster.Capacity = data.ContainsKey("capacity") ? data["capacity"].AsInt32() : 6;

        if (data.ContainsKey("members") && data["members"].Obj is Godot.Collections.Array membersArray)
        {
            foreach (var memberVar in membersArray)
            {
                if (memberVar.Obj is not Godot.Collections.Dictionary memberDict) continue;

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
                SetCurrentHp(unit, currentHp);

                roster.Members.Add(unit);
            }
        }

        return roster;
    }

    // ========================================
    // 属性暴露（绕过 source generator）
    // ========================================

    public override Variant _Get(StringName property)
    {
        return property.ToString() switch
        {
            "members" => MembersGd,
            "count" => Count,
            "capacity" => Capacity,
            "is_full" => IsFull,
            "leader_name" => LeaderName,
            "is_leader_alive" => IsLeaderAlive,
            _ => default,
        };
    }

    /// <summary>兼容的成员列表（Godot Array）</summary>
    public Godot.Collections.Array MembersGd
    {
        get
        {
            var arr = new Godot.Collections.Array();
            foreach (var m in Members) arr.Add(m);
            return arr;
        }
    }

    // ========================================
    // 调试
    // ========================================

    public override string ToString()
    {
        return $"PartyRoster[{Count}/{Capacity}] Leader={LeaderName} " +
               $"Alive={GetDeployableMembers().Count} Dead={GetDeadMembers().Count}";
    }
}
