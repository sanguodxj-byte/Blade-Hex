namespace BladeHex.Combat.Buff;

/// <summary>
/// Buff 上挂载的触发器。当指定事件发生时,按概率执行效果。
/// </summary>
public class BuffTrigger
{
    /// <summary>触发事件</summary>
    public TriggerEvent Event = TriggerEvent.OnTurnStart;

    /// <summary>
    /// 效果描述字符串。格式: "action:param1:param2"
    /// 示例:
    ///   "deal_damage:1d6:fire"     — 造成 1d6 火焰伤害
    ///   "heal:5"                   — 治疗 5 HP
    ///   "apply_buff:poison:2"      — 施加 2 回合中毒
    ///   "remove_buff:burning"      — 移除燃烧
    ///   "add_modifier:damage:increased:0.1" — 临时增加 10% 伤害
    ///   "gain_ap:2"                — 获得 2 AP
    /// </summary>
    public string Effect = "";

    /// <summary>触发条件(为空=无条件)。示例: "target_hp_below_50%", "self_hp_above_80%"</summary>
    public string Condition = "";

    /// <summary>触发概率 0~1。1.0 = 必定触发</summary>
    public float Chance = 1.0f;

    /// <summary>每场战斗最多触发次数。-1 = 无限</summary>
    public int MaxTriggersPerCombat = -1;

    /// <summary>当前已触发次数(运行时)</summary>
    public int CurrentTriggerCount;
}
