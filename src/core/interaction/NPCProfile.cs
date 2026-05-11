using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// NPC档案数据类 — 描述大地图上人形NPC的完整信息
/// </summary>
[GlobalClass]
public partial class NPCProfile : Resource
{
    public enum NpcType
    {
        Adventurer,        // 冒险者
        Merchant,          // 商队
        Traveler,          // 旅行者
        WanderingKnight,  // 流浪骑士
        BountyTarget,     // 通缉犯
        HostileHumanoid,  // 敌对人形生物
    }

    public enum Attitude
    {
        Friendly,   // 友好
        Neutral,    // 中立
        Cold,       // 冷漠
        Hostile,    // 敌意
    }

    [Export] public string npcName = "无名旅人";
    [Export] public NpcType npcType = NpcType.Traveler;
    [Export] public Attitude attitude = Attitude.Neutral;
    [Export] public string faction = "";
    [Export] public int relation = 0; // 与玩家的关系值 -100~100
    [Export] public string dialogueIntro = "";
    [Export] public Godot.Collections.Dictionary dialogueLines = new(); // 对话节点 key->DialogueEntry
    [Export] public int gold = 50;
    [Export] public int recruitCost = 100; // 招募费用

    public static string GetNpcTypeName(NpcType type) => type switch
    {
        NpcType.Adventurer => "冒险者",
        NpcType.Merchant => "商队",
        NpcType.Traveler => "旅行者",
        NpcType.WanderingKnight => "流浪骑士",
        NpcType.BountyTarget => "通缉犯",
        NpcType.HostileHumanoid => "敌对者",
        _ => "未知"
    };

    public string GetAttitudeText() => attitude switch
    {
        Attitude.Friendly => "友好",
        Attitude.Neutral => "中立",
        Attitude.Cold => "冷漠",
        Attitude.Hostile => "敌意",
        _ => "未知"
    };

    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(dialogueIntro)) return dialogueIntro;

        string typeName = GetNpcTypeName(npcType);
        string attitudeText = GetAttitudeText();

        return npcType switch
        {
            NpcType.Adventurer => $"一支{typeName}的冒险者队伍，态度{attitudeText}。",
            NpcType.Merchant => $"一支{typeName}，看起来有不少货物。态度{attitudeText}。",
            NpcType.Traveler => $"一位{typeName}，似乎在赶路。态度{attitudeText}。",
            NpcType.WanderingKnight => $"一位{typeName}，装备精良，神情傲然。态度{attitudeText}。",
            NpcType.BountyTarget => $"一名{typeName}，悬赏金额不菲。",
            NpcType.HostileHumanoid => $"一群{typeName}，看起来不太友善。",
            _ => $"一个{typeName}。"
        };
    }

    public Godot.Collections.Dictionary GetDefaultDialogue()
    {
        return npcType switch
        {
            NpcType.Adventurer => new Godot.Collections.Dictionary
            {
                ["greeting"] = "你好，旅人。我们是冒险者，正在寻找任务委托。",
                ["options"] = new Godot.Collections.Array { "你们愿意加入我的队伍吗？", "有什么消息吗？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "加入队伍？嗯，报酬合适的话可以考虑。", "听说北边的矿坑里有怪物出没，可能有好东西。", "后会有期。" }
            },
            NpcType.Merchant => new Godot.Collections.Dictionary
            {
                ["greeting"] = "欢迎！看看我们的商品吧，价格公道。",
                ["options"] = new Godot.Collections.Array { "让我看看你的货物。", "需要护送吗？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "当然，请随意挑选。", "如果你能护送我们到下一个城镇，我们会付你报酬。", "慢走，欢迎下次光临。" }
            },
            NpcType.Traveler => new Godot.Collections.Dictionary
            {
                ["greeting"] = "哦，你好。我只是在赶路而已。",
                ["options"] = new Godot.Collections.Array { "附近有什么有趣的地方吗？", "你从哪里来？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "东边的森林里据说有古老的遗迹，不过很危险。", "我从南方的城镇来，那里最近不太太平。", "再见，祝旅途平安。" }
            },
            NpcType.WanderingKnight => new Godot.Collections.Dictionary
            {
                ["greeting"] = "哼，又是一个旅人。你看起来还具有几分本事。",
                ["options"] = new Godot.Collections.Array { "我想和你切磋一下。", "你在寻找什么？", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "切磋？有意思。如果你赢了，我给你我的佩剑；如果你输了，留下你的金币。", "我在寻找一位强大的对手，证明我的实力。", "后会有期。" }
            },
            NpcType.BountyTarget => new Godot.Collections.Dictionary
            {
                ["greeting"] = "你想干什么？别挡路！",
                ["options"] = new Godot.Collections.Array { "你就是那个通缉犯？", "我可以放你走，但你得付钱。", "告辞。" },
                ["responses"] = new Godot.Collections.Array { "通缉犯？你认错人了！……好吧，你打算怎么办？", "哈，识时务。给我一个理由，我就走。", "算你聪明，快滚吧。" }
            },
            NpcType.HostileHumanoid => new Godot.Collections.Dictionary
            {
                ["greeting"] = "滚开！这是我们的地盘！",
                ["options"] = new Godot.Collections.Array { "我不想和你们冲突。", "那就让拳头说话吧。" },
                ["responses"] = new Godot.Collections.Array { "算你识相，快滚！", "找死！" }
            },
            _ => new Godot.Collections.Dictionary
            {
                ["greeting"] = "……",
                ["options"] = new Godot.Collections.Array { "告辞。" },
                ["responses"] = new Godot.Collections.Array { "……" }
            }
        };
    }
}
