// TerrainTooltip.cs
// 地形信息提示 — 悬停六边形格子时显示地形名、移动消耗、防御加值等
// 对应策划案 09-UI设计.md → 六边形网格 → 地形信息tooltip
// 对应策划案 03-战术战斗系统 → 二、地形系统
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BladeHex.UI.Combat;

/// <summary>
/// 地形信息提示 — 悬停六边形格子时显示地形名、移动消耗、防御加值等
/// </summary>
[GlobalClass]
public partial class TerrainTooltip : PanelContainer
{
    // ============================================================================
    // 内部组件
    // ============================================================================
    private Label _terrainLabel = null!;
    private Label _moveCostLabel = null!;
    private Label _defenseLabel = null!;
    private Label _coverLabel = null!;
    private Label _elevationLabel = null!;
    private RichTextLabel _specialLabel = null!;
    private Label _coordLabel = null!;

    private UITheme _theme => UITheme.Instance!;

    // ============================================================================
    // 地形数据类
    // ============================================================================
    private class TerrainInfo
    {
        public string Name { get; set; }
        public int MoveCost { get; set; }
        public int DefenseBonus { get; set; }
        public string Cover { get; set; }
        public string Elevation { get; set; }
        public string Special { get; set; }

        public TerrainInfo(string name, int moveCost, int defenseBonus,
            string cover, string elevation, string special)
        {
            Name = name;
            MoveCost = moveCost;
            DefenseBonus = defenseBonus;
            Cover = cover;
            Elevation = elevation;
            Special = special;
        }
    }

    // ============================================================================
    // 地形类型定义（对应策划案03的地形表）
    // ============================================================================
    private static readonly Dictionary<string, TerrainInfo> TerrainData = new()
    {
        { "plains",       new TerrainInfo("平地",     1,  0,  "无",    "平地", "") },
        { "grass",        new TerrainInfo("草地",     1,  0,  "无",    "平地", "") },
        { "savanna",      new TerrainInfo("稀树草原", 1,  1,  "无",    "平地", "") },
        { "forest",       new TerrainInfo("森林",     2,  2,  "半掩体", "平地", "潜行加成 / 阻挡穿越视线") },
        { "deep_forest",  new TerrainInfo("密林",     3,  3,  "全掩体", "平地", "潜行大幅加成 / 阻挡全部视线") },
        { "hills",        new TerrainInfo("丘陵",     2,  2,  "半掩体", "高地", "高地优势 / 可越过低矮障碍") },
        { "mountain",     new TerrainInfo("山地",     3,  3,  "全掩体", "高地", "高处视野+2 / 不可骑乘") },
        { "shallow_water",new TerrainInfo("浅水",     2,  -1, "无",    "低地", "火抗+2 / 冰雷弱点") },
        { "deep_water",   new TerrainInfo("深水",     3,  -2, "无",    "低地", "需游泳 / 施法劣势") },
        { "swamp",        new TerrainInfo("沼泽",     2,  -1, "无",    "低地", "强韧豁免DC12 / 失败中毒") },
        { "road",         new TerrainInfo("道路",     1,  0,  "无",    "平地", "移动消耗减半") },
        { "sand",         new TerrainInfo("沙地",     2,  0,  "无",    "平地", "冲锋失效") },
        { "snow",         new TerrainInfo("雪地",     2,  0,  "无",    "平地", "每回合移动-1格") },
        { "wall",         new TerrainInfo("墙壁",     -1, 0,  "全掩体", "平地", "不可通过 / 可被攻城器械破坏") },
        { "ruins",        new TerrainInfo("建筑废墟", 2,  2,  "半掩体", "平地", "可被破坏变平地") },
        { "poison_mush",  new TerrainInfo("毒菇群",   1,  0,  "无",    "平地", "站上去中毒2回合") },
        { "lucky_grass",  new TerrainInfo("幸运草丛", 1,  0,  "无",    "平地", "暴击率+10%(1次攻击)") },
    };

    // ============================================================================
    // 初始化
    // ============================================================================

    public override void _Ready()
    {
        Setup();
        Visible = false;
    }

    private void Setup()
    {
        AddThemeStyleboxOverride("panel", _theme.MakePanelStyle(
            _theme.BgTooltip, _theme.BorderHighlight, 1, _theme.RadiusMd, _theme.SpacingMd));
        ZIndex = 100;
        MouseFilter = Control.MouseFilterEnum.Ignore;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", _theme.SpacingXs);
        AddChild(vbox);

        // 地形名
        _terrainLabel = CreateBodyLabel("", _theme.TextAccent);
        _terrainLabel.AddThemeFontSizeOverride("font_size", _theme.FontSizeLg);
        vbox.AddChild(_terrainLabel);

        vbox.AddChild(CreateSeparatorH(_theme.BorderDefault));

        // 移动消耗
        _moveCostLabel = CreateBodyLabel("");
        vbox.AddChild(_moveCostLabel);

        // 防御加成
        _defenseLabel = CreateBodyLabel("");
        vbox.AddChild(_defenseLabel);

        // 掩护等级
        _coverLabel = CreateBodyLabel("");
        vbox.AddChild(_coverLabel);

        // 高程
        _elevationLabel = CreateBodyLabel("");
        vbox.AddChild(_elevationLabel);

        // 特殊效果
        _specialLabel = CreateRichText(new Vector2(180, 0));
        vbox.AddChild(_specialLabel);

        // 坐标
        _coordLabel = CreateMutedLabel("");
        vbox.AddChild(_coordLabel);
    }

    // ============================================================================
    // 内联 UI 组件创建（替代 UIFactory）
    // ============================================================================

    private static Label CreateBodyLabel(string text, Color? color = null)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", UITheme.Instance!.FontSizeMd);
        lbl.AddThemeColorOverride("font_color", color ?? UITheme.Instance.TextPrimary);
        return lbl;
    }

    private static Label CreateMutedLabel(string text)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", UITheme.Instance!.FontSizeSm);
        lbl.AddThemeColorOverride("font_color", UITheme.Instance.TextMuted);
        return lbl;
    }

    private static RichTextLabel CreateRichText(Vector2 minSize)
    {
        var rt = new RichTextLabel();
        rt.CustomMinimumSize = minSize;
        rt.BbcodeEnabled = true;
        rt.ScrollActive = false;
        rt.FitContent = true;
        return rt;
    }

    private static HSeparator CreateSeparatorH(Color? color = null)
    {
        var sep = new HSeparator();
        var c = color ?? UITheme.Instance!.BorderDefault;
        var style = new StyleBoxFlat();
        style.BgColor = c;
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>
    /// 显示地形信息
    /// </summary>
    /// <param name="globalPos">全局位置</param>
    /// <param name="terrainType">地形类型键</param>
    /// <param name="coord">网格坐标（默认无效值）</param>
    /// <param name="occupantName">占据者名称（可选）</param>
    /// <param name="coverOverride">掩护覆盖值（预留）</param>
    /// <param name="elevationOverride">高程覆盖值（预留）</param>
    public async void ShowTerrainInfo(Vector2 globalPos, string terrainType,
        Vector2I coord = default, string occupantName = "",
        int coverOverride = -1, int elevationOverride = -999)
    {
        // 从紧凑模式恢复标准标签可见性
        RestoreLabels();

        Visible = true;

        TerrainInfo? info;
        if (!TerrainData.TryGetValue(terrainType, out info) || info == null)
        {
            info = new TerrainInfo(terrainType, 1, 0, "无", "平地", "");
        }

        _terrainLabel.Text = info.Name;

        // 移动消耗
        if (info.MoveCost < 0)
        {
            _moveCostLabel.Text = "移动: 不可通过";
            _moveCostLabel.AddThemeColorOverride("font_color", _theme.TextNegative);
        }
        else
        {
            _moveCostLabel.Text = $"移动消耗: {info.MoveCost}";
            _moveCostLabel.AddThemeColorOverride("font_color",
                info.MoveCost >= 3 ? _theme.TextNegative : _theme.TextPrimary);
        }

        // 防御加成
        if (info.DefenseBonus > 0)
        {
            _defenseLabel.Text = $"防御加成: +{info.DefenseBonus} AC";
            _defenseLabel.AddThemeColorOverride("font_color", _theme.TextPositive);
        }
        else if (info.DefenseBonus < 0)
        {
            _defenseLabel.Text = $"防御惩罚: {info.DefenseBonus} AC";
            _defenseLabel.AddThemeColorOverride("font_color", _theme.TextNegative);
        }
        else
        {
            _defenseLabel.Text = "防御加成: —";
            _defenseLabel.AddThemeColorOverride("font_color", _theme.TextMuted);
        }

        // 掩护
        _coverLabel.Text = $"掩护: {info.Cover}";
        switch (info.Cover)
        {
            case "全掩体":
                _coverLabel.AddThemeColorOverride("font_color", _theme.TextPositive);
                break;
            case "半掩体":
                _coverLabel.AddThemeColorOverride("font_color", _theme.TextWarning);
                break;
            default:
                _coverLabel.AddThemeColorOverride("font_color", _theme.TextMuted);
                break;
        }

        // 高程
        _elevationLabel.Text = $"高程: {info.Elevation}";
        switch (info.Elevation)
        {
            case "高地":
                _elevationLabel.AddThemeColorOverride("font_color", _theme.TextPositive);
                break;
            case "低地":
                _elevationLabel.AddThemeColorOverride("font_color", _theme.TextNegative);
                break;
            default:
                _elevationLabel.AddThemeColorOverride("font_color", _theme.TextMuted);
                break;
        }

        // 特殊效果
        if (!string.IsNullOrEmpty(info.Special))
        {
            _specialLabel.Text = $"[color={_theme.TextAccent.ToHtml(false)}]{info.Special}[/color]";
            _specialLabel.Visible = true;
        }
        else
        {
            _specialLabel.Text = "";
            _specialLabel.Visible = false;
        }

        // 占据者
        if (!string.IsNullOrEmpty(occupantName))
        {
            _specialLabel.Text += $"\n[color=cyan]占据: {occupantName}[/color]";
        }

        // 坐标
        if (coord.X >= 0)
        {
            _coordLabel.Text = $"({coord.X}, {coord.Y})";
        }
        else
        {
            _coordLabel.Text = "";
        }

        // 定位
        Position = globalPos + new Vector2(15, 15);

        // 边界修正
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var vpSize = GetViewport().GetVisibleRect().Size;
        if (Position.X + Size.X > vpSize.X)
        {
            Position = new Vector2(globalPos.X - Size.X - 10, Position.Y);
        }
        if (Position.Y + Size.Y > vpSize.Y)
        {
            Position = new Vector2(Position.X, globalPos.Y - Size.Y - 10);
        }
    }

    /// <summary>
    /// 隐藏提示
    /// </summary>
    public void HideTooltip()
    {
        Visible = false;
    }

    /// <summary>
    /// 显示富文本信息（紧凑模式：隐藏其他标签，只用富文本）
    /// 由 C# 传入格式化文本，跟随鼠标右下方
    /// </summary>
    public async void ShowRichText(string text)
    {
        if (_specialLabel == null)
            return;

        // 紧凑模式：隐藏其他标签
        _terrainLabel.Visible = false;
        _moveCostLabel.Visible = false;
        _defenseLabel.Visible = false;
        _coverLabel.Visible = false;
        _elevationLabel.Visible = false;
        _coordLabel.Visible = false;

        _specialLabel.Text = text;
        _specialLabel.Visible = true;
        _specialLabel.FitContent = true;
        _specialLabel.CustomMinimumSize = new Vector2(200, 0);
        CustomMinimumSize = new Vector2(220, 0);
        Visible = true;

        // 定位到鼠标右下方
        var mousePos = GetViewport().GetMousePosition();
        Position = mousePos + new Vector2(16, 16);

        // 边界修正
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var vpSize = GetViewport().GetVisibleRect().Size;
        if (Position.X + Size.X > vpSize.X)
        {
            Position = new Vector2(mousePos.X - Size.X - 10, Position.Y);
        }
        if (Position.Y + Size.Y > vpSize.Y)
        {
            Position = new Vector2(Position.X, mousePos.Y - Size.Y - 10);
        }
    }

    /// <summary>
    /// 恢复标准模式（下次 ShowTerrainInfo 时自动恢复）
    /// </summary>
    public void RestoreLabels()
    {
        if (_terrainLabel == null) return;
        _terrainLabel.Visible = true;
        _moveCostLabel.Visible = true;
        _defenseLabel.Visible = true;
        _coverLabel.Visible = true;
        _elevationLabel.Visible = true;
        _coordLabel.Visible = true;
    }
}
