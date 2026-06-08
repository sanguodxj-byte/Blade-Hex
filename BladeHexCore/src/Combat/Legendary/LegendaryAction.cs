// LegendaryAction.cs
// 传奇动作数据 — 定义传奇生物的动作
// T12: Legendary creature action system
using Godot;

namespace BladeHex.Combat.Legendary;

/// <summary>
/// 传奇动作类型
/// </summary>
public enum LegendaryActionType
{
    MeleeStrike,    // 近战打击
    AoeSave,        // 范围豁免
    StatusApply,    // 状态附加
    MoveBurst,      // 移动爆发
}

/// <summary>
/// 传奇动作数据
/// </summary>
public class LegendaryAction
{
    /// <summary>动作 ID</summary>
    public string Id { get; set; } = "";

    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";

    /// <summary>描述</summary>
    public string Description { get; set; } = "";

    /// <summary>动作类型</summary>
    public LegendaryActionType Type { get; set; }

    /// <summary>消耗的传奇动作点数</summary>
    public int Cost { get; set; } = 1;

    /// <summary>伤害骰子数量</summary>
    public int DamageDiceCount { get; set; }

    /// <summary>伤害骰子面数</summary>
    public int DamageDiceSides { get; set; }

    /// <summary>伤害类型</summary>
    public string DamageType { get; set; } = "";

    /// <summary>范围半径（格子）</summary>
    public int Radius { get; set; }

    /// <summary>豁免类型</summary>
    public string SaveType { get; set; } = "";

    /// <summary>豁免 DC</summary>
    public int SaveDC { get; set; }

    /// <summary>附加的状态效果 ID</summary>
    public string StatusEffect { get; set; } = "";

    /// <summary>状态效果持续回合</summary>
    public int StatusDuration { get; set; }

    /// <summary>从字典创建</summary>
    public static LegendaryAction FromDictionary(Godot.Collections.Dictionary dict)
    {
        var action = new LegendaryAction();
        action.Id = dict.ContainsKey("id") ? dict["id"].AsString() : "";
        action.Name = dict.ContainsKey("name") ? dict["name"].AsString() : "";
        action.Description = dict.ContainsKey("description") ? dict["description"].AsString() : "";
        action.Type = dict.ContainsKey("type") ? (LegendaryActionType)dict["type"].AsInt32() : LegendaryActionType.MeleeStrike;
        action.Cost = dict.ContainsKey("cost") ? dict["cost"].AsInt32() : 1;
        action.DamageDiceCount = dict.ContainsKey("damage_dice_count") ? dict["damage_dice_count"].AsInt32() : 0;
        action.DamageDiceSides = dict.ContainsKey("damage_dice_sides") ? dict["damage_dice_sides"].AsInt32() : 0;
        action.DamageType = dict.ContainsKey("damage_type") ? dict["damage_type"].AsString() : "";
        action.Radius = dict.ContainsKey("radius") ? dict["radius"].AsInt32() : 0;
        action.SaveType = dict.ContainsKey("save_type") ? dict["save_type"].AsString() : "";
        action.SaveDC = dict.ContainsKey("save_dc") ? dict["save_dc"].AsInt32() : 0;
        action.StatusEffect = dict.ContainsKey("status_effect") ? dict["status_effect"].AsString() : "";
        action.StatusDuration = dict.ContainsKey("status_duration") ? dict["status_duration"].AsInt32() : 0;
        return action;
    }
}
