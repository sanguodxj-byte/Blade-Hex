using System;

namespace BladeHex.Strategic;

/// <summary>
/// 对话运行时上下文 — 携带双方兵力、种族、好感度、声望等动态变量，
/// 注入到 DialogueRunner 后由 DialogueConditionEvaluator 进行条件匹配。
/// </summary>
public class DialogueContext
{
    // ─── 兵力 ────────────────────────────────────────────────────────────────────
    /// <summary>玩家当前麾下总兵力</summary>
    public int PlayerArmySize { get; set; } = 1;
    /// <summary>NPC 所在队伍总兵力</summary>
    public int NpcArmySize { get; set; } = 1;

    // ─── 战力评级（可用于更精细的强弱对比）────────────────────────────────────
    public float PlayerPowerRating { get; set; } = 1f;
    public float NpcPowerRating    { get; set; } = 1f;

    // ─── 种族与身份 ──────────────────────────────────────────────────────────────
    /// <summary>玩家所属种族（Human / Orc / Elf / Undead …）</summary>
    public string PlayerRace { get; set; } = "Human";
    /// <summary>NPC 所属种族</summary>
    public string NpcRace   { get; set; } = "Human";
    /// <summary>NPC 所属阵营/势力</summary>
    public string NpcFaction { get; set; } = "";

    // ─── 好感度与声望 ─────────────────────────────────────────────────────────
    /// <summary>玩家与该 NPC 的个人关系值 -100~100</summary>
    public int PlayerNpcRelation   { get; set; } = 0;
    /// <summary>玩家在 NPC 所属阵营中的声望值</summary>
    public int PlayerFactionReputation { get; set; } = 0;

    // ─── 其他实用变量 ─────────────────────────────────────────────────────────
    /// <summary>玩家当前携带的金币量</summary>
    public int PlayerGold { get; set; } = 0;

    // ─── 便捷属性（条件表达式中直接引用的 token 名称）──────────────────────
    /// <summary>player_army 的别名（与 JSON 表达式保持一致）</summary>
    public int player_army   => PlayerArmySize;
    /// <summary>npc_army 的别名</summary>
    public int npc_army      => NpcArmySize;
    /// <summary>relation 的别名</summary>
    public int relation      => PlayerNpcRelation;
    /// <summary>faction_rep 的别名</summary>
    public int faction_rep   => PlayerFactionReputation;
    /// <summary>player_gold 的别名</summary>
    public int player_gold   => PlayerGold;
    /// <summary>npc_race 的别名（小写）</summary>
    public string npc_race   => NpcRace.ToLowerInvariant();
    /// <summary>player_race 的别名（小写）</summary>
    public string player_race => PlayerRace.ToLowerInvariant();
}
