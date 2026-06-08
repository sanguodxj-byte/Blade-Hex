using Godot;

namespace BladeHex.Localization;

/// <summary>
/// 本地化工具类 - 封装 TranslationServer 调用
/// 用法: L10n.Tr("KEY") 或 L10n.Tr("KEY", arg1, arg2)
/// </summary>
public static class L10n
{
    /// <summary>
    /// 获取翻译文本
    /// </summary>
    public static string Tr(string key)
    {
        return TranslationServer.Translate(key);
    }

    /// <summary>
    /// 获取带参数的翻译文本
    /// CSV 中使用 {0}, {1} 等占位符
    /// </summary>
    public static string Tr(string key, params object[] args)
    {
        string translated = TranslationServer.Translate(key);
        if (args == null || args.Length == 0)
            return translated;
        return string.Format(translated, args);
    }

    /// <summary>
    /// 获取当前语言代码 (如 "zh_CN", "en")
    /// </summary>
    public static string CurrentLocale => TranslationServer.GetLocale();

    /// <summary>
    /// 检查某个 key 是否有对应翻译
    /// </summary>
    public static bool HasTranslation(string key)
    {
        // Translate 返回原 key 表示未找到翻译
        return TranslationServer.Translate(key) != key;
    }
}
