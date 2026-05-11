// SkillData.cs
// 技能与法术数据
// 迁移自 GDScript SkillData.gd
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class SkillData : Resource
{
    [Export] public string SkillName = "未命名技能";
    [Export] public string Description = "";
    [Export] public Texture2D? Icon;
    [Export] public int ApCost = 1;
    [Export] public int RangeCells = 1;
    [Export] public int Cooldown;
}
