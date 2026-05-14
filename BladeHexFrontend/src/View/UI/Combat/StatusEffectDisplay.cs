// StatusEffectDisplay.cs
// 状态效果显示 — 在底部面板显示单位当前所有活跃状态效果图标
// 对应策划案 03-战术战斗系统 → 七、战斗状态效果
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.UI.Combat;

/// <summary>
/// 状态效果显示 — 在 HBoxContainer 中以缩略标签展示所有活跃状态效果。
/// 每个效果显示为 "[短名N]" 格式，自带颜色编码和 tooltip。
/// </summary>
[GlobalClass]
public partial class StatusEffectDisplay : HBoxContainer
{
    // ============================================================================
    // 常量
    // ============================================================================

    /// <summary>
    /// 状态效果颜色映射 — 根据效果ID分配颜色
    /// </summary>
    private static readonly Dictionary<string, Color> EFFECT_COLORS = new()
    {
        { "poison",      new Color(0.3f, 0.8f, 0.2f) },
        { "burning",     new Color(1.0f, 0.4f, 0.1f) },
        { "freeze",      new Color(0.3f, 0.6f, 1.0f) },
        { "fear",        new Color(0.6f, 0.2f, 0.7f) },
        { "silence",     new Color(0.5f, 0.5f, 0.5f) },
        { "blind",       new Color(0.3f, 0.3f, 0.3f) },
        { "stun",        new Color(1.0f, 1.0f, 0.2f) },
        { "bleed",       new Color(0.8f, 0.1f, 0.1f) },
        { "slow",        new Color(0.4f, 0.6f, 0.8f) },
        { "root",        new Color(0.5f, 0.3f, 0.1f) },
        { "wet",         new Color(0.3f, 0.5f, 0.9f) },
        { "bless",       new Color(1.0f, 0.9f, 0.3f) },
        { "shield",      new Color(0.3f, 0.6f, 1.0f) },
        { "haste",       new Color(0.9f, 0.9f, 0.2f) },
        { "regen",       new Color(0.2f, 0.9f, 0.3f) },
        { "invisibility",new Color(0.7f, 0.7f, 1.0f) },
        { "phantom",     new Color(0.7f, 0.5f, 1.0f) },
        { "temp_hp",     new Color(0.5f, 0.8f, 0.9f) },
    };

    /// <summary>
    /// 状态效果显示名映射 — 每个效果ID对应的1字中文简称
    /// </summary>
    private static readonly Dictionary<string, string> EFFECT_NAMES = new()
    {
        { "poison", "毒" }, { "burning", "火" }, { "freeze", "冰" }, { "fear", "惧" },
        { "silence", "默" }, { "blind", "盲" }, { "stun", "晕" }, { "bleed", "血" },
        { "slow", "慢" }, { "root", "缚" }, { "wet", "湿" },
        { "bless", "祝" }, { "shield", "盾" }, { "haste", "速" }, { "regen", "愈" },
        { "invisibility", "隐" }, { "phantom", "幻" }, { "temp_hp", "护" },
    };

    // ============================================================================
    // 内部
    // ============================================================================

    private readonly List<Label> _effectLabels = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 2);
    }

    // ============================================================================
    // 更新显示
    // ============================================================================

    /// <summary>
    /// 刷新状态效果显示。
    /// 清除所有现有标签并根据传入的效果数据重建。
    /// </summary>
    /// <param name="activeEffects">
    /// 活跃状态效果数组。每个条目为 Dictionary，包含键：
    ///   "id" (string), "duration" (int), "is_negative" (bool), "name" (string)
    /// </param>
    public void UpdateEffects(Godot.Collections.Array<Godot.Collections.Dictionary> activeEffects)
    {
        // 清除旧的
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        _effectLabels.Clear();

        foreach (var effect in activeEffects)
        {
            string eid = effect.GetValueOrDefault("id", "").AsString();
            int duration = effect.GetValueOrDefault("duration", 0).AsInt32();
            bool isNegative = effect.GetValueOrDefault("is_negative", true).AsBool();

            var lbl = new Label();

            // 短名：查找 EFFECT_NAMES 映射，找不到则取 ID 首字符
            string nameStr = EFFECT_NAMES.GetValueOrDefault(eid, eid.Length > 0 ? eid[..1] : "?");
            Color color = EFFECT_COLORS.GetValueOrDefault(eid, Colors.White);

            lbl.Text = $"[{nameStr}{duration}]";
            lbl.AddThemeFontSizeOverride("font_size", 12);

            // 按 is_negative 着色（正/负都使用 map 颜色）
            lbl.AddThemeColorOverride("font_color", color);

            // tooltip
            string effectName = effect.GetValueOrDefault("name", eid).AsString();
            lbl.TooltipText = $"{effectName} ({duration}回合)";

            AddChild(lbl);
            _effectLabels.Add(lbl);
        }
    }
}
