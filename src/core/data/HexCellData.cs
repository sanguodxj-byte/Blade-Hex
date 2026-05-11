// HexCellData.cs
// 地形数据资源
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class HexCellData : Resource
{
    [Export] public string terrainName = "平地";
    [Export] public int moveCost = 1;
    [Export] public int acBonus = 0;
    [Export] public string coverType = "无"; // 无, 半掩体, 全掩体
    [Export] public Color terrainColor = Colors.White;
    [Export] public Texture2D? icon;
}