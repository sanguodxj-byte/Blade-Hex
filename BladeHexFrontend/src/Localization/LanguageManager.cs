using Godot;

namespace BladeHex.Localization;

/// <summary>
/// 语言管理器 - 作为 AutoLoad 单例运行
/// 负责启动时语言检测、玩家偏好持久化、运行时切换
/// 
/// 在 project.godot 中注册:
///   [autoload]
///   LanguageManager="*res://BladeHexFrontend/src/Localization/LanguageManager.cs"
/// </summary>
public partial class LanguageManager : Node
{
    public static LanguageManager Instance { get; private set; } = null!;

    private const string SettingsPath = "user://settings.cfg";
    private const string SectionName = "localization";
    private const string KeyLocale = "locale";

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    public static readonly string[] SupportedLocales = { "zh_CN", "en" };

    /// <summary>
    /// 语言显示名称映射
    /// </summary>
    public static readonly System.Collections.Generic.Dictionary<string, string> LocaleNames = new()
    {
        ["zh_CN"] = "简体中文",
        ["en"] = "English"
    };

    /// <summary>
    /// 当前语言变更时触发
    /// </summary>
    [Signal]
    public delegate void LocaleChangedEventHandler(string locale);

    public override void _Ready()
    {
        Instance = this;
        InitializeLocale();
    }

    /// <summary>
    /// 初始化语言：优先读取玩家设置，否则检测系统语言
    /// </summary>
    private void InitializeLocale()
    {
        string savedLocale = LoadSavedLocale();

        if (!string.IsNullOrEmpty(savedLocale) && IsSupported(savedLocale))
        {
            SetLocale(savedLocale, save: false);
        }
        else
        {
            // 检测系统语言，中文环境默认中文，否则英文
            string systemLocale = OS.GetLocale();
            string target = systemLocale.StartsWith("zh") ? "zh_CN" : "en";
            SetLocale(target, save: true);
        }
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    public void SetLocale(string locale, bool save = true)
    {
        if (!IsSupported(locale))
        {
            GD.PushWarning($"[LanguageManager] Unsupported locale: {locale}");
            return;
        }

        TranslationServer.SetLocale(locale);

        if (save)
            SaveLocale(locale);

        EmitSignal(SignalName.LocaleChanged, locale);
        GD.Print($"[LanguageManager] Locale set to: {locale}");
    }

    /// <summary>
    /// 获取当前语言
    /// </summary>
    public string GetLocale() => TranslationServer.GetLocale();

    /// <summary>
    /// 获取当前语言的显示名称
    /// </summary>
    public string GetCurrentLocaleName()
    {
        string locale = GetLocale();
        return LocaleNames.TryGetValue(locale, out string? name) ? name : locale;
    }

    /// <summary>
    /// 切换到下一个支持的语言
    /// </summary>
    public void CycleLocale()
    {
        string current = GetLocale();
        int index = System.Array.IndexOf(SupportedLocales, current);
        int next = (index + 1) % SupportedLocales.Length;
        SetLocale(SupportedLocales[next]);
    }

    private static bool IsSupported(string locale)
    {
        return System.Array.IndexOf(SupportedLocales, locale) >= 0;
    }

    private static string? LoadSavedLocale()
    {
        var config = new ConfigFile();
        Error err = config.Load(SettingsPath);
        if (err != Error.Ok)
            return null;
        return config.GetValue(SectionName, KeyLocale).AsString();
    }

    private static void SaveLocale(string locale)
    {
        var config = new ConfigFile();
        // 先加载已有配置（避免覆盖其他设置）
        config.Load(SettingsPath);
        config.SetValue(SectionName, KeyLocale, locale);
        config.Save(SettingsPath);
    }
}
