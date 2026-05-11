// GlobalState.cs
// 全局状态管理单例，用于跨场景数据传递
// 迁移自 GDScript GlobalState.gd
using Godot;

namespace BladeHex.Data;

/// <summary>
/// 全局状态管理 — Autoload 单例
/// </summary>
[GlobalClass]
public partial class GlobalState : Node
{
    /// <summary>是否正在加载存档</summary>
    [Export] public bool IsLoadingSave { get; set; }

    /// <summary>已加载的存档数据</summary>
    [Export] public Godot.Collections.Dictionary LoadedData { get; set; } = new();

    /// <summary>快速游戏模式（跳过出身选择，随机生成角色）</summary>
    [Export] public bool IsQuickGame { get; set; }

    /// <summary>
    /// 玩家选择的出身数据（由 OriginSelect 设置）
    /// 包含: race (RaceData), unit_data (UnitData)
    /// </summary>
    [Export] public Godot.Collections.Dictionary PlayerOrigin { get; set; } = new();

    // ========================================
    // 设置相关（供 GDScript 调用）
    // ========================================

    /// <summary>获取当前游戏设置（从文件加载或创建默认）</summary>
    public GameSettings GetSettings()
    {
        // 尝试从文件加载
        var settings = new GameSettings();
        if (Godot.FileAccess.FileExists("user://settings.dat"))
        {
            var file = Godot.FileAccess.Open("user://settings.dat", Godot.FileAccess.ModeFlags.Read);
            if (file != null)
            {
                var json = file.GetAsText();
                file.Close();
                if (!string.IsNullOrEmpty(json))
                {
                    var j = new Godot.Json();
                    var err = j.Parse(json);
                    if (err == Error.Ok)
                    {
                        var data = (Godot.Collections.Dictionary)j.Data;
                        settings.Deserialize(data);
                    }
                }
            }
        }
        return settings;
    }

    /// <summary>应用设置到引擎</summary>
    public void ApplySettings(GameSettings settings)
    {
        settings.ApplyToEngine();
        // 保存到文件
        var data = settings.Serialize();
        var json = Godot.Json.Stringify(data, "\t");
        var file = Godot.FileAccess.Open("user://settings.dat", Godot.FileAccess.ModeFlags.Write);
        if (file != null)
        {
            file.StoreString(json);
            file.Close();
        }
    }
}
