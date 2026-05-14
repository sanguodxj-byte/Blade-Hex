// HexCellData.cs
// 地形数据资源
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class HexCellData : Resource
{
    [Export] public string terrainName { get; set; } = "平地";
    [Export] public int moveCost { get; set; } = 1;
    [Export] public int acBonus { get; set; } = 0;
    [Export] public string coverType { get; set; } = "无"; // 无, 半掩体, 全掩体
    [Export] public Color terrainColor = Colors.White;
    [Export] public string iconId { get; set; } = "";
}