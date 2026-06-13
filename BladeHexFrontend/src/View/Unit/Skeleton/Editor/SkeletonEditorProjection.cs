using Godot;

namespace BladeHex.View.Unit.Skeleton.Editor;

internal static class SkeletonEditorProjection
{
    private const float DegenerateBasisEpsilon = 0.00001f;

    public static Vector2 GetBoneCanvasOffset(Node2D node)
    {
        return node.GlobalPosition - UpperBodySkeleton.CanvasCenter;
    }

    public static bool TryProjectBoneToScreen(
        Camera3D camera,
        Sprite3D billboard,
        BoneConfig config,
        Node2D node,
        out Vector2 screenPos)
    {
        return TryProjectCanvasOffsetToScreen(
            camera,
            billboard,
            config.PixelSize,
            GetBoneCanvasOffset(node),
            out screenPos);
    }

    public static bool TryProjectCanvasOffsetToScreen(
        Camera3D camera,
        Sprite3D billboard,
        float pixelSize,
        Vector2 canvasOffset,
        out Vector2 screenPos)
    {
        var worldPoint = CanvasOffsetToWorldPoint(camera, billboard, pixelSize, canvasOffset);
        if (camera.IsPositionBehind(worldPoint))
        {
            screenPos = Vector2.Zero;
            return false;
        }

        screenPos = camera.UnprojectPosition(worldPoint);
        return true;
    }

    public static bool TryScreenDeltaToCanvasDelta(
        Camera3D camera,
        Sprite3D billboard,
        float pixelSize,
        Vector2 canvasOriginOffset,
        Vector2 screenDelta,
        out Vector2 canvasDelta)
    {
        if (!TryProjectCanvasOffsetToScreen(camera, billboard, pixelSize, canvasOriginOffset, out var originScreen)
            || !TryProjectCanvasOffsetToScreen(camera, billboard, pixelSize, canvasOriginOffset + new Vector2(1, 0), out var xScreen)
            || !TryProjectCanvasOffsetToScreen(camera, billboard, pixelSize, canvasOriginOffset + new Vector2(0, 1), out var yScreen))
        {
            canvasDelta = Vector2.Zero;
            return false;
        }

        return TrySolveCanvasDelta(xScreen - originScreen, yScreen - originScreen, screenDelta, out canvasDelta);
    }

    internal static bool TrySolveCanvasDelta(
        Vector2 screenPerCanvasX,
        Vector2 screenPerCanvasY,
        Vector2 screenDelta,
        out Vector2 canvasDelta)
    {
        float det = screenPerCanvasX.X * screenPerCanvasY.Y - screenPerCanvasX.Y * screenPerCanvasY.X;
        if (Mathf.Abs(det) < DegenerateBasisEpsilon)
        {
            canvasDelta = Vector2.Zero;
            return false;
        }

        canvasDelta = new Vector2(
            (screenDelta.X * screenPerCanvasY.Y - screenDelta.Y * screenPerCanvasY.X) / det,
            (screenPerCanvasX.X * screenDelta.Y - screenPerCanvasX.Y * screenDelta.X) / det);
        return true;
    }

    private static Vector3 CanvasOffsetToWorldPoint(
        Camera3D camera,
        Sprite3D billboard,
        float pixelSize,
        Vector2 canvasOffset)
    {
        var right = GetFixedYBillboardRight(camera, billboard);
        var worldOffset = (right * canvasOffset.X - Vector3.Up * canvasOffset.Y) * pixelSize;
        return billboard.GlobalPosition + worldOffset;
    }

    private static Vector3 GetFixedYBillboardRight(Camera3D camera, Sprite3D billboard)
    {
        var right = camera.GlobalTransform.Basis.X;
        right = new Vector3(right.X, 0, right.Z);
        if (right.LengthSquared() > DegenerateBasisEpsilon)
            return right.Normalized();

        var toCamera = camera.GlobalPosition - billboard.GlobalPosition;
        toCamera.Y = 0;
        if (toCamera.LengthSquared() <= DegenerateBasisEpsilon)
            return Vector3.Right;

        return Vector3.Up.Cross(toCamera.Normalized()).Normalized();
    }
}
