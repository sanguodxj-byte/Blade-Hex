// HexFootprintRenderer.cs
// 重构的程序化脚印绘制器 — 二维靴印贴图渲染版。
// 通过向 SurfaceBegin 的 ImmediateMesh 追加带 UV 的四边形，可以直接将皮靴底纹渲染到地表。
using Godot;
using BladeHex.Map;

namespace BladeHex.View.Combat;

/// <summary>
/// 程序化鞋靴印渲染工具。支持在已 SurfaceBegin 的 ImmediateMesh 中写入带 UV 的四边形面片。
/// </summary>
public static class HexFootprintRenderer
{
    // ===== 鞋印尺寸比例（相对于 HexUtils.Size） =====
    private const float FootLengthRatio = 0.18f; // 鞋印半长
    private const float FootWidthRatio = 0.09f;  // 鞋印半宽

    /// <summary>
    /// 绘制一个高精度的详细鞋印。
    /// </summary>
    public static void AddDetailedFootprint(
        ImmediateMesh mesh,
        Vector3 center,
        Vector3 forward,
        Vector3 right,
        Color color,
        float scale = 1.0f)
    {
        float size = HexUtils.Size * scale;
        float halfLength = FootLengthRatio * size;
        float halfWidth = FootWidthRatio * size;

        DrawQuad(mesh, center, forward, right, halfLength, halfWidth, color);
    }

    /// <summary>
    /// 绘制一个简化鞋印。
    /// </summary>
    public static void AddSimpleFootprint(
        ImmediateMesh mesh,
        Vector3 center,
        Vector3 forward,
        Vector3 right,
        Color color)
    {
        float size = HexUtils.Size;
        float halfLength = FootLengthRatio * size;
        float halfWidth = FootWidthRatio * size;

        DrawQuad(mesh, center, forward, right, halfLength, halfWidth, color);
    }

    /// <summary>
    /// 在 ImmediateMesh 写入两个三角形组成的带 UV 矩形。
    /// </summary>
    private static void DrawQuad(
        ImmediateMesh mesh,
        Vector3 center,
        Vector3 forward,
        Vector3 right,
        float halfLength,
        float halfWidth,
        Color color)
    {
        mesh.SurfaceSetNormal(Vector3.Up);

        // 计算 Quad 的四个顶点
        var p0 = center - forward * halfLength - right * halfWidth; // 左后 (UV 0, 0)
        var p1 = center - forward * halfLength + right * halfWidth; // 右后 (UV 1, 0)
        var p2 = center + forward * halfLength + right * halfWidth; // 右前 (UV 1, 1)
        var p3 = center + forward * halfLength - right * halfWidth; // 左前 (UV 0, 1)

        // 三角形 1
        mesh.SurfaceSetUV(new Vector2(0f, 0f));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(p0);

        mesh.SurfaceSetUV(new Vector2(1f, 0f));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(p1);

        mesh.SurfaceSetUV(new Vector2(1f, 1f));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(p2);

        // 三角形 2
        mesh.SurfaceSetUV(new Vector2(0f, 0f));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(p0);

        mesh.SurfaceSetUV(new Vector2(1f, 1f));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(p2);

        mesh.SurfaceSetUV(new Vector2(0f, 1f));
        mesh.SurfaceSetColor(color);
        mesh.SurfaceAddVertex(p3);
    }
}
