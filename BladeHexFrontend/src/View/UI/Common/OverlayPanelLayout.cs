using Godot;

namespace BladeHex.UI.Common;

public static class OverlayPanelLayout
{
    private const string ModalLayerName = "OverlayModalLayer";

    /// <summary>当前通过 AttachModal 挂载且尚未释放的模态层数量</summary>
    private static int _activeModalCount = 0;

    /// <summary>是否有任意通过 AttachModal 挂载的模态面板正在显示</summary>
    public static bool IsAnyModalOpen => _activeModalCount > 0;

    public static void Center(Control panel)
    {
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Both;
    }

    public static void AttachCentered(Node parent, Control panel)
    {
        parent.AddChild(panel);
        Center(panel);
    }

    public static Control AttachModal(
        Node parent,
        Control panel,
        Color? overlayColor = null,
        bool closeOnOverlayClick = true)
    {
        var layer = new Control
        {
            Name = ModalLayerName,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        parent.AddChild(layer);

        var overlay = new ColorRect
        {
            Color = overlayColor ?? new Color(0, 0, 0, 0.6f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(overlay);

        if (closeOnOverlayClick)
        {
            overlay.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    layer.QueueFree();
            };
        }

        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        layer.AddChild(panel);
        Center(panel);

        // 追踪模态层生命周期
        _activeModalCount++;
        layer.TreeExiting += () => _activeModalCount = System.Math.Max(0, _activeModalCount - 1);

        return layer;
    }

    public static void CloseModal(Control panel)
    {
        if (panel.GetParent() is Control layer && layer.Name.ToString() == ModalLayerName)
        {
            layer.QueueFree();
            return;
        }

        panel.QueueFree();
    }
}
