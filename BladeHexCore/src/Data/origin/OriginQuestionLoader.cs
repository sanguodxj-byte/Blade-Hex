// OriginQuestionLoader.cs
// 加载 origin_questions.json — 服务于架构优化 spec R4。
//
// 设计目标：
//   1. 一次加载、多次访问（静态缓存）
//   2. 不依赖 Godot 节点树（纯静态方法）
//   3. 容错：JSON 缺失或解析失败时返回空数据 + 错误日志，不抛异常
using System;
using System.Text.Json;
using Godot;

namespace BladeHex.Data.Origin;

/// <summary>
/// 起源问答数据加载器 — 从 res:// 路径读取 JSON 并反序列化。
/// </summary>
public static class OriginQuestionLoader
{
    /// <summary>默认数据路径。</summary>
    public const string DefaultPath = "res://BladeHexCore/src/Data/origin/origin_questions.json";

    private static OriginQuestionData? _cached;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>加载（带缓存）。首次调用读 JSON，后续返回缓存。</summary>
    public static OriginQuestionData Load(string path = DefaultPath)
    {
        if (_cached != null) return _cached;

        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"[OriginQuestionLoader] 找不到 {path}");
                _cached = new OriginQuestionData();
                return _cached;
            }

            var json = file.GetAsText();
            file.Close();

            var data = JsonSerializer.Deserialize<OriginQuestionData>(json, JsonOpts);
            _cached = data ?? new OriginQuestionData();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[OriginQuestionLoader] 解析失败: {ex.Message}");
            _cached = new OriginQuestionData();
        }

        return _cached;
    }

    /// <summary>清空缓存（测试用 / 热重载用）。</summary>
    public static void ClearCache() => _cached = null;
}
