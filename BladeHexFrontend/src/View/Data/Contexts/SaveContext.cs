using Godot;

namespace BladeHex.Data.Contexts;

/// <summary>
/// 存档相关的全局状态。
///
/// 由 <see cref="GlobalState"/> 持有，集中管理"是否正在加载存档 / 当前存档 ID / 加载到的原始数据"等
/// 跨场景共享的存档进度信息。
/// </summary>
[GlobalClass]
public partial class SaveContext : Resource
{
    /// <summary>是否正在加载存档（影响 OverworldScene3D 初始化分支）。</summary>
    [Export] public bool IsLoadingSave { get; set; }

    /// <summary>已加载的存档原始字典（懒读取，由 SaveManager 填充）。</summary>
    [Export] public Godot.Collections.Dictionary LoadedData { get; set; } = new();

    /// <summary>当前存档 ID（用于 chunk 持久化路径）。</summary>
    [Export] public string? CurrentSaveId { get; set; }
}
