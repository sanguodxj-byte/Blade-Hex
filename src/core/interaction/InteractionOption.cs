using Godot;
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 交互选项数据类 — 描述一个可选择的交互选项
/// </summary>
public partial class InteractionOption : RefCounted
{
    public string Id = "";
    public string Label = "";
    public string IconName = "";
    public bool Enabled = true;
    public string Tooltip = "";
    public InteractionType.Type CurrentInteractionType = InteractionType.Type.Leave;
    public Godot.Collections.Dictionary Metadata = new();

    public InteractionOption() { }

    public InteractionOption(string id, string label, InteractionType.Type type, string tooltip = "")
    {
        Id = id;
        Label = label;
        CurrentInteractionType = type;
        Tooltip = tooltip;
        IconName = InteractionType.GetIconName(type);
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
}
