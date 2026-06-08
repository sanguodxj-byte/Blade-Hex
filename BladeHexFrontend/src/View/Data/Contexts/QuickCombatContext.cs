using Godot;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 快速战斗配置 — 由主菜单的 QuickCombatSetup 面板写入，QuickCombatScene 读取。
///
/// 该上下文不参与正式存档流程，仅在主菜单 → 快速战斗这条路径上传递参数。
/// </summary>
[GlobalClass]
public partial class QuickCombatContext : Resource
{
    /// <summary>地图模板键名，空字符串表示随机。</summary>
    [Export] public string Template { get; set; } = "";

    /// <summary>
    /// 战斗规模档位 — 决定大地图采样范围和战斗地图大小。
    /// 0=小型(K=0, 169格), 1=中型(K=1, 397格), 2=大型(K=2, 631格), 3=巨大(K=3, 973格)
    /// </summary>
    [Export] public int Size { get; set; }

    /// <summary>玩家方单位数量（1-6）。</summary>
    [Export] public int PlayerCount { get; set; } = 2;

    /// <summary>敌方单位数量（1-10）。</summary>
    [Export] public int EnemyCount { get; set; } = 3;

    /// <summary>敌方难度档（0=Easy, 1=Normal, 2=Hard）。</summary>
    [Export] public int Difficulty { get; set; } = 1;

    /// <summary>玩家方等级（1-120）。保证有足够技能点解锁主动技能。</summary>
    [Export] public int PlayerLevel { get; set; } = 5;

    /// <summary>敌方种类（0=人形, 1=亡灵, 2=野兽, 3=混合, 4=传奇生物）。</summary>
    [Export] public int EnemyType { get; set; }

    /// <summary>传奇生物子类型（仅当 EnemyType==4 时有效）。-1=随机, 0~N 对应具体传奇模板索引。</summary>
    [Export] public int LegendaryType { get; set; } = -1;
}
