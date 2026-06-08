using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.View.UI.Overworld;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Encyclopedia;

/// <summary>
/// 家族百科磨砂详情面板
/// </summary>
public partial class FamilyDetailPanel : PanelContainer
{
    private static readonly Color BgPanel = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);

    private string _familyName;
    private List<HeroData> _members;
    private OverworldEntityManager _entityMgr;

    public static void ShowDetail(string familyName, List<HeroData> members, OverworldEntityManager entityMgr, Node parent)
    {
        var panel = new FamilyDetailPanel(familyName, members, entityMgr);
        OverlayPanelLayout.AttachModal(parent, panel);
    }

    public FamilyDetailPanel(string familyName, List<HeroData> members, OverworldEntityManager entityMgr)
    {
        _familyName = familyName;
        _members = members;
        _entityMgr = entityMgr;
    }

    public override void _Ready()
    {
        // 1. Panel 样式 - 通透玻璃暗金外阴影
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.06f, 0.97f),
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderColor = new Color(0.6f, 0.5f, 0.35f, 0.85f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ContentMarginLeft = 25,
            ContentMarginRight = 25,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            ShadowSize = 12,
            ShadowColor = new Color(0, 0, 0, 0.6f)
        };
        AddThemeStyleboxOverride("panel", style);

        CustomMinimumSize = new Vector2(650, 420);
        OverlayPanelLayout.Center(this);

        // 3. 布局组装
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddThemeConstantOverride("separation", 12);
        AddChild(mainVbox);

        // Header 行
        var header = new HBoxContainer();
        mainVbox.AddChild(header);

        var titleLabel = _MakeLabel($"✦  家族: {_familyName} 氏  ✦", 24, TextAccent);
        header.AddChild(titleLabel);

        var closeBtn = new Button();
        _StyleCloseButton(closeBtn);
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => OverlayPanelLayout.CloseModal(this);
        header.AddChild(closeBtn);

        var headerSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.25f) };
        mainVbox.AddChild(headerSep);

        // 双栏布局
        var split = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 25);
        mainVbox.AddChild(split);

        // 左栏：家族概要
        var leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        leftCol.AddThemeConstantOverride("separation", 10);
        split.AddChild(leftCol);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 15);
        grid.AddThemeConstantOverride("v_separation", 10);
        leftCol.AddChild(grid);

        // 领袖选取第一个成员（MVP最简方案）
        var leader = _members.FirstOrDefault();
        string leaderName = leader != null ? leader.DisplayName : "未知";
        _AddLabelPair(grid, "👑 家族领袖:", leaderName);

        // 总好感度
        double avgRelation = _members.Count > 0 ? _members.Average(m => _entityMgr.Relations.Get("player", m.HeroId)) : 0;
        _AddLabelPair(grid, "📈 平均好感:", $"{(avgRelation >= 0 ? "+" : "")}{avgRelation:F1}");

        // 名下领地
        var fiefs = _members.Where(m => !string.IsNullOrEmpty(m.BoundPoiName)).Select(m => m.BoundPoiName).Distinct().ToList();
        string fiefsText = fiefs.Count > 0 ? string.Join(", ", fiefs) : "暂无封地";
        
        var leftSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        leftCol.AddChild(leftSep);
        leftCol.AddChild(_MakeLabel("🏰 家族封地:", 16, TextSecondary));
        
        var fiefsLabel = new Label { Text = fiefsText, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        fiefsLabel.AddThemeFontSizeOverride("font_size", 14);
        fiefsLabel.AddThemeColorOverride("font_color", TextPrimary);
        leftCol.AddChild(fiefsLabel);

        // 右栏：成员列表
        var rightCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(rightCol);

        rightCol.AddChild(_MakeLabel("👥 家族成员列表:", 18, TextAccent));
        
        var rightSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        rightCol.AddChild(rightSep);

        var memberScroll = new ScrollContainer { 
            SizeFlagsHorizontal = SizeFlags.ExpandFill, 
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var memberVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        memberVbox.AddThemeConstantOverride("separation", 6);
        memberScroll.AddChild(memberVbox);
        rightCol.AddChild(memberScroll);

        foreach (var member in _members)
        {
            string statusStr = member.State == CapturedState.Captured ? "被俘" : "自由";
            var mBtn = new Button
            {
                Text = $"  {member.DisplayName} ({statusStr})",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 38)
            };
            _StyleListButton(mBtn, TextPrimary, TextAccent);
            mBtn.Pressed += () =>
            {
                HeroDetailPanel.ShowDetail(member, _entityMgr, GetParent());
            };
            memberVbox.AddChild(mBtn);
        }
    }

    private void _AddLabelPair(GridContainer grid, string key, string val)
    {
        var k = _MakeLabel(key, 16, TextSecondary);
        grid.AddChild(k);

        var v = _MakeLabel(val, 16, TextPrimary);
        grid.AddChild(v);
    }

    private static Label _MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static void _StyleCloseButton(Button closeBtn)
    {
        closeBtn.Text = "✕";
        closeBtn.FocusMode = Control.FocusModeEnum.None;
        var btnStyleNormal = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0f) };
        var btnStyleHover = new StyleBoxFlat {
            BgColor = new Color(0.9f, 0.3f, 0.25f, 0.4f),
            CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15
        };
        var btnStylePressed = new StyleBoxFlat {
            BgColor = new Color(0.9f, 0.3f, 0.25f, 0.6f),
            CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
            CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15
        };
        closeBtn.AddThemeStyleboxOverride("normal", btnStyleNormal);
        closeBtn.AddThemeStyleboxOverride("hover", btnStyleHover);
        closeBtn.AddThemeStyleboxOverride("pressed", btnStylePressed);
        closeBtn.AddThemeStyleboxOverride("focus", btnStyleNormal);
        closeBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.63f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 1f));
        closeBtn.AddThemeFontSizeOverride("font_size", 16);
        closeBtn.CustomMinimumSize = new Vector2(30, 30);
    }

    private static void _StyleListButton(Button btn, Color fontColor, Color accentColor)
    {
        btn.FocusMode = Control.FocusModeEnum.None;
        var btnNormal = new StyleBoxFlat {
            BgColor = new Color(1, 1, 1, 0.03f),
            BorderWidthBottom = 1,
            BorderColor = new Color(1, 1, 1, 0.08f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10,
            ContentMarginRight = 10
        };
        var btnHover = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.12f),
            BorderWidthBottom = 1,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.3f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginRight = 6
        };
        var btnPressed = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.22f),
            BorderWidthBottom = 1,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.5f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 14,
            ContentMarginRight = 6
        };
        btn.AddThemeStyleboxOverride("normal", btnNormal);
        btn.AddThemeStyleboxOverride("hover", btnHover);
        btn.AddThemeStyleboxOverride("pressed", btnPressed);
        btn.AddThemeStyleboxOverride("focus", btnNormal);
        btn.AddThemeColorOverride("font_color", fontColor);
        btn.AddThemeColorOverride("font_hover_color", accentColor);
        btn.AddThemeFontSizeOverride("font_size", 14);
    }
}
