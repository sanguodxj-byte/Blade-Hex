// BattleOutcome.cs
// 战斗结果数据模型 — 战略层与战斗层之间的桥梁
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗结果 — 战斗结束后传递给战略层的数据
/// </summary>
[GlobalClass]
public partial class BattleOutcome : Resource
{
    // ===== 胜负 =====
    /// <summary>攻击方是否胜利</summary>
    public bool AttackerWon = false;

    /// <summary>攻击方是否被全灭</summary>
    public bool AttackerDestroyed = false;

    /// <summary>防御方是否被全灭</summary>
    public bool DefenderDestroyed = false;

    // ===== 伤亡 =====
    /// <summary>攻击方损失比例 (0.0~1.0)</summary>
    public float AttackerLossPercent = 0.0f;

    /// <summary>防御方损失比例 (0.0~1.0)</summary>
    public float DefenderLossPercent = 0.0f;

    // ===== 队员级伤亡明细（PartyRoster 回写用）=====
    /// <summary>存活队员：unitName → 战后剩余 HP</summary>
    public Dictionary<string, int> SurvivorHp = new();

    /// <summary>阵亡队员名字列表</summary>
    public List<string> DeadUnitNames = new();

    // ===== 奖励 =====
    /// <summary>经验值</summary>
    public int XpGranted = 0;

    /// <summary>金币</summary>
    public int GoldGranted = 0;

    /// <summary>掉落物品资源路径列表</summary>
    public List<string> LootItemPaths = new();

    /// <summary>掉落的装备/物品详细数据（战利品面板显示用）</summary>
    public List<LootEntry> LootEntries = new();

    // ===== 势力关系 =====
    /// <summary>势力声望变化: factionId → change</summary>
    public Dictionary<string, int> FactionReputationChanges = new();

    // ===== 实体状态更新 =====
    /// <summary>关联的攻击方实体名称</summary>
    public string AttackerEntityName = "";

    /// <summary>攻击方战后剩余队伍人数</summary>
    public int AttackerRemainingPartySize = 0;

    /// <summary>攻击方战后剩余战力</summary>
    public float AttackerRemainingCombatPower = 0.0f;

    // ===== POI 状态 =====
    /// <summary>关联的POI名称（如果是围攻/劫掠）</summary>
    public string PoiName = "";

    /// <summary>POI 是否被攻占</summary>
    public bool PoiCaptured = false;

    /// <summary>POI 新繁荣度</summary>
    public int NewProsperity = 0;

    /// <summary>POI 新驻军数量</summary>
    public int NewGarrisonSize = 0;

    // ===== 叙事 =====
    /// <summary>战斗类型 ("field_battle", "siege", "raid", "ambush")</summary>
    public string BattleType = "field_battle";

    /// <summary>战斗描述文本</summary>
    public string BattleDescription = "";

    // ===== 序列化 =====
    public Godot.Collections.Dictionary Serialize()
    {
        var loot = new Godot.Collections.Array();
        foreach (var path in LootItemPaths) loot.Add(path);

        var factionChanges = new Godot.Collections.Dictionary();
        foreach (var kv in FactionReputationChanges) factionChanges[kv.Key] = kv.Value;

        return new Godot.Collections.Dictionary
        {
            ["attacker_won"] = AttackerWon,
            ["attacker_destroyed"] = AttackerDestroyed,
            ["defender_destroyed"] = DefenderDestroyed,
            ["attacker_loss_pct"] = AttackerLossPercent,
            ["defender_loss_pct"] = DefenderLossPercent,
            ["xp"] = XpGranted,
            ["gold"] = GoldGranted,
            ["loot"] = loot,
            ["faction_changes"] = factionChanges,
            ["attacker_name"] = AttackerEntityName,
            ["attacker_remaining"] = AttackerRemainingPartySize,
            ["attacker_power"] = AttackerRemainingCombatPower,
            ["poi_name"] = PoiName,
            ["poi_captured"] = PoiCaptured,
            ["new_prosperity"] = NewProsperity,
            ["new_garrison"] = NewGarrisonSize,
            ["battle_type"] = BattleType,
            ["description"] = BattleDescription,
        };
    }

    public static BattleOutcome Deserialize(Godot.Collections.Dictionary data)
    {
        var outcome = new BattleOutcome();
        if (data.ContainsKey("attacker_won")) outcome.AttackerWon = data["attacker_won"].AsBool();
        if (data.ContainsKey("attacker_destroyed")) outcome.AttackerDestroyed = data["attacker_destroyed"].AsBool();
        if (data.ContainsKey("defender_destroyed")) outcome.DefenderDestroyed = data["defender_destroyed"].AsBool();
        if (data.ContainsKey("attacker_loss_pct")) outcome.AttackerLossPercent = data["attacker_loss_pct"].AsSingle();
        if (data.ContainsKey("defender_loss_pct")) outcome.DefenderLossPercent = data["defender_loss_pct"].AsSingle();
        if (data.ContainsKey("xp")) outcome.XpGranted = data["xp"].AsInt32();
        if (data.ContainsKey("gold")) outcome.GoldGranted = data["gold"].AsInt32();
        if (data.ContainsKey("attacker_name")) outcome.AttackerEntityName = data["attacker_name"].AsString();
        if (data.ContainsKey("attacker_remaining")) outcome.AttackerRemainingPartySize = data["attacker_remaining"].AsInt32();
        if (data.ContainsKey("attacker_power")) outcome.AttackerRemainingCombatPower = data["attacker_power"].AsSingle();
        if (data.ContainsKey("poi_name")) outcome.PoiName = data["poi_name"].AsString();
        if (data.ContainsKey("poi_captured")) outcome.PoiCaptured = data["poi_captured"].AsBool();
        if (data.ContainsKey("new_prosperity")) outcome.NewProsperity = data["new_prosperity"].AsInt32();
        if (data.ContainsKey("new_garrison")) outcome.NewGarrisonSize = data["new_garrison"].AsInt32();
        if (data.ContainsKey("battle_type")) outcome.BattleType = data["battle_type"].AsString();
        if (data.ContainsKey("description")) outcome.BattleDescription = data["description"].AsString();

        if (data.ContainsKey("loot") && data["loot"].Obj is Godot.Collections.Array loot)
            foreach (var item in loot) outcome.LootItemPaths.Add(item.AsString());

        if (data.ContainsKey("faction_changes") && data["faction_changes"].Obj is Godot.Collections.Dictionary factions)
            foreach (var key in factions.Keys) outcome.FactionReputationChanges[key.AsString()] = factions[key].AsInt32();

        return outcome;
    }
}