using BladeHex.View.AssetSystem;
using Godot;
using System.Collections.Generic;

namespace BladeHex.UI;

public enum CursorState
{
    Default,
    Dragging,
    DragForbidden,
    CombatTargeting,
    Busy,
}

[GlobalClass]
public partial class CursorManager : Node
{
    public static CursorManager? Instance { get; private set; }

    private const string CursorDir = "res://BladeHexFrontend/src/assets/ui/cursors/";

    private static readonly (string id, string file, Vector2 hotspot, Input.CursorShape shape)[] CursorDefs =
    [
        ("cursor_default", "cursor_default.png", new Vector2(7, 5), Input.CursorShape.Arrow),
        ("cursor_pointing_hand", "cursor_pointing_hand.png", new Vector2(11, 0), Input.CursorShape.PointingHand),
        ("cursor_move", "cursor_move.png", new Vector2(16, 16), Input.CursorShape.Move),
        ("cursor_attack", "cursor_attack.png", new Vector2(16, 16), Input.CursorShape.Cross),
        ("cursor_forbidden", "cursor_forbidden.png", new Vector2(16, 16), Input.CursorShape.Forbidden),
        ("cursor_busy", "cursor_busy.png", new Vector2(16, 16), Input.CursorShape.Busy),
    ];

    private static readonly Dictionary<Input.CursorShape, (Texture2D Tex, Vector2 Hotspot)> LoadedCursors = new();
    private static CursorState _currentState = CursorState.Default;

    public static CursorState CurrentState => _currentState;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        if (DisplayServer.GetName() == "headless")
        {
            GD.Print("[CursorManager] Headless mode; custom cursor loading skipped.");
            return;
        }

        ApplyAllCursors();
        GD.Print("[CursorManager] Initialized.");
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void SetState(CursorState state)
    {
        if (_currentState == state)
            return;

        _currentState = state;
        RemapArrowTo(state switch
        {
            CursorState.Default => Input.CursorShape.Arrow,
            CursorState.Dragging => Input.CursorShape.Move,
            CursorState.DragForbidden => Input.CursorShape.Forbidden,
            CursorState.CombatTargeting => Input.CursorShape.Cross,
            CursorState.Busy => Input.CursorShape.Busy,
            _ => Input.CursorShape.Arrow,
        });
    }

    private static void ApplyAllCursors()
    {
        LoadedCursors.Clear();
        foreach (var (id, file, hotspot, shape) in CursorDefs)
        {
            string path = CursorDir + file;
            var tex = TextureAssetResolver.LoadUiTexture(id, path);
            if (tex == null)
            {
                if (shape == Input.CursorShape.Arrow)
                    GD.Print($"[CursorManager] Default cursor not found: {path}; using system default.");
                continue;
            }

            Input.SetCustomMouseCursor(tex, shape, hotspot);
            LoadedCursors[shape] = (tex, hotspot);
        }
    }

    private static void RemapArrowTo(Input.CursorShape sourceShape)
    {
        if (LoadedCursors.TryGetValue(sourceShape, out var data))
            Input.SetCustomMouseCursor(data.Tex, Input.CursorShape.Arrow, data.Hotspot);
    }
}
