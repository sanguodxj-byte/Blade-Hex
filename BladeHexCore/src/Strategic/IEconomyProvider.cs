// IEconomyProvider.cs
// 经济提供者接口 — Core 层通过此接口访问金币和背包操作
// Frontend 层的 EconomyManager 负责实现并注册
namespace BladeHex.Strategic;

/// <summary>
/// 经济提供者接口 — 将金币操作从 Frontend 下放到 Core
/// </summary>
public interface IEconomyProvider
{
    /// <summary>当前金币</summary>
    int Gold { get; }

    /// <summary>当前天数（委托自 ITimeProvider）</summary>
    int DaysPassed { get; }

    /// <summary>增加金币</summary>
    void AddGold(int amount);

    /// <summary>扣除金币，返回是否成功</summary>
    bool SpendGold(int amount);
}

/// <summary>
/// 全局经济访问点 — 静态 ServiceLocator，Core 层自给自足
/// </summary>
public static class EconomyProvider
{
    private static IEconomyProvider? _instance;

    /// <summary>注册经济提供者（由 Frontend 在启动时调用）</summary>
    public static void Set(IEconomyProvider provider) => _instance = provider;

    /// <summary>取消注册</summary>
    public static void Clear() => _instance = null;

    /// <summary>当前金币 — 未注册时返回 0 作为安全后备</summary>
    public static int Gold => _instance?.Gold ?? 0;

    /// <summary>当前天数</summary>
    public static int DaysPassed => _instance?.DaysPassed ?? 1;

    /// <summary>增加金币</summary>
    public static void AddGold(int amount) => _instance?.AddGold(amount);

    /// <summary>扣除金币</summary>
    public static bool SpendGold(int amount) => _instance?.SpendGold(amount) ?? false;

    /// <summary>是否已注册</summary>
    public static bool IsRegistered => _instance != null;
}
