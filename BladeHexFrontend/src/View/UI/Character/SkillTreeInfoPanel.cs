using BladeHex.Strategic;
using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;
using System.Text;

namespace BladeHex.UI;

public partial class SkillTreeInfoPanel : PanelContainer
{
    [Signal] public delegate void ActivateRequestedEventHandler();
    [Signal] public delegate void JumpRequestedEventHandler();

    private readonly UIFactory _factory = new();
    private UITheme UiTheme => UITheme.Instance!;

    private Label _title = null!;
    private Label _careerBanner = null!;
    private RichTextLabel _description = null!;
    private Button _activateButton = null!;
    private Button _jumpButton = null!;
    private bool _isBuilt;

    public override void _Ready()
    {
        Build();
        ShowEmpty();
    }

    public void ShowEmpty()
    {
        if (!_isBuilt)
            return;

        _title.Text = "选择节点";
        _title.RemoveThemeColorOverride("font_color");
        _description.Text = "";
        _activateButton.Text = "激活";
        _jumpButton.Text = "跳跃激活";
        _activateButton.Disabled = true;
        _jumpButton.Disabled = true;
    }

    public void ShowNode(
    	SkillNodeData node,
    	bool activated,
    	bool canActivate,
    	bool canJump,
    	int filledTiles,
    	int requiredTiles,
    	string careerTransitionText,
    	string effectText)
    {
    	if (!_isBuilt)
    		return;
   
        bool isGiant = IsGiantNode(node, requiredTiles);
        string tierName = GetNodeTierName(requiredTiles);
        string titleText = GetNodeTitle(node, tierName, isGiant);

    	_title.Text = titleText;
    	_title.AddThemeColorOverride("font_color", UiTheme.GetRegionColor(node.CurrentRegion));
   
    	// 命座转变横幅：如果点亮后会改变职业，在标题上方显示
    	if (!string.IsNullOrWhiteSpace(careerTransitionText))
    	{
    		_careerBanner.Text = careerTransitionText;
    		_careerBanner.Visible = true;
    	}
    	else
    	{
    		_careerBanner.Visible = false;
    	}
   
        _description.Text = isGiant
            ? BuildGiantDetail(effectText)
            : BuildNodeDetail(node, tierName, filledTiles, requiredTiles, effectText);
    	_activateButton.Text = activated ? "已完成" : "点亮";
    	_jumpButton.Text = "跳跃点亮";
    	_activateButton.Disabled = !canActivate;
    	_jumpButton.Disabled = !canJump;
    }

    public void ShowError(string message)
    {
        if (!_isBuilt)
            return;

        _description.Text = $"[color=red]{message}[/color]";
    }

    public void AppendSuccessMessage(string message)
    {
        if (!_isBuilt)
            return;

        _description.Text += $"\n[color=green]{message}[/color]";
    }

    private void Build()
    {
        if (_isBuilt)
            return;

        _isBuilt = true;
        CustomMinimumSize = new Vector2(380, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        ApplyPanelStyle();

        var margin = _factory.CreateMargin(14, 14, 12, 12);
        AddChild(margin);

        var centerWrapper = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        centerWrapper.AddThemeConstantOverride("separation", 0);
        margin.AddChild(centerWrapper);

        var topSpacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        centerWrapper.AddChild(topSpacer);

        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        vbox.AddThemeConstantOverride("separation", UiTheme.SpacingMd);
        centerWrapper.AddChild(vbox);

        _title = _factory.CreateTitleLabel("选择节点查看详情", UiTheme.FontSizeXl);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_title);
       
        vbox.AddChild(_factory.CreateSeparatorH(UiTheme.BorderMagic));
       
        _description = _factory.CreateRichText(new Vector2(250, 0));
        vbox.AddChild(_description);
       
        // 命座转变横幅（在按钮上方）
        _careerBanner = _factory.CreateBodyLabel("", UiTheme.TextAccent);
        _careerBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _careerBanner.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _careerBanner.Visible = false;
        vbox.AddChild(_careerBanner);
       
        _activateButton = _factory.CreateButton("点亮", new Vector2(0, 52));
        _activateButton.Disabled = true;
        _activateButton.Pressed += () => EmitSignal(SignalName.ActivateRequested);
        vbox.AddChild(_activateButton);
       
        _jumpButton = _factory.CreateButton("跳跃点亮", new Vector2(0, 52));
        _jumpButton.Disabled = true;
        _jumpButton.Pressed += () => EmitSignal(SignalName.JumpRequested);
        vbox.AddChild(_jumpButton);

        var bottomSpacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        centerWrapper.AddChild(bottomSpacer);
    }

    private void ApplyPanelStyle()
    {
        var panelTexture = TextureAssetResolver.LoadUiTexture(
            "Astral_InfoPanel_Bg",
            "res://BladeHexFrontend/src/assets/ui/Astral_InfoPanel_Bg.png");

        if (panelTexture != null)
        {
            var textureStyle = new StyleBoxTexture
            {
                Texture = panelTexture,
                ModulateColor = new Color(0.82f, 0.84f, 1.0f, 0.9f),
            };
            textureStyle.SetContentMarginAll(UiTheme.SpacingMd + 6);
            AddThemeStyleboxOverride("panel", textureStyle);
            return;
        }

        AddThemeStyleboxOverride(
            "panel",
            UiTheme.MakePanelStyle(UiTheme.BgSecondary, UiTheme.BorderHighlight, 1, UiTheme.RadiusMd, UiTheme.SpacingMd));
    }

    private string BuildNodeDetail(
        SkillNodeData node,
        string tierName,
        int filledTiles,
        int requiredTiles,
        string effectText)
    {
        var lines = new List<string>();
        AddDetailLine(lines, node.GetRegionName(), UiTheme.TextMuted, 13);
        AddDetailLine(lines, tierName, UiTheme.TextMuted, 13);

        string cleanEffect = CleanEffectText(effectText);
        if (!string.IsNullOrWhiteSpace(node.KeystoneCost))
            cleanEffect = string.IsNullOrWhiteSpace(cleanEffect)
                ? node.KeystoneCost
                : $"{cleanEffect}\n{node.KeystoneCost}";
        AddDetailLine(lines, cleanEffect, UiTheme.TextPrimary, 14);

        if (requiredTiles > 0)
            AddDetailLine(lines, $"{filledTiles}/{requiredTiles}", UiTheme.TextMuted, 12);

        return $"[center]{string.Join("\n", lines)}[/center]";
    }

    private string BuildGiantDetail(string effectText)
    {
        var lines = new List<string>();
        AddDetailLine(lines, CleanEffectText(effectText), UiTheme.TextPrimary, 14);
        return $"[center]{string.Join("\n", lines)}[/center]";
    }

    private static void AddDetailLine(List<string> lines, string text, Color color, int fontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lines.Add($"[font_size={fontSize}][color=#{ColorToHex(color)}]{text.Trim()}[/color][/font_size]");
    }

    private static bool IsGiantNode(SkillNodeData node, int requiredTiles)
        => requiredTiles == 12 || node.CurrentNodeType == SkillNodeData.NodeType.Giant;

    private static string GetNodeTierName(int requiredTiles)
    {
        return requiredTiles switch
        {
            1 => "星尘",
            2 => "星点",
            3 or 4 => "星纹",
            6 => "命座",
            12 => "",
            _ => "星纹",
        };
    }

    private static string GetNodeTitle(SkillNodeData node, string tierName, bool isGiant)
    {
        if (isGiant)
            return string.IsNullOrWhiteSpace(node.NodeName) ? node.NodeId : node.NodeName;

        if (IsGenericAttributeName(node.NodeName))
            return tierName;

        return string.IsNullOrWhiteSpace(node.NodeName) ? tierName : node.NodeName;
    }

    private static bool IsGenericAttributeName(string nodeName)
    {
        return string.IsNullOrWhiteSpace(nodeName)
            || nodeName is "属性星纹" or "微光星点" or "星点" or "星尘" or "星纹";
    }

    private static string CleanEffectText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var sb = new StringBuilder();
        foreach (string rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = CleanEffectLine(rawLine.Trim());
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(line);
        }

        return sb.ToString();
    }

    private static string CleanEffectLine(string line)
    {
        string[] prefixes =
        [
            "常驻：",
            "触发：",
            "武器类型：",
            "效果：",
            "节点效果：",
            "属性词条：",
            "装备需求: ",
            "装备需求：",
            "[代价] ",
            "代价：",
        ];

        foreach (string prefix in prefixes)
        {
            if (line.StartsWith(prefix, System.StringComparison.Ordinal))
                return line[prefix.Length..].Trim();
        }

        return line;
    }

    private static string ColorToHex(Color color)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.R * 255.0f), 0, 255);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.G * 255.0f), 0, 255);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.B * 255.0f), 0, 255);
        int a = Mathf.Clamp(Mathf.RoundToInt(color.A * 255.0f), 0, 255);
        return $"{r:X2}{g:X2}{b:X2}{a:X2}";
    }
   
    /// <summary>显示职业速查信息（从职业速查表选择后调用）</summary>
    public void ShowCareerInfo(string careerTitle, string typeTag, string skillName, string skillDesc)
    {
    	if (!_isBuilt) return;
   
    	_careerBanner.Visible = false;
    	_title.Text = careerTitle;
    	_title.RemoveThemeColorOverride("font_color");
   
        var lines = new List<string>();
        AddDetailLine(lines, typeTag, UiTheme.TextMuted, 13);
    	AddDetailLine(lines, skillName, UiTheme.TextPrimary, 14);
    	AddDetailLine(lines, CleanEffectText(skillDesc), UiTheme.TextPrimary, 14);
   
    	_description.Text = $"[center]{string.Join("\n", lines)}[/center]";
    	_activateButton.Disabled = true;
    	_jumpButton.Disabled = true;
    	_activateButton.Text = "点亮";
    	_jumpButton.Text = "跳跃点亮";
    }
   }
