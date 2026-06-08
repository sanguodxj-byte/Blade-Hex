using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.View.UI.Encyclopedia;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 具名英雄/领主百科磨砂详情面板
/// </summary>
public partial class HeroDetailPanel : PanelContainer
{
    private static readonly Color BgPanel = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);

    private HeroData _hero = null!;
    private OverworldEntityManager _entityMgr = null!;

    public static void ShowDetail(HeroData hero, OverworldEntityManager entityMgr, Node parent)
    {
        var panel = new HeroDetailPanel(hero, entityMgr);
        OverlayPanelLayout.AttachModal(parent, panel);
    }

    public HeroDetailPanel(HeroData hero, OverworldEntityManager entityMgr)
    {
        _hero = hero;
        _entityMgr = entityMgr;
    }

    public override void _Ready()
    {
        // 1. 本身 Dialog 样式 - 通透玻璃暗金外阴影
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

        CustomMinimumSize = new Vector2(1150, 500);
        OverlayPanelLayout.Center(this);

        // 3. 布局组装
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddThemeConstantOverride("separation", 12);
        AddChild(mainVbox);

        // Header 行
        var header = new HBoxContainer();
        mainVbox.AddChild(header);

        var titleLabel = _MakeLabel($"✦  {_hero.DisplayName}  ✦", 28, TextAccent);
        header.AddChild(titleLabel);

        var closeBtn = new Button();
        _StyleCloseButton(closeBtn);
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => OverlayPanelLayout.CloseModal(this);
        header.AddChild(closeBtn);

        var headerSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.25f) };
        mainVbox.AddChild(headerSep);

        // 多栏布局
        var split = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 25);
        mainVbox.AddChild(split);

        // 左栏：领主人物卡
        var leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        leftCol.AddThemeConstantOverride("separation", 10);
        split.AddChild(leftCol);

        // 基础属性网格
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 15);
        grid.AddThemeConstantOverride("v_separation", 8);
        leftCol.AddChild(grid);

        // 势力名解析
        string factionName = _hero.FactionId;
        var nation = _entityMgr.Nations.FirstOrDefault(n => n.Id == _hero.FactionId);
        if (nation != null) factionName = nation.DisplayName;

        _AddLabelPair(grid, "👥 家族姓氏:", _hero.FamilyName);
        _AddLabelPair(grid, "🏳 效忠阵营:", factionName);
        
        string stateText = _hero.State switch
        {
            CapturedState.Free => "自由行动",
            CapturedState.Captured => $"被俘 (关押在: {(!string.IsNullOrEmpty(_hero.PrisonPoiName) ? _hero.PrisonPoiName : "敌军随队")})",
            CapturedState.Recovering => "隐退疗伤中",
            _ => "未知"
        };
        _AddLabelPair(grid, "🩹 当前状态:", stateText);
        
        string personalityText = _hero.Personality switch
        {
            OverworldPOI.LordPersonality.Balanced => "务实平衡",
            OverworldPOI.LordPersonality.Aggressive => "好战掠夺",
            OverworldPOI.LordPersonality.Cautious => "保守防御",
            _ => "务实平衡"
        };
        _AddLabelPair(grid, "🧠 性格倾向:", personalityText);
        _AddLabelPair(grid, "🏰 封地领地:", !string.IsNullOrEmpty(_hero.BoundPoiName) ? _hero.BoundPoiName : "暂无封地");

        var leftSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        leftCol.AddChild(leftSep);

        // 好感度关系条
        int relation = _entityMgr.Relations.Get("player", _hero.HeroId);
        leftCol.AddChild(_MakeLabel("🤝 与玩家势力好感度:", 16, TextSecondary));
        
        var relHbox = new HBoxContainer();
        relHbox.AddThemeConstantOverride("separation", 10);
        leftCol.AddChild(relHbox);

        var bar = new ProgressBar
        {
            MinValue = -100,
            MaxValue = 100,
            Value = relation,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(200, 16),
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        
        var barBg = new StyleBoxFlat { 
            BgColor = new Color(0.04f, 0.04f, 0.05f, 0.9f),
            BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            BorderColor = new Color(1, 1, 1, 0.08f),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        };
        bar.AddThemeStyleboxOverride("background", barBg);
        
        var relColor = relation >= 30 ? TextPositive : relation <= -30 ? TextNegative : TextAccent;
        var fill = new StyleBoxFlat { 
            BgColor = relColor,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
        };
        bar.AddThemeStyleboxOverride("fill", fill);
        relHbox.AddChild(bar);

        var valLabel = _MakeLabel($"{(relation >= 0 ? "+" : "")}{relation}", 16, relColor);
        relHbox.AddChild(valLabel);

        // 背景简短叙事
        if (!string.IsNullOrEmpty(_hero.Background))
        {
            var descSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
            leftCol.AddChild(descSep);
            var bgLabel = new Label 
            { 
                Text = _hero.Background,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            bgLabel.AddThemeFontSizeOverride("font_size", 13);
            bgLabel.AddThemeColorOverride("font_color", TextMuted);
            leftCol.AddChild(bgLabel);
        }

        // 右栏：领主编年史
        var rightCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(rightCol);

        rightCol.AddChild(_MakeLabel("⚔ 百科编年史:", 20, TextAccent));
        
        var rightSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        rightCol.AddChild(rightSep);

        var chronicleScroll = new ScrollContainer { 
            SizeFlagsHorizontal = SizeFlags.ExpandFill, 
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var chronicleVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        chronicleVbox.AddThemeConstantOverride("separation", 8);
        chronicleScroll.AddChild(chronicleVbox);
        rightCol.AddChild(chronicleScroll);

        // 从 WorldEngine.NewsQueue 过滤出关于这名领主的新闻
        bool hasNews = false;
        if (_entityMgr.WorldEngine != null)
        {
            var matchedNews = _entityMgr.WorldEngine.NewsQueue
                .Where(n => n.Description.Contains(_hero.DisplayName) || n.Description.Contains(_hero.FamilyName))
                .OrderByDescending(n => n.Day)
                .ToList();

            foreach (var news in matchedNews)
            {
                hasNews = true;
                
                // 使用带微弱半透明背景的卡片式 VBox 装载新闻，增加视觉层次
                var itemPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                var pStyle = new StyleBoxFlat {
                    BgColor = new Color(1, 1, 1, 0.02f),
                    ContentMarginLeft = 10, ContentMarginRight = 10,
                    ContentMarginTop = 6, ContentMarginBottom = 6,
                    CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                    CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
                };
                itemPanel.AddThemeStyleboxOverride("panel", pStyle);
                
                var itemHbox = new HBoxContainer();
                itemPanel.AddChild(itemHbox);
                
                var dayLabel = _MakeLabel($"第 {news.Day} 天", 14, TextAccent);
                dayLabel.CustomMinimumSize = new Vector2(70, 0);
                itemHbox.AddChild(dayLabel);

                var contentLabel = new Label
                {
                    Text = news.Description,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                contentLabel.AddThemeFontSizeOverride("font_size", 13);
                contentLabel.AddThemeColorOverride("font_color", TextPrimary);
                itemHbox.AddChild(contentLabel);

                chronicleVbox.AddChild(itemPanel);
            }
        }

        if (!hasNews)
        {
            chronicleVbox.AddChild(_MakeLabel("暂无此角色的重大编年史记录。", 14, TextMuted));
        }

        // 第三栏：关系图谱 (RelationGraphView)
        var graphCol = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(graphCol);
        
        graphCol.AddChild(_MakeLabel("🔗 社交关系网:", 20, TextAccent));
        
        var graphSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        graphCol.AddChild(graphSep);
        
        var relationGraph = new RelationGraphView();
        relationGraph.Initialize(_hero, _entityMgr);
        graphCol.AddChild(relationGraph);
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
}
