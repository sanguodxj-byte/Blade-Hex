// FoundKingdomDialog.cs
// 创建王国对话框
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Strategic.Kingdom;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.WorldEvents;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 创建王国对话框
/// </summary>
[GlobalClass]
public partial class FoundKingdomDialog : CanvasLayer
{
    [Signal]
    public delegate void KingdomFoundedEventHandler(string kingdomName, string familyName, Color bannerColor, string capitalPoiName);

    [Signal]
    public delegate void DialogCancelledEventHandler();

    private LineEdit _kingdomNameEdit = null!;
    private LineEdit _familyNameEdit = null!;
    private OptionButton _colorOption = null!;
    private OptionButton _capitalOption = null!;

    private List<string> _pendingConquests = new();
    private List<OverworldPOI> _pois = new();

    private static readonly Color[] BannerColors = new[]
    {
        new Color(0.2f, 0.4f, 0.8f),  // 蓝
        new Color(0.8f, 0.2f, 0.2f),  // 红
        new Color(0.2f, 0.7f, 0.3f),  // 绿
        new Color(0.9f, 0.7f, 0.1f),  // 金
        new Color(0.6f, 0.3f, 0.8f),  // 紫
        new Color(0.9f, 0.5f, 0.2f),  // 橙
        new Color(0.3f, 0.3f, 0.3f),  // 黑
        new Color(0.9f, 0.9f, 0.9f),  // 白
    };

    private static readonly string[] ColorNames = new[]
    {
        "蓝色", "红色", "绿色", "金色", "紫色", "橙色", "黑色", "白色"
    };

    public override void _Ready()
    {
        Visible = false;
    }

    /// <summary>显示创建王国对话框</summary>
    public void ShowDialog(List<string> pendingConquests, List<OverworldPOI> pois)
    {
        _pendingConquests = pendingConquests;
        _pois = pois;

        // 清除旧内容
        foreach (var child in GetChildren())
            child.QueueFree();

        // 创建面板
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(400, 350);
        OverlayPanelLayout.Center(panel);

        // 样式
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderColor = new Color(0.9f, 0.7f, 0.3f, 0.8f),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 25,
            ContentMarginRight = 25,
            ContentMarginTop = 20,
            ContentMarginBottom = 20
        };
        panel.AddThemeStyleboxOverride("panel", style);
        AddChild(panel);

        var mainVbox = new VBoxContainer();
        panel.AddChild(mainVbox);

        // 标题
        var titleLabel = new Label { Text = "🏰 建立你的王国" };
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        mainVbox.AddChild(titleLabel);

        mainVbox.AddChild(new HSeparator());

        // 王国名
        var nameHbox = new HBoxContainer();
        mainVbox.AddChild(nameHbox);
        var nameLabel = new Label { Text = "王国名: " };
        nameLabel.CustomMinimumSize = new Vector2(80, 0);
        nameHbox.AddChild(nameLabel);
        _kingdomNameEdit = new LineEdit { PlaceholderText = "输入王国名称" };
        _kingdomNameEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameHbox.AddChild(_kingdomNameEdit);

        // 家族姓
        var familyHbox = new HBoxContainer();
        mainVbox.AddChild(familyHbox);
        var familyLabel = new Label { Text = "家族姓: " };
        familyLabel.CustomMinimumSize = new Vector2(80, 0);
        familyHbox.AddChild(familyLabel);
        _familyNameEdit = new LineEdit { PlaceholderText = "输入家族姓氏" };
        _familyNameEdit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        familyHbox.AddChild(_familyNameEdit);

        // 旗帜色
        var colorHbox = new HBoxContainer();
        mainVbox.AddChild(colorHbox);
        var colorLabel = new Label { Text = "旗帜色: " };
        colorLabel.CustomMinimumSize = new Vector2(80, 0);
        colorHbox.AddChild(colorLabel);
        _colorOption = new OptionButton();
        for (int i = 0; i < ColorNames.Length; i++)
            _colorOption.AddItem(ColorNames[i], i);
        _colorOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        colorHbox.AddChild(_colorOption);

        // 都城选择
        var capitalHbox = new HBoxContainer();
        mainVbox.AddChild(capitalHbox);
        var capitalLabel = new Label { Text = "都城:   " };
        capitalLabel.CustomMinimumSize = new Vector2(80, 0);
        capitalHbox.AddChild(capitalLabel);
        _capitalOption = new OptionButton();
        foreach (var poiName in pendingConquests)
        {
            var poi = pois.FirstOrDefault(p => p.PoiName == poiName);
            if (poi != null)
                _capitalOption.AddItem($"{poiName} ({poi.PoiType})", _capitalOption.ItemCount);
        }
        _capitalOption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        capitalHbox.AddChild(_capitalOption);

        mainVbox.AddChild(new HSeparator());

        // 按钮
        var btnHbox = new HBoxContainer();
        btnHbox.Alignment = BoxContainer.AlignmentMode.Center;
        mainVbox.AddChild(btnHbox);

        var cancelBtn = new Button { Text = "取消" };
        cancelBtn.Pressed += () => Cancel();
        btnHbox.AddChild(cancelBtn);

        var confirmBtn = new Button { Text = "建立王国！" };
        confirmBtn.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 0.3f));
        confirmBtn.Pressed += () => Confirm();
        btnHbox.AddChild(confirmBtn);

        Visible = true;
    }

    private void Confirm()
    {
        string kingdomName = _kingdomNameEdit.Text.Trim();
        string familyName = _familyNameEdit.Text.Trim();

        if (string.IsNullOrEmpty(kingdomName))
        {
            GD.Print("[FoundKingdom] 王国名不能为空");
            return;
        }
        if (string.IsNullOrEmpty(familyName))
        {
            GD.Print("[FoundKingdom] 家族姓不能为空");
            return;
        }
        if (_capitalOption.Selected < 0 || _capitalOption.Selected >= _pendingConquests.Count)
        {
            GD.Print("[FoundKingdom] 请选择都城");
            return;
        }

        string capitalName = _pendingConquests[_capitalOption.Selected];
        var capitalPoi = _pois.FirstOrDefault(p => p.PoiName == capitalName);
        if (capitalPoi == null)
        {
            GD.Print("[FoundKingdom] 都城 POI 不存在");
            return;
        }

        Color bannerColor = BannerColors[_colorOption.Selected];

        EmitSignal(SignalName.KingdomFounded, kingdomName, familyName, bannerColor, capitalName);
        Visible = false;
    }

    private void Cancel()
    {
        Visible = false;
        EmitSignal(SignalName.DialogCancelled);
    }
}
