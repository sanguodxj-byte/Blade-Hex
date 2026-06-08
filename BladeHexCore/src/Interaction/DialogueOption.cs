using Godot;
using System;

namespace BladeHex.Strategic;

/// <summary>
/// 对话选项数据类 — 描述玩家可选择的分支选项
/// </summary>
public class DialogueOption
{
    public string Text { get; set; } = "";
    public string NextNodeId { get; set; } = "";
    public string Condition { get; set; } = "";
    public string ConditionParam { get; set; } = "";
    public string Action { get; set; } = "";
    public string ActionParam { get; set; } = "";
}
