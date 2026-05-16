// CursorManager.cs
// 自定义光标管理 — 全局Autoload。
// 启动时把默认箭头光标替换成游戏专用的木刻风光标。
using Godot;

namespace BladeHex.UI;

/// <summary>
/// 游戏光标管理器（Autoload）。
/// 启动时设置默认箭头光标为木刻风游戏光标。
/// </summary>
[GlobalClass]
public partial class CursorManager : Node
{
    public static CursorManager? Instance { get; private set; }

    private const string CursorPath = "res://src/assets/ui/cursors/cursor_default.png";
    private static readonly Vector2 Hotspot = Vector2.Zero; // 左上角尖端

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _ApplyDefaultCursor();
        GD.Print("[CursorManager] Initialized");
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    private static void _ApplyDefaultCursor()
    {
        if (!ResourceLoader.Exists(CursorPath))
        {
            GD.Print($"[CursorManager] Cursor texture not found: {CursorPath}, using system default");
            return;
        }
        var tex = GD.Load<Texture2D>(CursorPath);
        if (tex != null)
            Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, Hotspot);
    }
}
