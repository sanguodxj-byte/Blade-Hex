// SkillTreeUI.cs
// 技能盘可视化UI - 六边形网格布局, 184节点, 圆形节点渲染
// 节点为空心圆（未点亮）/ 实心圆（已点亮）
// 只有有技能点时，可点亮节点才显示名称
// 支持 WASD 平移 + 滚轮缩放 + 右键拖拽
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Strategic;

namespace BladeHex.UI;

[GlobalClass]
public partial class SkillTreeUI : PanelContainer
{
    [Signal] public delegate void NodeClickedEventHandler(string nodeId);
    [Signal] public delegate void CloseRequestedEventHandler();

    // ========================================
    // 常量
    // ========================================

    private const float HexSize = 48.0f;
    private const float PanSpeed = 500.0f;

    /// <summary>方格网格间距（节点之间的距离）</summary>
    private const float GridSpacing = 22.0f;

    /// <summary>正六边形半径（以网格单位计）— 决定六边形大小</summary>
    private const int HexagonRadius = 20;

    /// <summary>节点圆半径（按类型）</summary>
    private const float RadiusSmall = 8.0f;
    private const float RadiusBig = 13.0f;
    private const float RadiusKeystone = 16.0f;
    private const float RadiusStart = 18.0f;

    /// <summary>点击检测半径（比视觉稍大，方便点击）</summary>
    private const float ClickRadius = 18.0f;

    // ========================================
    // 状态
    // ========================================

    private UIFactory _factory = null!;
    private new UITheme Theme => UITheme.Instance!;

    private Control _drawContainer = null!;
    private readonly Dictionary<string, Vector2> _nodePositions = new();
    private CharacterSkillTree? _characterTree;
    private SkillTreeData? _treeData;
    private Vector2 _center = new(600, 500);
    private float _zoom = 1.0f;
    private Vector2 _panOffset = Vector2.Zero;
    private bool _isPanning = false;
    private Vector2 _panStart = Vector2.Zero;
    private string _selectedNodeId = "";
    private string _hoveredNodeId = "";

    private PanelContainer _infoPanel = null!;
    private Label _infoTitle = null!;
    private RichTextLabel _infoDesc = null!;
    private Button _infoActivateBtn = null!;
    private Button _infoJumpBtn = null!;
    private readonly Dictionary<string, Label> _statLabels = new();
    private SkillTreeCoord _coord = null!;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        _factory = new UIFactory();
        _coord = new SkillTreeCoord { HexSize = HexSize };
        Setup();
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        var panDir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) panDir.Y += 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) panDir.Y -= 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) panDir.X += 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) panDir.X -= 1;

        if (panDir != Vector2.Zero)
        {
            _panOffset += panDir.Normalized() * PanSpeed * (float)delta;
            RebuildPositions();
            _drawContainer.QueueRedraw();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } && Visible)
        {
            Visible = false;
            EmitSignal(SignalName.CloseRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    // ========================================
    // UI 构建
    // ========================================

    private void Setup()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", Theme.MakePanelStyle(
            Theme.BgPrimary, Theme.BorderMagic, 2, Theme.RadiusLg, 0));

        var rootMargin = _factory.CreateMargin(20, 20, 15, 15);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", Theme.SpacingMd);
        rootMargin.AddChild(mainVbox);

        // === Header ===
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", Theme.SpacingMd);
        mainVbox.AddChild(header);

        var title = _factory.CreateTitleLabel("技 能 盘", Theme.FontSizeXxl);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var spLbl = _factory.CreateBodyLabel("技能点: 0", Theme.TextAccent);
        header.AddChild(spLbl);
        _statLabels["skill_points"] = spLbl;

        var jmpLbl = _factory.CreateBodyLabel("跳跃: 0", Theme.TextMagic);
        header.AddChild(jmpLbl);
        _statLabels["jumps"] = jmpLbl;

        var hintLbl = _factory.CreateBodyLabel("[WASD移动 / 滚轮缩放 / 右键拖拽]", Theme.TextMuted);
        header.AddChild(hintLbl);

        var resetBtn = _factory.CreateButton("回到中心", new Vector2(90, 32));
        resetBtn.Pressed += () => { _panOffset = Vector2.Zero; _zoom = 1.0f; RebuildPositions(); _drawContainer.QueueRedraw(); };
        header.AddChild(resetBtn);

        var closeBtn = _factory.CreateButton("返回 (ESC)", new Vector2(100, 32));
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
        header.AddChild(closeBtn);

        mainVbox.AddChild(_factory.CreateSeparatorH());

        // === Body ===
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", Theme.SpacingLg);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(body);

        // --- Left: draw canvas ---
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
        _drawContainer.GuiInput += OnDrawInput;
        _drawContainer.MouseFilter = Control.MouseFilterEnum.Stop;
        drawPanel.AddChild(_drawContainer);

        // --- Right: info panel ---
        _infoPanel = _factory.CreatePanel(new Vector2(280, 0), Theme.BgSecondary, Theme.BorderMagic);
        _infoPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddChild(_infoPanel);

        var infoMargin = _factory.CreateMargin(14, 14, 12, 12);
        _infoPanel.AddChild(infoMargin);

        var infoVbox = new VBoxContainer();
        infoVbox.AddThemeConstantOverride("separation", Theme.SpacingMd);
        infoMargin.AddChild(infoVbox);

        _infoTitle = _factory.CreateTitleLabel("选择节点查看详情", Theme.FontSizeLg);
        infoVbox.AddChild(_infoTitle);

        infoVbox.AddChild(_factory.CreateSeparatorH(Theme.BorderMagic));

        _infoDesc = _factory.CreateRichText(new Vector2(250, 0));
        _infoDesc.SizeFlagsVertical = SizeFlags.ExpandFill;
        infoVbox.AddChild(_infoDesc);

        _infoActivateBtn = _factory.CreateButton("点亮节点", new Vector2(0, 40));
        _infoActivateBtn.Disabled = true;
        _infoActivateBtn.Pressed += OnActivatePressed;
        infoVbox.AddChild(_infoActivateBtn);

        _infoJumpBtn = _factory.CreateButton("跳跃点亮", new Vector2(0, 40));
        _infoJumpBtn.Disabled = true;
        _infoJumpBtn.Pressed += OnJumpPressed;
        infoVbox.AddChild(_infoJumpBtn);
    }

    // ========================================
    // 公共 API
    // ========================================

    public void OpenSkillTree(CharacterSkillTree characterTree, SkillTreeData treeData)
    {
        _characterTree = characterTree;
        _treeData = treeData;
        _zoom = 1.0f;
        _panOffset = Vector2.Zero;
        _selectedNodeId = "";
        Visible = true;
        CallDeferred(nameof(DeferredOpen));
    }

    private void DeferredOpen()
    {
        _center = _drawContainer.Size / 2.0f;
        if (_center.LengthSquared() < 100) _center = new Vector2(500, 400);
        RebuildPositions();
        UpdateStats();
        UpdateInfoPanel();
        _drawContainer.QueueRedraw();
    }

    // ========================================
    // 坐标与位置
    // ========================================

    private Vector2 NodeToPixel(SkillNodeData node)
    {
        return GridToPixel(node.GridPosition);
    }

    /// <summary>
    /// 网格坐标 → 像素坐标
    /// 使用三角形格点变换：将整数 (x, y) 映射到形成正六边形的位置
    /// X 间距 = S, Y 行高 = S * √3/2, 奇数行 X 偏移 S/2
    /// 这样所有相邻点等距（正三角形格），整体形状为正六边形
    /// </summary>
    private Vector2 GridToPixel(Vector2I gridPos)
    {
        float px = gridPos.X * GridSpacing + gridPos.Y * GridSpacing * 0.5f;
        float py = gridPos.Y * GridSpacing * Mathf.Sqrt(3.0f) / 2.0f;
        return _center + new Vector2(px, py) * _zoom + _panOffset;
    }

    /// <summary>
    /// 判断网格点 (x, y) 是否在以原点为中心、半径 radius 的正六边形内
    /// 使用 cube 坐标距离：max(|x|, |y|, |x+y|) ≤ radius
    /// 配合 GridToPixel 的三角形格变换，几何上是正六边形
    /// </summary>
    private static bool IsInsideHexagon(int x, int y, int radius)
    {
        int z = -x - y;
        return Math.Abs(x) <= radius && Math.Abs(y) <= radius && Math.Abs(z) <= radius;
    }

    /// <summary>获取所有在正六边形内的网格点</summary>
    private static List<Vector2I> GetHexagonGridPoints(int radius)
    {
        var points = new List<Vector2I>();
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
                if (IsInsideHexagon(x, y, radius))
                    points.Add(new Vector2I(x, y));
        return points;
    }

    /// <summary>三角形格的 6 个相邻方向（每对相邻点等距）</summary>
    private static readonly Vector2I[] GridDirections =
    {
        new(1, 0),   new(-1, 0),    // 水平
        new(0, 1),   new(0, -1),    // 斜下/斜上
        new(1, -1),  new(-1, 1),    // 反斜
    };

    private static List<Vector2I> GetGridNeighbors(Vector2I pos, int radius)
    {
        var result = new List<Vector2I>();
        foreach (var dir in GridDirections)
        {
            var nb = pos + dir;
            if (IsInsideHexagon(nb.X, nb.Y, radius))
                result.Add(nb);
        }
        return result;
    }

    private void RebuildPositions()
    {
        if (_treeData == null) return;
        foreach (var pair in _treeData.Nodes)
            _nodePositions[pair.Key] = NodeToPixel(pair.Value);
    }

    private float GetNodeRadius(SkillNodeData node)
    {
        return node.CurrentNodeType switch
        {
            SkillNodeData.NodeType.Start => RadiusStart * _zoom,
            SkillNodeData.NodeType.Keystone => RadiusKeystone * _zoom,
            SkillNodeData.NodeType.Big => RadiusBig * _zoom,
            _ => RadiusSmall * _zoom,
        };
    }

    // ========================================
    // 输入处理
    // ========================================

    private void OnDrawInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
            {
                float oldZoom = _zoom;
                _zoom = MathF.Min(_zoom * 1.12f, 3.0f);
                AdjustPanForZoom(mouseBtn.Position, oldZoom, _zoom);
                RebuildPositions();
                _drawContainer.QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
            {
                float oldZoom = _zoom;
                _zoom = MathF.Max(_zoom * 0.88f, 0.2f);
                AdjustPanForZoom(mouseBtn.Position, oldZoom, _zoom);
                RebuildPositions();
                _drawContainer.QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Right)
            {
                _isPanning = mouseBtn.Pressed;
                _panStart = mouseBtn.Position;
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Middle && mouseBtn.Pressed)
            {
                _panOffset = Vector2.Zero;
                _zoom = 1.0f;
                RebuildPositions();
                _drawContainer.QueueRedraw();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
            {
                HandleClick(mouseBtn.Position);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            if (_isPanning)
            {
                _panOffset += mouseMotion.Position - _panStart;
                _panStart = mouseMotion.Position;
                RebuildPositions();
                _drawContainer.QueueRedraw();
            }
            else
            {
                // Hover 检测
                string newHover = HitTestNode(mouseMotion.Position);
                if (newHover != _hoveredNodeId)
                {
                    _hoveredNodeId = newHover;
                    _drawContainer.QueueRedraw();
                }
            }
        }
    }

    private void AdjustPanForZoom(Vector2 mousePos, float oldZoom, float newZoom)
    {
        var mouseOffset = mousePos - _center;
        float ratio = newZoom / oldZoom;
        _panOffset = (_panOffset - mouseOffset) * ratio + mouseOffset;
    }

    private string HitTestNode(Vector2 pos)
    {
        if (_treeData == null) return "";
        float bestDist = ClickRadius * _zoom;
        string bestId = "";
        foreach (var pair in _nodePositions)
        {
            float dist = pos.DistanceTo(pair.Value);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestId = pair.Key;
            }
        }
        return bestId;
    }

    private void HandleClick(Vector2 pos)
    {
        string nodeId = HitTestNode(pos);
        if (!string.IsNullOrEmpty(nodeId))
        {
            _selectedNodeId = nodeId;
            UpdateInfoPanel();
            _drawContainer.QueueRedraw();
        }
    }

    // ========================================
    // 绘制
    // ========================================

    private void OnDraw()
    {
        if (_treeData == null) return;

        bool hasSkillPoints = (_characterTree?.AvailableSkillPoints ?? 0) > 0;

        // 1. 绘制连线
        DrawConnections();

        // 2. 绘制节点（圆形）
        foreach (var pair in _treeData.Nodes)
        {
            if (!_nodePositions.TryGetValue(pair.Key, out var pos)) continue;
            DrawNode(pair.Key, pair.Value, pos, hasSkillPoints);
        }

        // 3. 绘制区域标签
        DrawRegionLabels();
    }

    private void DrawConnections()
    {
        // 1. 绘制六边形外轮廓（坐标系边界，不画内部网格连线）
        _DrawHexagonOutline();

        // 2. 绘制实际节点之间的技能连线（手工定义的 Neighbors）
        foreach (var pair in _treeData!.Nodes)
        {
            string nodeId = pair.Key;
            if (!_nodePositions.TryGetValue(nodeId, out var from)) continue;

            foreach (var nid in pair.Value.Neighbors)
            {
                if (!_nodePositions.TryGetValue(nid, out var to)) continue;
                if (string.Compare(nid, nodeId, StringComparison.Ordinal) < 0) continue;

                bool fa = _characterTree?.IsActivated(nodeId) ?? false;
                bool ta = _characterTree?.IsActivated(nid) ?? false;

                Color lineColor;
                float lineWidth;
                if (fa && ta)
                {
                    lineColor = new Color(0.8f, 0.85f, 1.0f, 0.9f);
                    lineWidth = 2.5f * _zoom;
                }
                else if (fa || ta)
                {
                    lineColor = new Color(0.5f, 0.5f, 0.6f, 0.6f);
                    lineWidth = 1.8f * _zoom;
                }
                else
                {
                    lineColor = new Color(0.35f, 0.35f, 0.42f, 0.5f);
                    lineWidth = 1.2f * _zoom;
                }

                _drawContainer.DrawLine(from, to, lineColor, lineWidth);
            }
        }
    }

    /// <summary>绘制方格网格点阵（仅六边形内部），所有相邻点用细线相连</summary>
    private void _DrawGridLattice()
    {
        var points = GetHexagonGridPoints(HexagonRadius);
        var pointSet = new HashSet<Vector2I>(points);
        var latticeColor = new Color(0.18f, 0.2f, 0.25f, 0.35f);
        float lineWidth = 0.8f * _zoom;

        // 用 HashSet 避免重复绘制（每条边只画一次）
        var drawnEdges = new HashSet<(Vector2I, Vector2I)>();

        foreach (var p in points)
        {
            var pPx = GridToPixel(p);
            foreach (var dir in GridDirections)
            {
                var nb = p + dir;
                if (!pointSet.Contains(nb)) continue;
                // 规范化边的两端避免重复
                var edge = string.Compare($"{p.X},{p.Y}", $"{nb.X},{nb.Y}") < 0
                    ? (p, nb) : (nb, p);
                if (drawnEdges.Contains(edge)) continue;
                drawnEdges.Add(edge);

                var nbPx = GridToPixel(nb);
                _drawContainer.DrawLine(pPx, nbPx, latticeColor, lineWidth);
            }
        }

        // 绘制六边形外轮廓（粗线突出边界）
        _DrawHexagonOutline();
    }

    /// <summary>绘制正六边形的外轮廓（以六边形顶点连线）</summary>
    private void _DrawHexagonOutline()
    {
        // 六边形 6 个顶点（以方格坐标表示）
        Vector2I[] vertices = new[]
        {
            new Vector2I(HexagonRadius, 0),
            new Vector2I(0, HexagonRadius),
            new Vector2I(-HexagonRadius, HexagonRadius),
            new Vector2I(-HexagonRadius, 0),
            new Vector2I(0, -HexagonRadius),
            new Vector2I(HexagonRadius, -HexagonRadius),
        };

        var outlineColor = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        float lineWidth = 1.5f * _zoom;
        for (int i = 0; i < 6; i++)
        {
            var a = GridToPixel(vertices[i]);
            var b = GridToPixel(vertices[(i + 1) % 6]);
            _drawContainer.DrawLine(a, b, outlineColor, lineWidth);
        }
    }

    private void DrawNode(string nodeId, SkillNodeData node, Vector2 pos, bool hasSkillPoints)
    {
        bool activated = _characterTree?.IsActivated(nodeId) ?? false;
        bool available = _characterTree?.IsAvailable(nodeId) ?? false;
        bool isSelected = nodeId == _selectedNodeId;
        bool isHovered = nodeId == _hoveredNodeId;

        var regionColor = Theme.GetRegionColor(node.CurrentRegion);
        float radius = GetNodeRadius(node);

        // --- 绘制圆形节点 ---
        if (activated)
        {
            // 实心圆 — 已点亮
            _drawContainer.DrawCircle(pos, radius, regionColor);
            // 外圈光晕
            _drawContainer.DrawArc(pos, radius + 2.0f * _zoom, 0, MathF.Tau, 32,
                new Color(regionColor.R, regionColor.G, regionColor.B, 0.4f), 1.5f * _zoom);
        }
        else if (available && hasSkillPoints)
        {
            // 空心圆 + 高亮边框 — 可点亮（有技能点时）
            _drawContainer.DrawArc(pos, radius, 0, MathF.Tau, 32,
                new Color(regionColor.R, regionColor.G, regionColor.B, 0.8f), 2.0f * _zoom);
            // 内部微弱填充
            _drawContainer.DrawCircle(pos, radius * 0.6f,
                new Color(regionColor.R, regionColor.G, regionColor.B, 0.15f));
        }
        else
        {
            // 空心圆 — 未点亮/不可用
            float alpha = available ? 0.5f : 0.25f;
            _drawContainer.DrawArc(pos, radius, 0, MathF.Tau, 24,
                new Color(regionColor.R, regionColor.G, regionColor.B, alpha), 1.0f * _zoom);
        }

        // --- 选中高亮 ---
        if (isSelected)
        {
            _drawContainer.DrawArc(pos, radius + 4.0f * _zoom, 0, MathF.Tau, 32,
                new Color(1.0f, 1.0f, 1.0f, 0.7f), 2.0f * _zoom);
        }
        else if (isHovered)
        {
            _drawContainer.DrawArc(pos, radius + 3.0f * _zoom, 0, MathF.Tau, 32,
                new Color(1.0f, 1.0f, 1.0f, 0.3f), 1.0f * _zoom);
        }

        // --- 节点名称（仅在有技能点且可点亮时显示，或已点亮的大节点/Keystone）---
        bool showName = false;
        if (activated && node.CurrentNodeType != SkillNodeData.NodeType.Small)
            showName = true;
        else if (available && hasSkillPoints)
            showName = true;
        else if (isSelected || isHovered)
            showName = true;

        if (showName && _zoom >= 0.5f)
        {
            float fontSize = node.CurrentNodeType == SkillNodeData.NodeType.Small ? 10.0f : 12.0f;
            fontSize *= _zoom;
            var nameColor = activated ? regionColor : new Color(0.8f, 0.8f, 0.85f, 0.9f);
            var namePos = pos + new Vector2(0, radius + 10.0f * _zoom);
            _drawContainer.DrawString(ThemeDB.FallbackFont, namePos,
                node.NodeName, HorizontalAlignment.Center, -1, (int)fontSize, nameColor);
        }
    }

    private void DrawRegionLabels()
    {
        var labelDirs = new (SkillNodeData.Region Region, Vector2I Coord, string Name)[]
        {
            (SkillNodeData.Region.Str, new Vector2I(12, 0), "STR 力量"),
            (SkillNodeData.Region.Dex, new Vector2I(0, 12), "DEX 灵巧"),
            (SkillNodeData.Region.Con, new Vector2I(-12, 12), "CON 体魄"),
            (SkillNodeData.Region.Int, new Vector2I(-12, 0), "INT 智力"),
            (SkillNodeData.Region.Wis, new Vector2I(0, -12), "WIS 感知"),
            (SkillNodeData.Region.Cha, new Vector2I(12, -12), "CHA 魅力"),
        };

        foreach (var (region, coord, name) in labelDirs)
        {
            var lp = _center + _coord.HexToPixel(coord.X, coord.Y) * _zoom + _panOffset;
            var col = Theme.GetRegionColor(region);
            _drawContainer.DrawString(ThemeDB.FallbackFont, lp,
                name, HorizontalAlignment.Center, -1, (int)(18 * _zoom), col);
        }
    }

    // ========================================
    // 操作回调
    // ========================================

    private void OnActivatePressed()
    {
        if (_characterTree == null || string.IsNullOrEmpty(_selectedNodeId)) return;
        var r = _characterTree.TryActivateNode(_selectedNodeId);
        if ((bool)r["success"])
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("char_node_activate");
            RefreshAfterChange((string)r["message"]);
        }
        else
        {
            _infoDesc.Text = $"[color=red]{(string)r["message"]}[/color]";
        }
    }

    private void OnJumpPressed()
    {
        if (_characterTree == null || string.IsNullOrEmpty(_selectedNodeId)) return;
        var r = _characterTree.TryJumpActivate(_selectedNodeId);
        if ((bool)r["success"])
        {
            BladeHex.Data.Globals.AudioOrNull?.PlaySfxName("char_node_activate");
            RefreshAfterChange((string)r["message"]);
        }
        else
        {
            _infoDesc.Text = $"[color=red]{(string)r["message"]}[/color]";
        }
    }

    private void RefreshAfterChange(string msg)
    {
        UpdateStats();
        UpdateInfoPanel();
        _drawContainer.QueueRedraw();
        _infoDesc.Text += $"\n[color=green]{msg}[/color]";
    }

    // ========================================
    // 信息面板
    // ========================================

    private void UpdateInfoPanel()
    {
        if (_treeData == null || string.IsNullOrEmpty(_selectedNodeId))
        {
            _infoTitle.Text = "选择节点查看详情";
            _infoDesc.Text = "点击左侧技能盘上的节点查看信息。\n\n[color=gray]● 空心圆 = 未点亮\n● 实心圆 = 已点亮\n● 高亮圆 = 可点亮[/color]";
            _infoActivateBtn.Disabled = true;
            _infoJumpBtn.Disabled = true;
            return;
        }

        if (!_treeData.Nodes.TryGetValue(_selectedNodeId, out var node)) return;

        _infoTitle.Text = node.NodeName;
        _infoTitle.AddThemeColorOverride("font_color", Theme.GetRegionColor(node.CurrentRegion));

        string typeStr = node.CurrentNodeType switch
        {
            SkillNodeData.NodeType.Big => "◆ 大节点 (技能)",
            SkillNodeData.NodeType.Keystone => "★ Keystone (代价)",
            SkillNodeData.NodeType.Start => "◎ 启程",
            _ => "● 小节点 (属性)"
        };

        string d = $"[color=gray]{typeStr}[/color]\n";
        d += $"[color=gray]区域:[/color] {node.GetRegionName()}\n";
        if (node.RequiredLevel > 0) d += $"[color=gray]需要等级:[/color] {node.RequiredLevel}\n";
        d += $"\n[color=white]效果:[/color] {node.GetEffectText()}\n";
        if (!string.IsNullOrEmpty(node.KeystoneCost))
            d += $"\n[color=red]代价:[/color] {node.KeystoneCost}\n";

        bool activated = _characterTree?.IsActivated(_selectedNodeId) ?? false;
        if (activated)
            d += "\n[color=green]✓ 已点亮[/color]";

        _infoDesc.Text = d;

        bool canNormal = !activated
            && (_characterTree?.IsAvailable(_selectedNodeId) ?? false)
            && (_characterTree?.AvailableSkillPoints ?? 0) > 0;
        bool canJump = !activated
            && (_characterTree?.GetRemainingJumps() ?? 0) > 0
            && (_characterTree?.AvailableSkillPoints ?? 0) > 0
            && node.RequiredLevel <= (_characterTree?.CharacterLevel ?? 0);

        _infoActivateBtn.Disabled = !canNormal;
        _infoJumpBtn.Disabled = !canJump;
    }

    private void UpdateStats()
    {
        if (_characterTree == null) return;
        if (_statLabels.TryGetValue("skill_points", out var spL))
            spL.Text = $"技能点: {_characterTree.AvailableSkillPoints}";
        if (_statLabels.TryGetValue("jumps", out var jL))
            jL.Text = $"跳跃: {_characterTree.GetRemainingJumps()}/{_characterTree.TotalJumps}";
    }
}
