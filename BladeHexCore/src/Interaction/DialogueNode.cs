using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 条件文本条目 — 在节点的 Texts 列表中，每个条目带有一个可选条件表达式。
/// 由 DialogueConditionEvaluator 按顺序扫描，第一个满足条件的文本生效。
/// 最后一个无 Condition 的条目作为兜底（始终匹配）。
/// </summary>
public class ConditionalText
{
    /// <summary>
    /// 条件表达式，例如 "player_army >= npc_army * 3"。
    /// 为 null 或空字符串时表示无条件（兜底文本）。
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>正文文本（可含 {占位符}）</summary>
    public string Text { get; set; } = "";
}

/// <summary>
/// 对话树节点类 — 描述对话中 NPC 所说的一句话和对应的选项。
///
/// Text（旧格式，向下兼容）与 Texts（新的条件文本列表）并存：
/// - 如果 Texts 不为空，则由 DialogueRunner 从中匹配第一个满足条件的文本。
/// - 如果 Texts 为空，则直接使用 Text（内联硬编码，用于代码生成的默认对话树）。
/// </summary>
public class DialogueNode
{
    public string NodeId { get; set; } = "";

    /// <summary>
    /// 旧式单一文本（内联生成的默认对话树专用，向下兼容）
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// 新式条件文本列表（JSON 配置驱动，优先级高于 Text）
    /// </summary>
    public List<ConditionalText> Texts { get; set; } = new();

    public List<DialogueOption> Options { get; set; } = new();
}
