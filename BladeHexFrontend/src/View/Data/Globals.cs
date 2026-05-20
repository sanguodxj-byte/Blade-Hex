using Godot;
using BladeHex.Audio;
using BladeHex.Debug;
using BladeHex.Events;
using BladeHex.Strategic;
using BladeHex.UI.Global;
using BladeHex.View.Environment;

namespace BladeHex.Data;

/// <summary>
/// Autoload 单例的统一访问入口。
///
/// 设计目的：
/// - 消除散落在代码各处的 <c>GetNode&lt;T&gt;("/root/Xxx")</c> 字符串路径硬编码
/// - 在一处集中管理 Autoload 名称，便于重命名和测试替换
///
/// 使用约定：
/// <code>
/// // 旧写法
/// var gs = GetNode&lt;GlobalState&gt;("/root/GlobalState");
/// // 新写法
/// var gs = Globals.State;
/// </code>
///
/// 何时不使用此类：
/// - 类本身不应当持有静态状态时（直接通过节点树父级查找更合适）
/// - 测试代码（参见 <see cref="ResetForTests"/>）
/// </summary>
public static class Globals
{
    /// <summary>跨场景全局状态聚合根。</summary>
    public static GlobalState State => _state ??= GetAutoload<GlobalState>("GlobalState");

    /// <summary>全局事件总线。</summary>
    public static EventBus Events => _events ??= GetAutoload<EventBus>("EventBus");

    /// <summary>音频管理器。</summary>
    public static AudioManager Audio => _audio ??= GetAutoload<AudioManager>("AudioManager");

    /// <summary>全局系统菜单（ESC 菜单 + 设置）。</summary>
    public static GameMenuManager GameMenu => _gameMenu ??= GetAutoload<GameMenuManager>("GameMenuManager");

    /// <summary>调试控制台（开发期工具，可能为 null）。</summary>
    public static DebugConsole? DebugConsole => GetAutoloadOrNull<DebugConsole>("DebugConsole");

    /// <summary>角色技能盘进度管理器（跨场景持久）。</summary>
    public static SkillTreeManager SkillTrees => _skillTrees ??= GetAutoload<SkillTreeManager>("SkillTreeManager");

    /// <summary><see cref="SkillTrees"/> 的容错版本。</summary>
    public static SkillTreeManager? SkillTreesOrNull => GetAutoloadOrNull<SkillTreeManager>("SkillTreeManager");

    /// <summary>跨场景共享的天气状态机（大地图与战斗共用一份实例）。</summary>
    public static WeatherManager Weather
    {
        get
        {
            if (_weather != null && !GodotObject.IsInstanceValid(_weather))
                _weather = null;
            return _weather ??= GetOrCreateWeatherManager()
                ?? throw new System.InvalidOperationException("WeatherManager 创建失败 — SceneTree 不可用");
        }
    }

    /// <summary><see cref="Weather"/> 的容错版本。
    /// autoload 注册失败时会**自动 lazy-create**一个 WeatherManager 挂到 root，避免 Node not found 异常。</summary>
    public static WeatherManager? WeatherOrNull
    {
        get
        {
            if (_weather != null && !GodotObject.IsInstanceValid(_weather))
                _weather = null;
            return _weather ??= GetOrCreateWeatherManager();
        }
    }

    /// <summary><see cref="State"/> 的容错版本：autoload 不存在时返回 null（启动早期场景测试用）。</summary>
    public static GlobalState? StateOrNull => GetAutoloadOrNull<GlobalState>("GlobalState");

    /// <summary><see cref="Audio"/> 的容错版本。</summary>
    public static AudioManager? AudioOrNull => GetAutoloadOrNull<AudioManager>("AudioManager");

    /// <summary><see cref="GameMenu"/> 的容错版本。</summary>
    public static GameMenuManager? GameMenuOrNull => GetAutoloadOrNull<GameMenuManager>("GameMenuManager");

    /// <summary><see cref="Events"/> 的容错版本。</summary>
    public static EventBus? EventsOrNull => GetAutoloadOrNull<EventBus>("EventBus");

    private static T GetAutoload<T>(string name) where T : Node
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        return tree.Root.GetNode<T>(name);
    }

    /// <summary>
    /// 获取或创建 WeatherManager 单例。
    /// 优先按**类型**遍历 root 子节点查找已存在的 autoload 实例（节点名可能因 Godot 命名约定与 autoload 注册名不一致）；
    /// 如果真的找不到则**自动创建并立即挂到 root**。
    /// 这样代码可以无视 autoload 节点名状态正常工作。
    /// </summary>
    private static WeatherManager? GetOrCreateWeatherManager()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        if (tree?.Root == null) return null;

        // 优先按字面名查找（autoload 默认名字）
        var byName = tree.Root.GetNodeOrNull<WeatherManager>("WeatherManager");
        if (byName != null) return byName;

        // 回退：按类型遍历 root 子节点查找（应付 Godot autoload 命名约定差异）
        foreach (var child in tree.Root.GetChildren())
        {
            if (child is WeatherManager wm)
            {
                GD.Print($"[Globals] WeatherManager 在 root 下找到（节点名='{child.Name}'，非默认 'WeatherManager'）");
                return wm;
            }
        }

        // Lazy-create fallback：autoload 真没注册成功时手动创建并立即挂到 root
        GD.Print("[Globals] WeatherManager autoload 未注册，使用 lazy-create fallback 实例化");
        var fallback = new WeatherManager { Name = "WeatherManager" };
        tree.Root.AddChild(fallback);
        return fallback;
    }

    private static T? GetAutoloadOrNull<T>(string name) where T : Node
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        if (tree?.Root == null) return null;
        var node = tree.Root.GetNodeOrNull<T>(name);
        return node != null && GodotObject.IsInstanceValid(node) ? node : null;
    }

    /// <summary>
    /// 测试钩子：清空缓存以便测试切换不同的 SceneTree。
    /// 仅供 <c>BladeHexCore.tests</c> 调用。
    /// </summary>
    internal static void ResetForTests()
    {
        _state = null;
        _events = null;
        _audio = null;
        _gameMenu = null;
        _skillTrees = null;
        _weather = null;
    }

    private static GlobalState? _state;
    private static EventBus? _events;
    private static AudioManager? _audio;
    private static GameMenuManager? _gameMenu;
    private static SkillTreeManager? _skillTrees;
    private static WeatherManager? _weather;
}
