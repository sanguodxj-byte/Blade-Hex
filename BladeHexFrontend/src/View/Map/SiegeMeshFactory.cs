// SiegeMeshFactory.cs
// 程序化生成攻城建筑 mesh — 城墙（带城垛）、塔楼（更高+旗帜柱）、城门（带拱门缺口）
//
// 设计：
//   - 所有 mesh 以 (0,0,0) 为中心，Y 轴向上
//   - 高度从 Y=0 开始向上延伸（不是从中心向两边）
//   - 这样在 MultiMesh 中放置时，底面自然贴地
//   - 城垛在顶部边缘交替凸起，模拟真实城墙 battlement
//   - 侧面完整渲染（从底部到顶部），不会悬空
using Godot;
using System;

namespace BladeHex.View.Map;

/// <summary>
/// 攻城建筑 mesh 工厂 — 程序化生成城墙/塔楼/城门的 ArrayMesh
/// </summary>
public static class SiegeMeshFactory
{
    // 几何常量（与 HexCellMultiMeshBatcher 一致）
    private const float HexRadius = 96.0f;
    private const float HexHeight = 48.0f;  // 一层高度
    private const int Sides = 6;
    private const float RotOffset = 30.0f;  // flat-top 六边形旋转偏移

    // 城垛参数
    private const float MerlonWidth = 0.35f;   // 城垛宽度占边长比例
    private const float MerlonHeight = 16.0f;  // 城垛凸起高度
    private const float CrenelDepth = 12.0f;   // 城垛凹槽深度（向内缩）

    /// <summary>
    /// 生成城墙 mesh（Rampart）— 六棱柱 + 顶部城垛
    /// 总高 = 2 层（elevation=2）= HexHeight * 2，顶部有城垛
    /// </summary>
    public static ArrayMesh CreateRampartMesh()
    {
        float wallHeight = HexHeight * 2.0f;  // elevation=2 的总高
        float merlonTop = wallHeight + MerlonHeight;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // 1. 底面（Y=0）
        AddHexFace(st, 0.0f, HexRadius, isTop: false);

        // 2. 侧面（Y=0 到 Y=wallHeight）
        AddHexSides(st, 0.0f, wallHeight, HexRadius);

        // 3. 顶面主体（Y=wallHeight，内缩一圈留出城垛位置）
        float innerRadius = HexRadius * 0.75f;
        AddHexFace(st, wallHeight, innerRadius, isTop: true);

        // 4. 城垛：顶部边缘交替凸起
        // 每条边分 3 段：城垛-缺口-城垛
        for (int i = 0; i < Sides; i++)
        {
            float angle1 = Mathf.DegToRad(60.0f * i + RotOffset);
            float angle2 = Mathf.DegToRad(60.0f * (i + 1) + RotOffset);

            var outerV1 = new Vector3(Mathf.Cos(angle1) * HexRadius, 0, Mathf.Sin(angle1) * HexRadius);
            var outerV2 = new Vector3(Mathf.Cos(angle2) * HexRadius, 0, Mathf.Sin(angle2) * HexRadius);
            var innerV1 = new Vector3(Mathf.Cos(angle1) * innerRadius, 0, Mathf.Sin(angle1) * innerRadius);
            var innerV2 = new Vector3(Mathf.Cos(angle2) * innerRadius, 0, Mathf.Sin(angle2) * innerRadius);

            // 左城垛（边的前 1/3）
            var mL1 = Lerp3(outerV1, outerV2, 0.0f);
            var mL2 = Lerp3(outerV1, outerV2, MerlonWidth);
            var mLi1 = Lerp3(innerV1, innerV2, 0.0f);
            var mLi2 = Lerp3(innerV1, innerV2, MerlonWidth);
            AddMerlon(st, mL1, mL2, mLi1, mLi2, wallHeight, merlonTop);

            // 右城垛（边的后 1/3）
            var mR1 = Lerp3(outerV1, outerV2, 1.0f - MerlonWidth);
            var mR2 = Lerp3(outerV1, outerV2, 1.0f);
            var mRi1 = Lerp3(innerV1, innerV2, 1.0f - MerlonWidth);
            var mRi2 = Lerp3(innerV1, innerV2, 1.0f);
            AddMerlon(st, mR1, mR2, mRi1, mRi2, wallHeight, merlonTop);

            // 中间缺口的顶面（低于城垛，形成射击口）
            var cL = Lerp3(outerV1, outerV2, MerlonWidth);
            var cR = Lerp3(outerV1, outerV2, 1.0f - MerlonWidth);
            var cLi = Lerp3(innerV1, innerV2, MerlonWidth);
            var cRi = Lerp3(innerV1, innerV2, 1.0f - MerlonWidth);
            // 缺口顶面在 wallHeight（不凸起）
            AddQuadY(st, cL, cR, cRi, cLi, wallHeight);
        }

        // 5. 城垛侧面（外侧）
        // 已在 AddMerlon 中处理

        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>
    /// 生成塔楼 mesh（Tower）— 更高的六棱柱 + 城垛 + 中心旗杆
    /// 总高 = 3 层（elevation=3）
    /// </summary>
    public static ArrayMesh CreateTowerMesh()
    {
        float towerHeight = HexHeight * 3.0f;
        float merlonTop = towerHeight + MerlonHeight * 1.2f;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // 底面
        AddHexFace(st, 0.0f, HexRadius, isTop: false);

        // 侧面（全高）
        AddHexSides(st, 0.0f, towerHeight, HexRadius);

        // 顶面
        float innerRadius = HexRadius * 0.70f;
        AddHexFace(st, towerHeight, innerRadius, isTop: true);

        // 城垛（与城墙类似但更高）
        for (int i = 0; i < Sides; i++)
        {
            float angle1 = Mathf.DegToRad(60.0f * i + RotOffset);
            float angle2 = Mathf.DegToRad(60.0f * (i + 1) + RotOffset);

            var outerV1 = new Vector3(Mathf.Cos(angle1) * HexRadius, 0, Mathf.Sin(angle1) * HexRadius);
            var outerV2 = new Vector3(Mathf.Cos(angle2) * HexRadius, 0, Mathf.Sin(angle2) * HexRadius);
            var innerV1 = new Vector3(Mathf.Cos(angle1) * innerRadius, 0, Mathf.Sin(angle1) * innerRadius);
            var innerV2 = new Vector3(Mathf.Cos(angle2) * innerRadius, 0, Mathf.Sin(angle2) * innerRadius);

            // 每边一个城垛（塔楼城垛更宽）
            var mL = Lerp3(outerV1, outerV2, 0.15f);
            var mR = Lerp3(outerV1, outerV2, 0.85f);
            var mLi = Lerp3(innerV1, innerV2, 0.15f);
            var mRi = Lerp3(innerV1, innerV2, 0.85f);
            AddMerlon(st, mL, mR, mLi, mRi, towerHeight, merlonTop);
        }

        // 中心旗杆（细长方柱）
        float poleRadius = 3.0f;
        float poleHeight = towerHeight + 40.0f;
        AddPole(st, Vector3.Zero, towerHeight, poleHeight, poleRadius);

        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>
    /// 生成城门 mesh（Gate）— 六棱柱 + 正面拱门缺口
    /// 总高 = 2 层（elevation=2），正面有拱门开口
    /// </summary>
    public static ArrayMesh CreateGateMesh()
    {
        float gateHeight = HexHeight * 2.0f;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // 底面
        AddHexFace(st, 0.0f, HexRadius, isTop: false);

        // 侧面（除了正面方向 0 的那一面用拱门替代）
        for (int i = 0; i < Sides; i++)
        {
            float angle1 = Mathf.DegToRad(60.0f * i + RotOffset);
            float angle2 = Mathf.DegToRad(60.0f * (i + 1) + RotOffset);

            var bl = new Vector3(Mathf.Cos(angle1) * HexRadius, 0.0f, Mathf.Sin(angle1) * HexRadius);
            var br = new Vector3(Mathf.Cos(angle2) * HexRadius, 0.0f, Mathf.Sin(angle2) * HexRadius);
            var tl = new Vector3(bl.X, gateHeight, bl.Z);
            var tr = new Vector3(br.X, gateHeight, br.Z);

            if (i == 0)
            {
                // 正面：拱门（中间 60% 宽度镂空到 70% 高度）
                float archLeft = 0.20f;
                float archRight = 0.80f;
                float archTop = gateHeight * 0.75f;

                // 左柱
                var pillarLB = bl;
                var pillarLT = new Vector3(bl.X, gateHeight, bl.Z);
                var pillarLR_B = Lerp3(bl, br, archLeft);
                var pillarLR_T = new Vector3(pillarLR_B.X, gateHeight, pillarLR_B.Z);
                AddQuad(st, pillarLB, pillarLR_B, new Vector3(pillarLR_B.X, archTop, pillarLR_B.Z), pillarLT);

                // 右柱
                var pillarRB = Lerp3(bl, br, archRight);
                var pillarRT = new Vector3(pillarRB.X, gateHeight, pillarRB.Z);
                AddQuad(st, pillarRB, br, tr, pillarRT);

                // 拱顶（archTop 到 gateHeight 的横梁）
                var archLT = new Vector3(Lerp3(bl, br, archLeft).X, archTop, Lerp3(bl, br, archLeft).Z);
                var archRT = new Vector3(Lerp3(bl, br, archRight).X, archTop, Lerp3(bl, br, archRight).Z);
                var archLTop = new Vector3(archLT.X, gateHeight, archLT.Z);
                var archRTop = new Vector3(archRT.X, gateHeight, archRT.Z);
                AddQuad(st, archLT, archRT, archRTop, archLTop);
            }
            else
            {
                // 普通侧面
                AddQuad(st, bl, br, tr, tl);
            }
        }

        // 顶面
        AddHexFace(st, gateHeight, HexRadius, isTop: true);

        st.GenerateNormals();
        return st.Commit();
    }

    // ========================================================================
    // 几何辅助方法
    // ========================================================================

    /// <summary>添加六边形面（顶面或底面）</summary>
    private static void AddHexFace(SurfaceTool st, float y, float radius, bool isTop)
    {
        var center = new Vector3(0, y, 0);
        var normal = isTop ? Vector3.Up : Vector3.Down;

        for (int i = 0; i < Sides; i++)
        {
            float a1 = Mathf.DegToRad(60.0f * i + RotOffset);
            float a2 = Mathf.DegToRad(60.0f * (i + 1) + RotOffset);

            var v1 = new Vector3(Mathf.Cos(a1) * radius, y, Mathf.Sin(a1) * radius);
            var v2 = new Vector3(Mathf.Cos(a2) * radius, y, Mathf.Sin(a2) * radius);

            st.SetNormal(normal);
            if (isTop)
            {
                st.AddVertex(center);
                st.AddVertex(v1);
                st.AddVertex(v2);
            }
            else
            {
                st.AddVertex(center);
                st.AddVertex(v2);
                st.AddVertex(v1);
            }
        }
    }

    /// <summary>添加六棱柱侧面</summary>
    private static void AddHexSides(SurfaceTool st, float bottomY, float topY, float radius)
    {
        for (int i = 0; i < Sides; i++)
        {
            float a1 = Mathf.DegToRad(60.0f * i + RotOffset);
            float a2 = Mathf.DegToRad(60.0f * (i + 1) + RotOffset);

            var bl = new Vector3(Mathf.Cos(a1) * radius, bottomY, Mathf.Sin(a1) * radius);
            var br = new Vector3(Mathf.Cos(a2) * radius, bottomY, Mathf.Sin(a2) * radius);
            var tl = new Vector3(bl.X, topY, bl.Z);
            var tr = new Vector3(br.X, topY, br.Z);

            // 法线朝外
            var normal = ((bl + br) * 0.5f - new Vector3(0, (bottomY + topY) * 0.5f, 0)).Normalized();
            normal.Y = 0;
            normal = normal.Normalized();

            st.SetNormal(normal);
            st.AddVertex(bl); st.AddVertex(br); st.AddVertex(tr);
            st.AddVertex(bl); st.AddVertex(tr); st.AddVertex(tl);
        }
    }

    /// <summary>添加一个城垛（凸起的方块）</summary>
    private static void AddMerlon(SurfaceTool st,
        Vector3 outerL, Vector3 outerR, Vector3 innerL, Vector3 innerR,
        float baseY, float topY)
    {
        // 城垛是一个从 baseY 到 topY 的方块，外侧在 outer，内侧在 inner
        var oLB = new Vector3(outerL.X, baseY, outerL.Z);
        var oRB = new Vector3(outerR.X, baseY, outerR.Z);
        var oLT = new Vector3(outerL.X, topY, outerL.Z);
        var oRT = new Vector3(outerR.X, topY, outerR.Z);
        var iLB = new Vector3(innerL.X, baseY, innerL.Z);
        var iRB = new Vector3(innerR.X, baseY, innerR.Z);
        var iLT = new Vector3(innerL.X, topY, innerL.Z);
        var iRT = new Vector3(innerR.X, topY, innerR.Z);

        // 顶面
        AddQuadY(st, oLT, oRT, iRT, iLT, topY);

        // 外侧面
        var outNormal = ((oLB + oRB) * 0.5f).Normalized();
        outNormal.Y = 0; outNormal = outNormal.Normalized();
        st.SetNormal(outNormal);
        st.AddVertex(oLB); st.AddVertex(oRB); st.AddVertex(oRT);
        st.AddVertex(oLB); st.AddVertex(oRT); st.AddVertex(oLT);

        // 左侧面
        st.SetNormal(Vector3.Left);
        st.AddVertex(iLB); st.AddVertex(oLB); st.AddVertex(oLT);
        st.AddVertex(iLB); st.AddVertex(oLT); st.AddVertex(iLT);

        // 右侧面
        st.SetNormal(Vector3.Right);
        st.AddVertex(oRB); st.AddVertex(iRB); st.AddVertex(iRT);
        st.AddVertex(oRB); st.AddVertex(iRT); st.AddVertex(oRT);
    }

    /// <summary>添加水平四边形（指定 Y 高度）</summary>
    private static void AddQuadY(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float y)
    {
        st.SetNormal(Vector3.Up);
        var va = new Vector3(a.X, y, a.Z);
        var vb = new Vector3(b.X, y, b.Z);
        var vc = new Vector3(c.X, y, c.Z);
        var vd = new Vector3(d.X, y, d.Z);
        st.AddVertex(va); st.AddVertex(vb); st.AddVertex(vc);
        st.AddVertex(va); st.AddVertex(vc); st.AddVertex(vd);
    }

    /// <summary>添加垂直四边形</summary>
    private static void AddQuad(SurfaceTool st, Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl)
    {
        var normal = (br - bl).Cross(tl - bl).Normalized();
        st.SetNormal(normal);
        st.AddVertex(bl); st.AddVertex(br); st.AddVertex(tr);
        st.AddVertex(bl); st.AddVertex(tr); st.AddVertex(tl);
    }

    /// <summary>添加细长方柱（旗杆）</summary>
    private static void AddPole(SurfaceTool st, Vector3 basePos, float bottomY, float topY, float radius)
    {
        // 简单 4 面柱
        var offsets = new Vector3[]
        {
            new(radius, 0, 0), new(0, 0, radius),
            new(-radius, 0, 0), new(0, 0, -radius),
        };

        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            var bl = basePos + offsets[i] + new Vector3(0, bottomY, 0);
            var br = basePos + offsets[j] + new Vector3(0, bottomY, 0);
            var tl = basePos + offsets[i] + new Vector3(0, topY, 0);
            var tr = basePos + offsets[j] + new Vector3(0, topY, 0);
            AddQuad(st, bl, br, tr, tl);
        }
    }

    private static Vector3 Lerp3(Vector3 a, Vector3 b, float t)
    {
        return new Vector3(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t);
    }
}
