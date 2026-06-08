using BladeHex.Strategic;
using BladeHex.View.AssetSystem;
using Godot;

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
        _description.Text = "点击星图节点查看详情。\n\n[color=gray]每个节点属于一个区域，可能解锁属性、主动技能、被动技能或职业里程碑。[/color]";
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
   
    	_title.Text = node.NodeName;
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
   
    	string nodeTypeText = GetNodeTypeText(node);
    	var figure = SkillNodeShape.GetFigure(node);
    	string figureKind = figure.IsCareerDefining ? "命座" : "星纹";
   
    	string detail = $"[color=gray]{nodeTypeText}[/color]\n";
    	if (!string.IsNullOrWhiteSpace(node.NodeSubtitle))
    		detail += $"[color=white]{node.NodeSubtitle}[/color]\n";
    	detail += $"[color=gray]图形：[/color]{figure.FigureName}（{figureKind}）\n";
    	detail += $"[color=gray]区域：[/color]{node.GetRegionName()}\n";
    	if (requiredTiles > 0)
    		detail += $"[color=gray]瓦片：[/color]{filledTiles}/{requiredTiles}\n";
    	if (node.RequiredLevel > 0)
    		detail += $"[color=gray]需要等级：[/color]{node.RequiredLevel}\n";
   
    	string tileRewardText = GetTileRewardText(node);
    	if (!string.IsNullOrWhiteSpace(tileRewardText))
    		detail += $"\n[color=white]逐片属性：[/color]{tileRewardText}\n";
    	detail += $"[color=white]节点效果：[/color]{effectText}\n";
   
    	if (!string.IsNullOrWhiteSpace(node.KeystoneCost))
    		detail += $"\n[color=red]代价：[/color]{node.KeystoneCost}\n";
    	if (activated)
    		detail += "\n[color=green]✓ 已点亮[/color]";
   
    	_description.Text = detail;
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

    private static string GetNodeTypeText(SkillNodeData node)
    {
    	return node.CurrentNodeType switch
    	{
    		SkillNodeData.NodeType.Giant => "✦ 巨型符文命座",
    		SkillNodeData.NodeType.Big when node.IsActiveSkill => "◆ 主动星纹",
    		SkillNodeData.NodeType.Big => "◆ 被动星纹",
    		SkillNodeData.NodeType.Keystone => "★ 代价命座",
    		SkillNodeData.NodeType.Pip => "· 微型星纹",
    		SkillNodeData.NodeType.Start => "◎ 启程",
    		_ => "● 属性星纹",
    	};
    }

    private static string GetTileRewardText(SkillNodeData node)
    {
    	if (node.CurrentNodeType == SkillNodeData.NodeType.Start)
    		return "";
    	if (node.CurrentContentMode == SkillNodeData.ContentMode.RandomAttribute)
    		return "完成后获得该角色专属随机词条";

    	string region = node.GetRegionName();
    	return string.IsNullOrWhiteSpace(region) ? "" : $"每片 +1 {region}";
    }
   
    /// <summary>显示职业速查信息（从职业速查表选择后调用）</summary>
    public void ShowCareerInfo(string careerTitle, string typeTag, string skillName, string skillDesc)
    {
    	if (!_isBuilt) return;
   
    	_careerBanner.Visible = false;
    	_title.Text = $"职业：{careerTitle}";
    	_title.RemoveThemeColorOverride("font_color");
   
    	string detail = $"[color=gray]类型：[/color]{typeTag}\n";
    	if (!string.IsNullOrWhiteSpace(skillName))
    		detail += $"[color=white]专属技能：[/color]{skillName}\n";
    	if (!string.IsNullOrWhiteSpace(skillDesc))
    		detail += $"[color=white]效果：[/color]{skillDesc}\n";
   
    	_description.Text = detail;
    	_activateButton.Disabled = true;
    	_jumpButton.Disabled = true;
    	_activateButton.Text = "点亮";
    	_jumpButton.Text = "跳跃点亮";
    }
   }
