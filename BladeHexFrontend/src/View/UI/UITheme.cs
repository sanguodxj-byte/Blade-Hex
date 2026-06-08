using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.UI;

/// <summary>
/// Global UI theme tokens and reusable Godot style resources.
/// </summary>
[GlobalClass]
public partial class UITheme : Node
{
    public static UITheme? Instance { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }

        Instance = this;
        LoadCombatTextures();
        LoadOverworldTextures();
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public enum ThemeMode
    {
        ProceduralDark,
        ImageBased,
    }

    public ThemeMode CurrentMode { get; set; } = ThemeMode.ProceduralDark;

    public Color BgPrimary { get; set; } = new(0.08f, 0.08f, 0.10f, 0.85f);
    public Color BgSecondary { get; set; } = new(0.12f, 0.12f, 0.14f, 0.80f);
    public Color BgTertiary { get; set; } = new(0.06f, 0.06f, 0.08f, 0.75f);
    public Color BgPanel { get; set; } = new(0.10f, 0.10f, 0.12f, 0.85f);
    public Color BgCard { get; set; } = new(0.15f, 0.14f, 0.18f, 0.75f);
    public Color BgCardHover { get; set; } = new(0.25f, 0.22f, 0.30f, 0.90f);
    public Color BgOverlay { get; set; } = new(0f, 0f, 0f, 0.4f);
    public Color BgTooltip { get; set; } = new(0.06f, 0.05f, 0.09f, 0.95f);
    public Color BgPositive { get; set; } = new(0.1f, 0.25f, 0.1f, 0.85f);
    public Color BgNegative { get; set; } = new(0.25f, 0.1f, 0.1f, 0.85f);

    public Color BorderDefault { get; set; } = new(0.3f, 0.3f, 0.35f, 0.6f);
    public Color BorderHighlight { get; set; } = new(0.5f, 0.45f, 0.3f, 0.8f);
    public Color BorderFriendly { get; set; } = new(0.2f, 0.5f, 0.8f, 0.8f);
    public Color BorderEnemy { get; set; } = new(0.6f, 0.2f, 0.2f, 0.8f);
    public Color BorderMagic { get; set; } = new(0.4f, 0.35f, 0.6f, 0.8f);

    public Color TextPrimary { get; set; } = new(0.95f, 0.93f, 0.88f);
    public Color TextSecondary { get; set; } = new(0.7f, 0.68f, 0.63f);
    public Color TextMuted { get; set; } = new(0.5f, 0.48f, 0.45f);
    public Color TextAccent { get; set; } = new(0.9f, 0.8f, 0.5f);
    public Color TextPositive { get; set; } = new(0.3f, 0.85f, 0.3f);
    public Color TextNegative { get; set; } = new(0.9f, 0.3f, 0.25f);
    public Color TextMagic { get; set; } = new(0.7f, 0.6f, 1.0f);
    public Color TextWarning { get; set; } = new(0.9f, 0.7f, 0.2f);

    public Color HpHigh { get; set; } = new(0.2f, 0.75f, 0.2f);
    public Color HpMid { get; set; } = new(0.85f, 0.75f, 0.1f);
    public Color HpLow { get; set; } = new(0.9f, 0.15f, 0.1f);
    public Color HpBarBg { get; set; } = new(0.15f, 0.08f, 0.08f, 0.7f);
    public Color ManaFill { get; set; } = new(0.3f, 0.5f, 1.0f);
    public Color ManaBg { get; set; } = new(0.1f, 0.1f, 0.2f, 0.7f);
    public Color XpFill { get; set; } = new(0.6f, 0.5f, 0.9f);
    public Color XpBg { get; set; } = new(0.1f, 0.08f, 0.15f, 0.7f);

    public Color HighlightMove { get; set; } = new(0.2f, 0.6f, 1.0f, 0.4f);
    public Color HighlightAttack { get; set; } = new(1.0f, 0.2f, 0.2f, 0.4f);
    public Color HighlightSpell { get; set; } = new(1.0f, 0.5f, 0.0f, 0.4f);
    public Color HighlightSelect { get; set; } = new(1.0f, 0.9f, 0.2f, 0.5f);
    public Color HighlightAoe { get; set; } = new(0.9f, 0.3f, 0.9f, 0.35f);
    public Color HighlightFriendly { get; set; } = new(0.2f, 0.8f, 0.4f, 0.3f);

    public Color RarityCommon { get; set; } = new(0.7f, 0.7f, 0.7f);
    public Color RarityUncommon { get; set; } = new(0.3f, 0.9f, 0.3f);
    public Color RarityRare { get; set; } = new(0.3f, 0.5f, 1.0f);
    public Color RarityEpic { get; set; } = new(0.7f, 0.3f, 0.9f);
    public Color RarityLegendary { get; set; } = new(1.0f, 0.7f, 0.2f);

    public Color RegionStr { get; set; } = new(0.9f, 0.3f, 0.25f);
    public Color RegionDex { get; set; } = new(0.3f, 0.8f, 0.3f);
    public Color RegionCon { get; set; } = new(0.8f, 0.7f, 0.2f);
    public Color RegionInt { get; set; } = new(0.4f, 0.5f, 1.0f);
    public Color RegionWis { get; set; } = new(0.3f, 0.8f, 0.8f);
    public Color RegionCha { get; set; } = new(0.8f, 0.4f, 0.9f);

    public Color SchoolEvocation { get; set; } = new(1.0f, 0.4f, 0.2f);
    public Color SchoolAbjuration { get; set; } = new(0.4f, 0.6f, 1.0f);
    public Color SchoolIllusion { get; set; } = new(0.7f, 0.5f, 1.0f);
    public Color SchoolNecromancy { get; set; } = new(0.5f, 0.8f, 0.3f);
    public Color SchoolTransmutation { get; set; } = new(0.9f, 0.8f, 0.2f);
    public Color SchoolEnchantment { get; set; } = new(0.9f, 0.4f, 0.7f);
    public Color SchoolDivination { get; set; } = new(0.3f, 0.9f, 0.9f);
    public Color SchoolConjuration { get; set; } = new(0.6f, 0.4f, 0.2f);

    public Color RaceHuman { get; set; } = new(0.85f, 0.8f, 0.7f);
    public Color RaceElf { get; set; } = new(0.5f, 0.9f, 0.6f);
    public Color RaceDwarf { get; set; } = new(0.8f, 0.65f, 0.3f);
    public Color RaceHalforc { get; set; } = new(0.7f, 0.35f, 0.3f);
    public Color RaceHalfelf { get; set; } = new(0.6f, 0.6f, 0.9f);

    public int FontSizeXs { get; set; } = 10;
    public int FontSizeSm { get; set; } = 12;
    public int FontSizeMd { get; set; } = 14;
    public int FontSizeLg { get; set; } = 16;
    public int FontSizeXl { get; set; } = 20;
    public int FontSizeXxl { get; set; } = 24;
    public int FontSizeTitle { get; set; } = 28;

    public int SpacingXs { get; set; } = 2;
    public int SpacingSm { get; set; } = 4;
    public int SpacingMd { get; set; } = 8;
    public int SpacingLg { get; set; } = 12;
    public int SpacingXl { get; set; } = 16;
    public int SpacingXxl { get; set; } = 24;
    public int SpacingXxxl { get; set; } = 32;

    public int RadiusSm { get; set; } = 4;
    public int RadiusMd { get; set; } = 8;
    public int RadiusLg { get; set; } = 12;
    public int RadiusXl { get; set; } = 16;
    public int RadiusRound { get; set; } = 24;

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

    public float AnimFast { get; set; } = 0.15f;
    public float AnimNormal { get; set; } = 0.25f;
    public float AnimSlow { get; set; } = 0.4f;
    public float AnimVerySlow { get; set; } = 0.6f;

    public Texture2D? BtnNormalTexture { get; set; }
    public Texture2D? BtnHoverTexture { get; set; }
    public Texture2D? BtnPressedTexture { get; set; }
    public Texture2D? BtnDisabledTexture { get; set; }
    public Texture2D? PanelBgTexture { get; set; }
    public Texture2D? CardBgTexture { get; set; }
    public Texture2D? TooltipBgTexture { get; set; }
    public Texture2D? IconAtlas { get; set; }
    public Texture2D? PortraitFrame { get; set; }
    public Texture2D? SkillNodeActiveTexture { get; set; }
    public Texture2D? SkillNodeInactiveTexture { get; set; }
    public Texture2D? SkillNodeLockedTexture { get; set; }
    public Texture2D? TerrainIconAtlas { get; set; }

    public Texture2D? CombatPowerBarBg { get; private set; }
    public Texture2D? CombatPowerBarForceFriendly { get; private set; }
    public Texture2D? CombatPowerBarForceEnemy { get; private set; }
    public Texture2D? CombatTurnOrderBg { get; private set; }
    public Texture2D? CombatTurnOrderActiveFrame { get; private set; }
    public Texture2D? CombatTurnOrderNormalFrame { get; private set; }
    public Texture2D? CombatBottomPanelBg { get; private set; }
    public Texture2D? CombatPortraitFrameTexture { get; private set; }
    public Texture2D? CombatSlotBg { get; private set; }
    public Texture2D? CombatRadialMenuBg { get; private set; }
    public Texture2D? CombatRadialMenuHover { get; private set; }
    public Texture2D? CombatResultVictory { get; private set; }
    public Texture2D? CombatResultDefeat { get; private set; }
    public Texture2D? CombatLogBg { get; private set; }
    public Texture2D? CombatMagicHexOverlay { get; private set; }

    public StyleBox? OverworldPanelStyle { get; private set; }
    public ButtonStyleSet? OverworldButtonStyle { get; private set; }

    private readonly Dictionary<string, StyleBoxFlat> _styleCache = new();

    private void LoadCombatTextures()
    {
        try
        {
            const string baseDir = "res://BladeHexFrontend/src/assets/ui/";
            CombatPowerBarBg = LoadUiTexture("Combat_PowerBar_Bg", baseDir);
            CombatPowerBarForceFriendly = LoadUiTexture("Combat_PowerBar_ForceFriendly", baseDir);
            CombatPowerBarForceEnemy = LoadUiTexture("Combat_PowerBar_ForceEnemy", baseDir);
            CombatTurnOrderBg = LoadUiTexture("Combat_TurnOrder_Bg", baseDir);
            CombatTurnOrderActiveFrame = LoadUiTexture("Combat_TurnOrder_ActiveFrame", baseDir);
            CombatTurnOrderNormalFrame = LoadUiTexture("Combat_TurnOrder_NormalFrame", baseDir);
            CombatBottomPanelBg = LoadUiTexture("Combat_BottomPanel_Bg", baseDir);
            CombatPortraitFrameTexture = LoadUiTexture("Combat_PortraitFrame", baseDir);
            CombatSlotBg = LoadOptionalUiTexture("Combat_Slot_Bg", baseDir);
            CombatRadialMenuBg = LoadUiTexture("Combat_RadialMenu_Bg", baseDir);
            CombatRadialMenuHover = LoadUiTexture("Combat_RadialMenu_Hover", baseDir);
            CombatResultVictory = LoadUiTexture("Combat_Result_Victory", baseDir);
            CombatResultDefeat = LoadUiTexture("Combat_Result_Defeat", baseDir);
            CombatLogBg = LoadUiTexture("Combat_Log_Bg", baseDir);
            CombatMagicHexOverlay = LoadUiTexture("Combat_MagicHex_Overlay", baseDir);
        }
        catch (System.Exception e)
        {
            GD.PrintErr("[UITheme] Failed to load combat UI textures: ", e.Message);
        }
    }

    private static Texture2D? LoadUiTexture(string id, string baseDir)
    {
        return TextureAssetResolver.LoadUiTexture(id, $"{baseDir}{id}.png");
    }

    private static Texture2D? LoadOptionalUiTexture(string id, string baseDir)
    {
        string fallbackPath = $"{baseDir}{id}.png";
        if (!AssetCatalog.TryGetPath(AssetKind.UiTexture, id, out _)
            && !ResourceLoader.Exists(fallbackPath))
        {
            return null;
        }

        return TextureAssetResolver.LoadUiTexture(id, fallbackPath);
    }

    private void LoadOverworldTextures()
    {
        try
        {
            var flatPanel = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.76f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.72f, 0.58f, 0.35f, 0.65f),
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                ContentMarginLeft = 16f,
                ContentMarginRight = 16f,
                ContentMarginTop = 10f,
                ContentMarginBottom = 10f,
                ShadowColor = new Color(0f, 0f, 0f, 0.25f),
                ShadowSize = 4,
                ShadowOffset = new Vector2(0, 2),
            };

            OverworldPanelStyle = flatPanel;
            OverworldButtonStyle = new ButtonStyleSet
            {
                Normal = MakeOverworldButtonStyle(new Color(0.05f, 0.05f, 0.06f, 0.85f), new Color(0.55f, 0.45f, 0.3f, 0.4f)),
                Hover = MakeOverworldButtonStyle(new Color(0.12f, 0.12f, 0.15f, 0.9f), new Color(0.92f, 0.76f, 0.42f, 0.95f)),
                Pressed = MakeOverworldButtonStyle(new Color(0.03f, 0.03f, 0.04f, 0.95f), new Color(0.72f, 0.4f, 0.2f, 0.85f)),
                Disabled = MakeOverworldButtonStyle(new Color(0.05f, 0.05f, 0.06f, 0.35f), new Color(0.3f, 0.3f, 0.3f, 0.2f)),
            };

            GD.Print("[UITheme] Overworld procedural styles initialized.");
        }
        catch (System.Exception e)
        {
            GD.PrintErr("[UITheme] Failed to build overworld procedural styles: ", e.Message);
        }
    }

    private static StyleBoxFlat MakeOverworldButtonStyle(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = border,
            CornerRadiusTopLeft = 1,
            CornerRadiusTopRight = 1,
            CornerRadiusBottomLeft = 1,
            CornerRadiusBottomRight = 1,
            ContentMarginLeft = 14f,
            ContentMarginRight = 14f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f,
        };
    }

    private StyleBoxTexture MakeTextureButtonStyle(Texture2D texture, float left, float right, float top, float bottom)
    {
        var style = new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginLeft = left,
            TextureMarginRight = right,
            TextureMarginTop = top,
            TextureMarginBottom = bottom,
            ContentMarginLeft = left,
            ContentMarginRight = right,
            ContentMarginTop = top,
            ContentMarginBottom = bottom,
        };
        return style;
    }

    public StyleBoxFlat MakePanelStyle(Color? bg = null, Color? border = null, int borderWidth = 1, int cornerRadius = -1, int contentMargin = -1)
    {
        var bgColor = bg ?? BgPanel;
        var borderColor = border ?? BorderDefault;
        int cr = cornerRadius >= 0 ? cornerRadius : RadiusMd;
        int cm = contentMargin >= 0 ? contentMargin : SpacingMd;
        var cacheKey = $"panel_{bgColor.ToHtml()}_{borderColor.ToHtml()}_{borderWidth}_{cr}_{cm}";

        if (_styleCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var style = new StyleBoxFlat
        {
            BgColor = bgColor,
            BorderColor = borderColor,
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(cr);
        style.SetContentMarginAll(cm);

        _styleCache[cacheKey] = style;
        return style;
    }

    public ButtonStyleSet MakeButtonStyle(
        Color? bgNormal = null,
        Color? bgHover = null,
        Color? bgPressed = null,
        Color? bgDisabled = null,
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
        if (_styleCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(cr);
        style.SetContentMarginAll(SpacingSm);

        _styleCache[cacheKey] = style;
        return style;
    }

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
        if (_styleCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(cr);
        _styleCache[cacheKey] = style;
        return style;
    }

    private StyleBoxFlat MakeBarBg(Color color, int cr)
    {
        var cacheKey = $"bar_bg_{color.ToHtml()}_{cr}";
        if (_styleCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(cr);
        _styleCache[cacheKey] = style;
        return style;
    }

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
            _ => TextMuted,
        };
    }

    public Color GetHpColor(float ratio)
    {
        if (ratio > 0.6f)
            return HpHigh;
        if (ratio > 0.3f)
            return HpMid;
        return HpLow;
    }

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

    public void ApplyButtonTheme(Button btn, ButtonStyleSet? styles = null)
    {
        styles ??= OverworldButtonStyle ?? MakeButtonStyle();
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

    public void ApplyBarTheme(ProgressBar bar, Color fillColor, Color? bgColor = null)
    {
        var styles = MakeBarStyle(fillColor, bgColor ?? new Color(0.1f, 0.1f, 0.12f));
        bar.AddThemeStyleboxOverride("fill", styles.Fill);
        bar.AddThemeStyleboxOverride("background", styles.Background);
    }

    public class ButtonStyleSet
    {
        public StyleBox Normal { get; set; } = new StyleBoxFlat();
        public StyleBox Hover { get; set; } = new StyleBoxFlat();
        public StyleBox Pressed { get; set; } = new StyleBoxFlat();
        public StyleBox Disabled { get; set; } = new StyleBoxFlat();
    }

    public class BarStyleSet
    {
        public StyleBoxFlat Fill { get; set; } = new();
        public StyleBoxFlat Background { get; set; } = new();
    }
}
