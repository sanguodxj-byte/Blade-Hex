using Godot;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 世界生成相关参数。
///
/// 由 <see cref="GlobalState"/> 持有，承载新建世界时的种子、世界规模、是否快速游戏等设置。
/// 仅在新游戏入口与 OverworldScene3D 启动期被读取，进入大地图后这些值不再变化。
/// </summary>
[GlobalClass]
public partial class WorldGenContext : Resource
{
    /// <summary>世界生成种子（0 表示随机）。</summary>
    [Export] public int Seed { get; set; }

    /// <summary>世界大小档位（0=Small, 1=Medium, 2=Large），由 OriginSelect / 主菜单设置。</summary>
    [Export] public int Size { get; set; } = 1;

    /// <summary>快速游戏模式（跳过出身选择，随机生成角色）。</summary>
    [Export] public bool IsQuickGame { get; set; }
}
