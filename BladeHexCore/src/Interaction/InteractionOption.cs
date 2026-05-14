using Godot;
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 交互选项数据类 — 描述一个可选择的交互选项
/// </summary>
[GlobalClass]
public partial class InteractionOption : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string OptionLabel { get; set; } = "";
    [Export] public string IconName { get; set; } = "";
    [Export] public bool Enabled { get; set; } = true;
    [Export] public string Tooltip { get; set; } = "";
    [Export] public InteractionType.Type CurrentInteractionType { get; set; } = InteractionType.Type.Leave;
    public Godot.Collections.Dictionary Metadata = new();

    public InteractionOption() { }

    public InteractionOption(string id, string label, InteractionType.Type type, string tooltip = "")
    {
        Id = id;
        OptionLabel = label;
        CurrentInteractionType = type;
        Tooltip = tooltip;
        IconName = InteractionType.GetIconName(type);
    }

    // 手动属性暴露（绕过 source generator 问题）
    public override Variant _Get(StringName property)
    {
        return property.ToString() switch
        {
            "option_label" => OptionLabel,
            "tooltip" => Tooltip,
            "enabled" => Enabled,
            "id" => Id,
            "icon_name" => IconName,
            "current_interaction_type" => (int)CurrentInteractionType,
            _ => default,
        };
    }

    public override bool _Set(StringName property, Variant value)
    {
        switch (property.ToString())
        {
            case "option_label": OptionLabel = value.AsString(); return true;
            case "tooltip": Tooltip = value.AsString(); return true;
            case "enabled": Enabled = value.AsBool(); return true;
            case "id": Id = value.AsString(); return true;
            case "icon_name": IconName = value.AsString(); return true;
            case "current_interaction_type": CurrentInteractionType = (InteractionType.Type)value.AsInt32(); return true;
            default: return false;
        }
    }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        var props = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        props.Add(MakeProp("option_label", Variant.Type.String));
        props.Add(MakeProp("tooltip", Variant.Type.String));
        props.Add(MakeProp("enabled", Variant.Type.Bool));
        props.Add(MakeProp("id", Variant.Type.String));
        props.Add(MakeProp("icon_name", Variant.Type.String));
        props.Add(MakeProp("current_interaction_type", Variant.Type.Int));
        return props;
    }

    private static Godot.Collections.Dictionary MakeProp(string name, Variant.Type type)
    {
        return new Godot.Collections.Dictionary
        {
            { "name", name },
            { "type", (int)type },
            { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) },
        };
    }

    // ========================================
    // 工厂方法
    // ========================================

    public static InteractionOption CreateAttack() => new("attack", InteractionType.GetDisplayName(InteractionType.Type.Attack), InteractionType.Type.Attack, InteractionType.GetDescription(InteractionType.Type.Attack));
    public static InteractionOption CreateTalk() => new("talk", InteractionType.GetDisplayName(InteractionType.Type.Talk), InteractionType.Type.Talk, InteractionType.GetDescription(InteractionType.Type.Talk));
    public static InteractionOption CreateTrade() => new("trade", InteractionType.GetDisplayName(InteractionType.Type.Trade), InteractionType.Type.Trade, InteractionType.GetDescription(InteractionType.Type.Trade));
    public static InteractionOption CreateLeave() => new("leave", InteractionType.GetDisplayName(InteractionType.Type.Leave), InteractionType.Type.Leave, InteractionType.GetDescription(InteractionType.Type.Leave));
    public static InteractionOption CreateRecruit() => new("recruit", InteractionType.GetDisplayName(InteractionType.Type.Recruit), InteractionType.Type.Recruit, InteractionType.GetDescription(InteractionType.Type.Recruit));
    public static InteractionOption CreateDuel() => new("duel", InteractionType.GetDisplayName(InteractionType.Type.Duel), InteractionType.Type.Duel, InteractionType.GetDescription(InteractionType.Type.Duel));
    public static InteractionOption CreateInformation() => new("information", InteractionType.GetDisplayName(InteractionType.Type.Information), InteractionType.Type.Information, InteractionType.GetDescription(InteractionType.Type.Information));
    public static InteractionOption CreateBounty() => new("bounty", InteractionType.GetDisplayName(InteractionType.Type.Bounty), InteractionType.Type.Bounty, InteractionType.GetDescription(InteractionType.Type.Bounty));
    public static InteractionOption CreateEscort() => new("escort", InteractionType.GetDisplayName(InteractionType.Type.Escort), InteractionType.Type.Escort, InteractionType.GetDescription(InteractionType.Type.Escort));
    public static InteractionOption CreateRest() => new("rest", InteractionType.GetDisplayName(InteractionType.Type.Rest), InteractionType.Type.Rest, InteractionType.GetDescription(InteractionType.Type.Rest));
    public static InteractionOption CreateTrain() => new("train", InteractionType.GetDisplayName(InteractionType.Type.Train), InteractionType.Type.Train, InteractionType.GetDescription(InteractionType.Type.Train));
    public static InteractionOption CreateHeal() => new("heal", InteractionType.GetDisplayName(InteractionType.Type.Heal), InteractionType.Type.Heal, InteractionType.GetDescription(InteractionType.Type.Heal));
    public static InteractionOption CreateQuest() => new("quest", InteractionType.GetDisplayName(InteractionType.Type.Quest), InteractionType.Type.Quest, InteractionType.GetDescription(InteractionType.Type.Quest));
    public static InteractionOption CreateArena() => new("arena", InteractionType.GetDisplayName(InteractionType.Type.Arena), InteractionType.Type.Arena, InteractionType.GetDescription(InteractionType.Type.Arena));
    public static InteractionOption CreateRepair() => new("repair", InteractionType.GetDisplayName(InteractionType.Type.Repair), InteractionType.Type.Repair, InteractionType.GetDescription(InteractionType.Type.Repair));
    public static InteractionOption CreateFerry() => new("ferry", InteractionType.GetDisplayName(InteractionType.Type.Ferry), InteractionType.Type.Ferry, InteractionType.GetDescription(InteractionType.Type.Ferry));
}
