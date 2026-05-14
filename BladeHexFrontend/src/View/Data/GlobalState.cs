// GlobalState.cs
// 全局状态管理单例，用于跨场景数据传递
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

    /// <summary>世界种子（新游戏时设置，0 表示随机）</summary>
    [Export] public int WorldSeed { get; set; }

    /// <summary>世界大小（0=小, 1=中, 2=大）— 新游戏时由玩家选择</summary>
    [Export] public int WorldSize { get; set; } = 1; // 默认中型

    /// <summary>当前存档 ID（用于 chunk 持久化路径）</summary>
    [Export] public string? CurrentSaveId { get; set; }

    /// <summary>
    /// 玩家选择的出身数据（由 OriginSelect 设置）
    /// 包含: race (RaceData), unit_data (UnitData)
    /// </summary>
    [Export] public Godot.Collections.Dictionary PlayerOrigin { get; set; } = new();

    // ========================================
    // 快速战斗配置（由 QuickCombatSetup 面板设置）
    // ========================================

    /// <summary>快速战斗地图模板名（空=随机）</summary>
    [Export] public string QuickCombatTemplate { get; set; } = "";

    /// <summary>快速战斗规模 (0=Mercenary, 1=Knight, 2=Lord, 3=Stronghold)</summary>
    [Export] public int QuickCombatSize { get; set; } = 0;

    /// <summary>玩家方单位数量</summary>
    [Export] public int QuickCombatPlayerCount { get; set; } = 2;

    /// <summary>敌方单位数量</summary>
    [Export] public int QuickCombatEnemyCount { get; set; } = 3;

    /// <summary>敌方难度 (0=简单, 1=普通, 2=困难)</summary>
    [Export] public int QuickCombatDifficulty { get; set; } = 1;

    // ========================================
    // 天气状态（大地图 → 战斗场景传递）
    // ========================================

    /// <summary>当前天气类型 (-1=Clear, 0=Rain, 1=Snow, 2=Sandstorm)</summary>
    public int CurrentWeatherType { get; set; } = -1;

    /// <summary>当前天气强度 [0, 1]</summary>
    public float CurrentWeatherIntensity { get; set; } = 0.0f;

    // ========================================
    // 设置相关（供 调用）
    // ========================================

    /// <summary>获取当前游戏设置（从文件加载或创建默认）</summary>
    public GameSettings GetSettings()
    {
        var settings = new GameSettings();
        settings.LoadFromFile();
        return settings;
    }

    /// <summary>应用设置到引擎并保存到文件</summary>
    public void ApplySettings(GameSettings settings)
    {
        settings.ApplyToEngine();
        settings.SaveToFile();
    }
}
