// AiPeaceProposalDialog.cs
// AI 主动求和向玩家发送的确认弹窗与接受/拒绝流程
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// AI 主动向玩家提出议和时的确认弹窗
/// </summary>
[GlobalClass]
public partial class AiPeaceProposalDialog : PanelContainer
{
    private Action? _onAccept;
    private Action? _onReject;

    public void Init(
        string proposerFactionId, 
        int warDays, 
        List<NationConfig> nations, 
        Action onAccept, 
        Action onReject)
    {
        _onAccept = onAccept;
        _onReject = onReject;

        // 清除旧内容
        foreach (var child in GetChildren())
            child.QueueFree();

        // 居中和最小尺寸
        CustomMinimumSize = new Vector2(480, 280);
        OverlayPanelLayout.Center(this);

        // 高端暗色面板背景样式 (玻璃暗色 + 优雅的黄铜色边框)
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.11f, 0.96f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.85f, 0.65f, 0.25f, 0.85f), // 优雅的黄铜色
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 30,
            ContentMarginRight = 30,
            ContentMarginTop = 24,
            ContentMarginBottom = 24
        };
        AddThemeStyleboxOverride("panel", style);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 16);
        AddChild(mainVbox);

        // 顶部图标与标题
        var titleHbox = new HBoxContainer();
        titleHbox.Alignment = BoxContainer.AlignmentMode.Center;
        titleHbox.AddThemeConstantOverride("separation", 8);
        mainVbox.AddChild(titleHbox);

        var titleLabel = new Label { Text = "🕊️ 议和提案" };
        titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f));
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleHbox.AddChild(titleLabel);

        // 分割线
        var separator = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 2),
            Color = new Color(0.85f, 0.65f, 0.25f, 0.3f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        mainVbox.AddChild(separator);

        // 获取势力名称
        string proposerName = "敌对势力";
        if (proposerFactionId == "player") proposerName = "玩家王国";
        else if (proposerFactionId == "neutral") proposerName = "中立势力";
        else
        {
            var nation = nations?.FirstOrDefault(n => n.Id == proposerFactionId);
            if (nation != null) proposerName = nation.DisplayName;
        }

        // 叙事文本内容
        var contentLabel = new Label
        {
            Text = $"来自 [{proposerName}] 的使者带着白旗与求和信函来到了你的宫殿。\n\n双方已交战了 {warDays} 天，战火使人民疲惫不堪。为了避免更惨烈的流血，他们恳请与我们握手言和，并签署为期 30 天的停战协议。\n\n我们是否应当接受这份和平契约？",
            CustomMinimumSize = new Vector2(400, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        contentLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
        contentLabel.AddThemeFontSizeOverride("font_size", 14);
        mainVbox.AddChild(contentLabel);

        // 按钮部分
        var btnHbox = new HBoxContainer();
        btnHbox.Alignment = BoxContainer.AlignmentMode.Center;
        btnHbox.AddThemeConstantOverride("separation", 30);
        mainVbox.AddChild(btnHbox);

        // 拒绝/战到底按钮
        var rejectBtn = new Button
        {
            Text = "拒绝 (坚守战线)",
            CustomMinimumSize = new Vector2(160, 40)
        };
        // 优雅的暗红色按钮风格
        var rejectNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.08f, 0.08f, 0.9f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(0.6f, 0.2f, 0.2f, 0.8f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        var rejectHover = new StyleBoxFlat
        {
            BgColor = new Color(0.3f, 0.1f, 0.1f, 0.9f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(0.8f, 0.3f, 0.3f, 1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        rejectBtn.AddThemeStyleboxOverride("normal", rejectNormal);
        rejectBtn.AddThemeStyleboxOverride("hover", rejectHover);
        rejectBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.7f));
        rejectBtn.Pressed += () => 
        {
            _onReject?.Invoke();
            OverlayPanelLayout.CloseModal(this);
        };
        btnHbox.AddChild(rejectBtn);

        // 接受/停战按钮
        var acceptBtn = new Button
        {
            Text = "接受 (重归和平)",
            CustomMinimumSize = new Vector2(160, 40)
        };
        // 优雅的柔绿色/碧玉色按钮风格
        var acceptNormal = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.18f, 0.12f, 0.9f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(0.2f, 0.5f, 0.3f, 0.8f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        var acceptHover = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.25f, 0.16f, 0.9f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(0.3f, 0.7f, 0.4f, 1f),
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        acceptBtn.AddThemeStyleboxOverride("normal", acceptNormal);
        acceptBtn.AddThemeStyleboxOverride("hover", acceptHover);
        acceptBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.9f, 0.7f));
        acceptBtn.Pressed += () =>
        {
            _onAccept?.Invoke();
            OverlayPanelLayout.CloseModal(this);
        };
        btnHbox.AddChild(acceptBtn);
    }
}
