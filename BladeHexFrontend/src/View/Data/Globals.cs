using Godot;
using BladeHex.Audio;
using BladeHex.Debug;
using BladeHex.Events;
using BladeHex.Strategic;
using BladeHex.UI.Global;

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

    private static T? GetAutoloadOrNull<T>(string name) where T : Node
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        if (tree?.Root == null) return null;
        return tree.Root.GetNodeOrNull<T>(name);
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
    }

    private static GlobalState? _state;
    private static EventBus? _events;
    private static AudioManager? _audio;
    private static GameMenuManager? _gameMenu;
    private static SkillTreeManager? _skillTrees;
}
