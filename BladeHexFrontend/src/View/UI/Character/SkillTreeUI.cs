using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.View.AssetSystem;

namespace BladeHex.UI;

[GlobalClass]
public partial class SkillTreeUI : PanelContainer
{
    [Signal] public delegate void NodeClickedEventHandler(string nodeId);
    [Signal] public delegate void CloseRequestedEventHandler();

    private const float HexSize = 48.0f;
    private const float PanSpeed = 500.0f;

    private const int HexagonRadius = SkillTreeData.FixedLayoutRadius;

    private const float RadiusSmall = 8.0f;
    private const float RadiusBig = 13.0f;
    private const float RadiusKeystone = 16.0f;
    private const float RadiusStart = 18.0f;

    private const float ClickRadius = 18.0f;

    private UIFactory _factory = null!;
    private new UITheme Theme => UITheme.Instance!;

    private Control _drawContainer = null!;
    private readonly Dictionary<string, Vector2> _nodeWorldPositions = new();
    private readonly Dictionary<string, Vector2I[]> _nodeShapeTiles = new();
    private readonly Dictionary<string, Vector2[][]> _nodeShapeWorldVertices = new();
    private readonly Dictionary<string, Vector2[]> _nodeShapeBoundaryWorldLines = new();
    private readonly Vector2[] _triVertsScratch = new Vector2[3];
    private readonly Vector2[] _shrunkVertsScratch = new Vector2[3];
    private readonly Vector2[] _outlineScratch = new Vector2[4];
    private readonly Vector2[] _sectorVertsScratch = new Vector2[4];
    private readonly Vector2[] _hexOutlineScratch = new Vector2[7];
    private readonly Vector2[] _startHexVertexScratch = new Vector2[6];
    private readonly Vector2[] _startHexHighlightScratch = new Vector2[3];
    private readonly Color[] _crystalColorsScratch = new Color[3];
    private readonly Vector2[] _highlightVertsScratch = new Vector2[3];
    private Vector2[]? _cachedRingBoundaryWorldLines;
    private CharacterSkillTree? _characterTree;
    private SkillTreeData? _treeData;
    private SkillTreeViewportState _viewport = null!;
    private bool _isPanning = false;
    private bool _isKeyboardPanning = false;
    private bool _leftPressed = false;
    private const float DragThreshold = 4.0f;
    private Vector2 _panStart = Vector2.Zero;
    private string _selectedNodeId = "";
    private string _hoveredNodeId = "";

    private SkillTreeInfoPanel _infoPanel = null!;
    private readonly Dictionary<string, Label> _statLabels = new();

    private UnitData? _currentUnit;
    private IReadOnlyList<UnitData>? _roster;
    private int _rosterIdx = 0;
    private OptionButton? _rosterSelect;
    private HBoxContainer? _equipSlotRow;
    private readonly Button[] _equipSlotButtons = new Button[UnitData.MaxEquippedSkills];
    private PanelContainer? _skillPickerPopup;
    private HBoxContainer? _skillPickerRow;
    private int _selectedEquipSlot = 0;

    private Button? _careerLookupBtn;
    private PanelContainer? _careerLookupPopup;
    private int _pendingCareerLookupFlags = 0;
    private int _highlightCareerFlags = 0;

    private Texture2D? _bgTexture;
    private Texture2D? _starfieldTexture;
    private Texture2D? _hexagramFrameTexture;
    private Texture2D? _runeOverlayTexture;
    private Texture2D? _nodeInactiveTexture;
    private Texture2D? _nodeAvailableTexture;
    private Texture2D? _nodeActiveTexture;
    private Texture2D? _nodeLockedTexture;
    private Texture2D? _selectorRingTexture;
    private Texture2D? _labelPlateTexture;
    private ArrayMesh? _starryMesh;
    private ArrayMesh? _starryOutlineMesh;
    private bool _redrawQueued;

    public override void _Ready()
    {
        _factory = new UIFactory();
        _viewport = new SkillTreeViewportState(new SkillTreeCoord { HexSize = HexSize });
        LoadAstralTextures();
        Setup();
        Visible = false;
    }

    private void RebuildAstralMesh()
    {
        if (_treeData == null || _characterTree == null) return;

        var fillVerts = new List<Vector2>();
        var fillColors = new List<Color>();

        var lineVerts = new List<Vector2>();
        var lineColors = new List<Color>();

        bool hasAttributePoints = _characterTree.AvailableAttributePoints > 0;

        foreach (var pair in _treeData.Nodes)
        {
            string nodeId = pair.Key;
            var node = pair.Value;

            if (!_nodeWorldPositions.TryGetValue(nodeId, out var pos)) continue;

            bool activated = _characterTree.IsActivated(nodeId);
            bool available = _characterTree.IsAvailable(nodeId);
            int filledTiles = _characterTree.GetTileProgress(nodeId);
            bool partiallyFilled = filledTiles > 0 && !activated;
            bool isSelected = nodeId == _selectedNodeId;
            bool isHovered = nodeId == _hoveredNodeId;

            var regionColor = Theme.GetRegionColor(node.CurrentRegion);

            if (nodeId == SkillTreeData.StartNodeId)
            {
                _startHexVertexScratch[0] = _viewport.Coord.VertexToPixel(1, 0);
                _startHexVertexScratch[1] = _viewport.Coord.VertexToPixel(0, 1);
                _startHexVertexScratch[2] = _viewport.Coord.VertexToPixel(-1, 1);
                _startHexVertexScratch[3] = _viewport.Coord.VertexToPixel(-1, 0);
                _startHexVertexScratch[4] = _viewport.Coord.VertexToPixel(0, -1);
                _startHexVertexScratch[5] = _viewport.Coord.VertexToPixel(1, -1);

                for (int i = 0; i < 6; i++)
                {
                    var left = _startHexVertexScratch[i];
                    var right = _startHexVertexScratch[(i + 1) % 6];
                    var col = Theme.GetRegionColor(StartHexRegions[i]);
                    var state = activated ? TileVisualState.Activated : TileVisualState.Locked;

                    AddCrystalTileToMeshArrays(new[] { pos, left, right }, col, state, fillVerts, fillColors, lineVerts, lineColors);
                }

                for (int i = 0; i < 6; i++)
                {
                    lineVerts.Add(_startHexVertexScratch[i]);
                    lineVerts.Add(_startHexVertexScratch[(i + 1) % 6]);
                    var col = new Color(0.85f, 0.75f, 0.45f, 0.6f);
                    lineColors.Add(col);
                    lineColors.Add(col);
                }
                continue;
            }

            if (_nodeShapeWorldVertices.TryGetValue(nodeId, out var shapeVerts))
            {
                bool canFill = available && hasAttributePoints;
                for (int tileIndex = 0; tileIndex < shapeVerts.Length; tileIndex++)
                {
                    var worldVerts = shapeVerts[tileIndex];
                    bool filled = activated || tileIndex < filledTiles;
                    TileVisualState state = filled
                        ? TileVisualState.Activated
                        : canFill
                            ? TileVisualState.Available
                            : TileVisualState.Locked;

                    AddCrystalTileToMeshArrays(worldVerts, regionColor, state, fillVerts, fillColors, lineVerts, lineColors);
                }

                if (_nodeShapeBoundaryWorldLines.TryGetValue(nodeId, out var boundaryLines))
                {
                    bool majorNode = IsMajorShapeNode(node);
                    float colorAlpha = majorNode
                        ? activated ? 0.96f : partiallyFilled ? 0.76f : canFill ? 0.62f : 0.46f
                        : activated ? 0.26f : partiallyFilled ? 0.16f : canFill ? 0.12f : 0.045f;
                    var colColor = new Color(regionColor.R, regionColor.G, regionColor.B, colorAlpha);

                    for (int i = 0; i + 1 < boundaryLines.Length; i += 2)
                    {
                        var from = boundaryLines[i];
                        var to = boundaryLines[i + 1];

                        lineVerts.Add(from);
                        lineVerts.Add(to);
                        lineColors.Add(colColor);
                        lineColors.Add(colColor);
                    }

                    if (majorNode)
                    {
                        var edgeGlow = new Color(
                            MathF.Min(regionColor.R * 1.25f, 1.0f),
                            MathF.Min(regionColor.G * 1.25f, 1.0f),
                            MathF.Min(regionColor.B * 1.25f, 1.0f),
                            activated ? 0.54f : partiallyFilled ? 0.40f : canFill ? 0.30f : 0.24f);
                        for (int i = 0; i + 1 < boundaryLines.Length; i += 2)
                        {
                            lineVerts.Add(boundaryLines[i]);
                            lineVerts.Add(boundaryLines[i + 1]);
                            lineColors.Add(edgeGlow);
                            lineColors.Add(edgeGlow);
                        }
                    }
                }
            }
        }

        _starryMesh = CreateArrayMeshFromArrays(fillVerts, fillColors, Mesh.PrimitiveType.Triangles);
        _starryOutlineMesh = CreateArrayMeshFromArrays(lineVerts, lineColors, Mesh.PrimitiveType.Lines);
    }

    private void LoadAstralTextures()
    {
        const string baseDir = "res://BladeHexFrontend/src/assets/ui/";
        _bgTexture = LoadTextureOrNull("astral_chart_bg", baseDir + "astral_chart_bg.png");
        _starfieldTexture = LoadTextureOrNull("Astral_Starfield_Tile", baseDir + "Astral_Starfield_Tile.png");
        _hexagramFrameTexture = LoadTextureOrNull("Astral_Hexagram_Frame", baseDir + "Astral_Hexagram_Frame.png");
        _runeOverlayTexture = LoadTextureOrNull("Astral_Sector_RuneOverlay", baseDir + "Astral_Sector_RuneOverlay.png");
        _nodeInactiveTexture = LoadTextureOrNull("Astral_Node_Inactive", baseDir + "Astral_Node_Inactive.png");
        _nodeAvailableTexture = LoadTextureOrNull("Astral_Node_Available", baseDir + "Astral_Node_Available.png");
        _nodeActiveTexture = LoadTextureOrNull("Astral_Node_Active", baseDir + "Astral_Node_Active.png");
        _nodeLockedTexture = LoadTextureOrNull("Astral_Node_Locked", baseDir + "Astral_Node_Locked.png");
        _selectorRingTexture = LoadTextureOrNull("Astral_Selector_Ring", baseDir + "Astral_Selector_Ring.png");
        _labelPlateTexture = LoadTextureOrNull("Astral_Label_Plate", baseDir + "Astral_Label_Plate.png");
    }

    private static Texture2D? LoadTextureOrNull(string assetId, string fallbackPath)
    {
        return TextureAssetResolver.LoadUiTexture(assetId, fallbackPath);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        bool needsRedraw = false;
        var panDir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) panDir.Y += 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) panDir.Y -= 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) panDir.X += 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) panDir.X -= 1;

        if (panDir != Vector2.Zero)
        {
            _isKeyboardPanning = true;
            _viewport.PanBy(panDir.Normalized() * PanSpeed * (float)delta);
            needsRedraw = true;
        }
        else if (_isKeyboardPanning)
        {
            _isKeyboardPanning = false;
            needsRedraw = true;
        }

        if (_viewport.UpdateSmoothZoom((float)delta))
            needsRedraw = true;

        if (needsRedraw)
            RequestRedraw();
    }

    private void RequestRedraw()
    {
        if (_redrawQueued || _drawContainer == null) return;
        _redrawQueued = true;
        _drawContainer.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        // 点击弹出层外部 → 关闭弹出层
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            bool closed = false;
            if (_careerLookupPopup?.Visible == true)
            {
                var localPos = _careerLookupPopup.GetLocalMousePosition();
                var rect = new Rect2(Vector2.Zero, _careerLookupPopup.Size);
                if (!rect.HasPoint(localPos))
                {
                    _careerLookupPopup.Hide();
                    closed = true;
                }
            }
            if (_skillPickerPopup?.Visible == true)
            {
                var localPos = _skillPickerPopup.GetLocalMousePosition();
                var rect = new Rect2(Vector2.Zero, _skillPickerPopup.Size);
                if (!rect.HasPoint(localPos))
                {
                    _skillPickerPopup.Hide();
                    closed = true;
                }
            }
            if (closed)
            {
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // ESC → 先关闭弹出层，再关闭星盘
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            if (_careerLookupPopup?.Visible == true) { _careerLookupPopup.Hide(); GetViewport().SetInputAsHandled(); return; }
            if (_skillPickerPopup?.Visible == true) { _skillPickerPopup.Hide(); GetViewport().SetInputAsHandled(); return; }

            Visible = false;
            _careerLookupPopup?.Hide();
            _skillPickerPopup?.Hide();
            EmitSignal(SignalName.CloseRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HideAllPopups()
    {
        if (_careerLookupPopup != null) _careerLookupPopup.Visible = false;
        if (_skillPickerPopup != null) _skillPickerPopup.Visible = false;
    }

    private void Setup()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgPrimary, Theme.BorderHighlight, 2, Theme.RadiusLg, 0));

        var rootMargin = _factory.CreateMargin(20, 20, 15, 15);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", Theme.SpacingMd);
        rootMargin.AddChild(mainVbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", Theme.SpacingMd);
        mainVbox.AddChild(header);

        var title = _factory.CreateTitleLabel("✦ 星 盘 ✦", Theme.FontSizeXxl);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var spLbl = _factory.CreateBodyLabel("星辉: 0", Theme.TextAccent);
        header.AddChild(spLbl);
        _statLabels["skill_points"] = spLbl;

        var jmpLbl = _factory.CreateBodyLabel("跳跃: 0", Theme.TextMagic);
        header.AddChild(jmpLbl);
        _statLabels["jumps"] = jmpLbl;

        var careerLbl = _factory.CreateBodyLabel("职业: 无名者", Theme.TextAccent);
        header.AddChild(careerLbl);
        _statLabels["career"] = careerLbl;

        var careerSkillLbl = _factory.CreateBodyLabel("专属: —", Theme.TextMuted);
        header.AddChild(careerSkillLbl);
        _statLabels["career_skill"] = careerSkillLbl;

        var hintLbl = _factory.CreateBodyLabel("[WASD移动 / 滚轮缩放 / 右键拖拽]", Theme.TextMuted);
        header.AddChild(hintLbl);

        var resetBtn = _factory.CreateButton("回到中心", new Vector2(90, 32));
        resetBtn.Pressed += () => { _viewport.Reset(); RequestRedraw(); };
        header.AddChild(resetBtn);

        var closeBtn = _factory.CreateButton("返回 (ESC)", new Vector2(100, 32));
        closeBtn.Pressed += () => { HideAllPopups(); Visible = false; EmitSignal(SignalName.CloseRequested); };
        header.AddChild(closeBtn);

        mainVbox.AddChild(_factory.CreateSeparatorH());

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", Theme.SpacingLg);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(body);

        var drawPanel = new PanelContainer();
        drawPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        drawPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        drawPanel.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            new Color(0.05f, 0.05f, 0.07f), Theme.BorderDefault, 1, Theme.RadiusMd, 4));
        body.AddChild(drawPanel);

        _drawContainer = new Control();
        _drawContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _drawContainer.ClipContents = true;
        _drawContainer.Draw += OnDraw;
        drawPanel.GuiInput += OnDrawInput;
        _drawContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        drawPanel.AddChild(_drawContainer);

        _drawContainer.TextureFilter = Control.TextureFilterEnum.Linear;

        var infoPanelWrapper = new PanelContainer();
        infoPanelWrapper.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderColor = Theme.BorderHighlight,
            CornerRadiusTopLeft = Theme.RadiusMd,
            CornerRadiusTopRight = Theme.RadiusMd,
            CornerRadiusBottomLeft = Theme.RadiusMd,
            CornerRadiusBottomRight = Theme.RadiusMd,
        });
        body.AddChild(infoPanelWrapper);

        _infoPanel = new SkillTreeInfoPanel();
        _infoPanel.ActivateRequested += OnActivatePressed;
        _infoPanel.JumpRequested += OnJumpPressed;
        infoPanelWrapper.AddChild(_infoPanel);

        mainVbox.AddChild(_factory.CreateSeparatorH());
        BuildSkillLoadoutPanel(mainVbox);
    }

    private void BuildSkillLoadoutPanel(VBoxContainer mainVbox)
    {

    	var panel = new PanelContainer();
    	panel.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
    		new Color(0.045f, 0.045f, 0.06f, 0.96f), Theme.BorderDefault, 1, Theme.RadiusMd, 8));
    	mainVbox.AddChild(panel);

    	var row = new HBoxContainer();
    	row.AddThemeConstantOverride("separation", Theme.SpacingSm);
    	panel.AddChild(row);

    	_rosterSelect = new OptionButton();
    	_rosterSelect.CustomMinimumSize = new Vector2(140, 36);
    	_rosterSelect.ItemSelected += OnRosterSelected;
    	row.AddChild(_rosterSelect);

    	row.AddChild(_factory.CreateSeparatorV());

    	_equipSlotRow = row;
    	for (int i = 0; i < UnitData.MaxEquippedSkills; i++)
    	{
    		int slot = i;
    		var btn = new Button
    		{
    			Text = i < 9 ? $"{i + 1}" : "0",
    			CustomMinimumSize = new Vector2(48, 48),
    			ToggleMode = false,
    		};
    		btn.GuiInput += (ev) => OnEquipSlotGuiInput(ev, slot);
    		_equipSlotButtons[i] = btn;
    		row.AddChild(btn);
    	}

    	row.AddChild(_factory.CreateSeparatorV());

    	_careerLookupBtn = new Button
    	{
    		Text = "职业列表",
    		CustomMinimumSize = new Vector2(90, 36),
    		ToggleMode = false,
    	};
    	_careerLookupBtn.Pressed += OnCareerLookupPressed;
    	row.AddChild(_careerLookupBtn);

    	_careerLookupPopup = new PanelContainer();
    	_careerLookupPopup.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
    		new Color(0.05f, 0.05f, 0.07f, 0.98f), Theme.BorderHighlight, 1, Theme.RadiusSm, 4));
    	_careerLookupPopup.TopLevel = true;
    	_careerLookupPopup.ZIndex = 100;
    	_careerLookupPopup.Visible = false;
    	AddChild(_careerLookupPopup);

    	BuildCareerLookupContent();

    	_skillPickerPopup = new PanelContainer();
    	_skillPickerPopup.AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
    		new Color(0.06f, 0.06f, 0.08f, 0.98f), Theme.BorderHighlight, 1, Theme.RadiusSm, 4));
    	_skillPickerPopup.TopLevel = true;
    	_skillPickerPopup.ZIndex = 100;
    	_skillPickerPopup.Visible = false;
    	AddChild(_skillPickerPopup);

    	_skillPickerRow = new HBoxContainer();
    	_skillPickerRow.AddThemeConstantOverride("separation", 4);
    	_skillPickerPopup.AddChild(_skillPickerRow);
    }

    private void BuildCareerLookupContent()
    {
    	if (_careerLookupPopup == null) return;

    	foreach (var child in _careerLookupPopup.GetChildren())
    		child.QueueFree();

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", Theme.SpacingSm);
        _careerLookupPopup.AddChild(root);

    	var scroll = new ScrollContainer();
    	scroll.CustomMinimumSize = new Vector2(320, 400);
    	scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    	root.AddChild(scroll);

    	var vbox = new VBoxContainer();
    	vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    	vbox.AddThemeConstantOverride("separation", 2);
    	scroll.AddChild(vbox);

    	var registry = CareerSkillRegistry.Registry;
    	var sortedFlags = new List<int>(registry.Keys);
    	sortedFlags.Sort((a, b) =>
    	{
    		int ca = CountBits(a), cb = CountBits(b);
    		return ca != cb ? ca.CompareTo(cb) : a.CompareTo(b);
    	});

    	int lastCount = 0;
    	foreach (int flags in sortedFlags)
    	{
    		int count = CountBits(flags);
    		if (count != lastCount)
    		{
    			string groupLabel = count switch
    			{
    				1 => "— 单属性 —",
    				2 => "— 双属性 —",
    				3 => "— 三属性 —",
    				4 => "— 四属性 —",
    				5 => "— 五属性 —",
    				6 => "— 全属性 —",
    				_ => "— ? —",
    			};
    			var header = _factory.CreateBodyLabel(groupLabel, Theme.TextMuted);
    			header.HorizontalAlignment = HorizontalAlignment.Center;
    			vbox.AddChild(header);
    			lastCount = count;
    		}

    		var skill = registry[flags];
    		string title = ClassTitleResolver.GetTitleByFlags(flags);
    		string typeTag = skill.IsPassive ? "被动" : "主动";

    		var btn = new Button
    		{
    			Text = $"{title} [{typeTag}]",
    			CustomMinimumSize = new Vector2(300, 32),
    			TooltipText = skill.Description,
    		};
    		int capturedFlags = flags;
    		btn.Pressed += () => PreviewCareerLookup(capturedFlags);
    		vbox.AddChild(btn);
    	}

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", Theme.SpacingSm);
        root.AddChild(actions);

        var resetBtn = _factory.CreateButton("重置", new Vector2(0, 36));
        resetBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        resetBtn.Pressed += ResetCareerLookupPreview;
        actions.AddChild(resetBtn);

        var confirmBtn = _factory.CreateButton("确定", new Vector2(0, 36));
        confirmBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        confirmBtn.Pressed += ConfirmCareerLookupPreview;
        actions.AddChild(confirmBtn);
    }

    private static int CountBits(int n)
    {
    	int count = 0;
    	while (n != 0) { count++; n &= n - 1; }
    	return count;
    }

    private void OnCareerLookupPressed()
    {
    	if (_careerLookupPopup == null || _careerLookupBtn == null) return;

    	Vector2 globalPos = _careerLookupBtn.GlobalPosition;
    	Vector2 popupMinSize = _careerLookupPopup.GetCombinedMinimumSize();
    	float offsetY = -(popupMinSize.Y + 4);
    	_careerLookupPopup.Position = new Vector2I(
    		(int)globalPos.X,
    		(int)(globalPos.Y + offsetY));
    	_careerLookupPopup.Visible = true;
    }

    private void PreviewCareerLookup(int flags)
    {
        _pendingCareerLookupFlags = flags;
    	_highlightCareerFlags = flags;
    	RequestRedraw();

    	var skill = CareerSkillRegistry.GetByTitleFlags(flags);
    	string title = ClassTitleResolver.GetTitleByFlags(flags);
    	if (skill != null)
    	{
    		string typeTag = skill.IsPassive ? "被动" : "主动";
    		_infoPanel.ShowCareerInfo(title, typeTag, skill.DisplayName, skill.Description);
    	}
    	else
    	{
    		_infoPanel.ShowCareerInfo(title, "", "", "");
    	}
    }

    private void ResetCareerLookupPreview()
    {
        _pendingCareerLookupFlags = 0;
        _highlightCareerFlags = 0;
        UpdateInfoPanel();
        RequestRedraw();
    }

    private void ConfirmCareerLookupPreview()
    {
        if (_pendingCareerLookupFlags != 0)
            _highlightCareerFlags = _pendingCareerLookupFlags;
    	_careerLookupPopup?.Hide();
    }

    public void OpenSkillTree(CharacterSkillTree characterTree, SkillTreeData treeData,
        UnitData? unit = null, IReadOnlyList<UnitData>? roster = null)
    {
        _characterTree = characterTree;
        _treeData = treeData;
        _currentUnit = unit;
        _roster = roster;
        _rosterIdx = 0;
        if (roster != null && unit != null)
        {
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] == unit) { _rosterIdx = i; break; }
        }
        _viewport.Reset();
        _selectedNodeId = "";
        SyncCurrentUnitSkillTree();
        Visible = true;
        CallDeferred(nameof(DeferredOpen));
    }

    private void SyncCurrentUnitSkillTree()
    {
        if (_currentUnit == null || _characterTree == null) return;
        _currentUnit.Runtime.SkillTree = _characterTree;
        _currentUnit.SkillTreeData = _characterTree.Serialize();
    }

    private void DeferredOpen()
    {
        _viewport.SetCenterFromCanvas(_drawContainer.Size);
        _currentUnit?.SanitizeEquipmentBySkillTree();
        RebuildPositions();
        UpdateStats();
        UpdateInfoPanel();
        RefreshLoadoutPanel();
        RequestRedraw();
    }

    private void RefreshLoadoutPanel()
    {
    	RefreshRosterSelect();
    	SanitizeEquippedSkills();
    	RefreshEquipSlotButtons();
    }

    private void RefreshRosterSelect()
    {
        if (_rosterSelect == null) return;
        _rosterSelect.Clear();
        if (_roster == null || _roster.Count == 0)
        {
            _rosterSelect.AddItem(_currentUnit?.UnitName ?? "当前角色", 0);
            _rosterSelect.Select(0);
            return;
        }

        for (int i = 0; i < _roster.Count; i++)
        {
            var unit = _roster[i];
            _rosterSelect.AddItem(unit?.UnitName ?? $"角色 {i + 1}", i);
        }
        _rosterSelect.Select(Mathf.Clamp(_rosterIdx, 0, _roster.Count - 1));
    }

    private void OnRosterSelected(long index)
    {
        if (_roster == null || index < 0 || index >= _roster.Count) return;
        var unit = _roster[(int)index];
        var stm = Globals.SkillTreesOrNull;
        if (stm?.TreeData == null) return;

        long charId = unit.CharacterId >= 0 ? unit.CharacterId : (long)unit.GetInstanceId();
        var tree = stm.GetSkillTree(charId) ?? stm.CreateSkillTree(charId, unit.Level);
        stm.InitCharacterLevel(charId, unit.Level);
        _currentUnit = unit;
        _characterTree = tree;
        _treeData = stm.TreeData;
        _rosterIdx = (int)index;
        _selectedNodeId = "";
        SyncCurrentUnitSkillTree();
        _currentUnit.SanitizeEquipmentBySkillTree();
        RebuildPositions();
        UpdateStats();
        UpdateInfoPanel();
        RefreshLoadoutPanel();
        RequestRedraw();
    }

    private void SanitizeEquippedSkills()
    {
        if (_currentUnit == null) return;
        while (_currentUnit.EquippedSkills.Count < UnitData.MaxEquippedSkills)
            _currentUnit.EquippedSkills.Add("");

        for (int i = 0; i < UnitData.MaxEquippedSkills; i++)
        {
            string entry = _currentUnit.GetEquippedSkill(i);
            if (string.IsNullOrEmpty(entry)) continue;
            if (!IsLoadoutEntryValid(entry))
                _currentUnit.SetEquippedSkill(i, "");
        }
    }

    private bool IsLoadoutEntryValid(string entry)
    {
        if (_currentUnit == null || string.IsNullOrEmpty(entry)) return false;

        if (SpellStudyCatalog.IsEquippedSpellEntry(entry))
        {
            string spellId = SpellStudyCatalog.GetSpellIdFromEntry(entry);
            return SpellStudyCatalog.GetKnownSpell(_currentUnit, spellId) != null;
        }

        return SkillRegistry.IsEquippableCombatSkill(entry)
            && _characterTree?.HasSkillEffect(entry) == true;
    }

    private void RefreshEquipSlotButtons()
    {
    	if (_currentUnit == null) return;

    	for (int i = 0; i < UnitData.MaxEquippedSkills; i++)
    	{
    		var btn = _equipSlotButtons[i];
    		if (btn == null) continue;

    		string effect = _currentUnit.GetEquippedSkill(i);
    		string label = i < 9 ? $"{i + 1}" : "0";
    		if (!string.IsNullOrEmpty(effect))
    			label = GetSkillDisplayName(effect);

    		btn.Text = label.Length > 4 ? label[..4] : label;
    		btn.TooltipText = string.IsNullOrEmpty(effect) ? "空槽位 (右键选择)" : GetSkillTooltip(effect);
    	}
    }

    private void OnEquipSlotGuiInput(InputEvent ev, int slot)
    {
    	if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
    	{
    		_selectedEquipSlot = Mathf.Clamp(slot, 0, UnitData.MaxEquippedSkills - 1);
    		ShowSkillPickerPopup(slot);
    	}
    }

    private void ShowSkillPickerPopup(int slot)
    {
    	if (_skillPickerPopup == null || _skillPickerRow == null) return;

    	foreach (var child in _skillPickerRow.GetChildren())
    		child.QueueFree();

    	var clearBtn = new Button
    	{
    		Text = "✕",
    		CustomMinimumSize = new Vector2(48, 48),
    		TooltipText = "卸下该槽位",
    	};
    	clearBtn.AddThemeFontSizeOverride("font_size", 14);
    	clearBtn.Pressed += () =>
    	{
    		if (_currentUnit != null)
    		{
    			_currentUnit.SetEquippedSkill(slot, "");
    			RefreshEquipSlotButtons();
    			SyncCurrentUnitSkillTree();
    		}
    		_skillPickerPopup.Hide();
    	};
    	_skillPickerRow.AddChild(clearBtn);

    	if (_characterTree != null)
    	{
    		foreach (var node in _characterTree.GetActiveSkills())
    		{
    			string effect = node.SkillEffect;
    			if (!SkillRegistry.IsEquippableCombatSkill(effect)) continue;

    			string name = node.NodeName;
    			string marker = _currentUnit?.IsSkillEquipped(effect) == true ? " ✓" : "";
    			var skillBtn = new Button
    			{
    				Text = (name.Length > 3 ? name[..3] : name) + marker,
    				CustomMinimumSize = new Vector2(48, 48),
    				TooltipText = GetSkillTooltip(effect),
    			};
    			skillBtn.AddThemeFontSizeOverride("font_size", 11);
    			string capturedEffect = effect;
    			skillBtn.Pressed += () =>
    			{
    				if (_currentUnit != null)
    				{
    					_currentUnit.SetEquippedSkill(slot, capturedEffect);
    					RefreshEquipSlotButtons();
    					SyncCurrentUnitSkillTree();
    				}
    				_skillPickerPopup.Hide();
    			};
    			_skillPickerRow.AddChild(skillBtn);
    		}
    	}

    	if (_currentUnit?.KnownSpells != null)
    	{
    		foreach (var spell in _currentUnit.KnownSpells)
    		{
    			if (spell == null || string.IsNullOrEmpty(spell.SpellId)) continue;

    			string entry = SpellStudyCatalog.MakeEquippedSpellEntry(spell.SpellId);
    			string marker = _currentUnit.IsSkillEquipped(entry) ? " ✓" : "";
    			int tier = Mathf.Max(0, (int)spell.tier);
    			var spellBtn = new Button
    			{
    				Text = (spell.SpellName.Length > 3 ? spell.SpellName[..3] : spell.SpellName) + marker,
    				CustomMinimumSize = new Vector2(48, 48),
    				TooltipText = $"{spell.SpellName} ({tier}环)",
    			};
    			spellBtn.AddThemeFontSizeOverride("font_size", 11);
    			string capturedEntry = entry;
    			spellBtn.Pressed += () =>
    			{
    				if (_currentUnit != null)
    				{
    					_currentUnit.SetEquippedSkill(slot, capturedEntry);
    					RefreshEquipSlotButtons();
    					SyncCurrentUnitSkillTree();
    				}
    				_skillPickerPopup.Hide();
    			};
    			_skillPickerRow.AddChild(spellBtn);
    		}
    	}

    	var slotBtn = _equipSlotButtons[slot];
    	if (slotBtn != null)
    	{
    		Vector2 popupMinSize = _skillPickerPopup.GetCombinedMinimumSize();
    		Vector2 globalPos = slotBtn.GlobalPosition;
    		float offsetY = -(popupMinSize.Y + 4);
    		_skillPickerPopup.Position = new Vector2I(
    			(int)globalPos.X,
    			(int)(globalPos.Y + offsetY));
    	}

    	_skillPickerPopup.Visible = true;
    }

    private string GetSkillDisplayName(string effect)
    {
        if (_currentUnit != null && SpellStudyCatalog.IsEquippedSpellEntry(effect))
        {
            string spellId = SpellStudyCatalog.GetSpellIdFromEntry(effect);
            var spell = SpellStudyCatalog.GetKnownSpell(_currentUnit, spellId) ?? SpellStudyCatalog.FindById(spellId);
            return spell?.SpellName ?? spellId;
        }

        if (_characterTree != null)
        {
            foreach (var node in _characterTree.GetActiveSkills())
                if (node.SkillEffect == effect) return node.NodeName;
        }

        var cfg = SkillRegistry.GetSkillConfig(effect);
        return cfg.ContainsKey("name") ? cfg["name"].AsString() : effect;
    }

    private string GetSkillTooltip(string effect)
    {
        if (_currentUnit != null && SpellStudyCatalog.IsEquippedSpellEntry(effect))
        {
            string spellId = SpellStudyCatalog.GetSpellIdFromEntry(effect);
            var spell = SpellStudyCatalog.GetKnownSpell(_currentUnit, spellId) ?? SpellStudyCatalog.FindById(spellId);
            if (spell == null) return effect;

            int apCost = spell.castingTime == SpellData.CastingTime.MainAction ? 4 : 0;
            var spellTooltip = new System.Text.StringBuilder();
            spellTooltip.Append(spell.SpellName);
            if (!string.IsNullOrEmpty(spell.Description))
                spellTooltip.Append($"\n{spell.Description}");
            spellTooltip.Append($"\nAP {apCost} / 法力 {spell.ManaCost} / 射程 {spell.RangeCells}");
            return spellTooltip.ToString();
        }

        var cfg = SkillRegistry.GetSkillConfig(effect);
        if (cfg.Count == 0) return effect;
        string name = cfg.ContainsKey("name") ? cfg["name"].AsString() : effect;
        string desc = cfg.ContainsKey("description") ? cfg["description"].AsString() : "";
        string equipment = SkillRegistry.GetEquipmentRequirementText(effect);

        var sb = new System.Text.StringBuilder(name);
        if (!string.IsNullOrEmpty(desc))
            sb.Append($"\n{desc}");
        AppendSkillSpecLines(sb, cfg);
        if (!string.IsNullOrEmpty(equipment))
            sb.Append($"\n装备需求: {equipment}");
        return sb.ToString();
    }

    private static string BuildActionCostText(Godot.Collections.Dictionary cfg)
    {
        if (cfg.ContainsKey("weapon_ap_bonus"))
        {
            int bonus = cfg["weapon_ap_bonus"].AsInt32();
            return bonus == 0 ? "武器AP" : $"武器AP+{bonus}";
        }
        if (cfg.ContainsKey("movement_ap_bonus"))
        {
            int bonus = cfg["movement_ap_bonus"].AsInt32();
            return bonus == 0 ? "距离AP" : $"距离AP+{bonus}";
        }
        if (!cfg.ContainsKey("action_cost"))
            return "";

        int cost = cfg["action_cost"].AsInt32();
        return cost == 0 ? "0 (免费行动)" : cost.ToString();
    }

    private static string BuildUsageLimitText(Godot.Collections.Dictionary cfg)
    {
        var parts = new List<string>();
        if (cfg.ContainsKey("uses_per_battle"))
        {
            int uses = cfg["uses_per_battle"].AsInt32();
            if (uses > 0)
                parts.Add($"每场战斗 {uses} 次");
        }
        if (cfg.ContainsKey("cooldown"))
        {
            int cooldown = cfg["cooldown"].AsInt32();
            if (cooldown > 0)
                parts.Add($"冷却 {cooldown} 回合");
        }
        return string.Join("；", parts);
    }

    private static string BuildCostText(Godot.Collections.Dictionary cfg)
    {
        var parts = new List<string>();
        string apText = BuildActionCostText(cfg);
        if (!string.IsNullOrWhiteSpace(apText) && apText != "0 (免费行动)")
            parts.Add($"AP {apText}");

        int manaCost = cfg.ContainsKey("mana_cost") ? cfg["mana_cost"].AsInt32() : 0;
        if (manaCost > 0)
            parts.Add($"法力 {manaCost}");

        if (parts.Count == 0 && (cfg.ContainsKey("action_cost") || cfg.ContainsKey("mana_cost")))
            return "无";

        return string.Join(" / ", parts);
    }

    private static void AppendSkillSpecLines(System.Text.StringBuilder sb, Godot.Collections.Dictionary cfg)
    {
        string usageLimit = BuildUsageLimitText(cfg);
        if (!string.IsNullOrWhiteSpace(usageLimit))
            sb.Append($"\n使用限制：{usageLimit}");

        string cost = BuildCostText(cfg);
        if (!string.IsNullOrWhiteSpace(cost))
            sb.Append($"\n消耗：{cost}");
    }

    private static void AppendDefaultGiantActiveSpecLines(System.Text.StringBuilder sb)
    {
        sb.Append("\n使用限制：每场战斗 1 次");
        sb.Append("\n消耗：无");
    }

    private Vector2 NodeToPixel(SkillNodeData node)
    {
        return _viewport.NodeToWorld(node);
    }

    private Vector2 VertexToScreen(int q, int r)
    {
        return _viewport.VertexToScreen(q, r);
    }

    private static bool IsInsideHexagon(int x, int y, int radius)
    {
        int z = -x - y;
        return Math.Abs(x) <= radius && Math.Abs(y) <= radius && Math.Abs(z) <= radius;
    }

    private static List<Vector2I> GetHexagonGridPoints(int radius)
    {
        var points = new List<Vector2I>();
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (IsInsideHexagon(x, y, radius))
                    points.Add(new Vector2I(x, y));
        return points;
    }

    private static readonly Vector2I[] GridDirections =
    {
        new(1, 0),   new(-1, 0),
        new(0, 1),   new(0, -1),
        new(1, -1),  new(-1, 1),
    };

    private static readonly Vector2I[] HexOutlineVertices =
    [
        new(HexagonRadius, 0),
        new(0, HexagonRadius),
        new(-HexagonRadius, HexagonRadius),
        new(-HexagonRadius, 0),
        new(0, -HexagonRadius),
        new(HexagonRadius, -HexagonRadius),
    ];

    private static readonly SkillNodeData.Region[] HexOutlineRegions =
    [
        SkillNodeData.Region.Int,
        SkillNodeData.Region.Con,
        SkillNodeData.Region.Str,
        SkillNodeData.Region.Dex,
        SkillNodeData.Region.Cha,
        SkillNodeData.Region.Wis
    ];

    private static readonly SkillNodeData.Region[] StartHexRegions =
    [
        SkillNodeData.Region.Int,
        SkillNodeData.Region.Con,
        SkillNodeData.Region.Str,
        SkillNodeData.Region.Dex,
        SkillNodeData.Region.Cha,
        SkillNodeData.Region.Wis
    ];

    private static readonly int[] RingBoundaryRadii = [8, 15];

    private static readonly SkillNodeData.Region[] HexSectorRegions =
    [
        SkillNodeData.Region.Int,
        SkillNodeData.Region.Con,
        SkillNodeData.Region.Str,
        SkillNodeData.Region.Dex,
        SkillNodeData.Region.Cha,
        SkillNodeData.Region.Wis
    ];

    private void RebuildPositions()
    {
        if (_treeData == null) return;
        _nodeWorldPositions.Clear();
        _nodeShapeTiles.Clear();
        _nodeShapeWorldVertices.Clear();
        _nodeShapeBoundaryWorldLines.Clear();
        foreach (var pair in _treeData.Nodes)
        {
            var node = pair.Value;
            _nodeWorldPositions[pair.Key] = NodeToPixel(node);
            if (pair.Value.NodeId != SkillTreeData.StartNodeId)
            {
                var shapeTiles = SkillNodeShape.GetTiles(node);
                _nodeShapeTiles[pair.Key] = shapeTiles;
                _nodeShapeWorldVertices[pair.Key] = BuildShapeWorldVertices(shapeTiles);
                _nodeShapeBoundaryWorldLines[pair.Key] = BuildShapeBoundaryWorldLines(shapeTiles);
                _nodeWorldPositions[pair.Key] = GetShapeWorldCenter(shapeTiles);
            }
        }
        RebuildAstralMesh();
    }

    private Vector2[][] BuildShapeWorldVertices(Vector2I[] shapeTiles)
    {
        var result = new Vector2[shapeTiles.Length][];
        for (int i = 0; i < shapeTiles.Length; i++)
            result[i] = _viewport.Coord.TileVertices(shapeTiles[i]);
        return result;
    }

    private Vector2 GetShapeWorldCenter(Vector2I[] shapeTiles)
    {
        if (shapeTiles.Length == 0) return Vector2.Zero;

        var sum = Vector2.Zero;
        foreach (var tile in shapeTiles)
            sum += _viewport.Coord.TileCentroid(tile);
        return sum / shapeTiles.Length;
    }

    private Vector2[] BuildShapeBoundaryWorldLines(Vector2I[] shapeTiles)
    {
        var edgeCounts = new Dictionary<(Vector2I A, Vector2I B), int>();
        foreach (var tile in shapeTiles)
        {
            var vertices = GetTileVertexCoords(tile);
            AddShapeEdge(edgeCounts, vertices[0], vertices[1]);
            AddShapeEdge(edgeCounts, vertices[1], vertices[2]);
            AddShapeEdge(edgeCounts, vertices[2], vertices[0]);
        }

        var lines = new List<Vector2>();
        foreach (var kvp in edgeCounts)
        {
            if (kvp.Value != 1) continue;
            lines.Add(_viewport.Coord.VertexToPixel(kvp.Key.A.X, kvp.Key.A.Y));
            lines.Add(_viewport.Coord.VertexToPixel(kvp.Key.B.X, kvp.Key.B.Y));
        }

        return lines.ToArray();
    }

    private static Vector2I[] GetTileVertexCoords(Vector2I encoded)
    {
        var (q, r, t) = SkillTreeCoord.DecodeTile(encoded);
        return t == 0
            ? [new Vector2I(q, r), new Vector2I(q + 1, r), new Vector2I(q, r + 1)]
            : [new Vector2I(q + 1, r), new Vector2I(q, r + 1), new Vector2I(q + 1, r + 1)];
    }

    private static void AddShapeEdge(Dictionary<(Vector2I A, Vector2I B), int> edges, Vector2I a, Vector2I b)
    {
        var key = CompareVertex(a, b) <= 0 ? (a, b) : (b, a);
        edges[key] = edges.GetValueOrDefault(key, 0) + 1;
    }

    private static int CompareVertex(Vector2I a, Vector2I b)
    {
        int x = a.X.CompareTo(b.X);
        return x != 0 ? x : a.Y.CompareTo(b.Y);
    }

    private bool IsPerformancePanning() => _isPanning || _isKeyboardPanning || _viewport.IsZoomAnimating;

    private Transform2D CreateWorldDrawTransform()
    {
        var scale = _viewport.Zoom;
        return new Transform2D(
            new Vector2(scale, 0),
            new Vector2(0, scale),
            _viewport.Center + _viewport.PanOffset);
    }

    private Rect2 GetWorldCullRect(float screenMargin)
    {
        var min = _viewport.ScreenToWorld(new Vector2(-screenMargin, -screenMargin));
        var max = _viewport.ScreenToWorld(_drawContainer.Size + new Vector2(screenMargin, screenMargin));
        return new Rect2(min, max - min);
    }

    private float GetNodeRadius(SkillNodeData node)
    {
        return node.CurrentNodeType switch
        {
            SkillNodeData.NodeType.Start => RadiusStart * _viewport.Zoom,
            SkillNodeData.NodeType.Giant => (RadiusKeystone + 5.0f) * _viewport.Zoom,
            SkillNodeData.NodeType.Keystone => RadiusKeystone * _viewport.Zoom,
            SkillNodeData.NodeType.Pip => (RadiusSmall * 0.75f) * _viewport.Zoom,
            SkillNodeData.NodeType.Big => RadiusBig * _viewport.Zoom,
            _ => RadiusSmall * _viewport.Zoom,
        };
    }

    private void OnDrawInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
            {
                _viewport.ZoomBySmooth(1.12f, mouseBtn.Position);
                RequestRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
            {
                _viewport.ZoomBySmooth(0.88f, mouseBtn.Position);
                RequestRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Middle && mouseBtn.Pressed)
            {
            	_viewport.Reset();
            	RequestRedraw();
            	GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Left)
            {
            	if (mouseBtn.Pressed)
            	{

            		_panStart = mouseBtn.Position;
            		_isPanning = false;
            		_leftPressed = true;
            	}
            	else
            	{

            		if (_leftPressed && !_isPanning)
            		{
            			HandleClick(mouseBtn.Position);
            			GetViewport().SetInputAsHandled();
            		}
            		_isPanning = false;
            		_leftPressed = false;
            	}
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
        	if (_isPanning)
        	{
        		_viewport.PanBy(mouseMotion.Position - _panStart);
        		_panStart = mouseMotion.Position;
        		RequestRedraw();
        	}
        	else if (_leftPressed && !_isPanning)
        	{

        		if (mouseMotion.Position.DistanceTo(_panStart) > DragThreshold)
        		{
        			_isPanning = true;
        			_panStart = mouseMotion.Position;
        			GetViewport().SetInputAsHandled();
        		}
        	}
        	else
        	{

        		string newHover = HitTestNode(mouseMotion.Position);
        		if (newHover != _hoveredNodeId)
        		{
        			_hoveredNodeId = newHover;
        			RequestRedraw();
        		}
        	}
        }
    }

    private string HitTestNode(Vector2 pos)
    {
        if (_treeData == null) return "";
        var worldPos = _viewport.ScreenToWorld(pos);

        foreach (var pair in _nodeShapeWorldVertices)
        {
            if (pair.Key == "start") continue;
            foreach (var tri in pair.Value)
            {
                if (PointInTriangle(worldPos, tri[0], tri[1], tri[2]))
                    return pair.Key;
            }
        }

        float bestDist = ClickRadius;
        string bestId = "";
        foreach (var pair in _nodeWorldPositions)
        {
            if (pair.Key == "start") continue;

            float dist = worldPos.DistanceTo(pair.Value);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestId = pair.Key;
            }
        }
        return bestId;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = SignedTriangleArea(p, a, b);
        float d2 = SignedTriangleArea(p, b, c);
        float d3 = SignedTriangleArea(p, c, a);

        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float SignedTriangleArea(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private void HandleClick(Vector2 pos)
    {
        string nodeId = HitTestNode(pos);
        if (!string.IsNullOrEmpty(nodeId))
        {
            bool clickedSelectedNode = nodeId == _selectedNodeId;
            _selectedNodeId = nodeId;

            if (clickedSelectedNode && _characterTree?.IsActivated(nodeId) != true)
            {
                OnActivatePressed();
                return;
            }

            UpdateInfoPanel();
            if (_characterTree?.IsActivated(nodeId) == true)
                TryOpenSpellStudyForNode(nodeId);
            RequestRedraw();
        }
    }

    private enum TileVisualState
    {
        Locked,
        Available,
        Activated
    }

    private void DrawCrystalTile(
        Vector2[] vertices,
        Color regionColor,
        TileVisualState state,
        float outlineWidth = 1.0f)
    {
        if (vertices.Length < 3) return;

        Vector2 centroid = (vertices[0] + vertices[1] + vertices[2]) / 3.0f;

        for (int i = 0; i < 3; i++)
            _shrunkVertsScratch[i] = centroid + (vertices[i] - centroid) * 0.94f;

        Color innerColor, outerColor, outlineColor;
        switch (state)
        {
            case TileVisualState.Activated:
                innerColor = new Color(regionColor.R * 1.10f, regionColor.G * 1.10f, regionColor.B * 1.10f, 0.54f);
                outerColor = new Color(regionColor.R * 0.42f, regionColor.G * 0.42f, regionColor.B * 0.42f, 0.24f);
                outlineColor = new Color(regionColor.R * 0.55f, regionColor.G * 0.55f, regionColor.B * 0.55f, 0.24f);
                break;
            case TileVisualState.Available:
                innerColor = new Color(regionColor.R * 0.62f, regionColor.G * 0.62f, regionColor.B * 0.62f, 0.16f);
                outerColor = new Color(regionColor.R * 0.24f, regionColor.G * 0.24f, regionColor.B * 0.24f, 0.045f);
                outlineColor = new Color(regionColor.R * 0.40f, regionColor.G * 0.40f, regionColor.B * 0.40f, 0.11f);
                break;
            case TileVisualState.Locked:
            default:
                innerColor = new Color(regionColor.R * 0.22f, regionColor.G * 0.22f, regionColor.B * 0.22f, 0.028f);
                outerColor = new Color(regionColor.R * 0.10f, regionColor.G * 0.10f, regionColor.B * 0.10f, 0.006f);
                outlineColor = new Color(regionColor.R * 0.10f, regionColor.G * 0.10f, regionColor.B * 0.10f, 0.04f);
                break;
        }

        if (_viewport.Zoom < 0.35f)
        {
            Color flatColor = new Color(innerColor.R, innerColor.G, innerColor.B, innerColor.A * 0.8f);
            _drawContainer.DrawColoredPolygon(_shrunkVertsScratch, flatColor);
            DrawTriOutline(_shrunkVertsScratch, outlineColor, outlineWidth * 0.6f);
            return;
        }

        _crystalColorsScratch[0] = outerColor;
        _crystalColorsScratch[1] = outerColor;
        _crystalColorsScratch[2] = innerColor;

        _triVertsScratch[0] = _shrunkVertsScratch[0];
        _triVertsScratch[1] = _shrunkVertsScratch[1];
        _triVertsScratch[2] = centroid;
        _drawContainer.DrawPolygon(_triVertsScratch, _crystalColorsScratch);

        _triVertsScratch[0] = _shrunkVertsScratch[1];
        _triVertsScratch[1] = _shrunkVertsScratch[2];
        _triVertsScratch[2] = centroid;
        _drawContainer.DrawPolygon(_triVertsScratch, _crystalColorsScratch);

        _triVertsScratch[0] = _shrunkVertsScratch[2];
        _triVertsScratch[1] = _shrunkVertsScratch[0];
        _triVertsScratch[2] = centroid;
        _drawContainer.DrawPolygon(_triVertsScratch, _crystalColorsScratch);

        if (state == TileVisualState.Activated)
        {
            for (int i = 0; i < 3; i++)
                _highlightVertsScratch[i] = centroid + (_shrunkVertsScratch[i] - centroid) * 1.16f;
            _drawContainer.DrawColoredPolygon(_highlightVertsScratch, new Color(regionColor.R, regionColor.G, regionColor.B, 0.11f));
            for (int i = 0; i < 3; i++)
                _highlightVertsScratch[i] = centroid + (_shrunkVertsScratch[i] - centroid) * 1.04f;
            _drawContainer.DrawColoredPolygon(_highlightVertsScratch, new Color(regionColor.R * 1.15f, regionColor.G * 1.15f, regionColor.B * 1.15f, 0.10f));
            for (int i = 0; i < 3; i++)
                _highlightVertsScratch[i] = centroid + (_shrunkVertsScratch[i] - centroid) * 0.32f;
            _drawContainer.DrawColoredPolygon(_highlightVertsScratch, new Color(regionColor.R * 1.25f, regionColor.G * 1.25f, regionColor.B * 1.25f, 0.16f));
        }
        else if (state == TileVisualState.Available)
        {
            for (int i = 0; i < 3; i++)
                _highlightVertsScratch[i] = centroid + (_shrunkVertsScratch[i] - centroid) * 0.32f;
            _drawContainer.DrawColoredPolygon(_highlightVertsScratch, new Color(regionColor.R * 1.4f, regionColor.G * 1.4f, regionColor.B * 1.4f, 0.15f));
        }

        DrawTriOutline(_shrunkVertsScratch, outlineColor, outlineWidth);
    }

    private void _DrawBackground()
    {

        _drawContainer.DrawRect(new Rect2(Vector2.Zero, _drawContainer.Size),
            new Color(0.02f, 0.02f, 0.04f, 1.0f), true);

        var bgTexture = _starfieldTexture ?? _bgTexture;
        if (bgTexture == null) return;

        var canvasSize = _drawContainer.Size;
        int texW = bgTexture.GetWidth();
        int texH = bgTexture.GetHeight();
        if (texW <= 0 || texH <= 0) return;

        float offX = _viewport.PanOffset.X % texW;
        float offY = _viewport.PanOffset.Y % texH;

        if (offX < 0) offX += texW;
        if (offY < 0) offY += texH;

        var tint = bgTexture == _starfieldTexture
            ? new Color(0.78f, 0.75f, 0.68f, 0.52f)
            : new Color(1f, 1f, 1f, 0.45f);

        for (float x = -texW + offX; x < canvasSize.X; x += texW)
        {
            for (float y = -texH + offY; y < canvasSize.Y; y += texH)
            {
                _drawContainer.DrawTextureRect(bgTexture, new Rect2(x, y, texW, texH), false, tint);
            }
        }
    }

    private static float GetNodeTextureSize(SkillNodeData node)
    {
        return node.CurrentNodeType switch
        {
            SkillNodeData.NodeType.Giant => 58.0f,
            SkillNodeData.NodeType.Keystone => 48.0f,
            SkillNodeData.NodeType.Big => 38.0f,
            SkillNodeData.NodeType.Pip => 18.0f,
            _ => 26.0f,
        };
    }

    private void OnDraw()
    {
        _redrawQueued = false;
        if (_treeData == null) return;

        _DrawBackground();

        _drawContainer.DrawSetTransformMatrix(CreateWorldDrawTransform());

        DrawAstralBoardTexturesWorld();
        _DrawGridLatticeWorld();

        bool hasAttributePoints = (_characterTree?.AvailableAttributePoints ?? 0) > 0;
        bool performancePanning = IsPerformancePanning();

        if (_starryMesh != null)
        {
            _drawContainer.DrawMesh(_starryMesh, null);
            if (_starryOutlineMesh != null)
                _drawContainer.DrawMesh(_starryOutlineMesh, null);
            DrawPerformanceSelectionWorld();
        }
        else
        {
            Rect2 cullRectWorld = GetWorldCullRect(180.0f);

            foreach (var pair in _treeData.Nodes)
            {
                if (!_nodeWorldPositions.TryGetValue(pair.Key, out var worldPos)) continue;
                if (!cullRectWorld.HasPoint(worldPos)) continue;

                DrawNodeWorld(pair.Key, pair.Value, worldPos, hasAttributePoints);
            }
        }

        _drawContainer.DrawSetTransformMatrix(Transform2D.Identity);

        if (!performancePanning)
        {
            foreach (var pair in _treeData.Nodes)
            {
                if (!_nodeWorldPositions.TryGetValue(pair.Key, out var worldPos)) continue;
                var screenPos = _viewport.WorldToScreen(worldPos);

                if (!new Rect2(Vector2.Zero, _drawContainer.Size).Grow(100.0f).HasPoint(screenPos)) continue;

                DrawNodeLabels(pair.Key, pair.Value, screenPos, hasAttributePoints);
            }

            DrawRegionLabels();
        }
    }

    private void DrawAstralBoardTexturesWorld()
    {
        DrawAttributeSectorFillsWorld();

        var circleBounds = GetHexCircumcircleWorldBounds();
        var frameRect = ExpandRectForTextureContent(circleBounds, 56.0f / 1024.0f, 56.0f / 1024.0f, 968.0f / 1024.0f, 968.0f / 1024.0f);

        if (_hexagramFrameTexture != null)
            _drawContainer.DrawTextureRect(_hexagramFrameTexture, frameRect, false, new Color(0.82f, 0.74f, 0.62f, 0.28f));

        if (_runeOverlayTexture != null)
        {

            _drawContainer.DrawTextureRect(_runeOverlayTexture, circleBounds, false, new Color(0.68f, 0.60f, 0.48f, 0.14f));
        }

        DrawAttributeSectorSeparatorsWorld();
    }

    private void DrawPerformanceSelectionWorld()
    {
        DrawPerformanceNodeOverlay(_selectedNodeId, new Color(0.95f, 0.78f, 0.36f, 0.75f), 3.0f);
        DrawPerformanceNodeOverlay(_hoveredNodeId, new Color(0.82f, 0.76f, 0.62f, 0.45f), 2.0f);
    }

    private void DrawPerformanceNodeOverlay(string nodeId, Color color, float width)
    {
        if (string.IsNullOrEmpty(nodeId) || _treeData == null) return;
        if (!_treeData.Nodes.TryGetValue(nodeId, out var node)) return;
        if (!_nodeWorldPositions.TryGetValue(nodeId, out var center)) return;

        if (nodeId != SkillTreeData.StartNodeId && _nodeShapeBoundaryWorldLines.ContainsKey(nodeId))
        {
            DrawShapeBoundary(nodeId, color, width, worldSpace: true);
            return;
        }

        float radius = node.CurrentNodeType switch
        {
            SkillNodeData.NodeType.Start => 24.0f,
            SkillNodeData.NodeType.Giant => 26.0f,
            SkillNodeData.NodeType.Keystone => 22.0f,
            SkillNodeData.NodeType.Big => 18.0f,
            SkillNodeData.NodeType.Pip => 9.0f,
            _ => 13.0f,
        };
        _drawContainer.DrawArc(center, radius, 0, MathF.PI * 2.0f, 24, color, width, true);
    }

    private void DrawAttributeSectorFillsWorld()
    {
        var center = Vector2.Zero;
        for (int i = 0; i < HexSectorRegions.Length; i++)
        {
            var leftVertex = _viewport.Coord.VertexToPixel(HexOutlineVertices[i].X, HexOutlineVertices[i].Y);
            var rightVertex = _viewport.Coord.VertexToPixel(
                HexOutlineVertices[(i + 1) % HexOutlineVertices.Length].X,
                HexOutlineVertices[(i + 1) % HexOutlineVertices.Length].Y);
            var color = Theme.GetRegionColor(HexSectorRegions[i]);
   
            // 职业速查高亮：如果该扇区属于选中的职业 flags，增加亮度
            float alpha = 0.115f;
            if (_highlightCareerFlags != 0)
            {
                int sectorFlag = ClassTitleResolver.GetNodeRegionFlag(new SkillNodeData { CurrentRegion = HexSectorRegions[i] });
                if ((sectorFlag & _highlightCareerFlags) != 0)
                    alpha = 0.35f;
            }
   
            _triVertsScratch[0] = center;
            _triVertsScratch[1] = leftVertex;
            _triVertsScratch[2] = rightVertex;
            _drawContainer.DrawColoredPolygon(_triVertsScratch, new Color(color.R, color.G, color.B, alpha));
        }
    }

    private void DrawAttributeSectorSeparatorsWorld()
    {
        var center = Vector2.Zero;
        for (int i = 0; i < HexOutlineVertices.Length; i++)
        {
            var boundary = _viewport.Coord.VertexToPixel(HexOutlineVertices[i].X, HexOutlineVertices[i].Y);
            var leftRegion = HexSectorRegions[(i + HexSectorRegions.Length - 1) % HexSectorRegions.Length];
            var rightRegion = HexSectorRegions[i % HexSectorRegions.Length];
            var leftColor = Theme.GetRegionColor(leftRegion);
            var rightColor = Theme.GetRegionColor(rightRegion);
            var splitColor = new Color(
                (leftColor.R + rightColor.R) * 0.5f,
                (leftColor.G + rightColor.G) * 0.5f,
                (leftColor.B + rightColor.B) * 0.5f,
                0.68f);

            _drawContainer.DrawLine(center, boundary, new Color(0.0f, 0.0f, 0.0f, 0.72f), 5.5f);
            _drawContainer.DrawLine(center, boundary, splitColor, 1.65f);
        }
    }

    private void DrawRingBoundaryLinesWorld()
    {
        _EnsureRingBoundaryCache();

        var worldLines = _cachedRingBoundaryWorldLines;
        if (worldLines == null || worldLines.Length == 0)
            return;

        _drawContainer.DrawSetTransformMatrix(Transform2D.Identity);

        for (int i = 0; i + 1 < worldLines.Length; i += 2)
        {
            var from = SnapScreenPoint(_viewport.WorldToScreen(worldLines[i]));
            var to = SnapScreenPoint(_viewport.WorldToScreen(worldLines[i + 1]));
            _drawContainer.DrawLine(from, to, new Color(0.0f, 0.0f, 0.0f, 0.72f), 4.0f);
            _drawContainer.DrawLine(from, to, new Color(1.0f, 1.0f, 1.0f, 0.72f), 1.5f);
        }

        _drawContainer.DrawSetTransformMatrix(CreateWorldDrawTransform());
    }

    private static Vector2 SnapScreenPoint(Vector2 point)
    {
        return new Vector2(MathF.Round(point.X) + 0.5f, MathF.Round(point.Y) + 0.5f);
    }

    private void _EnsureRingBoundaryCache()
    {
        if (_cachedRingBoundaryWorldLines != null)
            return;

        var lines = new List<Vector2>(RingBoundaryRadii.Length * 12);
        foreach (int radius in RingBoundaryRadii)
        {
            for (int i = 0; i < 6; i++)
            {
                var fromVertex = GetRingBoundaryVertex(radius, i);
                var toVertex = GetRingBoundaryVertex(radius, (i + 1) % 6);
                lines.Add(_viewport.Coord.VertexToPixel(fromVertex.X, fromVertex.Y));
                lines.Add(_viewport.Coord.VertexToPixel(toVertex.X, toVertex.Y));
            }
        }

        _cachedRingBoundaryWorldLines = lines.ToArray();
    }

    private static Vector2I GetRingBoundaryVertex(int radius, int index)
    {
        return (index % 6) switch
        {
            0 => new Vector2I(radius, 0),
            1 => new Vector2I(0, radius),
            2 => new Vector2I(-radius, radius),
            3 => new Vector2I(-radius, 0),
            4 => new Vector2I(0, -radius),
            _ => new Vector2I(radius, -radius),
        };
    }

    private Rect2 GetHexCircumcircleWorldBounds()
    {
        float radius = 0.0f;

        foreach (var vertex in HexOutlineVertices)
        {
            var p = _viewport.Coord.VertexToPixel(vertex.X, vertex.Y);
            radius = MathF.Max(radius, p.Length());
        }

        return new Rect2(new Vector2(-radius, -radius), new Vector2(radius * 2.0f, radius * 2.0f));
    }

    private static Rect2 ExpandRectForTextureContent(Rect2 visibleBounds, float minU, float minV, float maxU, float maxV)
    {
        float contentU = MathF.Max(0.01f, maxU - minU);
        float contentV = MathF.Max(0.01f, maxV - minV);
        var rectSize = new Vector2(visibleBounds.Size.X / contentU, visibleBounds.Size.Y / contentV);
        var rectPos = visibleBounds.Position - new Vector2(rectSize.X * minU, rectSize.Y * minV);
        return new Rect2(rectPos, rectSize);
    }

    private void DrawTriOutline(Vector2[] verts, Color color, float width)
    {
        _outlineScratch[0] = verts[0];
        _outlineScratch[1] = verts[1];
        _outlineScratch[2] = verts[2];
        _outlineScratch[3] = verts[0];
        _drawContainer.DrawPolyline(_outlineScratch, color, width);
    }

    private List<Vector2I>? _cachedHexPoints;
    private HashSet<Vector2I>? _cachedHexPointSet;
    private List<(Vector2I, Vector2I)>? _cachedHexEdges;
    private Vector2[]? _cachedHexPointWorld;
    private Vector2[]? _cachedHexEdgeWorldLines;
    private Vector2[]? _scratchHexEdgeScreenLines;

    private void _EnsureLatticeCache()
    {
        if (_cachedHexPoints != null) return;
        _cachedHexPoints = GetHexagonGridPoints(HexagonRadius);
        _cachedHexPointSet = new HashSet<Vector2I>(_cachedHexPoints);
        _cachedHexEdges = new List<(Vector2I, Vector2I)>();
        _cachedHexPointWorld = new Vector2[_cachedHexPoints.Count];
        for (int i = 0; i < _cachedHexPoints.Count; i++)
        {
            var p = _cachedHexPoints[i];
            _cachedHexPointWorld[i] = _viewport.Coord.VertexToPixel(p.X, p.Y);
        }

        var drawnEdges = new HashSet<(Vector2I, Vector2I)>();
        foreach (var p in _cachedHexPoints)
        {
            foreach (var dir in GridDirections)
            {
                var nb = p + dir;
                if (!_cachedHexPointSet.Contains(nb)) continue;
                var edge = string.Compare($"{p.X},{p.Y}", $"{nb.X},{nb.Y}") < 0
                    ? (p, nb) : (nb, p);
                if (drawnEdges.Contains(edge)) continue;
                drawnEdges.Add(edge);
                _cachedHexEdges.Add(edge);
            }
        }

        _cachedHexEdgeWorldLines = new Vector2[_cachedHexEdges.Count * 2];
        _scratchHexEdgeScreenLines = new Vector2[_cachedHexEdgeWorldLines.Length];
        for (int i = 0; i < _cachedHexEdges.Count; i++)
        {
            var edge = _cachedHexEdges[i];
            _cachedHexEdgeWorldLines[i * 2] = _viewport.Coord.VertexToPixel(edge.Item1.X, edge.Item1.Y);
            _cachedHexEdgeWorldLines[i * 2 + 1] = _viewport.Coord.VertexToPixel(edge.Item2.X, edge.Item2.Y);
        }
    }

    private void _DrawGridLatticeWorld()
    {
        _EnsureLatticeCache();

        if (IsPerformancePanning() || _viewport.Zoom < 0.42f)
        {
            _DrawHexagonOutlineWorld();
            DrawRingBoundaryLinesWorld();
            return;
        }

        var latticeColor = new Color(0.18f, 0.2f, 0.3f, 0.4f);
        float lineWidth = 1.0f;
        var worldLines = _cachedHexEdgeWorldLines!;
        if (worldLines.Length > 0)
            _drawContainer.DrawMultiline(worldLines, latticeColor, lineWidth);

        _DrawHexagonOutlineWorld();
        DrawRingBoundaryLinesWorld();
    }

    private void _DrawHexagonOutlineWorld()
    {
        var center = _viewport.Coord.VertexToPixel(0, 0);
        float dividerWidth = 1.2f;

        for (int i = 0; i < 6; i++)
        {
            var vertex = _viewport.Coord.VertexToPixel(HexOutlineVertices[i].X, HexOutlineVertices[i].Y);
            Color regCol = Theme.GetRegionColor(HexOutlineRegions[i]);
            Color lineCol = new Color(regCol.R, regCol.G, regCol.B, 0.28f);
            _drawContainer.DrawLine(center, vertex, lineCol, dividerWidth);

            var mid = (center + vertex) / 2.0f;
            _drawContainer.DrawCircle(mid, 2.2f, new Color(regCol.R, regCol.G, regCol.B, 0.45f));
            _drawContainer.DrawCircle(vertex, 4.5f, new Color(regCol.R, regCol.G, regCol.B, 0.65f));
        }

        var outlineColor = new Color(0.45f, 0.48f, 0.58f, 0.55f);
        float lineWidth = 2.0f;
        for (int i = 0; i < 6; i++)
            _hexOutlineScratch[i] = _viewport.Coord.VertexToPixel(HexOutlineVertices[i].X, HexOutlineVertices[i].Y);
        _hexOutlineScratch[6] = _hexOutlineScratch[0];
        _drawContainer.DrawPolyline(_hexOutlineScratch, outlineColor, lineWidth);
    }

    private void DrawNodeWorld(string nodeId, SkillNodeData node, Vector2 pos, bool hasAttributePoints)
    {
        bool activated = _characterTree?.IsActivated(nodeId) ?? false;
        bool available = _characterTree?.IsAvailable(nodeId) ?? false;
        bool isSelected = nodeId == _selectedNodeId;
        bool isHovered = nodeId == _hoveredNodeId;
        int filledTiles = _characterTree?.GetTileProgress(nodeId) ?? 0;
        bool partiallyFilled = filledTiles > 0 && !activated;
        var regionColor = Theme.GetRegionColor(node.CurrentRegion);

        if (_viewport.Zoom < 0.42f)
        {

            float radius = node.CurrentNodeType switch
            {
                SkillNodeData.NodeType.Start => 10.0f,
                SkillNodeData.NodeType.Giant => 10.0f,
                SkillNodeData.NodeType.Keystone => 8.0f,
                SkillNodeData.NodeType.Big => 6.0f,
                SkillNodeData.NodeType.Pip => 3.5f,
                _ => 4.5f,
            };

            Color dotColor = activated
                ? new Color(regionColor.R * 1.3f, regionColor.G * 1.3f, regionColor.B * 1.3f, 0.9f)
                : (available && hasAttributePoints)
                    ? new Color(regionColor.R * 0.95f, regionColor.G * 0.95f, regionColor.B * 0.95f, 0.6f)
                    : partiallyFilled
                        ? new Color(regionColor.R * 0.7f, regionColor.G * 0.7f, regionColor.B * 0.7f, 0.45f)
                        : new Color(regionColor.R * 0.35f, regionColor.G * 0.35f, regionColor.B * 0.35f, 0.25f);

            _drawContainer.DrawCircle(pos, radius, dotColor);

            if (isSelected)
            {
                _drawContainer.DrawCircle(pos, radius + 3.0f, new Color(0.95f, 0.78f, 0.36f, 0.6f));
                _drawContainer.DrawCircle(pos, radius + 1.0f, new Color(1.0f, 1.0f, 1.0f, 0.8f));
            }
            else if (isHovered)
            {
                _drawContainer.DrawCircle(pos, radius + 2.0f, new Color(0.82f, 0.76f, 0.62f, 0.4f));
            }
            return;
        }

        if (nodeId == "start")
        {
            DrawStartNodeHexWorld(pos, _characterTree?.IsActivated(nodeId) ?? true);
            return;
        }
        var centroid = pos;
        if (!DrawNodeTileShapeWorld(nodeId, regionColor, filledTiles, activated, available && hasAttributePoints, partiallyFilled))
            return;

        DrawNodeStateTextureWorld(node, centroid, activated, available && hasAttributePoints, partiallyFilled);
        DrawNodeSeparationWorld(node, nodeId, regionColor, activated, available && hasAttributePoints, partiallyFilled);

        if (node.CurrentNodeType == SkillNodeData.NodeType.Keystone)
        {
            float starRingRadius = 24.0f;
            Color ringCol = new Color(regionColor.R, regionColor.G, regionColor.B, activated ? 0.48f : 0.22f);
            _DrawDashedCircleWorld(centroid, starRingRadius, ringCol, 1.1f);
        }
        else if (node.CurrentNodeType == SkillNodeData.NodeType.Big)
        {
            float bigRingRadius = 18.0f;
            Color ringCol = new Color(regionColor.R, regionColor.G, regionColor.B, activated ? 0.36f : 0.18f);
            _drawContainer.DrawArc(centroid, bigRingRadius, 0, MathF.PI * 2.0f, 24, ringCol, 0.8f, true);
        }

        if (isSelected)
        {
            DrawSelectorRingWorld(centroid, node, new Color(0.95f, 0.78f, 0.36f, 0.58f));
            DrawShapeBoundary(nodeId, new Color(1.0f, 0.85f, 0.3f, 0.9f), 2.5f, worldSpace: true);
        }
        else if (isHovered)
        {
            DrawSelectorRingWorld(centroid, node, new Color(0.82f, 0.76f, 0.62f, 0.34f));
            DrawShapeBoundary(nodeId, new Color(1.0f, 1.0f, 1.0f, 0.7f), 2.5f, worldSpace: true);
        }
    }

    private void DrawNodeLabels(string nodeId, SkillNodeData node, Vector2 screenPos, bool hasAttributePoints)
    {
        if (nodeId == "start")
        {
            DrawStartNodeLabel(screenPos);
            return;
        }

        bool activated = _characterTree?.IsActivated(nodeId) ?? false;
        bool available = _characterTree?.IsAvailable(nodeId) ?? false;
        bool isSelected = nodeId == _selectedNodeId;
        bool isHovered = nodeId == _hoveredNodeId;

        var regionColor = Theme.GetRegionColor(node.CurrentRegion);

        bool showName = false;
        if (activated && node.CurrentNodeType != SkillNodeData.NodeType.Small)
            showName = true;
        else if (available && hasAttributePoints)
            showName = true;
        else if (isSelected || isHovered)
            showName = true;

        if (showName && _viewport.Zoom >= 0.45f)
        {
            float fontSize = node.CurrentNodeType == SkillNodeData.NodeType.Small ? 10.0f : 12.0f;
            fontSize = Mathf.Clamp(fontSize * _viewport.Zoom, 8.0f, 16.0f);
            var nameColor = activated ? new Color(1.0f, 0.98f, 0.95f) : new Color(0.85f, 0.85f, 0.9f, 0.9f);

            var font = ThemeDB.FallbackFont;
            string nameText = _characterTree?.GetEffectiveNode(node).NodeName ?? node.NodeName;

            Vector2 stringSize = font.GetStringSize(nameText, HorizontalAlignment.Center, -1, (int)fontSize);

            float padX = 7.0f * _viewport.Zoom;
            float padY = 3.5f * _viewport.Zoom;
            Vector2 boxSize = new Vector2(stringSize.X + padX * 2, stringSize.Y + padY * 2);

            float radius = GetNodeRadius(node);
            var labelCenter = screenPos + new Vector2(0, radius + 15.0f * _viewport.Zoom);
            Vector2 boxPos = labelCenter - new Vector2(boxSize.X / 2.0f, boxSize.Y / 2.0f);

            Color bgBoxColor = new Color(0.02f, 0.02f, 0.04f, 0.85f);
            _drawContainer.DrawRect(new Rect2(boxPos, boxSize), bgBoxColor, true);

            Color boxBorderColor = activated ? new Color(regionColor.R, regionColor.G, regionColor.B, 0.4f) : new Color(0.4f, 0.4f, 0.5f, 0.25f);
            _drawContainer.DrawRect(new Rect2(boxPos, boxSize), boxBorderColor, false, 1.0f);

            float ascent = font.GetAscent((int)fontSize);
            var textPos = new Vector2(boxPos.X, boxPos.Y + padY + ascent);
            _drawContainer.DrawString(font, textPos,
                nameText, HorizontalAlignment.Center, boxSize.X, (int)fontSize, nameColor);
        }
    }

    private void DrawNodeStateTextureWorld(
        SkillNodeData node,
        Vector2 center,
        bool activated,
        bool canFill,
        bool partiallyFilled)
    {
        Texture2D? texture = activated
            ? _nodeActiveTexture
            : (canFill || partiallyFilled)
                ? _nodeAvailableTexture
                : _nodeLockedTexture ?? _nodeInactiveTexture;

        if (texture == null) return;

        float size = GetNodeTextureSize(node);
        float alpha = activated ? 0.75f : canFill ? 0.55f : partiallyFilled ? 0.50f : 0.35f;
        DrawCenteredTexture(texture, center, size, new Color(1.0f, 1.0f, 1.0f, alpha));
    }

    private void DrawSelectorRingWorld(Vector2 center, SkillNodeData node, Color tint)
    {
        if (_selectorRingTexture == null) return;
        DrawCenteredTexture(_selectorRingTexture, center, GetNodeTextureSize(node) * 1.9f, tint);
    }

    private void DrawCenteredTexture(Texture2D texture, Vector2 center, float size, Color tint)
    {
        if (size <= 0.0f) return;
        var rect = new Rect2(center - new Vector2(size / 2.0f, size / 2.0f), new Vector2(size, size));
        _drawContainer.DrawTextureRect(texture, rect, false, tint);
    }

    private bool DrawNodeTileShapeWorld(
        string nodeId,
        Color regionColor,
        int filledTiles,
        bool activated,
        bool canFill,
        bool partiallyFilled)
    {
        if (!_nodeShapeWorldVertices.TryGetValue(nodeId, out var shapeVerts)) return false;

        for (int tileIndex = 0; tileIndex < shapeVerts.Length; tileIndex++)
        {
            var worldVerts = shapeVerts[tileIndex];
            bool filled = activated || tileIndex < filledTiles;
            TileVisualState state = filled
                ? TileVisualState.Activated
                : canFill
                    ? TileVisualState.Available
                    : TileVisualState.Locked;

            DrawCrystalTile(worldVerts, regionColor, state, 1.0f);
        }

        float boundaryAlpha = activated ? 0.78f : partiallyFilled ? 0.56f : canFill ? 0.42f : 0.16f;
        DrawShapeBoundary(nodeId, new Color(regionColor.R, regionColor.G, regionColor.B, boundaryAlpha), 1.15f, worldSpace: true);
        return true;
    }

    private void DrawNodeSeparationWorld(
        SkillNodeData node,
        string nodeId,
        Color regionColor,
        bool activated,
        bool canFill,
        bool partiallyFilled)
    {
        bool majorNode = IsMajorShapeNode(node);
        if (!majorNode)
        {
            float lowAlpha = activated ? 0.46f : partiallyFilled ? 0.30f : canFill ? 0.22f : 0.10f;
            DrawShapeBoundary(nodeId, new Color(regionColor.R, regionColor.G, regionColor.B, lowAlpha), 0.9f, worldSpace: true);
            return;
        }

        float colorAlpha = activated ? 0.88f : partiallyFilled ? 0.66f : canFill ? 0.52f : 0.32f;
        DrawShapeBoundary(nodeId, new Color(0.0f, 0.0f, 0.0f, 0.70f), 3.0f, worldSpace: true);
        DrawShapeBoundary(nodeId, new Color(regionColor.R, regionColor.G, regionColor.B, colorAlpha), 1.65f, worldSpace: true);
        DrawShapeBoundary(nodeId, new Color(
            MathF.Min(regionColor.R * 1.25f, 1.0f),
            MathF.Min(regionColor.G * 1.25f, 1.0f),
            MathF.Min(regionColor.B * 1.25f, 1.0f),
            activated ? 0.32f : 0.18f), 2.6f, worldSpace: true);
    }

    private static bool IsMajorShapeNode(SkillNodeData node)
        => node.CurrentNodeType != SkillNodeData.NodeType.Pip
            && node.CurrentNodeType != SkillNodeData.NodeType.Small
            && node.CurrentNodeType != SkillNodeData.NodeType.Start;

    private void DrawShapeBoundary(string nodeId, Color color, float width, bool worldSpace)
    {
        if (!_nodeShapeBoundaryWorldLines.TryGetValue(nodeId, out var worldLines)) return;

        for (int i = 0; i + 1 < worldLines.Length; i += 2)
        {
            var from = worldSpace ? worldLines[i] : _viewport.WorldToScreen(worldLines[i]);
            var to = worldSpace ? worldLines[i + 1] : _viewport.WorldToScreen(worldLines[i + 1]);
            _drawContainer.DrawLine(from, to, color, width);
        }
    }

    private void DrawStartNodeHexWorld(Vector2 pos, bool activated)
    {
        _startHexVertexScratch[0] = _viewport.Coord.VertexToPixel(1, 0);
        _startHexVertexScratch[1] = _viewport.Coord.VertexToPixel(0, 1);
        _startHexVertexScratch[2] = _viewport.Coord.VertexToPixel(-1, 1);
        _startHexVertexScratch[3] = _viewport.Coord.VertexToPixel(-1, 0);
        _startHexVertexScratch[4] = _viewport.Coord.VertexToPixel(0, -1);
        _startHexVertexScratch[5] = _viewport.Coord.VertexToPixel(1, -1);

        for (int i = 0; i < 6; i++)
        {
            var a = _startHexVertexScratch[i];
            var b = _startHexVertexScratch[(i + 1) % 6];

            Color col = Theme.GetRegionColor(StartHexRegions[i]);
            TileVisualState state = activated ? TileVisualState.Activated : TileVisualState.Locked;

            DrawCrystalTile(new[] { pos, a, b }, col, state, 1.0f);
        }

        for (int i = 0; i < 6; i++)
        {
            _drawContainer.DrawLine(_startHexVertexScratch[i], _startHexVertexScratch[(i + 1) % 6], new Color(0.85f, 0.75f, 0.45f, 0.6f), 1.3f);
        }

        _drawContainer.DrawCircle(pos, 3.5f, new Color(1.0f, 1.0f, 1.0f, 0.95f));
        _drawContainer.DrawCircle(pos, 8.0f, new Color(1.0f, 1.0f, 1.0f, 0.35f));
        _drawContainer.DrawCircle(pos, 14.0f, new Color(1.0f, 1.0f, 1.0f, 0.12f));
    }

    private void DrawStartNodeLabel(Vector2 screenPos)
    {
        if (_viewport.Zoom >= 0.55f)
        {
            float fontSize = 11.0f * _viewport.Zoom;
            fontSize = Mathf.Clamp(fontSize, 8.0f, 16.0f);
            var font = ThemeDB.FallbackFont;
            string nameText = "启程";
            Vector2 stringSize = font.GetStringSize(nameText, HorizontalAlignment.Center, -1, (int)fontSize);

            var boxSize = new Vector2(stringSize.X + 8.0f, stringSize.Y + 4.0f);
            var boxPos = screenPos - boxSize / 2.0f;
            float ascent = font.GetAscent((int)fontSize);
            var textPos = new Vector2(boxPos.X, boxPos.Y + (boxSize.Y - stringSize.Y) / 2.0f + ascent);

            _drawContainer.DrawString(font, textPos + new Vector2(1, 1), nameText, HorizontalAlignment.Center, boxSize.X, (int)fontSize, new Color(0, 0, 0, 0.85f));
            _drawContainer.DrawString(font, textPos + new Vector2(-1, -1), nameText, HorizontalAlignment.Center, boxSize.X, (int)fontSize, new Color(0, 0, 0, 0.85f));

            _drawContainer.DrawString(font, textPos, nameText, HorizontalAlignment.Center, boxSize.X, (int)fontSize, new Color(1.0f, 0.96f, 0.9f));
        }
    }

    private void _DrawDashedCircleWorld(Vector2 center, float radius, Color color, float width)
    {
        int segments = 8;
        float arcLen = MathF.PI * 2.0f / (segments * 2);
        for (int i = 0; i < segments; i++)
        {
            float startAngle = i * (arcLen * 2);
            _drawContainer.DrawArc(center, radius, startAngle, startAngle + arcLen, 6, color, width, true);
        }
    }

    private void DrawRegionLabels()
    {
        if (IsPerformancePanning()) return;

        Vector2I[] verts = new[]
        {
            new Vector2I(HexagonRadius, 0),
            new Vector2I(0, HexagonRadius),
            new Vector2I(-HexagonRadius, HexagonRadius),
            new Vector2I(-HexagonRadius, 0),
            new Vector2I(0, -HexagonRadius),
            new Vector2I(HexagonRadius, -HexagonRadius),
        };

        var sectorLabels = new (int Sector, SkillNodeData.Region Region, string Name)[]
        {
            (0, SkillNodeData.Region.Int, "✦ INT 智力 ✦"),
            (1, SkillNodeData.Region.Con, "✦ CON 体魄 ✦"),
            (2, SkillNodeData.Region.Str, "✦ STR 力量 ✦"),
            (3, SkillNodeData.Region.Dex, "✦ DEX 灵巧 ✦"),
            (4, SkillNodeData.Region.Cha, "✦ CHA 魅力 ✦"),
            (5, SkillNodeData.Region.Wis, "✦ WIS 感知 ✦"),
        };

        foreach (var (idx, region, name) in sectorLabels)
        {
            var left = VertexToScreen(verts[idx].X, verts[idx].Y);
            var right = VertexToScreen(verts[(idx + 1) % verts.Length].X, verts[(idx + 1) % verts.Length].Y);
            var sectorPx = (left + right) / 2.0f;
            var centerPx = VertexToScreen(0, 0);
            var outDir = (sectorPx - centerPx).Normalized();
            var lp = sectorPx + outDir * 38.0f * _viewport.Zoom;

            var col = Theme.GetRegionColor(region);
            var font = ThemeDB.FallbackFont;
            float fontSize = 15.0f * _viewport.Zoom;
            fontSize = Mathf.Clamp(fontSize, 10.0f, 22.0f);
            Vector2 stringSize = font.GetStringSize(name, HorizontalAlignment.Center, -1, (int)fontSize);

            float padX = 10.0f * _viewport.Zoom;

            float padY = 5.0f * _viewport.Zoom;
            Vector2 boxSize = new Vector2(stringSize.X + padX * 2, stringSize.Y + padY * 2);
            Vector2 boxPos = lp - new Vector2(boxSize.X / 2.0f, stringSize.Y / 2.0f + padY);

            var labelRect = new Rect2(boxPos, boxSize);
            if (_labelPlateTexture != null)
                _drawContainer.DrawTextureRect(_labelPlateTexture, labelRect, false, new Color(1.0f, 1.0f, 1.0f, 0.9f));
            else
                _drawContainer.DrawRect(labelRect, new Color(0.03f, 0.03f, 0.06f, 0.85f), true);

            _drawContainer.DrawRect(labelRect, new Color(col.R, col.G, col.B, 0.55f), false, 1.2f * _viewport.Zoom);

            float ascent = font.GetAscent((int)fontSize);
            _drawContainer.DrawString(font,
                new Vector2(boxPos.X, boxPos.Y + padY + ascent),
                name, HorizontalAlignment.Center, boxSize.X, (int)fontSize, col);
        }
    }

    private void OnActivatePressed()
    {
        if (_characterTree == null || string.IsNullOrEmpty(_selectedNodeId)) return;
        if (!CanActivateSelectedNode()) return;
        var r = _characterTree.TryActivateNode(_selectedNodeId);
        if ((bool)r["success"])
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("char_node_activate");
            RefreshAfterChange((string)r["message"]);
            if (r.ContainsKey("completed") && r["completed"].AsBool())
                TryOpenSpellStudyForNode(_selectedNodeId);
        }
        else
        {
            _infoPanel.ShowError((string)r["message"]);
        }
    }

    private void OnJumpPressed()
    {
        if (_characterTree == null || string.IsNullOrEmpty(_selectedNodeId)) return;
        if (!CanActivateSelectedNode()) return;
        var r = _characterTree.TryJumpActivate(_selectedNodeId);
        if ((bool)r["success"])
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("char_node_activate");
            RefreshAfterChange((string)r["message"]);
            if (r.ContainsKey("completed") && r["completed"].AsBool())
                TryOpenSpellStudyForNode(_selectedNodeId);
        }
        else
        {
            _infoPanel.ShowError((string)r["message"]);
        }
    }

    private bool CanActivateSelectedNode()
    {
        if (_treeData == null || _currentUnit == null || string.IsNullOrEmpty(_selectedNodeId))
            return true;
        if (!_treeData.Nodes.TryGetValue(_selectedNodeId, out var node))
            return true;
        string effect = _characterTree?.GetEffectiveSkillEffect(node.NodeId) ?? node.SkillEffect;
        if (!string.Equals(effect, "absolute_focus", StringComparison.Ordinal))
            return true;
        if (SkillTreeKeystoneResolver.CanActivateAbsoluteFocus(_currentUnit, out var reason))
            return true;

        _infoPanel.ShowError(reason);
        return false;
    }

    private void RefreshAfterChange(string msg)
    {
        SyncCurrentUnitSkillTree();
        SanitizeEquippedSkills();
        _currentUnit?.SanitizeEquipmentBySkillTree();
        UpdateStats();
        UpdateInfoPanel();
        RefreshLoadoutPanel();
        RebuildAstralMesh();
        RequestRedraw();
        _infoPanel.AppendSuccessMessage(msg);
    }

    private void TryOpenSpellStudyForNode(string nodeId)
    {
        if (_treeData == null || _currentUnit == null) return;
        if (!_treeData.Nodes.TryGetValue(nodeId, out var node)) return;
        var effectiveNode = _characterTree?.GetEffectiveNode(node) ?? node;
        if (!SpellStudyCatalog.IsSpellSlotEffect(effectiveNode.SkillEffect)) return;

        int tier = SpellStudyCatalog.GetTierFromSpellSlotEffect(effectiveNode.SkillEffect);
        if (tier <= 0) return;

        string existing = SpellStudyCatalog.GetKnownSpellNameForTier(_currentUnit, tier);
        if (!string.IsNullOrEmpty(existing))
        {
            _infoPanel.AppendSuccessMessage($"已掌握 {tier} 环法术：{existing}");
            return;
        }

        ShowSpellStudyDialog(tier, effectiveNode.NodeName);
    }

    private void ShowSpellStudyDialog(int tier, string nodeName)
    {
        var options = SpellStudyCatalog.GetOptions(tier);
        if (options.Length == 0 || _currentUnit == null) return;

        var dialog = new AcceptDialog
        {
            Title = $"{nodeName}：选择 {tier} 环法术",
            Exclusive = true,
            MinSize = new Vector2I(520, 360),
        };

        var list = new ItemList
        {
            CustomMinimumSize = new Vector2(480, 240),
            SelectMode = ItemList.SelectModeEnum.Single,
        };

        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            int idx = list.AddItem($"{opt.SchoolName} - {opt.SpellName}");
            list.SetItemMetadata(idx, i);
            list.SetItemTooltip(idx, opt.Description);
            if (!SkillTreeKeystoneResolver.CanStudySpell(_currentUnit, opt.Spell))
            {
                list.SetItemDisabled(idx, true);
                list.SetItemTooltip(idx, opt.Description + "\n绝对专注：只能研习已锁定学派。");
            }
        }

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        var hint = _factory.CreateBodyLabel("法术研习完成后，请从 5 个学派中选择 1 个法术。", Theme.TextMuted);
        body.AddChild(hint);
        body.AddChild(list);
        dialog.AddChild(body);

        int selected = FindFirstStudyableSpellIndex(options);
        if (selected >= 0)
            list.Select(selected);
        list.ItemSelected += idx =>
        {
            int optionIndex = list.GetItemMetadata((int)idx).AsInt32();
            selected = IsStudyableSpellOption(options, optionIndex) ? optionIndex : -1;
        };
        list.ItemActivated += idx =>
        {
            int optionIndex = list.GetItemMetadata((int)idx).AsInt32();
            if (!IsStudyableSpellOption(options, optionIndex)) return;
            LearnSpellOption(options[optionIndex]);
            dialog.QueueFree();
        };
        dialog.Confirmed += () =>
        {
            if (!IsStudyableSpellOption(options, selected))
                selected = FindFirstStudyableSpellIndex(options);
            if (selected >= 0)
                LearnSpellOption(options[selected]);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void LearnSpellOption(SpellStudyOption option)
    {
        if (_currentUnit == null) return;
        if (!SkillTreeKeystoneResolver.CanStudySpell(_currentUnit, option.Spell))
        {
            _infoPanel.ShowError("绝对专注限制：只能研习已锁定学派。");
            return;
        }

        if (!SpellStudyCatalog.HasSpell(_currentUnit, option.Spell.SpellId))
            _currentUnit.KnownSpells.Add(option.Spell);

        string entry = SpellStudyCatalog.MakeEquippedSpellEntry(option.Spell.SpellId);
        if (!_currentUnit.IsSkillEquipped(entry))
        {
            int slot = _currentUnit.FindFirstEmptyEquippedSlot();
            if (slot >= 0)
                _currentUnit.SetEquippedSkill(slot, entry);
        }

        SyncCurrentUnitSkillTree();
        RefreshLoadoutPanel();
        UpdateInfoPanel();
        _infoPanel.AppendSuccessMessage($"学会法术：{option.SpellName}");
    }

    private int FindFirstStudyableSpellIndex(SpellStudyOption[] options)
    {
        for (int i = 0; i < options.Length; i++)
            if (IsStudyableSpellOption(options, i))
                return i;
        return -1;
    }

    private bool IsStudyableSpellOption(SpellStudyOption[] options, int index)
    {
        return _currentUnit != null
            && index >= 0
            && index < options.Length
            && SkillTreeKeystoneResolver.CanStudySpell(_currentUnit, options[index].Spell);
    }

    private void UpdateInfoPanel()
    {
        if (_treeData == null || string.IsNullOrEmpty(_selectedNodeId))
        {
            _infoPanel.ShowEmpty();
            return;
        }

        if (!_treeData.Nodes.TryGetValue(_selectedNodeId, out var node)) return;

        bool activated = _characterTree?.IsActivated(_selectedNodeId) ?? false;
        bool canNormal = !activated
            && ((_characterTree?.IsAvailable(_selectedNodeId) ?? false)
                || (_characterTree?.GetTileProgress(_selectedNodeId) ?? 0) > 0)
            && (_characterTree?.AvailableAttributePoints ?? 0) > 0;
        int filledTiles = _characterTree?.GetTileProgress(_selectedNodeId) ?? 0;
        int requiredTiles = node.GetRequiredTileCount();
        var displayNode = _characterTree?.GetEffectiveNode(node) ?? node;
        string careerTransition = BuildCareerTransitionBanner(displayNode, activated);
        string effectText = BuildNodeEffectText(displayNode);
        bool canJump = !activated
        && filledTiles == 0
        && (_characterTree?.GetRemainingJumps() ?? 0) > 0
        && (_characterTree?.AvailableAttributePoints ?? 0) > 0
        && displayNode.RequiredLevel <= (_characterTree?.CharacterLevel ?? 0);
       
        _infoPanel.ShowNode(displayNode, activated, canNormal, canJump, filledTiles, requiredTiles, careerTransition, effectText);
    }

    private string BuildNodeEffectText(SkillNodeData node)
    {
        string treeText = _characterTree?.GetNodeEffectTextForCharacter(node) ?? node.GetEffectText();
        if (string.IsNullOrWhiteSpace(node.SkillEffect))
            return treeText;

        var cfg = SkillRegistry.GetSkillConfig(node.SkillEffect);
        if (cfg.Count == 0)
        {
            if (node.CurrentNodeType == SkillNodeData.NodeType.Giant && node.IsActiveSkill)
            {
                var giantSb = new System.Text.StringBuilder();
                if (!string.IsNullOrWhiteSpace(treeText))
                    giantSb.Append(treeText);
                AppendDefaultGiantActiveSpecLines(giantSb);
                return giantSb.ToString();
            }

            return treeText;
        }

        if (node.CurrentNodeType == SkillNodeData.NodeType.Giant)
        {
            var giantSb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(treeText))
                giantSb.Append(treeText);
            AppendSkillSpecLines(giantSb, cfg);
            return giantSb.ToString();
        }

        bool shouldUseConfig = node.IsActiveSkill
            || SpellStudyCatalog.IsSpellSlotEffect(node.SkillEffect)
            || IsPlaceholderEffectText(treeText);
        if (!shouldUseConfig)
            return treeText;

        string name = cfg.ContainsKey("name") ? cfg["name"].AsString() : node.NodeName;
        string desc = cfg.ContainsKey("description") ? cfg["description"].AsString() : "";
        string equipment = SkillRegistry.GetEquipmentRequirementText(node.SkillEffect);

        var sb = new System.Text.StringBuilder();
        sb.Append(name);
        if (!string.IsNullOrWhiteSpace(desc))
            sb.Append($"\n{desc}");
        if (SpellStudyCatalog.IsSpellSlotEffect(node.SkillEffect))
        {
            int tier = SpellStudyCatalog.GetTierFromSpellSlotEffect(node.SkillEffect);
            if (tier > 0)
                sb.Append($"\n点亮时研习 {tier} 环法术。");
        }
        AppendSkillSpecLines(sb, cfg);
        if (!string.IsNullOrWhiteSpace(equipment))
            sb.Append($"\n装备需求: {equipment}");
        return sb.ToString();
    }

    private static bool IsPlaceholderEffectText(string text)
        => !string.IsNullOrEmpty(text)
            && text.Contains("具体规则见技能星盘节点内容设计", StringComparison.Ordinal);

    /// <summary>
    /// 构建命座转变横幅文本：如果点亮该命座后会改变职业，返回横幅文本；否则返回空。
    /// 不再在详情描述中显示职业信息，改为在标题上方显示横幅。
    /// </summary>
    private string BuildCareerTransitionBanner(SkillNodeData node, bool activated)
    {
    	if (_characterTree == null) return "";
    	if (!ClassTitleResolver.IsCareerDefiningNode(node)) return "";
    	if (activated) return "";
   
    	int currentFlags = ClassTitleResolver.GetCareerFlags(_characterTree);
    	int previewFlags = ClassTitleResolver.GetCareerFlagsAfterCompleting(_characterTree, node);
   
    	if (previewFlags == currentFlags) return "";
   
    	string previewTitle = ClassTitleResolver.GetTitleByFlags(previewFlags);
    return $"命座激活后转变为：{previewTitle}";
    }

    private void UpdateStats()
    {
        if (_characterTree == null) return;
        if (_statLabels.TryGetValue("skill_points", out var spL))
            spL.Text = $"星辉: {_characterTree.AvailableAttributePoints}";
        if (_statLabels.TryGetValue("jumps", out var jL))
            jL.Text = $"跳跃: {_characterTree.GetRemainingJumps()}/{_characterTree.TotalJumps}";
        if (_statLabels.TryGetValue("career", out var careerL))
        {
            var resolved = _characterTree.GetClassTitle();
            string title = resolved.ContainsKey("title") ? resolved["title"].AsString() : "无名者";
            string label = resolved.ContainsKey("label") ? resolved["label"].AsString() : "";
            careerL.Text = string.IsNullOrEmpty(label) ? $"职业: {title}" : $"职业: {title} ({label})";
        }
        if (_statLabels.TryGetValue("career_skill", out var skillL))
        {
            var career = _characterTree.GetCareerSkill();
            skillL.Text = $"专属: {career?.DisplayName ?? "—"}";
        }
    }

    private void AddCrystalTileToMeshArrays(
        Vector2[] vertices,
        Color regionColor,
        TileVisualState state,
        List<Vector2> fillVerts,
        List<Color> fillColors,
        List<Vector2> lineVerts,
        List<Color> lineColors)
    {
        if (vertices.Length < 3) return;

        Vector2 centroid = (vertices[0] + vertices[1] + vertices[2]) / 3.0f;

        var shrunkVerts = new Vector2[3];
        for (int i = 0; i < 3; i++)
            shrunkVerts[i] = centroid + (vertices[i] - centroid) * 0.94f;

        Color innerColor, outerColor, outlineColor;
        switch (state)
        {
            case TileVisualState.Activated:
                innerColor = new Color(regionColor.R * 1.10f, regionColor.G * 1.10f, regionColor.B * 1.10f, 0.54f);
                outerColor = new Color(regionColor.R * 0.42f, regionColor.G * 0.42f, regionColor.B * 0.42f, 0.24f);
                outlineColor = new Color(regionColor.R * 0.55f, regionColor.G * 0.55f, regionColor.B * 0.55f, 0.24f);
                break;
            case TileVisualState.Available:
                innerColor = new Color(regionColor.R * 0.62f, regionColor.G * 0.62f, regionColor.B * 0.62f, 0.16f);
                outerColor = new Color(regionColor.R * 0.24f, regionColor.G * 0.24f, regionColor.B * 0.24f, 0.045f);
                outlineColor = new Color(regionColor.R * 0.40f, regionColor.G * 0.40f, regionColor.B * 0.40f, 0.11f);
                break;
            case TileVisualState.Locked:
            default:
                innerColor = new Color(regionColor.R * 0.22f, regionColor.G * 0.22f, regionColor.B * 0.22f, 0.028f);
                outerColor = new Color(regionColor.R * 0.10f, regionColor.G * 0.10f, regionColor.B * 0.10f, 0.006f);
                outlineColor = new Color(regionColor.R * 0.10f, regionColor.G * 0.10f, regionColor.B * 0.10f, 0.04f);
                break;
        }

        fillVerts.Add(shrunkVerts[0]);
        fillVerts.Add(shrunkVerts[1]);
        fillVerts.Add(centroid);
        fillColors.Add(outerColor);
        fillColors.Add(outerColor);
        fillColors.Add(innerColor);

        fillVerts.Add(shrunkVerts[1]);
        fillVerts.Add(shrunkVerts[2]);
        fillVerts.Add(centroid);
        fillColors.Add(outerColor);
        fillColors.Add(outerColor);
        fillColors.Add(innerColor);

        fillVerts.Add(shrunkVerts[2]);
        fillVerts.Add(shrunkVerts[0]);
        fillVerts.Add(centroid);
        fillColors.Add(outerColor);
        fillColors.Add(outerColor);
        fillColors.Add(innerColor);

        if (state == TileVisualState.Activated)
        {
            var hv = new Vector2[3];
            for (int i = 0; i < 3; i++)
                hv[i] = centroid + (shrunkVerts[i] - centroid) * 0.32f;
            fillVerts.Add(hv[0]);
            fillVerts.Add(hv[1]);
            fillVerts.Add(hv[2]);
            var glowOuter = new Color(regionColor.R, regionColor.G, regionColor.B, 0.11f);
            fillColors.Add(glowOuter);
            fillColors.Add(glowOuter);
            fillColors.Add(glowOuter);
        }
        else if (state == TileVisualState.Available)
        {
            var hv = new Vector2[3];
            for (int i = 0; i < 3; i++)
                hv[i] = centroid + (shrunkVerts[i] - centroid) * 0.32f;
            fillVerts.Add(hv[0]);
            fillVerts.Add(hv[1]);
            fillVerts.Add(hv[2]);
            var regionHigh = new Color(regionColor.R * 1.4f, regionColor.G * 1.4f, regionColor.B * 1.4f, 0.15f);
            fillColors.Add(regionHigh);
            fillColors.Add(regionHigh);
            fillColors.Add(regionHigh);
        }

        lineVerts.Add(shrunkVerts[0]); lineVerts.Add(shrunkVerts[1]);
        lineColors.Add(outlineColor); lineColors.Add(outlineColor);

        lineVerts.Add(shrunkVerts[1]); lineVerts.Add(shrunkVerts[2]);
        lineColors.Add(outlineColor); lineColors.Add(outlineColor);

        lineVerts.Add(shrunkVerts[2]); lineVerts.Add(shrunkVerts[0]);
        lineColors.Add(outlineColor); lineColors.Add(outlineColor);
    }

    private static ArrayMesh? CreateArrayMeshFromArrays(List<Vector2> verts, List<Color> colors, Mesh.PrimitiveType primitive)
    {
        if (verts.Count == 0) return null;

        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

        mesh.AddSurfaceFromArrays(primitive, arrays);
        return mesh;
    }
}
