// SkillData.cs
// 技能与法术数据
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class SkillData : Resource
{
    [Export] public string SkillName { get; set; } = "未命名技能";
    [Export] public string Description { get; set; } = "";
    [Export] public string IconId { get; set; } = "";
    [Export] public int ApCost { get; set; } = 1;
    [Export] public int RangeCells { get; set; } = 1;
    [Export] public int Cooldown;
}
