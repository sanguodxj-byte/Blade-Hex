using Godot;
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 交互类型枚举 — 定义大地图上所有可能的交互类型
/// </summary>
public static class InteractionType
{
    public enum Type
    {
        Attack,
        Talk,
        Trade,
        Leave,
        Recruit,
        Duel,
        Escort,
        Information,
        Bounty,
        Rest,
        Train,
        Repair,
        Heal,
        Quest,
        Arena,
        Ferry,
    }

    public static string GetDisplayName(Type type) => type switch
    {
        Type.Attack => "袭击",
        Type.Talk => "交谈",
        Type.Trade => "交易",
        Type.Leave => "离开",
        Type.Recruit => "招募",
        Type.Duel => "决斗",
        Type.Escort => "护送",
        Type.Information => "打听情报",
        Type.Bounty => "缉拿",
        Type.Rest => "休息",
        Type.Train => "训练",
        Type.Repair => "修理",
        Type.Heal => "治疗",
        Type.Quest => "委托",
        Type.Arena => "竞技场",
        Type.Ferry => "渡船",
        _ => "未知"
    };

    public static string GetDescription(Type type) => type switch
    {
        Type.Attack => "向对方发起攻击，进入战术战斗",
        Type.Talk => "与对方交谈，了解更多信息",
        Type.Trade => "查看对方的商品，进行交易",
        Type.Leave => "不做任何事，继续前进",
        Type.Recruit => "邀请对方加入你的队伍",
        Type.Duel => "向对方发起一对一决斗挑战",
        Type.Escort => "接受护送委托，护送商队前往目的地",
        Type.Information => "向对方打听周围的情报和传闻",
        Type.Bounty => "将通缉犯缉拿归案，送交领主领赏",
        Type.Rest => "在安全的地方休息，恢复队伍状态",
        Type.Train => "花费金币进行训练，获取经验",
        Type.Repair => "修理受损的装备",
        Type.Heal => "接受治疗，恢复生命值和状态",
        Type.Quest => "查看可领取的委托任务",
        Type.Arena => "参加竞技场比赛，赢取奖品和声望",
        Type.Ferry => "乘坐渡船前往其他港口或海岛",
        _ => ""
    };

    public static string GetIconName(Type type) => type switch
    {
        Type.Attack => "sword",
        Type.Talk => "chat",
        Type.Trade => "coins",
        Type.Leave => "exit",
        Type.Recruit => "user_plus",
        Type.Duel => "swords",
        Type.Escort => "shield",
        Type.Information => "info",
        Type.Bounty => "target",
        Type.Rest => "bed",
        Type.Train => "dumbbell",
        Type.Repair => "wrench",
        Type.Heal => "heart",
        Type.Quest => "scroll",
        Type.Arena => "trophy",
        Type.Ferry => "anchor",
        _ => "question"
    };
}
