// HexOverworldRenderer3D.cs
// 3D 大地图六边形渲染器 — 羊皮纸底色层
//
// 业界成熟方案（Catlike Coding hex map / Civ 风格）：
// 把所有 hex 的三角形合并到一个 MeshInstance3D 的单一 mesh 里，
// 不用 MultiMesh 实例化（每个实例独立 UV 是问题源头）。
// 顶点 UV 直接用世界 XZ 坐标 × scale，跨整个地图全局连续。
// 这从根本上消除：Z-fighting / 接缝 / 六边形网格视觉。
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 3D 大地图六边形渲染器（合并 mesh + 世界坐标 UV）
/// </summary>
public partial class HexOverworldRenderer3D : Node3D
{
    // ========================================
    // 常量
    // ========================================

    private const float HexRadius3D = 1.0f;
    private const float PixelToWorld = 1.0f / 156.0f;

    /// <summary>纹理 UV 缩放（世界坐标 × 此值 = UV 坐标）</summary>
    /// <remarks>
    /// scale 越大 → 纹理重复越密 → 像素越锐利。
    /// 0.5 → 一个纹理周期 = 2 世界单位 ≈ 1 个 hex 直径，
    /// 纹理细节清晰可见，且周期太密反而看不出重复模式。
    /// </remarks>
    private const float TextureUvScale = 0.5f;

    /// <summary>羊皮纸 tileable 纹理路径</summary>
    private const string ParchmentTexturePath = "res://src/assets/tiles/tileable/parchment_tile.png";

    // ========================================
    // 羊皮纸色板
    // ========================================

    private static readonly Color ParchmentBase = new(0.722f, 0.588f, 0.353f);

    // ========================================
    // 字段
    // ========================================

    private Mesh? _sharedHexMesh;
    private Shader? _parchmentShader;
    private MeshInstance3D? _groundInstance;
    private readonly Dictionary<Vector2I, HexOverworldTile> _tiles = new();
    private bool _dirty;

    // ========================================
    // 公共 API
    // ========================================

    public void Initialize()
    {
        Name = "HexOverworldRenderer3D";
        _parchmentShader = GD.Load<Shader>("res://src/assets/shaders/overworld_parchment.gdshader");
        if (_parchmentShader == null)
            GD.PrintErr("[HexOverworldRenderer3D] 无法加载 shader");

        _groundInstance = new MeshInstance3D();
        _groundInstance.Name = "ParchmentGround";
        _groundInstance.MaterialOverride = CreateGroundMaterial();
        _groundInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_groundInstance);

        GD.Print("[HexOverworldRenderer3D] *** Hex tiling shader initialized ***");
    }

    public void LoadTiles(IEnumerable<HexOverworldTile> tiles)
    {
        bool anyNew = false;
        foreach (var tile in tiles)
        {
            if (_tiles.ContainsKey(tile.Coord)) continue;
            _tiles[tile.Coord] = tile;
            anyNew = true;
        }

        if (anyNew)
            RebuildGround();
    }

    public void LoadFromGrid(HexOverworldGrid grid)
    {
        ClearAll();
        LoadTiles(grid.Tiles.Values);
        GD.Print($"[HexOverworldRenderer3D] 加载 {_tiles.Count} 个瓦片（合并 mesh）");
    }

    public void ClearAll()
    {
        _tiles.Clear();
        if (_groundInstance != null)
            _groundInstance.Mesh = null;
    }

    public void UpdateSunDirection(Vector3 sunDir)
    {
        // unshaded 模式无效果，保留 API 兼容
    }

    public Vector3 TileToWorld(HexOverworldTile tile)
    {
        return new Vector3(tile.PixelPos.X * PixelToWorld, 0.0f, tile.PixelPos.Y * PixelToWorld);
    }

    // ========================================
    // 材质
    // ========================================

    private Material CreateGroundMaterial()
    {
        var tex = LoadParchmentTexture();
        if (_parchmentShader != null)
        {
            var mat = new ShaderMaterial();
            mat.Shader = _parchmentShader;
            mat.SetShaderParameter("top_texture", tex);
            mat.SetShaderParameter("texture_scale", 0.5f);
            mat.SetShaderParameter("hex_tile_size", 8.0f);
            mat.SetShaderParameter("blend_sharpness", 4.0f);
            mat.SetShaderParameter("use_rotation", true);
            return mat;
        }

        // 回退：StandardMaterial3D
        var fallback = new StandardMaterial3D();
        fallback.AlbedoTexture = tex;
        fallback.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        fallback.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        return fallback;
    }

    private static Texture2D LoadParchmentTexture()
    {
        if (ResourceLoader.Exists(ParchmentTexturePath))
        {
            var tex = GD.Load<Texture2D>(ParchmentTexturePath);
            if (tex != null)
            {
                GD.Print($"[HexOverworldRenderer3D] 羊皮纸纹理 {tex.GetWidth()}x{tex.GetHeight()}");
                return tex;
            }
        }
        var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        img.Fill(ParchmentBase);
        return ImageTexture.CreateFromImage(img);
    }

    // ========================================
    // 合并 mesh 构建（核心）
    // ========================================

    /// <summary>
    /// 把所有 hex 的三角形合并到一个 ArrayMesh。
    /// 每个 hex 用 1 个中心顶点 + 6 个角顶点 = 7 顶点，6 个三角形。
    /// UV 直接用世界 XZ 坐标 × scale，跨 hex 全局连续。
    /// </summary>
    private void RebuildGround()
    {
        if (_groundInstance == null) return;
        if (_tiles.Count == 0)
        {
            _groundInstance.Mesh = null;
            return;
        }

        int hexCount = _tiles.Count;
        int vertCount = hexCount * 7;
        int idxCount = hexCount * 18; // 6 三角形 × 3 索引

        var verts = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var normals = new Vector3[vertCount];
        var indices = new int[idxCount];

        // 6 个角的预计算偏移
        var cornerOffsets = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60.0f * i);
            cornerOffsets[i] = new Vector3(
                HexRadius3D * Mathf.Cos(angle),
                0,
                HexRadius3D * Mathf.Sin(angle)
            );
        }

        int vBase = 0;
        int iBase = 0;
        foreach (var kvp in _tiles)
        {
            var tile = kvp.Value;
            float cx = tile.PixelPos.X * PixelToWorld;
            float cz = tile.PixelPos.Y * PixelToWorld;

            // 中心顶点
            verts[vBase] = new Vector3(cx, 0, cz);
            uvs[vBase] = new Vector2(cx * TextureUvScale, cz * TextureUvScale);
            normals[vBase] = Vector3.Up;

            // 6 个角顶点
            for (int i = 0; i < 6; i++)
            {
                Vector3 corner = new Vector3(cx, 0, cz) + cornerOffsets[i];
                verts[vBase + 1 + i] = corner;
                uvs[vBase + 1 + i] = new Vector2(corner.X * TextureUvScale, corner.Z * TextureUvScale);
                normals[vBase + 1 + i] = Vector3.Up;
            }

            // 6 个三角形（中心 → 角 i → 角 i+1）
            for (int i = 0; i < 6; i++)
            {
                indices[iBase + i * 3] = vBase;
                indices[iBase + i * 3 + 1] = vBase + 1 + i;
                indices[iBase + i * 3 + 2] = vBase + 1 + (i + 1) % 6;
            }

            vBase += 7;
            iBase += 18;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.ResourceName = "OverworldGroundMerged";
        _groundInstance.Mesh = mesh;
    }
}
