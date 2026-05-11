using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.UI.Combat;

/// <summary>
/// 地形信息提示 — 悬停六边形格子时显示地形名、移动消耗、防御加值等
/// 迁移自 GDScript TerrainTooltip.gd
/// </summary>
public partial class TerrainTooltip : PanelContainer
{
    private Label _terrainLabel = null!;
    private Label _moveCostLabel = null!;
    private Label _defenseLabel = null!;
    private Label _coverLabel = null!;
    private Label _elevationLabel = null!;
    private RichTextLabel _specialLabel = null!;
    private Label _coordLabel = null!;

    private readonly UIFactory _factory = new();

    private struct TerrainInfo
    {
        public string Name;
        public int Move;
        public int Defense;
        public string Cover;
        public string Elev;
        public string Special;
    }

    private static readonly Dictionary<string, TerrainInfo> TerrainDefs = new()
    {
        { "plains", new TerrainInfo { Name = "平地", Move = 1, Defense = 0, Cover = "无", Elev = "平地", Special = "" } },
        { "forest", new TerrainInfo { Name = "森林", Move = 2, Defense = 2, Cover = "半掩体", Elev = "平地", Special = "潜行加成 / 阻挡视野" } },
        { "mountain", new TerrainInfo { Name = "山地", Move = 3, Defense = 3, Cover = "全掩体", Elev = "高地", Special = "高处视野+2 / 不可骑乘" } },
        { "shallow_water", new TerrainInfo { Name = "浅水", Move = 2, Defense = -1, Cover = "无", Elev = "低地", Special = "火抗+2 / 冰雷弱点" } },
        { "road", new TerrainInfo { Name = "道路", Move = 1, Defense = 0, Cover = "无", Elev = "平地", Special = "移动消耗减半" } },
        { "wall", new TerrainInfo { Name = "墙壁", Move = -1, Defense = 0, Cover = "全掩体", Elev = "平地", Special = "不可通过" } }
    };

    public override void _Ready()
    {
        Setup();
        Visible = false;
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    private void Setup()
    {
        AddThemeStyleboxOverride("panel", UITheme.Instance.MakePanelStyle(new Color(0.08f, 0.08f, 0.12f, 0.9f), UITheme.Instance.BorderHighlight, 1, UITheme.Instance.RadiusMd));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        AddChild(vbox);

        _terrainLabel = _factory.CreateBodyLabel("", UITheme.Instance.TextAccent);
        _terrainLabel.AddThemeFontSizeOverride("font_size", UITheme.Instance.FontSizeLg);
        vbox.AddChild(_terrainLabel);

        vbox.AddChild(_factory.CreateSeparatorH(UITheme.Instance.BorderDefault));

        _moveCostLabel = _factory.CreateBodyLabel("");
        vbox.AddChild(_moveCostLabel);

        _defenseLabel = _factory.CreateBodyLabel("");
        vbox.AddChild(_defenseLabel);

        _coverLabel = _factory.CreateBodyLabel("");
        vbox.AddChild(_coverLabel);

        _elevationLabel = _factory.CreateBodyLabel("");
        vbox.AddChild(_elevationLabel);

        _specialLabel = _factory.CreateRichText(new Vector2(180, 0));
        vbox.AddChild(_specialLabel);

        _coordLabel = _factory.CreateMutedLabel("");
        vbox.AddChild(_coordLabel);
    }

    public void ShowTerrainInfo(Vector2 screenPos, string terrainType, Vector2I coord = default)
    {
        Visible = true;
        Position = screenPos + new Vector2(15, 15);

        if (!TerrainDefs.TryGetValue(terrainType, out var info))
        {
            info = new TerrainInfo { Name = terrainType, Move = 1, Defense = 0, Cover = "无", Elev = "平地", Special = "" };
        }

        _terrainLabel.Text = info.Name;
        _moveCostLabel.Text = info.Move < 0 ? "不可通过" : $"移动消耗: {info.Move}";
        _moveCostLabel.AddThemeColorOverride("font_color", info.Move >= 3 || info.Move < 0 ? UITheme.Instance.TextNegative : UITheme.Instance.TextPrimary);

        _defenseLabel.Text = info.Defense != 0 ? $"防御加成: {(info.Defense > 0 ? "+" : "")}{info.Defense} AC" : "防御加成: —";
        _defenseLabel.AddThemeColorOverride("font_color", info.Defense > 0 ? UITheme.Instance.TextPositive : (info.Defense < 0 ? UITheme.Instance.TextNegative : UITheme.Instance.TextMuted));

        _coverLabel.Text = $"掩护: {info.Cover}";
        _elevationLabel.Text = $"高程: {info.Elev}";

        if (!string.IsNullOrEmpty(info.Special))
        {
            _specialLabel.Visible = true;
            _specialLabel.Text = $"[color=#{UITheme.Instance.TextAccent.ToHtml(false)}]{info.Special}[/color]";
        }
        else
        {
            _specialLabel.Visible = false;
        }

        _coordLabel.Text = coord.X >= 0 ? $"({coord.X}, {coord.Y})" : "";
    }

    public void HideTooltip() => Visible = false;
}
