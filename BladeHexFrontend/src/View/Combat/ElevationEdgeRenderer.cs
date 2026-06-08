// ElevationEdgeRenderer.cs
// 战斗场景高差描边渲染器 — 借鉴 Battle Brothers 正式版的崖沿软阴影。
// 当某格 elevation 高于相邻格、且该边朝向相机远端（上半三条边）时，
// 沿这条共享边、贴着高格顶面绘制一条「外缘最深、向格心内缩渐隐」的黑色软阴影，
// 模拟高斯模糊薄阴影附着在地表纹理之上。深度测试保留开启，使其能被靠前的 hex 压住。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.View.Combat;

[GlobalClass]
public partial class ElevationEdgeRenderer : Node3D
{
    // ========================================
    // 可调参数
    // ========================================

    /// <summary>描边颜色（纯黑阴影；实际深浅由逐顶点 alpha 渐变控制）</summary>
    private static readonly Color ShadowColor = new(0.0f, 0.0f, 0.0f, 1.0f);

    /// <summary>崖脚（接触线）处的峰值 alpha；向低格外侧沿 smoothstep 曲线羽化至 0。
    /// 柔和化：从 0.42 降到 0.28，黑带更淡、不抢戏。</summary>
    private const float EdgeAlpha = 0.28f;

    /// <summary>软阴影沿"向低邻格外侧"方向的渐隐宽度（世界单位，基础值）。
    /// 柔和化：从 26 加宽到 40，半影拉长、过渡更缓。</summary>
    private const float BaseThickness = 40.0f;

    /// <summary>每多 1 级高差额外增加的宽度（世界单位）</summary>
    private const float ThicknessPerLevel = 6.0f;

    /// <summary>软阴影最大宽度封顶（世界单位）</summary>
    private const float MaxThickness = 64.0f;

    /// <summary>外侧渐隐方向的细分段数：越多曲线越平滑（顶点数 ≈ (段数+1)×2/边）。
    /// 柔和化：从 6 提到 12，加宽后曲线仍平滑无断阶。</summary>
    private const int FadeSegments = 12;

    /// <summary>阴影向崖脚内侧（高格底下）多覆盖的距离，交给深度测试被高格遮住，消除崖脚接缝</summary>
    private const float InnerOverlap = 10.0f;

    /// <summary>阴影相对低邻格顶面的 Y 偏移：略高于其地表精灵（TextureLayer=0.5）使其附着在纹理之上。
    /// 注意——深度测试保持开启，靠前/更高的相邻 hex 仍会在深度上压住这条边带，而非被它盖住。</summary>
    private const float EdgeYLift = 0.6f;

    /// <summary>只描朝相机远端的"上半三条边"（dir 1/2/3，世界 Z 为负）</summary>
    private static readonly bool[] UpperEdge = { false, true, true, true, false, false };

    // ========================================
    // 几何常量（平顶六边形，circumradius = HexUtils.Size）
    // ========================================

    /// <summary>边到格心的距离（内切圆半径）</summary>
    private static readonly float Apothem = HexUtils.Size * Mathf.Sqrt(3.0f) / 2.0f;

    /// <summary>六边形单边长的一半（circumradius=Size 时单边长=Size）</summary>
    private static readonly float HalfEdge = HexUtils.Size * 0.5f;

    /// <summary>hex 顶面相对 cell.Position 的 Y 偏移</summary>
    private static readonly float TopOffset = CombatLayerHeight.HexTopOffset;

    private MeshInstance3D? _mesh;
    private StandardMaterial3D? _material;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>遍历所有格子，为每条"本格高于邻格、且朝相机远端"的边生成黑色软阴影。</summary>
    public void Render(HexGrid hexGrid)
    {
        if (hexGrid == null) return;
        Clear();

        // 每条边记录：崖脚接触线两端点 (b1,b2)、向低格外侧的羽化向量 outward、
        // 以及向高格底下内缩的覆盖向量 cover（会被高格遮住，消接缝）。
        var edges = new List<(Vector3 b1, Vector3 b2, Vector3 outward, Vector3 cover)>();

        foreach (var kvp in hexGrid.Cells)
        {
            var coord = kvp.Key;
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            int elevC = cell.Elevation;

            for (int d = 0; d < 6; d++)
            {
                if (!UpperEdge[d]) continue;   // 只描朝相机远端的上半三条边

                var nb = HexUtils.GetNeighbor(coord.X, coord.Y, d);

                // 必须有更低的邻格才有崖脚可落影
                if (!hexGrid.Cells.TryGetValue(nb, out var nbCell) || nbCell == null) continue;
                int elevDiff = elevC - nbCell.Elevation;
                if (elevDiff <= 0) continue;   // 只在"本格更高"的那一侧描边

                // 朝向：dirXZ 从高格指向低邻格（即崖脚向低格外侧的方向）
                var dirXZ = new Vector3(nbCell.Position.X - cell.Position.X, 0, nbCell.Position.Z - cell.Position.Z).Normalized();
                var perp = new Vector3(-dirXZ.Z, 0, dirXZ.X);

                // 崖脚接触线：落在低邻格顶面、位于共享边处（高格外缘 = 低格朝高格的内缘）
                float footY = nbCell.Position.Y + TopOffset + EdgeYLift;
                var edgeCenter = new Vector3(cell.Position.X, footY, cell.Position.Z) + dirXZ * Apothem;
                var b1 = edgeCenter + perp * HalfEdge;
                var b2 = edgeCenter - perp * HalfEdge;

                // 向低格外侧羽化的宽度；向高格底下覆盖固定 InnerOverlap（消崖脚接缝）
                float fade = Mathf.Min(BaseThickness + (elevDiff - 1) * ThicknessPerLevel, MaxThickness);
                edges.Add((b1, b2, dirXZ * fade, -dirXZ * InnerOverlap));
            }
        }

        if (edges.Count == 0)
        {
            GD.Print("[ElevationEdgeRenderer] 无高差边，跳过");
            return;
        }

        var imm = new ImmediateMesh();
        imm.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var (b1, b2, outward, cover) in edges)
        {
            // 1) 内侧覆盖段：从高格底下(cover)到崖脚(0)，全程峰值 alpha，靠深度测试被高格遮住。
            AddBand(imm, b1 + cover, b2 + cover, b1, b2, EdgeAlpha, EdgeAlpha);

            // 2) 外侧羽化段：从崖脚(t=0,峰值)沿 outward 向低格细分 FadeSegments 段，
            //    alpha = EdgeAlpha × (1 - smoothstep(t))，平滑曲线羽化到 0 → 无硬边。
            for (int s = 0; s < FadeSegments; s++)
            {
                float t0 = (float)s / FadeSegments;
                float t1 = (float)(s + 1) / FadeSegments;
                float a0 = EdgeAlpha * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, t0));
                float a1 = EdgeAlpha * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, t1));
                AddBand(imm, b1 + outward * t0, b2 + outward * t0, b1 + outward * t1, b2 + outward * t1, a0, a1);
            }
        }
        imm.SurfaceEnd();

        _material = new StandardMaterial3D
        {
            AlbedoColor = Colors.White,                                   // 实际颜色来自逐顶点色
            VertexColorUseAsAlbedo = true,                                // 启用顶点色（含 alpha 渐变）
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            // 深度测试保持开启：靠前/更高的相邻 hex 会在深度上压住这条边带（即"被下面的 hex 压住"）。
            // 仅关闭深度写入，避免半透明边带之间互相遮挡产生硬边。
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        };

        _mesh = new MeshInstance3D
        {
            Mesh = imm,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_mesh);

        GD.Print($"[ElevationEdgeRenderer] 绘制了 {edges.Count} 条高差软阴影");
    }

    /// <summary>
    /// 向 ImmediateMesh 追加一个四边形条带（拆两三角形，顶面朝上）。
    /// (o1,o2) 为外侧一对端点、alpha=aOuter；(i1,i2) 为内侧一对、alpha=aInner。
    /// </summary>
    private static void AddBand(ImmediateMesh imm, Vector3 o1, Vector3 o2, Vector3 i1, Vector3 i2, float aOuter, float aInner)
    {
        var colO = new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, aOuter);
        var colI = new Color(ShadowColor.R, ShadowColor.G, ShadowColor.B, aInner);

        imm.SurfaceSetNormal(Vector3.Up);
        imm.SurfaceSetColor(colO);
        imm.SurfaceAddVertex(o1);
        imm.SurfaceSetColor(colO);
        imm.SurfaceAddVertex(o2);
        imm.SurfaceSetColor(colI);
        imm.SurfaceAddVertex(i2);

        imm.SurfaceSetColor(colO);
        imm.SurfaceAddVertex(o1);
        imm.SurfaceSetColor(colI);
        imm.SurfaceAddVertex(i2);
        imm.SurfaceSetColor(colI);
        imm.SurfaceAddVertex(i1);
    }

    /// <summary>清除已生成的描边几何</summary>
    public void Clear()
    {
        _mesh?.QueueFree();
        _mesh = null;
        _material = null;
    }
}
