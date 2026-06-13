using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BladeHex.Combat;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.Combat;
using BladeHex.View.Map;

namespace BladeHex.Debug;

[GlobalClass]
public partial class BattleVisualDebugPanel : Node
{
    private static readonly Key ToggleKey = Key.F3;

    private CanvasLayer? _layer;
    private PanelContainer? _panel;
    private RichTextLabel? _text;
    private Label? _subtitle;

    private HexGrid? _hexGrid;
    private BattleMapGenerator.BattleMapData? _mapData;
    private CombatManager? _combatManager;
    private SceneDecorationPlacer? _decorationPlacer;
    private BattlePropRenderer? _battlePropRenderer;
    private GrassOverlayBatcher? _grassOverlay;
    private ElevationEdgeRenderer? _elevationEdges;
    private WaterStripRenderer? _waterStripRenderer;
    private bool _visible;

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless")
        {
            SetProcessUnhandledInput(false);
            return;
        }

        ProcessMode = ProcessModeEnum.Always;
        SetProcessUnhandledInput(true);
        BuildUi();
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode == ToggleKey)
        {
            ToggleVisible();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Initialize(
        HexGrid hexGrid,
        BattleMapGenerator.BattleMapData? mapData,
        CombatManager combatManager,
        SceneDecorationPlacer decorationPlacer,
        BattlePropRenderer battlePropRenderer,
        GrassOverlayBatcher grassOverlay,
        ElevationEdgeRenderer elevationEdges,
        WaterStripRenderer waterStripRenderer)
    {
        _hexGrid = hexGrid;
        _mapData = mapData;
        _combatManager = combatManager;
        _decorationPlacer = decorationPlacer;
        _battlePropRenderer = battlePropRenderer;
        _grassOverlay = grassOverlay;
        _elevationEdges = elevationEdges;
        _waterStripRenderer = waterStripRenderer;
        Refresh();
    }

    public void ToggleVisible()
    {
        SetVisible(!_visible);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
        if (_panel != null)
            _panel.Visible = visible;
        if (visible)
            Refresh();
    }

    public void Refresh()
    {
        if (_text == null || _subtitle == null)
            return;

        _subtitle.Text = _mapData != null
            ? $"F3  |  {_mapData.TemplateName}  |  cells={CellCount()}"
            : "F3  |  no battle map data";
        _text.Text = BuildReport();
    }

    private void BuildUi()
    {
        _layer = new CanvasLayer { Name = "BattleVisualDebugLayer", Layer = 1150 };
        AddChild(_layer);

        _panel = new PanelContainer { Name = "BattleVisualDebugPanel", Visible = false };
        _panel.AnchorLeft = 1.0f;
        _panel.AnchorRight = 1.0f;
        _panel.AnchorTop = 0.0f;
        _panel.AnchorBottom = 1.0f;
        _panel.OffsetLeft = -520.0f;
        _panel.OffsetRight = -18.0f;
        _panel.OffsetTop = 70.0f;
        _panel.OffsetBottom = -70.0f;
        _layer.AddChild(_panel);

        var bg = new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.04f, 0.045f, 0.94f),
            BorderColor = new Color(0.50f, 0.62f, 0.74f, 0.85f),
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 10,
        };
        bg.SetCornerRadiusAll(6);
        bg.SetBorderWidthAll(1);
        _panel.AddThemeStyleboxOverride("panel", bg);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(root);

        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 6);
        root.AddChild(toolbar);

        var title = new Label { Text = "Battle Visual Audit" };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.92f, 0.96f, 1.0f));
        toolbar.AddChild(title);

        toolbar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        toolbar.AddChild(MakeButton("Refresh", Refresh));
        toolbar.AddChild(MakeButton("Close", () => SetVisible(false)));

        _subtitle = new Label();
        _subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _subtitle.AddThemeFontSizeOverride("font_size", 11);
        _subtitle.AddThemeColorOverride("font_color", new Color(0.68f, 0.74f, 0.82f));
        root.AddChild(_subtitle);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            SelectionEnabled = true,
            ContextMenuEnabled = true,
            FitContent = false,
            ScrollActive = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _text.AddThemeFontSizeOverride("normal_font_size", 12);
        _text.AddThemeColorOverride("default_color", new Color(0.86f, 0.90f, 0.93f));
        scroll.AddChild(_text);
    }

    private static Button MakeButton(string label, Action callback)
    {
        var button = new Button { Text = label };
        button.Pressed += callback;
        return button;
    }

    private string BuildReport()
    {
        if (_mapData == null || _hexGrid == null)
            return "[color=#ff8888]Battle map has not been initialized.[/color]";

        var cells = ReadCells();
        int total = cells.Count;
        if (total == 0)
            return "[color=#ff8888]Battle map has no cells.[/color]";

        int passable = cells.Count(c => c.isPassable);
        int losBlock = cells.Count(c => c.blocksLineOfSight);
        int halfCover = cells.Count(c => c.coverLevel == 1);
        int fullCover = cells.Count(c => c.coverLevel >= 2);
        int water = cells.Count(c => IsWater(c.terrainType));
        int road = cells.Count(c => c.terrainType == BattleCellData.TerrainType.Road || c.terrainType == BattleCellData.TerrainType.Bridge);
        int special = cells.Count(c => !string.IsNullOrEmpty(c.specialEffect));
        int minElev = cells.Min(c => c.elevation);
        int maxElev = cells.Max(c => c.elevation);
        double avgMove = cells.Average(c => c.moveCost);

        var sb = new StringBuilder();
        sb.AppendLine("[b]Map[/b]");
        AppendLine(sb, "Template", _mapData.TemplateName);
        AppendLine(sb, "Shape", _mapData.HexRadius > 0 ? $"hex radius {_mapData.HexRadius}" : $"{_mapData.Width} x {_mapData.Height}");
        AppendLine(sb, "Environment", string.IsNullOrEmpty(_mapData.EnvironmentEvent) ? "none" : _mapData.EnvironmentEvent);
        AppendLine(sb, "Cells", total.ToString());
        AppendLine(sb, "Deployment", $"player {_mapData.PlayerDeployment.Count}, enemy {_mapData.EnemyDeployment.Count}");
        sb.AppendLine();

        sb.AppendLine("[b]Tactical Metrics[/b]");
        AppendMetric(sb, "Passable", passable, total);
        AppendMetric(sb, "Impassable", total - passable, total);
        AppendMetric(sb, "Blocks LOS", losBlock, total);
        AppendMetric(sb, "Half cover", halfCover, total);
        AppendMetric(sb, "Full cover", fullCover, total);
        AppendMetric(sb, "Water", water, total);
        AppendMetric(sb, "Road/bridge", road, total);
        AppendMetric(sb, "Special effect", special, total);
        AppendLine(sb, "Elevation", $"{minElev}..{maxElev}");
        AppendLine(sb, "Avg move cost", avgMove.ToString("0.00"));
        sb.AppendLine();

        sb.AppendLine("[b]Terrain Mix[/b]");
        foreach (var kvp in cells.GroupBy(c => c.terrainType).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.ToString()))
            AppendMetric(sb, kvp.Key.ToString(), kvp.Count(), total);
        sb.AppendLine();

        sb.AppendLine("[b]Elevation Mix[/b]");
        foreach (var kvp in cells.GroupBy(c => c.elevation).OrderBy(g => g.Key))
            AppendMetric(sb, $"elevation {kvp.Key}", kvp.Count(), total);
        sb.AppendLine();

        sb.AppendLine("[b]Runtime[/b]");
        AppendLine(sb, "HexGrid cells", _hexGrid.Cells.Count.ToString());
        AppendLine(sb, "Units", BuildUnitSummary());
        AppendLine(sb, "Visual layers", BuildVisualLayerSummary());
        AppendLine(sb, "Prop scatter", BuildPropScatterSummary());

        return sb.ToString();
    }

    private List<BattleCellData> ReadCells()
    {
        var result = new List<BattleCellData>();
        if (_mapData == null)
            return result;

        foreach (Variant key in _mapData.Cells.Keys)
        {
            var cell = _mapData.Cells[key].As<BattleCellData>();
            if (cell != null)
                result.Add(cell);
        }
        return result;
    }

    private int CellCount()
    {
        if (_mapData != null)
            return _mapData.Cells.Count;
        return _hexGrid?.Cells.Count ?? 0;
    }

    private string BuildUnitSummary()
    {
        if (_combatManager == null)
            return "none";

        return $"player {_combatManager.PlayerUnits.Count}, enemy {_combatManager.EnemyUnits.Count}, all {_combatManager.AllUnits.Count}";
    }

    private string BuildVisualLayerSummary()
    {
        var parts = new List<string>
        {
            $"overlays {_grassOverlay?.OverlayCount ?? 0}",
            $"props {_battlePropRenderer?.TotalPropCount ?? 0}",
            $"decorations {_decorationPlacer?.DecorationCount ?? 0}",
            $"elevationEdges {LayerState(_elevationEdges)}",
            $"waterStrips {LayerState(_waterStripRenderer)}",
        };
        return string.Join(", ", parts);
    }

    private string BuildPropScatterSummary()
    {
        if (_battlePropRenderer == null)
            return "missing";

        return $"eligible cells {_battlePropRenderer.LastEligibleCellCount}, skipped cells {_battlePropRenderer.LastSkippedCellCount}, candidates {_battlePropRenderer.LastCandidateCount}, missing textures {_battlePropRenderer.LastSkippedMissingTextureCount}, placed {_battlePropRenderer.TotalPropCount}";
    }

    private static string LayerState(Node? node)
    {
        return node != null && GodotObject.IsInstanceValid(node) ? "on" : "missing";
    }

    private static bool IsWater(BattleCellData.TerrainType terrain)
    {
        return terrain == BattleCellData.TerrainType.ShallowWater
            || terrain == BattleCellData.TerrainType.DeepWater
            || terrain == BattleCellData.TerrainType.River;
    }

    private static void AppendLine(StringBuilder sb, string label, string value)
    {
        sb.Append("[color=#9fb4c8]");
        sb.Append(Escape(label));
        sb.Append(":[/color] ");
        sb.AppendLine(Escape(value));
    }

    private static void AppendMetric(StringBuilder sb, string label, int count, int total)
    {
        float pct = total > 0 ? count * 100.0f / total : 0.0f;
        sb.Append("[color=#9fb4c8]");
        sb.Append(Escape(label));
        sb.Append(":[/color] ");
        sb.Append(count);
        sb.Append("  ");
        sb.Append(pct.ToString("0.0"));
        sb.AppendLine("%");
    }

    private static string Escape(string text)
    {
        return text.Replace("[", "[lb]").Replace("]", "[rb]");
    }
}
