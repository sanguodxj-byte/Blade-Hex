// PhaseData.cs
// 阶段数据 — 定义传奇生物的多阶段
// T12: Legendary creature action system
using Godot;

namespace BladeHex.Combat.Legendary;

/// <summary>
/// 阶段数据 — 传奇生物在特定 HP 阈值切换形态
/// </summary>
public class PhaseData
{
    /// <summary>阶段 ID</summary>
    public string Id { get; set; } = "";

    /// <summary>显示名称</summary>
    public string Name { get; set; } = "";

    /// <summary>触发阈值（HP 百分比）</summary>
    public float HpThreshold { get; set; }

    /// <summary>伤害倍率修正</summary>
    public float DamageMultiplier { get; set; } = 1.0f;

    /// <summary>AC 修正</summary>
    public int AcModifier { get; set; }

    /// <summary>速度修正</summary>
    public int SpeedModifier { get; set; }

    /// <summary>新增的传奇动作 ID 列表</summary>
    public string[] AdditionalActions { get; set; } = [];

    /// <summary>新增免疫</summary>
    public string[] AdditionalImmunities { get; set; } = [];

    /// <summary>新增抗性</summary>
    public string[] AdditionalResistances { get; set; } = [];

    /// <summary>从字典创建</summary>
    public static PhaseData FromDictionary(Godot.Collections.Dictionary dict)
    {
        var phase = new PhaseData();
        phase.Id = dict.ContainsKey("id") ? dict["id"].AsString() : "";
        phase.Name = dict.ContainsKey("name") ? dict["name"].AsString() : "";
        phase.HpThreshold = dict.ContainsKey("hp_threshold") ? dict["hp_threshold"].AsSingle() : 0f;
        phase.DamageMultiplier = dict.ContainsKey("damage_multiplier") ? dict["damage_multiplier"].AsSingle() : 1.0f;
        phase.AcModifier = dict.ContainsKey("ac_modifier") ? dict["ac_modifier"].AsInt32() : 0;
        phase.SpeedModifier = dict.ContainsKey("speed_modifier") ? dict["speed_modifier"].AsInt32() : 0;

        if (dict.ContainsKey("additional_actions"))
        {
            var arr = (Godot.Collections.Array)dict["additional_actions"];
            phase.AdditionalActions = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++)
                phase.AdditionalActions[i] = arr[i].AsString();
        }

        if (dict.ContainsKey("additional_immunities"))
        {
            var arr = (Godot.Collections.Array)dict["additional_immunities"];
            phase.AdditionalImmunities = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++)
                phase.AdditionalImmunities[i] = arr[i].AsString();
        }

        if (dict.ContainsKey("additional_resistances"))
        {
            var arr = (Godot.Collections.Array)dict["additional_resistances"];
            phase.AdditionalResistances = new string[arr.Count];
            for (int i = 0; i < arr.Count; i++)
                phase.AdditionalResistances[i] = arr[i].AsString();
        }

        return phase;
    }
}
