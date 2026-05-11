using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 状态效果图标系统 - 提供状态效果的元数据和样式
/// 迁移自 GDScript StatusEffectIcon.gd
/// </summary>
public static class StatusEffectIcon
{
    public struct StatusDef
    {
        public string Icon;
        public string Text;
        public Color Color;
        public string Desc;
        public string Category; // "positive" or "negative"
    }

    public static readonly Dictionary<string, StatusDef> StatusDefs = new()
    {
        { "poison", new StatusDef { Icon = "☠", Text = "中毒", Color = new Color(0.4f, 0.85f, 0.2f), Desc = "每回合受到伤害", Category = "negative" } },
        { "burning", new StatusDef { Icon = "🔥", Text = "燃烧", Color = new Color(0.95f, 0.5f, 0.1f), Desc = "每回合受到火焰伤害", Category = "negative" } },
        { "frozen", new StatusDef { Icon = "❄", Text = "冰冻", Color = new Color(0.3f, 0.7f, 0.95f), Desc = "无法行动", Category = "negative" } },
        { "fear", new StatusDef { Icon = "😱", Text = "恐惧", Color = new Color(0.7f, 0.3f, 0.8f), Desc = "强制远离恐惧源", Category = "negative" } },
        { "silence", new StatusDef { Icon = "🤐", Text = "沉默", Color = new Color(0.6f, 0.6f, 0.6f), Desc = "无法施放法术", Category = "negative" } },
        { "stun", new StatusDef { Icon = "💫", Text = "眩晕", Color = new Color(0.95f, 0.85f, 0.2f), Desc = "行动受限", Category = "negative" } },
        { "bless", new StatusDef { Icon = "✨", Text = "祝福", Color = new Color(0.95f, 0.9f, 0.4f), Desc = "能力增强", Category = "positive" } },
        { "shield", new StatusDef { Icon = "🛡", Text = "护盾", Color = new Color(0.3f, 0.6f, 0.95f), Desc = "防御增强", Category = "positive" } }
    };

    public static Color GetEffectColor(string id) => StatusDefs.TryGetValue(id, out var def) ? def.Color : Colors.White;
    public static string GetEffectIcon(string id) => StatusDefs.TryGetValue(id, out var def) ? def.Icon : "?";
}

/// <summary>
/// 状态效果显示面板 — 在 UI 中显示单位当前所有活跃状态图标
/// 迁移自 GDScript StatusEffectDisplay.gd
/// </summary>
public partial class StatusEffectDisplay : HBoxContainer
{
    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);
    }

    public void UpdateEffects(Godot.Collections.Array<Godot.Collections.Dictionary> activeEffects)
    {
        foreach (var child in GetChildren()) child.QueueFree();

        foreach (var effect in activeEffects)
        {
            string id = effect.GetValueOrDefault("id", "").AsString();
            int duration = effect.GetValueOrDefault("duration", 0).AsInt32();
            
            var panel = new PanelContainer();
            var color = StatusEffectIcon.GetEffectColor(id);
            var icon = StatusEffectIcon.GetEffectIcon(id);
            
            var style = UITheme.Instance.MakePanelStyle(new Color(0.1f, 0.1f, 0.15f, 0.7f), color, 1, 2);
            panel.AddThemeStyleboxOverride("panel", style);
            
            var label = new Label
            {
                Text = $"{icon} {duration}",
                TooltipText = $"{id} ({duration}回合)"
            };
            label.AddThemeFontSizeOverride("font_size", 10);
            label.AddThemeColorOverride("font_color", color);
            
            panel.AddChild(label);
            AddChild(panel);
        }
    }
}
