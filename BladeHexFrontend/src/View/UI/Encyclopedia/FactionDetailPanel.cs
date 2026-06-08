using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.View.UI.Overworld;
using BladeHex.UI.Common;
using BladeHex.Strategic.Diplomacy;

namespace BladeHex.View.UI.Encyclopedia;

/// <summary>
/// 势力百科磨砂详情面板
/// </summary>
public partial class FactionDetailPanel : PanelContainer
{
    private static readonly Color BgPanel = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);

    private NationConfig _nation;
    private OverworldEntityManager _entityMgr;

    public static void ShowDetail(NationConfig nation, OverworldEntityManager entityMgr, Node parent)
    {
        var panel = new FactionDetailPanel(nation, entityMgr);
        OverlayPanelLayout.AttachModal(parent, panel);
    }

    public FactionDetailPanel(NationConfig nation, OverworldEntityManager entityMgr)
    {
        _nation = nation;
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

        CustomMinimumSize = new Vector2(750, 480);
        OverlayPanelLayout.Center(this);

        // 3. 布局组装
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddThemeConstantOverride("separation", 12);
        AddChild(mainVbox);

        // Header 行
        var header = new HBoxContainer();
        mainVbox.AddChild(header);

        var titleLabel = _MakeLabel($"✦  {_nation.DisplayName}  ✦", 24, TextAccent);
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

        // 左栏：势力概要
        var leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        leftCol.AddThemeConstantOverride("separation", 10);
        split.AddChild(leftCol);

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 10);
        leftCol.AddChild(grid);

        // 君主：选取该势力第一个领主
        var lords = _entityMgr.Heroes.AllHeroes.Where(h => h.FactionId == _nation.Id).ToList();
        var monarch = lords.FirstOrDefault();
        string monarchName = monarch != null ? monarch.DisplayName : "未知";
        _AddLabelPair(grid, "👑 君主领袖:", monarchName);

        // 种族
        _AddLabelPair(grid, "🧬 统治种族:", _nation.Race);

        // 首都
        var capitalPoi = _entityMgr.Pois.FirstOrDefault(p => p.OwningFaction == _nation.Id && 
            (p.PoiTypeEnum == OverworldPOI.POIType.Town || p.PoiName.Contains("首都") || p.PoiName.Contains("王都")));
        string capitalName = capitalPoi != null ? capitalPoi.PoiName : "未知";
        _AddLabelPair(grid, "🏛 首都城市:", capitalName);

        // 势力特产
        string goodsText = _nation.TradeGoods != null && _nation.TradeGoods.Length > 0 ? string.Join(", ", _nation.TradeGoods) : "无特产";
        _AddLabelPair(grid, "📦 特产物资:", goodsText);

        // 兵种池
        string poolText = _nation.RecruitPool != null && _nation.RecruitPool.Length > 0 ? string.Join(", ", _nation.RecruitPool) : "常规民兵";
        
        var sep1 = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        leftCol.AddChild(sep1);
        leftCol.AddChild(_MakeLabel("🏹 特色兵种:", 16, TextSecondary));
        var poolLabel = new Label { Text = poolText, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        poolLabel.AddThemeFontSizeOverride("font_size", 14);
        poolLabel.AddThemeColorOverride("font_color", TextPrimary);
        leftCol.AddChild(poolLabel);

        // 新版外交关系展示与按钮操作 (Phase 3)
        var sep2 = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        leftCol.AddChild(sep2);

        if (_nation.Id == "player")
        {
            leftCol.AddChild(_MakeLabel("🏳 玩家所属自身势力", 16, TextSecondary));
        }
        else
        {
            int currentDay = _entityMgr.WorldEngine.CurrentDay;
            int rel = _entityMgr.WorldEngine.FactionRelations.GetRelation("player", _nation.Id);
            
            var relHbox = new HBoxContainer();
            relHbox.AddChild(_MakeLabel($"🤝 与玩家关系: {rel}", 16, TextAccent));
            leftCol.AddChild(relHbox);

            var relBar = new ProgressBar {
                MinValue = 0,
                MaxValue = 100,
                Value = (rel + 100) / 2.0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(0, 10)
            };
            
            var bgStyle = new StyleBoxFlat {
                BgColor = new Color(0.15f, 0.15f, 0.18f, 1f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            };
            var fillStyle = new StyleBoxFlat {
                BgColor = rel < -30 ? new Color(0.8f, 0.25f, 0.2f) : (rel > 30 ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.8f, 0.6f, 0.2f)),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
            };
            relBar.AddThemeStyleboxOverride("background", bgStyle);
            relBar.AddThemeStyleboxOverride("fill", fillStyle);
            leftCol.AddChild(relBar);

            bool areAtWar = _entityMgr.WorldEngine.AreAtWar("player", _nation.Id);
            bool areAllied = _entityMgr.WorldEngine.AreAllied("player", _nation.Id);
            bool isInTruce = _entityMgr.WorldEngine.FactionRelations.IsInTruce("player", _nation.Id, currentDay);
            
            string statusStr = "中立和平";
            Color statusColor = TextPrimary;

            if (areAtWar) {
                statusStr = "💥 处于战争中";
                statusColor = new Color(0.95f, 0.35f, 0.3f);
            } else if (areAllied) {
                statusStr = "🛡 结盟关系";
                statusColor = new Color(0.2f, 0.7f, 0.3f);
            } else if (isInTruce) {
                int remDays = _entityMgr.WorldEngine.FactionRelations.GetTruceRemainingDays("player", _nation.Id, currentDay);
                statusStr = $"🏳 停战协议保护中 (剩余 {remDays} 天)";
                statusColor = new Color(0.35f, 0.65f, 0.95f);
            }

            leftCol.AddChild(_MakeLabel($"当前状态: {statusStr}", 14, statusColor));

            var btnHbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            btnHbox.AddThemeConstantOverride("separation", 15);
            leftCol.AddChild(btnHbox);

            var decWarBtn = new Button { Text = "发起宣战", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36) };
            var proposePeaceBtn = new Button { Text = "请求议和", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36) };

            btnHbox.AddChild(decWarBtn);
            btnHbox.AddChild(proposePeaceBtn);

            var config = DiplomacyBalanceConfig.Load();
            int playerInfluence = _entityMgr.WorldEngine.Influence.Get("player");
            bool canDeclare = true;
            string declareTooltip = "向该势力宣战 (消耗 50 影响力)";

            if (areAtWar) {
                canDeclare = false;
                declareTooltip = "已处于战争状态";
            } else if (isInTruce) {
                canDeclare = false;
                declareTooltip = "处于停战期保护中，暂不可宣战";
            } else if (rel > config.DeclareWarRelationThreshold) {
                canDeclare = false;
                declareTooltip = $"关系不够恶劣 (要求 <= {config.DeclareWarRelationThreshold})";
            } else if (_entityMgr.WorldEngine.FactionRelations.IsDeclareWarInCooldown("player", _nation.Id, currentDay)) {
                canDeclare = false;
                declareTooltip = "宣战在冷却中";
            } else if (playerInfluence < config.DeclareWarInfluenceCost) {
                canDeclare = false;
                declareTooltip = $"影响力不足 (需 {config.DeclareWarInfluenceCost})";
            }

            decWarBtn.Disabled = !canDeclare;
            decWarBtn.TooltipText = declareTooltip;
            _StyleActionBtn(decWarBtn, new Color(0.9f, 0.3f, 0.25f), canDeclare);

            bool canPeace = true;
            string peaceTooltip = "请求与该势力停战 (消耗 80 影响力)";

            if (!areAtWar) {
                canPeace = false;
                peaceTooltip = "未处于战争状态，无需议和";
            } else if (_entityMgr.WorldEngine.FactionRelations.IsProposePeaceInCooldown("player", _nation.Id, currentDay)) {
                canPeace = false;
                peaceTooltip = "议和在冷却中";
            } else if (playerInfluence < config.ProposePeaceInfluenceCost) {
                canPeace = false;
                peaceTooltip = $"影响力不足 (需 {config.ProposePeaceInfluenceCost})";
            }

            proposePeaceBtn.Disabled = !canPeace;
            proposePeaceBtn.TooltipText = peaceTooltip;
            _StyleActionBtn(proposePeaceBtn, new Color(0.9f, 0.75f, 0.35f), canPeace);

            decWarBtn.Pressed += () => {
                var res = DiplomacyService.DeclareWar("player", _nation.Id, _entityMgr.WorldEngine, _entityMgr.WorldEngine.FactionRelations);
                if (res == DiplomacyResult.Success) {
                    GD.Print($"[UI] 宣战成功：已向 {_nation.DisplayName} 宣战");
                    OverlayPanelLayout.CloseModal(this);
                    FactionDetailPanel.ShowDetail(_nation, _entityMgr, GetParent());
                }
            };

            proposePeaceBtn.Pressed += () => {
                var res = DiplomacyService.ProposePeace("player", _nation.Id, _entityMgr.WorldEngine, _entityMgr.WorldEngine.FactionRelations, _entityMgr.Relations, _entityMgr.Entities, skipAiCheck: false);
                if (res == DiplomacyResult.Success) {
                    GD.Print($"[UI] 议和成功！对方同意停战。");
                    OverlayPanelLayout.CloseModal(this);
                    FactionDetailPanel.ShowDetail(_nation, _entityMgr, GetParent());
                } else if (res == DiplomacyResult.Failed) {
                    GD.Print($"[UI] 议和失败！对方拒绝了和谈。");
                    OverlayPanelLayout.CloseModal(this);
                    FactionDetailPanel.ShowDetail(_nation, _entityMgr, GetParent());
                }
            };
        }

        // 右栏：效忠领主列表
        var rightCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddChild(rightCol);

        rightCol.AddChild(_MakeLabel("👥 效忠领主成员:", 18, TextAccent));
        
        var rightSep = new HSeparator { Modulate = new Color(1, 1, 1, 0.15f) };
        rightCol.AddChild(rightSep);

        var lordScroll = new ScrollContainer { 
            SizeFlagsHorizontal = SizeFlags.ExpandFill, 
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var lordVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lordVbox.AddThemeConstantOverride("separation", 6);
        lordScroll.AddChild(lordVbox);
        rightCol.AddChild(lordScroll);

        foreach (var lord in lords)
        {
            string statusStr = lord.State == CapturedState.Captured ? "被俘" : "自由";
            var lBtn = new Button
            {
                Text = $"  {lord.DisplayName} (家族: {lord.FamilyName}) - {statusStr}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 38)
            };
            _StyleListButton(lBtn, TextPrimary, TextAccent);
            lBtn.Pressed += () =>
            {
                HeroDetailPanel.ShowDetail(lord, _entityMgr, GetParent());
            };
            lordVbox.AddChild(lBtn);
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
            ContentMarginLeft = 14, // 悬浮偏移动效
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

    private static void _StyleActionBtn(Button btn, Color accentColor, bool enabled)
    {
        btn.FocusMode = Control.FocusModeEnum.None;
        if (!enabled)
        {
            var btnDisabled = new StyleBoxFlat {
                BgColor = new Color(0.12f, 0.12f, 0.15f, 0.5f),
                BorderWidthBottom = 1,
                BorderColor = new Color(0.2f, 0.2f, 0.22f, 0.5f),
                CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                ContentMarginLeft = 10, ContentMarginRight = 10
            };
            btn.AddThemeStyleboxOverride("disabled", btnDisabled);
            btn.AddThemeColorOverride("font_disabled_color", new Color(0.4f, 0.4f, 0.42f));
            return;
        }

        var btnNormal = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.08f),
            BorderWidthBottom = 2,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.3f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10
        };
        var btnHover = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.18f),
            BorderWidthBottom = 2,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.6f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10
        };
        var btnPressed = new StyleBoxFlat {
            BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.28f),
            BorderWidthBottom = 2,
            BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.9f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10, ContentMarginRight = 10
        };
        btn.AddThemeStyleboxOverride("normal", btnNormal);
        btn.AddThemeStyleboxOverride("hover", btnHover);
        btn.AddThemeStyleboxOverride("pressed", btnPressed);
        btn.AddThemeStyleboxOverride("focus", btnNormal);
        btn.AddThemeColorOverride("font_color", TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", accentColor);
        btn.AddThemeFontSizeOverride("font_size", 14);
    }
}

