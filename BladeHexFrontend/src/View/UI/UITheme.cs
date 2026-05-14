// UITheme.cs
// UI主题系统 — 集中管理所有设计令牌（颜色、字号、间距、圆角等）
// 支持未来切换为图像UI：所有视觉属性通过此单例访问，替换时只需修改此处
// 对应策划案 09-UI设计.md 的设计原则：信息层级清晰、反馈即时、不暴露骰子
using Godot;
using System.Collections.Generic;

namespace BladeHex.UI;

/// <summary>
/// UI主题系统 — Autoload 单例，集中管理所有设计令牌
/// </summary>
[GlobalClass]
public partial class UITheme : Node
{
    // ============================================================================
    // 单例模式
    // ============================================================================
    public static UITheme? Instance { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this) { QueueFree(); return; }
        Instance = this;
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    // ============================================================================
    // 主题模式（预留：未来可切换明暗/图像主题）
    // ============================================================================
    public enum ThemeMode
    {
        ProceduralDark,  // 当前：程序化深色主题
        ImageBased,      // 未来：图像资源主题
    }

    public ThemeMode CurrentMode { get; set; } = ThemeMode.ProceduralDark;

    // ============================================================================
    // 调色板 — 核心色彩系统
    // ============================================================================

    // 背景色
    public Color BgPrimary { get; set; } = new Color(0.08f, 0.08f, 0.10f, 0.85f);
    public Color BgSecondary { get; set; } = new Color(0.12f, 0.12f, 0.14f, 0.80f);
    public Color BgTertiary { get; set; } = new Color(0.06f, 0.06f, 0.08f, 0.75f);
    public Color BgPanel { get; set; } = new Color(0.10f, 0.10f, 0.12f, 0.85f);
    public Color BgCard { get; set; } = new Color(0.15f, 0.14f, 0.18f, 0.75f);
    public Color BgCardHover { get; set; } = new Color(0.25f, 0.22f, 0.30f, 0.90f);
    public Color BgOverlay { get; set; } = new Color(0, 0, 0, 0.4f);
    public Color BgTooltip { get; set; } = new Color(0.06f, 0.05f, 0.09f, 0.95f);
    public Color BgPositive { get; set; } = new Color(0.1f, 0.25f, 0.1f, 0.85f);
    public Color BgNegative { get; set; } = new Color(0.25f, 0.1f, 0.1f, 0.85f);

    // 边框色
    public Color BorderDefault { get; set; } = new Color(0.3f, 0.3f, 0.35f, 0.6f);
    public Color BorderHighlight { get; set; } = new Color(0.5f, 0.45f, 0.3f, 0.8f);
    public Color BorderFriendly { get; set; } = new Color(0.2f, 0.5f, 0.8f, 0.8f);
    public Color BorderEnemy { get; set; } = new Color(0.6f, 0.2f, 0.2f, 0.8f);
    public Color BorderMagic { get; set; } = new Color(0.4f, 0.35f, 0.6f, 0.8f);

    // 文字色
    public Color TextPrimary { get; set; } = new Color(0.95f, 0.93f, 0.88f);
    public Color TextSecondary { get; set; } = new Color(0.7f, 0.68f, 0.63f);
    public Color TextMuted { get; set; } = new Color(0.5f, 0.48f, 0.45f);
    public Color TextAccent { get; set; } = new Color(0.9f, 0.8f, 0.5f);       // 金色强调
    public Color TextPositive { get; set; } = new Color(0.3f, 0.85f, 0.3f);
    public Color TextNegative { get; set; } = new Color(0.9f, 0.3f, 0.25f);
    public Color TextMagic { get; set; } = new Color(0.7f, 0.6f, 1.0f);
    public Color TextWarning { get; set; } = new Color(0.9f, 0.7f, 0.2f);

    // HP 条颜色
    public Color HpHigh { get; set; } = new Color(0.2f, 0.75f, 0.2f);
    public Color HpMid { get; set; } = new Color(0.85f, 0.75f, 0.1f);
    public Color HpLow { get; set; } = new Color(0.9f, 0.15f, 0.1f);
    public Color HpBarBg { get; set; } = new Color(0.15f, 0.08f, 0.08f, 0.7f);

    // 魔力条颜色
    public Color ManaFill { get; set; } = new Color(0.3f, 0.5f, 1.0f);
    public Color ManaBg { get; set; } = new Color(0.1f, 0.1f, 0.2f, 0.7f);

    // 士气颜色
    public Color MoraleHigh { get; set; } = new Color(0.2f, 0.8f, 0.9f);
    public Color MoraleNormal { get; set; } = new Color(0.6f, 0.6f, 0.6f);
    public Color MoraleLow { get; set; } = new Color(0.9f, 0.7f, 0.1f);
    public Color MoraleBroken { get; set; } = new Color(0.9f, 0.2f, 0.1f);
    public Color MoraleRouting { get; set; } = new Color(1.0f, 0.1f, 0.1f);

    // 经验条
    public Color XpFill { get; set; } = new Color(0.6f, 0.5f, 0.9f);
    public Color XpBg { get; set; } = new Color(0.1f, 0.08f, 0.15f, 0.7f);

    // 高亮色（六边形网格）
    public Color HighlightMove { get; set; } = new Color(0.2f, 0.6f, 1.0f, 0.4f);     // 蓝色-移动范围
    public Color HighlightAttack { get; set; } = new Color(1.0f, 0.2f, 0.2f, 0.4f);    // 红色-攻击范围
    public Color HighlightSpell { get; set; } = new Color(1.0f, 0.5f, 0.0f, 0.4f);     // 橙色-法术范围
    public Color HighlightSelect { get; set; } = new Color(1.0f, 0.9f, 0.2f, 0.5f);    // 黄色-选中
    public Color HighlightAoe { get; set; } = new Color(0.9f, 0.3f, 0.9f, 0.35f);      // 紫色-AOE
    public Color HighlightFriendly { get; set; } = new Color(0.2f, 0.8f, 0.4f, 0.3f);  // 绿色-友方范围

    // 稀有度颜色
    public Color RarityCommon { get; set; } = new Color(0.7f, 0.7f, 0.7f);
    public Color RarityUncommon { get; set; } = new Color(0.3f, 0.9f, 0.3f);
    public Color RarityRare { get; set; } = new Color(0.3f, 0.5f, 1.0f);
    public Color RarityEpic { get; set; } = new Color(0.7f, 0.3f, 0.9f);
    public Color RarityLegendary { get; set; } = new Color(1.0f, 0.7f, 0.2f);

    // 属性方向颜色（技能盘6区域）
    public Color RegionStr { get; set; } = new Color(0.9f, 0.3f, 0.25f);   // 力量-红
    public Color RegionDex { get; set; } = new Color(0.3f, 0.8f, 0.3f);    // 敏捷-绿
    public Color RegionCon { get; set; } = new Color(0.8f, 0.7f, 0.2f);    // 体质-黄
    public Color RegionInt { get; set; } = new Color(0.4f, 0.5f, 1.0f);    // 智力-蓝
    public Color RegionWis { get; set; } = new Color(0.3f, 0.8f, 0.8f);    // 感知-青
    public Color RegionCha { get; set; } = new Color(0.8f, 0.4f, 0.9f);    // 魅力-紫

    // 法术学派颜色
    public Color SchoolEvocation { get; set; } = new Color(1.0f, 0.4f, 0.2f);
    public Color SchoolAbjuration { get; set; } = new Color(0.4f, 0.6f, 1.0f);
    public Color SchoolIllusion { get; set; } = new Color(0.7f, 0.5f, 1.0f);
    public Color SchoolNecromancy { get; set; } = new Color(0.5f, 0.8f, 0.3f);
    public Color SchoolTransmutation { get; set; } = new Color(0.9f, 0.8f, 0.2f);
    public Color SchoolEnchantment { get; set; } = new Color(0.9f, 0.4f, 0.7f);
    public Color SchoolDivination { get; set; } = new Color(0.3f, 0.9f, 0.9f);
    public Color SchoolConjuration { get; set; } = new Color(0.6f, 0.4f, 0.2f);

    // 种族颜色
    public Color RaceHuman { get; set; } = new Color(0.85f, 0.8f, 0.7f);
    public Color RaceElf { get; set; } = new Color(0.5f, 0.9f, 0.6f);
    public Color RaceDwarf { get; set; } = new Color(0.8f, 0.65f, 0.3f);
    public Color RaceHalforc { get; set; } = new Color(0.7f, 0.35f, 0.3f);
    public Color RaceHalfelf { get; set; } = new Color(0.6f, 0.6f, 0.9f);

    // ============================================================================
    // 字号系统
    // ============================================================================

    public int FontSizeXs { get; set; } = 10;
    public int FontSizeSm { get; set; } = 12;
    public int FontSizeMd { get; set; } = 14;
    public int FontSizeLg { get; set; } = 16;
    public int FontSizeXl { get; set; } = 20;
    public int FontSizeXxl { get; set; } = 24;
    public int FontSizeTitle { get; set; } = 28;

    // ============================================================================
    // 间距系统
    // ============================================================================

    public int SpacingXs { get; set; } = 2;
    public int SpacingSm { get; set; } = 4;
    public int SpacingMd { get; set; } = 8;
    public int SpacingLg { get; set; } = 12;
    public int SpacingXl { get; set; } = 16;
    public int SpacingXxl { get; set; } = 24;
    public int SpacingXxxl { get; set; } = 32;

    // ============================================================================
    // 圆角
    // ============================================================================

    public int RadiusSm { get; set; } = 4;
    public int RadiusMd { get; set; } = 8;
    public int RadiusLg { get; set; } = 12;
    public int RadiusXl { get; set; } = 16;
    public int RadiusRound { get; set; } = 24;

    // ============================================================================
    // 尺寸规范
    // ============================================================================

    public int ButtonHeight { get; set; } = 36;
    public int ButtonHeightLg { get; set; } = 45;
    public int BarHeightSm { get; set; } = 8;
    public int BarHeightMd { get; set; } = 12;
    public int BarHeightLg { get; set; } = 16;
    public int IconSizeSm { get; set; } = 24;
    public int IconSizeMd { get; set; } = 32;
    public int IconSizeLg { get; set; } = 48;
    public int IconSizeXl { get; set; } = 64;
    public int PanelMinWidth { get; set; } = 220;
    public int PanelMinWidthLg { get; set; } = 320;
    public int PortraitSize { get; set; } = 80;

    // ============================================================================
    // 动画时长
    // ============================================================================

    public float AnimFast { get; set; } = 0.15f;
    public float AnimNormal { get; set; } = 0.25f;
    public float AnimSlow { get; set; } = 0.4f;
    public float AnimVerySlow { get; set; } = 0.6f;

    // ============================================================================
    // 图像资源占位（未来替换）
    // ============================================================================

    // 按钮图像
    public Texture2D? BtnNormalTexture { get; set; }
    public Texture2D? BtnHoverTexture { get; set; }
    public Texture2D? BtnPressedTexture { get; set; }
    public Texture2D? BtnDisabledTexture { get; set; }

    // 面板图像
    public Texture2D? PanelBgTexture { get; set; }
    public Texture2D? CardBgTexture { get; set; }
    public Texture2D? TooltipBgTexture { get; set; }

    // 图标图像
    public Texture2D? IconAtlas { get; set; }          // 图标图集（未来）
    public Texture2D? PortraitFrame { get; set; }      // 头像框（未来）

    // 技能盘图像
    public Texture2D? SkillNodeActiveTexture { get; set; }
    public Texture2D? SkillNodeInactiveTexture { get; set; }
    public Texture2D? SkillNodeLockedTexture { get; set; }

    // 地形图标
    public Texture2D? TerrainIconAtlas { get; set; }

    // ============================================================================
    // 样式缓存
    // ============================================================================

    private readonly Dictionary<string, StyleBoxFlat> _styleCache = new();

    // ============================================================================
    // 辅助方法
    // ============================================================================

    /// <summary>创建标准面板样式</summary>
    public StyleBoxFlat MakePanelStyle(Color? bg = null, Color? border = null,
        int borderWidth = 1, int cornerRadius = -1, int contentMargin = -1)
    {
        var bgColor = bg ?? BgPanel;
        var borderColor = border ?? BorderDefault;
        int cr = cornerRadius >= 0 ? cornerRadius : RadiusMd;
        int cm = contentMargin >= 0 ? contentMargin : SpacingMd;

        var cacheKey = $"panel_{bgColor.ToHtml()}_{borderColor.ToHtml()}_{borderWidth}_{cr}_{cm}";
        if (_styleCache.TryGetValue(cacheKey, out var cached)) return cached;

        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.SetBorderWidthAll(borderWidth);
        style.BorderColor = borderColor;
        style.SetCornerRadiusAll(cr);
        style.SetContentMarginAll(cm);
        _styleCache[cacheKey] = style;
        return style;
    }

    /// <summary>创建标准按钮样式集</summary>
    public ButtonStyleSet MakeButtonStyle(
        Color? bgNormal = null, Color? bgHover = null,
        Color? bgPressed = null, Color? bgDisabled = null,
        int cornerRadius = -1)
    {
        int cr = cornerRadius >= 0 ? cornerRadius : RadiusMd;
        return new ButtonStyleSet
        {
            Normal = MakeBtnStyle(bgNormal ?? new Color(0.18f, 0.17f, 0.22f), BorderDefault, cr),
            Hover = MakeBtnStyle(bgHover ?? new Color(0.28f, 0.26f, 0.34f), BorderHighlight, cr),
            Pressed = MakeBtnStyle(bgPressed ?? new Color(0.12f, 0.11f, 0.15f), BorderHighlight, cr),
            Disabled = MakeBtnStyle(bgDisabled ?? new Color(0.12f, 0.12f, 0.12f, 0.5f), new Color(0.2f, 0.2f, 0.2f, 0.3f), cr),
        };
    }

    private StyleBoxFlat MakeBtnStyle(Color bg, Color border, int cr)
    {
        var cacheKey = $"btn_{bg.ToHtml()}_{border.ToHtml()}_{cr}";
        if (_styleCache.TryGetValue(cacheKey, out var cached)) return cached;

        var s = new StyleBoxFlat();
        s.BgColor = bg;
        s.SetBorderWidthAll(1);
        s.BorderColor = border;
        s.SetCornerRadiusAll(cr);
        s.SetContentMarginAll(SpacingSm);
        _styleCache[cacheKey] = s;
        return s;
    }

    /// <summary>创建进度条样式集</summary>
    public BarStyleSet MakeBarStyle(Color fillColor, Color? bgColor = null, int cornerRadius = -1)
    {
        int cr = cornerRadius >= 0 ? cornerRadius : RadiusSm;
        return new BarStyleSet
        {
            Fill = MakeBarFill(fillColor, cr),
            Background = MakeBarBg(bgColor ?? new Color(0.1f, 0.1f, 0.12f), cr),
        };
    }

    private StyleBoxFlat MakeBarFill(Color color, int cr)
    {
        var cacheKey = $"bar_fill_{color.ToHtml()}_{cr}";
        if (_styleCache.TryGetValue(cacheKey, out var cached)) return cached;

        var s = new StyleBoxFlat();
        s.BgColor = color;
        s.SetCornerRadiusAll(cr);
        _styleCache[cacheKey] = s;
        return s;
    }

    private StyleBoxFlat MakeBarBg(Color color, int cr)
    {
        var cacheKey = $"bar_bg_{color.ToHtml()}_{cr}";
        if (_styleCache.TryGetValue(cacheKey, out var cached)) return cached;

        var s = new StyleBoxFlat();
        s.BgColor = color;
        s.SetCornerRadiusAll(cr);
        _styleCache[cacheKey] = s;
        return s;
    }

    // ============================================================================
    // 颜色查找方法
    // ============================================================================

    /// <summary>获取属性方向颜色（匹配 SkillNodeData.Region 枚举值）</summary>
    public Color GetRegionColor(BladeHex.Strategic.SkillNodeData.Region region)
    {
        return region switch
        {
            Strategic.SkillNodeData.Region.Str => RegionStr,
            Strategic.SkillNodeData.Region.Dex => RegionDex,
            Strategic.SkillNodeData.Region.Con => RegionCon,
            Strategic.SkillNodeData.Region.Int => RegionInt,
            Strategic.SkillNodeData.Region.Wis => RegionWis,
            Strategic.SkillNodeData.Region.Cha => RegionCha,
            _ => TextMuted, // None/Transition/其他
        };
    }

    /// <summary>获取HP颜色</summary>
    public Color GetHpColor(float ratio)
    {
        if (ratio > 0.6f) return HpHigh;
        if (ratio > 0.3f) return HpMid;
        return HpLow;
    }

    /// <summary>获取士气颜色</summary>
    public Color GetMoraleColor(BladeHex.Data.MoraleLevel level)
    {
        return level switch
        {
            Data.MoraleLevel.High => MoraleHigh,
            Data.MoraleLevel.Normal => MoraleNormal,
            Data.MoraleLevel.Low => MoraleLow,
            Data.MoraleLevel.Broken => MoraleBroken,
            Data.MoraleLevel.Routing => MoraleRouting,
            _ => MoraleNormal,
        };
    }

    /// <summary>获取法术学派颜色</summary>
    public Color GetSchoolColor(BladeHex.Data.SpellData.SpellSchool school)
    {
        return school switch
        {
            Data.SpellData.SpellSchool.Evocation => SchoolEvocation,
            Data.SpellData.SpellSchool.Abjuration => SchoolAbjuration,
            Data.SpellData.SpellSchool.Illusion => SchoolIllusion,
            Data.SpellData.SpellSchool.Necromancy => SchoolNecromancy,
            Data.SpellData.SpellSchool.Transmutation => SchoolTransmutation,
            Data.SpellData.SpellSchool.Enchantment => SchoolEnchantment,
            Data.SpellData.SpellSchool.Divination => SchoolDivination,
            Data.SpellData.SpellSchool.Conjuration => SchoolConjuration,
            _ => TextMuted,
        };
    }

    /// <summary>获取稀有度颜色</summary>
    public Color GetRarityColor(BladeHex.Data.ItemData.Rarity rarity)
    {
        return rarity switch
        {
            Data.ItemData.Rarity.Common => RarityCommon,
            Data.ItemData.Rarity.Uncommon => RarityUncommon,
            Data.ItemData.Rarity.Rare => RarityRare,
            Data.ItemData.Rarity.Epic => RarityEpic,
            Data.ItemData.Rarity.Legendary => RarityLegendary,
            _ => RarityCommon,
        };
    }

    /// <summary>获取种族颜色</summary>
    public Color GetRaceColor(string race)
    {
        return race.ToLowerInvariant() switch
        {
            "human" => RaceHuman,
            "elf" => RaceElf,
            "dwarf" => RaceDwarf,
            "halforc" => RaceHalforc,
            "halfelf" => RaceHalfelf,
            _ => TextMuted,
        };
    }

    // ============================================================================
    // 应用主题方法
    // ============================================================================

    /// <summary>应用按钮主题样式到按钮</summary>
    public void ApplyButtonTheme(Button btn, ButtonStyleSet? styles = null)
    {
        styles ??= MakeButtonStyle();
        if (btn.HasThemeStyleboxOverride("normal"))
            btn.RemoveThemeStyleboxOverride("normal");
        btn.AddThemeStyleboxOverride("normal", styles.Normal);
        btn.AddThemeStyleboxOverride("hover", styles.Hover);
        btn.AddThemeStyleboxOverride("pressed", styles.Pressed);
        btn.AddThemeStyleboxOverride("disabled", styles.Disabled);
        btn.AddThemeColorOverride("font_color", TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", TextAccent);
        btn.AddThemeColorOverride("font_pressed_color", TextSecondary);
        btn.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.4f, 0.4f));
    }

    /// <summary>应用进度条主题样式到进度条</summary>
    public void ApplyBarTheme(ProgressBar bar, Color fillColor, Color? bgColor = null)
    {
        var styles = MakeBarStyle(fillColor, bgColor ?? new Color(0.1f, 0.1f, 0.12f));
        bar.AddThemeStyleboxOverride("fill", styles.Fill);
        bar.AddThemeStyleboxOverride("background", styles.Background);
    }

    // ============================================================================
    // 辅助类型
    // ============================================================================

    /// <summary>按钮样式集</summary>
    public class ButtonStyleSet
    {
        public StyleBoxFlat Normal { get; set; } = new();
        public StyleBoxFlat Hover { get; set; } = new();
        public StyleBoxFlat Pressed { get; set; } = new();
        public StyleBoxFlat Disabled { get; set; } = new();
    }

    /// <summary>进度条样式集</summary>
    public class BarStyleSet
    {
        public StyleBoxFlat Fill { get; set; } = new();
        public StyleBoxFlat Background { get; set; } = new();
    }
}
