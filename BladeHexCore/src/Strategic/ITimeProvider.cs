// ITimeProvider.cs
// 时间提供者接口 — Core 层通过此接口读取当前天数
// Frontend 层的 EconomyManager 负责实现并注册
namespace BladeHex.Strategic;

/// <summary>
/// 时间提供者接口 — 将时间源从 Frontend 下放到 Core
/// </summary>
public interface ITimeProvider
{
    int CurrentDay { get; }
}

/// <summary>
/// 全局时间访问点 — 静态 ServiceLocator，Core 层自给自足
/// </summary>
public static class TimeProvider
{
    private static ITimeProvider? _instance;

    /// <summary>注册时间提供者（由 Frontend 在启动时调用）</summary>
    public static void Set(ITimeProvider provider) => _instance = provider;

    /// <summary>取消注册</summary>
    public static void Clear() => _instance = null;

    /// <summary>当前天数 — 未注册时返回 1 作为安全后备</summary>
    public static int CurrentDay => _instance?.CurrentDay ?? 1;

    /// <summary>是否已注册时间提供者</summary>
    public static bool IsRegistered => _instance != null;
}