using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Combat;

/// <summary>Displays combat anchors such as battle banners until dedicated assets replace them.</summary>
public partial class BattleAnchorViewLayer : Node3D
{
    private readonly Dictionary<string, Node3D> _viewsBySource = new();
    private HexGrid? _hexGrid;
    private BladeHex.Combat.CombatManager? _combatManager;

    public void Initialize(HexGrid hexGrid, BladeHex.Combat.CombatManager combatManager)
    {
        _hexGrid = hexGrid;
        _combatManager = combatManager;
        combatManager.BattleAnchorCreated += OnAnchorCreatedOrChanged;
        combatManager.BattleAnchorChanged += OnAnchorCreatedOrChanged;
        combatManager.BattleAnchorDestroyed += OnAnchorDestroyed;
    }

    public override void _ExitTree()
    {
        if (_combatManager != null)
        {
            _combatManager.BattleAnchorCreated -= OnAnchorCreatedOrChanged;
            _combatManager.BattleAnchorChanged -= OnAnchorCreatedOrChanged;
            _combatManager.BattleAnchorDestroyed -= OnAnchorDestroyed;
        }

        _combatManager = null;
        _viewsBySource.Clear();
    }

    private void OnAnchorCreatedOrChanged(Godot.Collections.Dictionary anchor)
    {
        string source = anchor.ContainsKey("source") ? anchor["source"].AsString() : "";
        if (string.IsNullOrEmpty(source) || _hexGrid == null) return;

        int q = anchor.ContainsKey("q") ? anchor["q"].AsInt32() : 0;
        int r = anchor.ContainsKey("r") ? anchor["r"].AsInt32() : 0;
        var cell = _hexGrid.GetCell(q, r);
        if (cell == null) return;

        if (!_viewsBySource.TryGetValue(source, out var view) || !GodotObject.IsInstanceValid(view))
        {
            view = CreateAnchorView();
            _viewsBySource[source] = view;
            AddChild(view);
        }

        view.GlobalPosition = cell.GlobalPosition
            + new Vector3(0, CombatLayerHeight.HexTopOffset + CombatLayerHeight.CharacterLayer + 10f, 0);
        UpdateLabel(view, anchor);
    }

    private static Node3D CreateAnchorView()
    {
        var root = new Node3D { Name = "BattleAnchorView" };

        var pole = new MeshInstance3D { Name = "Pole" };
        pole.Mesh = new CylinderMesh
        {
            TopRadius = 1.2f,
            BottomRadius = 1.2f,
            Height = 42f,
        };
        pole.Position = new Vector3(0, 21f, 0);
        pole.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.74f, 0.62f, 0.42f) };
        root.AddChild(pole);

        var flag = new MeshInstance3D { Name = "Flag" };
        flag.Mesh = new BoxMesh { Size = new Vector3(24f, 14f, 1.2f) };
        flag.Position = new Vector3(12f, 34f, 0);
        flag.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.16f, 0.12f) };
        root.AddChild(flag);

        var label = new Label3D
        {
            Name = "Label",
            Text = "",
            PixelSize = 2.2f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(1f, 0.92f, 0.68f),
            OutlineModulate = new Color(0.1f, 0.05f, 0.03f),
            OutlineSize = 8,
            Position = new Vector3(0, 55f, 0),
        };
        root.AddChild(label);

        return root;
    }

    private static void UpdateLabel(Node3D view, Godot.Collections.Dictionary anchor)
    {
        var label = view.GetNodeOrNull<Label3D>("Label");
        if (label == null) return;

        string anchorId = anchor.ContainsKey("anchor_id") ? anchor["anchor_id"].AsString() : "anchor";
        int duration = anchor.ContainsKey("duration") ? anchor["duration"].AsInt32() : -1;
        int hp = anchor.ContainsKey("hp") ? anchor["hp"].AsInt32() : 0;
        string durationText = duration >= 0 ? $"R{duration}" : "R-";
        label.Text = anchorId == "battle_banner"
            ? $"Banner  HP {hp}  {durationText}"
            : $"{anchorId}  HP {hp}  {durationText}";
    }

    private void OnAnchorDestroyed(string source)
    {
        if (!_viewsBySource.TryGetValue(source, out var view)) return;
        _viewsBySource.Remove(source);
        if (GodotObject.IsInstanceValid(view))
            view.QueueFree();
    }
}
