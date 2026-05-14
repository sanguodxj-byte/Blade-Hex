// StatusEffectIcon.cs
// 状态效果图标系统 — 管理战斗中的状态效果显示
// 负面状态和正面状态的图标、颜色、持续时间可视化
// 不使用骰子术语，用直观描述
using Godot;
using System.Collections.Generic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 状态效果图标系统 — 静态工具类，管理战斗中的状态效果显示。
/// 提供 2D UI 面板图标列表、3D Label 显示文本、正/负效果筛选等功能。
/// </summary>
[GlobalClass]
public partial class StatusEffectIcon : Node
{
    /// <summary>
    /// 单个状态效果的定义数据
    /// </summary>
    public class StatusEffectDef
    {
        public string Icon { get; set; } = "";
        public string Text { get; set; } = "";
        public Color Color { get; set; } = Colors.White;
        public string Desc { get; set; } = "";
        public string Category { get; set; } = "negative";
    }

    /// <summary>
    /// 状态效果定义映射 — key 为效果 ID
    /// </summary>
    private static readonly Dictionary<string, StatusEffectDef> STATUS_DEFS = new()
    {
        // === 负面状态 ===
        { "poison",  new StatusEffectDef { Icon = "☠", Text = "中毒",   Color = new Color(0.4f, 0.85f, 0.2f), Desc = "每回合受到伤害",                         Category = "negative" } },
        { "burning", new StatusEffectDef { Icon = "🔥", Text = "燃烧",   Color = new Color(0.95f, 0.5f, 0.1f), Desc = "每回合受到火焰伤害，可蔓延",           Category = "negative" } },
        { "frozen",  new StatusEffectDef { Icon = "❄", Text = "冰冻",   Color = new Color(0.3f, 0.7f, 0.95f), Desc = "无法行动",                             Category = "negative" } },
        { "fear",    new StatusEffectDef { Icon = "😱", Text = "恐惧",   Color = new Color(0.7f, 0.3f, 0.8f), Desc = "强制远离恐惧源",                       Category = "negative" } },
        { "silence", new StatusEffectDef { Icon = "🤐", Text = "沉默",   Color = new Color(0.6f, 0.6f, 0.6f), Desc = "无法施放法术",                         Category = "negative" } },
        { "blind",   new StatusEffectDef { Icon = "🕶", Text = "致盲",   Color = new Color(0.5f, 0.5f, 0.5f), Desc = "近战困难，远程失效",                   Category = "negative" } },
        { "stun",    new StatusEffectDef { Icon = "💫", Text = "眩晕",   Color = new Color(0.95f, 0.85f, 0.2f), Desc = "只能移动或攻击(二选一)",              Category = "negative" } },
        { "bleed",   new StatusEffectDef { Icon = "🩸", Text = "流血",   Color = new Color(0.85f, 0.1f, 0.1f), Desc = "每回合受到伤害，可叠加",              Category = "negative" } },
        { "slow",    new StatusEffectDef { Icon = "🐌", Text = "减速",   Color = new Color(0.4f, 0.6f, 0.8f), Desc = "移动速度降低",                         Category = "negative" } },
        { "root",    new StatusEffectDef { Icon = "🔗", Text = "缚足",   Color = new Color(0.7f, 0.5f, 0.3f), Desc = "无法移动",                             Category = "negative" } },
        // === 正面状态 ===
        { "bless",    new StatusEffectDef { Icon = "✨", Text = "祝福",   Color = new Color(0.95f, 0.9f, 0.4f), Desc = "攻击和豁免增强",                       Category = "positive" } },
        { "shield",   new StatusEffectDef { Icon = "🛡", Text = "护盾",   Color = new Color(0.3f, 0.6f, 0.95f), Desc = "护甲大幅提升",                        Category = "positive" } },
        { "haste",    new StatusEffectDef { Icon = "⚡", Text = "加速",   Color = new Color(0.9f, 0.85f, 0.2f), Desc = "移动力提升，额外行动",                Category = "positive" } },
        { "regen",    new StatusEffectDef { Icon = "💚", Text = "再生",   Color = new Color(0.2f, 0.85f, 0.3f), Desc = "每回合恢复生命",                      Category = "positive" } },
        { "invisible",new StatusEffectDef { Icon = "👁", Text = "隐身",   Color = new Color(0.6f, 0.6f, 0.9f), Desc = "不可被直接瞄准",                       Category = "positive" } },
        { "temp_hp",  new StatusEffectDef { Icon = "💎", Text = "临时HP", Color = new Color(0.5f, 0.8f, 0.9f), Desc = "额外生命值层",                        Category = "positive" } },
    };

    // ============================================================================
    // 静态方法
    // ============================================================================

    /// <summary>
    /// 创建2D状态效果图标列表（用于UI面板）。
    /// 为每个效果创建带颜色编码的面板容器，添加到父控件并返回容器。
    /// </summary>
    /// <param name="parent">父控件，创建的容器将添加为其子节点</param>
    /// <param name="effects">效果ID列表</param>
    /// <param name="horizontal">是否水平排列（默认 true；false 时仍创建 HBoxContainer）</param>
    /// <returns>创建的 HBoxContainer</returns>
    public static HBoxContainer CreateStatusBar(Control parent, Godot.Collections.Array<string> effects, bool horizontal = true)
    {
        var container = new HBoxContainer();
        container.AddThemeConstantOverride("separation", 3);

        foreach (string effectKey in effects)
        {
            if (!STATUS_DEFS.TryGetValue(effectKey, out var def))
                continue;

            var iconContainer = new PanelContainer();
            var style = new StyleBoxFlat();

            if (def.Category == "negative")
            {
                style.BgColor = new Color(0.2f, 0.08f, 0.08f, 0.7f);
                style.BorderColor = new Color(def.Color.R, def.Color.G, def.Color.B, 0.6f);
            }
            else
            {
                style.BgColor = new Color(0.08f, 0.12f, 0.08f, 0.7f);
                style.BorderColor = new Color(def.Color.R, def.Color.G, def.Color.B, 0.6f);
            }

            style.SetBorderWidthAll(1);
            style.SetCornerRadiusAll(3);
            style.SetContentMarginAll(3);
            iconContainer.AddThemeStyleboxOverride("panel", style);

            var label = new Label();
            label.Text = $"{def.Icon} {def.Text}";
            label.AddThemeFontSizeOverride("font_size", 10);
            label.AddThemeColorOverride("font_color", def.Color);
            iconContainer.AddChild(label);

            // Tooltip
            iconContainer.TooltipText = def.Desc;
            container.AddChild(iconContainer);
        }

        parent.AddChild(container);
        return container;
    }

    /// <summary>
    /// 获取状态效果的3D显示数据（用于 Label3D）。
    /// </summary>
    /// <param name="effectKey">效果ID</param>
    /// <returns>Dictionary 包含 "text", "color", "desc", "category"</returns>
    public static Godot.Collections.Dictionary Get3DDisplay(string effectKey)
    {
        if (!STATUS_DEFS.TryGetValue(effectKey, out var def))
        {
            return new Godot.Collections.Dictionary
            {
                { "text", "?" },
                { "color", Colors.Gray },
                { "desc", "" },
                { "category", "" },
            };
        }

        return new Godot.Collections.Dictionary
        {
            { "text", def.Icon + def.Text },
            { "color", def.Color },
            { "desc", def.Desc },
            { "category", def.Category },
        };
    }

    /// <summary>
    /// 获取所有负面状态 key 列表
    /// </summary>
    public static List<string> GetNegativeEffects()
    {
        var result = new List<string>();
        foreach (var kvp in STATUS_DEFS)
        {
            if (kvp.Value.Category == "negative")
                result.Add(kvp.Key);
        }
        return result;
    }

    /// <summary>
    /// 获取所有正面状态 key 列表
    /// </summary>
    public static List<string> GetPositiveEffects()
    {
        var result = new List<string>();
        foreach (var kvp in STATUS_DEFS)
        {
            if (kvp.Value.Category == "positive")
                result.Add(kvp.Key);
        }
        return result;
    }
}
