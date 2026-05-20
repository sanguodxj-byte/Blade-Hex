// POIHexOverlayRenderer.cs
// POI 占位六边形覆盖层渲染器 — 用 POI 专属纹理填满所有占用 hex
//
// 设计意图：
//   城镇/村庄/城堡等 POI 不再只是一个悬浮图标，而是在大地图上
//   用程序化纹理填满其 footprint 内的所有 hex，形成明确的视觉占位。
//   纹理叠加在地形之上（Y 微偏移），保留地形底色的同时突出 POI 区域。
//
// 技术方案：
//   - 每种 POI 视觉类型一个 MultiMeshInstance3D 桶（批处理）
//   - 共享 hex mesh（与 HexOverworldRenderer3D 相同几何体）
//   - 使用 overworld_poi_hex.gdshader 程序化纹理
//   - 中心格通过 instance custom data 标记（shader 读取 is_center）
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.View.Map;

/// <summary>
/// POI 六边形覆盖层渲染器 — 在地形之上叠加 POI 专属纹理
/// </summary>
public partial class POIHexOverlayRenderer : Node3D
{
    // ========================================
    // 常量
    // ========================================

    /// <summary>覆盖层 Y 偏移（略高于地形，避免 Z-fighting）</summary>
    private const float OverlayYOffset = 0.05f;

    /// <summary>Shader 路径</summary>
    private const string POIShaderPath = "res://src/assets/shaders/overworld_poi_hex.gdshader";

    // ========================================
    // POI 视觉类型（决定 shader pattern + 配色）
    // ========================================

    /// <summary>POI 视觉分类 — 决定纹理图案和配色方案</summary>
    public enum POIVisualType
    {
        Town = 0,       // 城镇石板
        Village = 1,    // 村庄木屋
        Castle = 2,     // 城堡石墙
        Camp = 3,       // 营地帐篷
        Port = 4,       // 港口码头
        Generic = 5,    // 通用
    }

    // ========================================
    // 桶结构
    // ========================================

    private class OverlayBucket
    {
        public POIVisualType VisualType;
        public MultiMeshInstance3D Instance = null!;
        public List<Vector3> Positions = new();
        public List<bool> IsCenterFlags = new();
    }

    // ========================================
    // 字段
    // ========================================

    private Mesh? _sharedHexMesh;
    private Shader? _poiShader;
    private readonly Dictionary<POIVisualType, OverlayBucket> _buckets = new();

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>初始化渲染器（创建共享 mesh，加载 shader）</summary>
    public void Initialize()
    {
        Name = "POIHexOverlayRenderer";
        _sharedHexMesh = CreateHexMesh();
        _poiShader = GD.Load<Shader>(POIShaderPath);

        if (_poiShader == null)
            GD.PrintErr("[POIHexOverlayRenderer] 无法加载 POI shader: " + POIShaderPath);
    }

    /// <summary>
    /// 为一个 POI 的所有占用 hex 添加覆盖层
    /// </summary>
    /// <param name="poi">POI 数据</param>
    public void AddPOI(OverworldPOI poi)
    {
        var visualType = GetVisualType(poi);

        // 确保 OccupiedHexes 有数据
        var hexes = poi.OccupiedHexes;
        if (hexes == null || hexes.Length == 0)
        {
            // 回退：从 Position 计算中心 hex
            var centerHex = HexOverworldTile.PixelToAxial(poi.Position.X, poi.Position.Y);
            hexes = new[] { centerHex };
            GD.Print($"[POIHexOverlay] POI '{poi.PoiName}' OccupiedHexes 为空，回退到中心格 ({centerHex.X},{centerHex.Y})");
        }

        foreach (var hex in hexes)
        {
            // 直接从 axial 坐标计算像素位置，无需 tile 查询
            var pixelPos = HexOverworldTile.AxialToPixel(hex.X, hex.Y);
            var worldPos = new Vector3(
                pixelPos.X * CoordConverter.PixelToWorld,
                OverlayYOffset,
                pixelPos.Y * CoordConverter.PixelToWorld
            );

            bool isCenter = (hex == poi.CenterHex) || hexes.Length == 1;
            AddHexToBucket(visualType, worldPos, isCenter);
        }
    }

    /// <summary>所有 POI 添加完毕后调用 — 重建所有桶的 MultiMesh</summary>
    public void RebuildAll()
    {
        foreach (var kvp in _buckets)
            RebuildBucket(kvp.Value);

        int totalHexes = 0;
        foreach (var b in _buckets.Values)
            totalHexes += b.Positions.Count;

        if (totalHexes == 0)
            GD.PushWarning("[POIHexOverlayRenderer] 警告：没有任何 POI hex 被添加到覆盖层！");
        else
            GD.Print($"[POIHexOverlayRenderer] 渲染 {_buckets.Count} 种 POI 类型, 共 {totalHexes} 个覆盖 hex");
    }

    /// <summary>清除所有覆盖层</summary>
    public void ClearAll()
    {
        foreach (var kvp in _buckets)
            kvp.Value.Instance.QueueFree();
        _buckets.Clear();
    }

    // ========================================
    // 内部 — POI 类型映射
    // ========================================

    /// <summary>从 OverworldPOI 推导视觉类型</summary>
    private static POIVisualType GetVisualType(OverworldPOI poi)
    {
        return poi.PoiTypeEnum switch
        {
            OverworldPOI.POIType.Town => POIVisualType.Town,
            OverworldPOI.POIType.Village => POIVisualType.Village,
            OverworldPOI.POIType.Castle => POIVisualType.Castle,
            OverworldPOI.POIType.Settlement => POIVisualType.Camp,
            OverworldPOI.POIType.Lair => POIVisualType.Camp,
            OverworldPOI.POIType.Port => POIVisualType.Port,
            OverworldPOI.POIType.Outpost => POIVisualType.Castle,
            OverworldPOI.POIType.Tavern => POIVisualType.Village,
            OverworldPOI.POIType.Mine => POIVisualType.Camp,
            OverworldPOI.POIType.Farm => POIVisualType.Village,
            OverworldPOI.POIType.Shrine => POIVisualType.Generic,
            _ => POIVisualType.Generic,
        };
    }

    /// <summary>获取 POI 视觉类型的配色方案</summary>
    private static (Color baseC, Color darkC, Color lightC, Color accentC) GetColorScheme(POIVisualType type)
    {
        return type switch
        {
            // 城镇：暖灰石板色
            POIVisualType.Town => (
                new Color(0.65f, 0.55f, 0.40f),
                new Color(0.35f, 0.28f, 0.18f),
                new Color(0.80f, 0.72f, 0.55f),
                new Color(0.90f, 0.80f, 0.30f)
            ),
            // 村庄：暖褐木色
            POIVisualType.Village => (
                new Color(0.60f, 0.48f, 0.30f),
                new Color(0.32f, 0.24f, 0.14f),
                new Color(0.75f, 0.62f, 0.42f),
                new Color(0.80f, 0.70f, 0.35f)
            ),
            // 城堡：冷灰石色
            POIVisualType.Castle => (
                new Color(0.50f, 0.48f, 0.45f),
                new Color(0.28f, 0.26f, 0.24f),
                new Color(0.68f, 0.65f, 0.60f),
                new Color(0.55f, 0.55f, 0.70f)
            ),
            // 营地：泥土暖色
            POIVisualType.Camp => (
                new Color(0.55f, 0.42f, 0.28f),
                new Color(0.30f, 0.22f, 0.12f),
                new Color(0.70f, 0.58f, 0.38f),
                new Color(0.75f, 0.55f, 0.25f)
            ),
            // 港口：灰蓝木色
            POIVisualType.Port => (
                new Color(0.50f, 0.50f, 0.48f),
                new Color(0.30f, 0.32f, 0.35f),
                new Color(0.68f, 0.65f, 0.58f),
                new Color(0.45f, 0.60f, 0.70f)
            ),
            // 通用：中性暖色
            _ => (
                new Color(0.58f, 0.50f, 0.38f),
                new Color(0.32f, 0.26f, 0.18f),
                new Color(0.72f, 0.65f, 0.50f),
                new Color(0.70f, 0.60f, 0.40f)
            ),
        };
    }

    // ========================================
    // 内部 — 桶管理
    // ========================================

    private void AddHexToBucket(POIVisualType type, Vector3 worldPos, bool isCenter)
    {
        if (!_buckets.TryGetValue(type, out var bucket))
        {
            bucket = CreateBucket(type);
            _buckets[type] = bucket;
        }

        bucket.Positions.Add(worldPos);
        bucket.IsCenterFlags.Add(isCenter);
    }

    private OverlayBucket CreateBucket(POIVisualType type)
    {
        var bucket = new OverlayBucket { VisualType = type };
        bucket.Instance = new MultiMeshInstance3D();
        bucket.Instance.Name = $"POIOverlay_{type}";

        var (baseC, darkC, lightC, accentC) = GetColorScheme(type);

        if (_poiShader != null)
        {
            var mat = new ShaderMaterial();
            mat.Shader = _poiShader;
            mat.SetShaderParameter("color_base", baseC);
            mat.SetShaderParameter("color_dark", darkC);
            mat.SetShaderParameter("color_light", lightC);
            mat.SetShaderParameter("color_accent", accentC);
            mat.SetShaderParameter("poi_pattern", (int)type);
            mat.SetShaderParameter("noise_scale", 1.2f);
            mat.SetShaderParameter("feather_width", 0.06f);
            mat.SetShaderParameter("overlay_opacity", 0.85f);
            mat.SetShaderParameter("ink_amount", 0.35f);
            mat.SetShaderParameter("ink_threshold", 0.40f);
            mat.SetShaderParameter("is_center", 0.0f);
            mat.RenderPriority = 1;
            bucket.Instance.MaterialOverride = mat;
        }
        else
        {
            // Shader 加载失败时使用简单材质作为回退
            GD.PushWarning("[POIHexOverlay] Shader 加载失败，使用 StandardMaterial3D 回退");
            var mat = new StandardMaterial3D
            {
                AlbedoColor = new Color(baseC.R, baseC.G, baseC.B, 0.85f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            bucket.Instance.MaterialOverride = mat;
        }

        // 透明度支持 + 渲染优先级（确保在地形之上）
        bucket.Instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        bucket.Instance.Transparency = 0.01f;

        AddChild(bucket.Instance);
        return bucket;
    }

    private void RebuildBucket(OverlayBucket bucket)
    {
        int count = bucket.Positions.Count;
        if (count == 0)
        {
            bucket.Instance.Multimesh = null;
            return;
        }

        // 对于中心格需要单独的 material instance（is_center=1）
        // 简化方案：中心格和非中心格分开渲染会增加 draw call
        // 折中：用 instance custom data 传递 is_center 标记
        // MultiMesh 支持 UseCustomData → shader 通过 INSTANCE_CUSTOM 读取

        var mm = new MultiMesh();
        mm.Mesh = _sharedHexMesh;
        mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        mm.UseColors = false;
        mm.UseCustomData = true; // INSTANCE_CUSTOM.x = is_center
        mm.InstanceCount = count;

        for (int i = 0; i < count; i++)
        {
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, bucket.Positions[i]));
            // custom data: x=is_center (0 or 1), yzw=reserved
            float centerFlag = bucket.IsCenterFlags[i] ? 1.0f : 0.0f;
            mm.SetInstanceCustomData(i, new Color(centerFlag, 0, 0, 0));
        }

        bucket.Instance.Multimesh = mm;
    }

    // ========================================
    // Hex Mesh 创建（与 HexOverworldRenderer3D 相同几何体）
    // ========================================

    private static ArrayMesh CreateHexMesh()
    {
        float R = 1.0f; // 与 HexOverworldRenderer3D.HexRadius3D 一致

        // Flat-top hex 顶点（XZ 平面，Y=0）
        var verts = new Vector3[7];
        verts[0] = Vector3.Zero;
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60.0f * i);
            verts[i + 1] = new Vector3(R * Mathf.Cos(angle), 0, R * Mathf.Sin(angle));
        }

        // UV：hex 局部坐标映射到 0~1（用于 SDF 裁剪）
        var uvs = new Vector2[7];
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60.0f * i);
            uvs[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle));
        }

        // 三角形扇
        var indices = new int[18];
        for (int i = 0; i < 6; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = (i + 1) % 6 + 1;
        }

        var normals = new Vector3[7];
        for (int i = 0; i < 7; i++)
            normals[i] = Vector3.Up;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.ResourceName = "POIOverlayHexMesh";
        return mesh;
    }
}
