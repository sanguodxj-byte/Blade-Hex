// GlobalState.cs
// 全局状态管理 — Autoload 聚合根。
using Godot;
using BladeHex.Data.Contexts;

namespace BladeHex.Data;

/// <summary>
/// [Autoload Singleton] 全局状态聚合根。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>GlobalState</c>。</para>
/// <para>生命周期：应用全局。</para>
/// <para>访问方式：建议通过 <see cref="Globals.State"/> 或 <see cref="Globals.StateOrNull"/>。</para>
///
/// <para>状态按职责拆分为多个子上下文，禁止在此类直接添加新字段：</para>
/// <list type="bullet">
///   <item><see cref="Save"/> — 存档加载状态</item>
///   <item><see cref="WorldGen"/> — 世界生成参数</item>
///   <item><see cref="QuickCombat"/> — 快速战斗配置</item>
///   <item><see cref="Weather"/> — 天气快照</item>
///   <item><see cref="OriginContext"/> — 出身选择数据</item>
/// </list>
/// </summary>
[GlobalClass]
public partial class GlobalState : Node
{
    // ========================================
    // 子上下文（推荐访问方式）
    // ========================================

    [Export] public SaveContext Save { get; set; } = new();
    [Export] public WorldGenContext WorldGen { get; set; } = new();
    [Export] public QuickCombatContext QuickCombat { get; set; } = new();
    [Export] public WeatherContext Weather { get; set; } = new();
    [Export] public PlayerOriginContext OriginContext { get; set; } = new();

    // ========================================
    // 设置相关（薄包装，仍保留在 GlobalState）
    // ========================================

    /// <summary>获取当前游戏设置（从文件加载或创建默认）。</summary>
    public GameSettings GetSettings()
    {
        var settings = new GameSettings();
        settings.LoadFromFile();
        return settings;
    }

    /// <summary>应用设置到引擎并保存到文件。</summary>
    public void ApplySettings(GameSettings settings)
    {
        settings.ApplyToEngine();
        settings.SaveToFile();
    }
}
