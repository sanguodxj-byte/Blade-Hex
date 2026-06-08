// FogIllustrationLayer.cs
// 迷雾插画层 — 在未探索区域绘制抽象怪物/地标插画（文明系列风格）
// 插画使用 fog mask 逐像素裁剪，随迷雾揭示被逐步覆盖（而非整张消失）
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.Environment;

/// <summary>
/// 迷雾插画定义 — 描述一个插画的位置、纹理、大小等
/// </summary>
public class FogIllustration
{
    /// <summary>唯一标识</summary>
    public string Id { get; set; } = "";

    /// <summary>插画类型（用于选择纹理）</summary>
    public IllustrationType Type { get; set; } = IllustrationType.Dragon;

    /// <summary>世界像素坐标（中心点）</summary>
    public Vector2 WorldPosition { get; set; }

    /// <summary>显示大小（世界单位）</summary>
    public float Size { get; set; } = 5.0f;

    /// <summary>旋转角度（弧度）</summary>
    public float Rotation { get; set; } = 0.0f;

    /// <summary>色调（叠加在纹理上的颜色）</summary>
    public Color Tint { get; set; } = new(0.45f, 0.35f, 0.25f, 0.7f);

    /// <summary>自定义纹理路径（优先于 Type 自动选择）</summary>
    public string? CustomTexturePath { get; set; }

    /// <summary>关联的 POI/区域名称（用于调试）</summary>
    public string? LinkedRegion { get; set; }
}

/// <summary>
/// 插画类型枚举 — 预定义的抽象绘画主题
/// </summary>
public enum IllustrationType
{
    Dragon, SeaMonster, Skull, Giant, Serpent, Wolf,
    Eagle, Treant, Flame, IceCrystal, Compass, Ship, Custom,
}

/// <summary>
/// 迷雾插画层 — 管理所有迷雾区域的装饰性插画。
/// 
/// 核心机制：每个插画是一个带 shader 的 QuadMesh 平面。
/// Shader 采样全局 fog_mask 纹理，只在未探索区域（fog_mask.r > 0）显示像素。
/// 当玩家逐步探索时，插画被逐像素裁剪掉，和迷雾完全同步。
/// </summary>
[GlobalClass]
public partial class FogIllustrationLayer : Node3D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>插画悬浮高度（略低于迷雾覆盖层）</summary>
    [Export] public float IllustrationHeight { get; set; } = 0.6f;

    /// <summary>插画纹理目录路径</summary>
    [Export] public string TextureDirectory { get; set; } = "res://assets/fog_illustrations/";

    // ========================================
    // 内部状态
    // ========================================

    private BladeHex.Strategic.FogOfWar? _fogData;
    private ImageTexture? _fogMaskTexture;
    private float _worldWidthPx;
    private float _worldHeightPx;
    private Shader? _illustrationShader;

    private readonly List<IllustrationInstance> _instances = new();
    private readonly Dictionary<string, IllustrationInstance> _instanceMap = new();

    private class IllustrationInstance
    {
        public FogIllustration Definition = null!;
        public MeshInstance3D Mesh = null!;
        public bool IsFullyRevealed = false;
    }

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 初始化插画层
    /// </summary>
    /// <param name="fogData">迷雾数据源</param>
    /// <param name="fogMaskTexture">迷雾 mask 纹理（与 FogOverlay3D 共享）</param>
    /// <param name="worldWidthPx">世界宽度像素</param>
    /// <param name="worldHeightPx">世界高度像素</param>
    public void Initialize(BladeHex.Strategic.FogOfWar fogData, ImageTexture fogMaskTexture, float worldWidthPx, float worldHeightPx)
    {
        _fogData = fogData;
        _fogMaskTexture = fogMaskTexture;
        _worldWidthPx = worldWidthPx;
        _worldHeightPx = worldHeightPx;
        _illustrationShader = CreateIllustrationShader();
        GD.Print("[FogIllustrationLayer] 初始化完成");
    }

    // ========================================
    // 插画管理 API
    // ========================================

    public void AddIllustration(FogIllustration illustration)
    {
        if (_instanceMap.ContainsKey(illustration.Id)) return;

        var mesh = CreateIllustrationMesh(illustration);
        AddChild(mesh);

        var instance = new IllustrationInstance
        {
            Definition = illustration,
            Mesh = mesh,
        };
        _instances.Add(instance);
        _instanceMap[illustration.Id] = instance;
    }

    public void AddIllustrations(IEnumerable<FogIllustration> illustrations)
    {
        foreach (var ill in illustrations)
            AddIllustration(ill);
        GD.Print($"[FogIllustrationLayer] 已添加 {_instances.Count} 个插画");
    }

    public void RemoveIllustration(string id)
    {
        if (_instanceMap.TryGetValue(id, out var instance))
        {
            instance.Mesh.QueueFree();
            _instances.Remove(instance);
            _instanceMap.Remove(id);
        }
    }

    public int ActiveCount => _instances.Count;

    // ========================================
    // 每帧更新 — 清理完全揭示的插画（性能优化）
    // ========================================

    private int _cleanupTimer = 0;

    public override void _Process(double delta)
    {
        // 每 60 帧检查一次是否有插画已完全被揭示（可以销毁节省内存）
        _cleanupTimer++;
        if (_cleanupTimer < 60) return;
        _cleanupTimer = 0;

        if (_fogData == null) return;

        var toRemove = new List<IllustrationInstance>();
        foreach (var instance in _instances)
        {
            if (instance.IsFullyRevealed) continue;

            // 检查插画中心及四角是否全部已揭示
            var pos = instance.Definition.WorldPosition;
            float halfSize = instance.Definition.Size * 80.0f; // 近似像素半径

            bool allRevealed =
                _fogData.IsRevealed(pos.X, pos.Y) &&
                _fogData.IsRevealed(pos.X - halfSize, pos.Y - halfSize) &&
                _fogData.IsRevealed(pos.X + halfSize, pos.Y - halfSize) &&
                _fogData.IsRevealed(pos.X - halfSize, pos.Y + halfSize) &&
                _fogData.IsRevealed(pos.X + halfSize, pos.Y + halfSize);

            if (allRevealed)
            {
                instance.IsFullyRevealed = true;
                toRemove.Add(instance);
            }
        }

        foreach (var instance in toRemove)
        {
            instance.Mesh.QueueFree();
            _instances.Remove(instance);
            _instanceMap.Remove(instance.Definition.Id);
        }
    }

    // ========================================
    // Mesh 创建（带 fog mask shader）
    // ========================================

    private MeshInstance3D CreateIllustrationMesh(FogIllustration illustration)
    {
        var meshInst = new MeshInstance3D();
        meshInst.Name = $"Ill_{illustration.Id}";

        // 位置
        var worldPos = BladeHex.View.Map.CoordConverter.PixelToWorld3D(illustration.WorldPosition);
        meshInst.Position = new Vector3(worldPos.X, IllustrationHeight, worldPos.Z);

        // 旋转（绕 Y 轴）
        meshInst.RotationDegrees = new Vector3(-90, Mathf.RadToDeg(illustration.Rotation), 0);

        // Quad mesh 大小
        var quad = new QuadMesh();
        quad.Size = new Vector2(illustration.Size, illustration.Size);
        meshInst.Mesh = quad;

        // Shader material — 使用 fog mask 裁剪
        var mat = new ShaderMaterial();
        mat.Shader = _illustrationShader;

        // 加载插画纹理
        var texture = LoadIllustrationTexture(illustration);
        texture ??= GeneratePlaceholderTexture(illustration.Type);
        mat.SetShaderParameter("illustration_texture", texture);
        mat.SetShaderParameter("fog_mask", _fogMaskTexture!);
        mat.SetShaderParameter("tint_color", illustration.Tint);

        // 计算插画在世界中的 UV 范围（用于采样 fog mask）
        // fog mask UV = 像素坐标 / 世界总像素尺寸
        float uvCenterX = illustration.WorldPosition.X / _worldWidthPx;
        float uvCenterY = illustration.WorldPosition.Y / _worldHeightPx;
        float halfSizePx = illustration.Size * 80.0f; // 近似：1 世界单位 ≈ 160px
        float uvHalfW = halfSizePx / _worldWidthPx;
        float uvHalfH = halfSizePx / _worldHeightPx;

        mat.SetShaderParameter("fog_uv_rect", new Vector4(
            uvCenterX - uvHalfW, uvCenterY - uvHalfH,
            uvCenterX + uvHalfW, uvCenterY + uvHalfH));

        meshInst.MaterialOverride = mat;
        meshInst.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        return meshInst;
    }

    // ========================================
    // Shader — 使用 fog mask 逐像素裁剪
    // ========================================

    private static Shader CreateIllustrationShader()
    {
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded;

uniform sampler2D illustration_texture : filter_linear_mipmap, source_color;
uniform sampler2D fog_mask : filter_linear, repeat_disable;
uniform vec4 tint_color : source_color = vec4(0.45, 0.35, 0.25, 0.7);
// fog_uv_rect: (min_u, min_v, max_u, max_v) — 插画在 fog mask 中的 UV 范围
uniform vec4 fog_uv_rect = vec4(0.0, 0.0, 1.0, 1.0);

void fragment() {
    // 采样插画纹理
    vec4 tex_color = texture(illustration_texture, UV);
    
    // 计算当前像素在 fog mask 中的 UV
    vec2 fog_uv = mix(fog_uv_rect.xy, fog_uv_rect.zw, UV);
    float fog_value = texture(fog_mask, fog_uv).r;
    
    // fog_value > 0 = 未探索区域 = 显示插画
    // fog_value = 0 = 已揭示 = 隐藏插画
    float visibility = smoothstep(0.0, 0.15, fog_value);
    
    // 最终颜色 = 插画纹理 × 色调
    ALBEDO = tex_color.rgb * tint_color.rgb;
    ALPHA = tex_color.a * tint_color.a * visibility;
}
";
        return shader;
    }

    // ========================================
    // 纹理加载
    // ========================================

    private Texture2D? LoadIllustrationTexture(FogIllustration illustration)
    {
        if (!string.IsNullOrEmpty(illustration.CustomTexturePath))
        {
            var tex = TextureAssetResolver.LoadFogIllustration(illustration.CustomTexturePath);
            if (tex != null) return tex;
        }

        string fileName = illustration.Type.ToString();
        string[] extensions = { ".png", ".svg", ".webp" };
        foreach (var ext in extensions)
        {
            string path = TextureDirectory + fileName + ext;
            var texture = TextureAssetResolver.LoadFogIllustration(path);
            if (texture != null)
                return texture;
        }
        return null;
    }

    /// <summary>生成占位纹理</summary>
    private static ImageTexture GeneratePlaceholderTexture(IllustrationType type)
    {
        const int size = 256;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        img.Fill(Colors.Transparent);

        var color = new Color(0.3f, 0.22f, 0.15f, 0.85f);
        float cx = size * 0.5f, cy = size * 0.5f;

        switch (type)
        {
            case IllustrationType.Dragon:
                DrawEllipse(img, (int)cx, (int)cy, size / 3, size / 5, color);
                DrawCircle(img, (int)(cx + size * 0.25f), (int)(cy - size * 0.1f), size / 8, color);
                DrawTriangle(img, (int)cx, (int)(cy - size * 0.3f),
                    (int)(cx - size * 0.3f), (int)cy, (int)(cx + size * 0.1f), (int)cy, color);
                DrawTriangle(img, (int)cx, (int)(cy - size * 0.3f),
                    (int)(cx + size * 0.3f), (int)cy, (int)(cx - size * 0.1f), (int)cy, color);
                break;
            case IllustrationType.Skull:
                DrawCircle(img, (int)cx, (int)(cy * 0.9f), size / 4, color);
                var dark = new Color(0.15f, 0.1f, 0.08f, 0.9f);
                DrawCircle(img, (int)(cx - size * 0.08f), (int)(cy * 0.85f), size / 14, dark);
                DrawCircle(img, (int)(cx + size * 0.08f), (int)(cy * 0.85f), size / 14, dark);
                break;
            case IllustrationType.Compass:
                DrawCircle(img, (int)cx, (int)cy, size / 3, color);
                int r = size / 3;
                for (int i = -r; i <= r; i++)
                {
                    if ((int)cx + i >= 0 && (int)cx + i < size)
                        img.SetPixel((int)cx + i, (int)cy, color);
                    if ((int)cy + i >= 0 && (int)cy + i < size)
                        img.SetPixel((int)cx, (int)cy + i, color);
                }
                break;
            default:
                DrawCircle(img, (int)cx, (int)cy, size / 3, color);
                break;
        }

        return ImageTexture.CreateFromImage(img);
    }

    // ========================================
    // 基础绘图工具
    // ========================================

    private static void DrawCircle(Image img, int cx, int cy, int radius, Color color)
    {
        int size = img.GetWidth();
        int rSq = radius * radius;
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(size - 1, cy + radius); y++)
            for (int x = Math.Max(0, cx - radius); x <= Math.Min(size - 1, cx + radius); x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= rSq)
                    img.SetPixel(x, y, color);
    }

    private static void DrawEllipse(Image img, int cx, int cy, int rx, int ry, Color color)
    {
        int size = img.GetWidth();
        for (int y = Math.Max(0, cy - ry); y <= Math.Min(size - 1, cy + ry); y++)
            for (int x = Math.Max(0, cx - rx); x <= Math.Min(size - 1, cx + rx); x++)
            {
                float dx = (float)(x - cx) / rx, dy = (float)(y - cy) / ry;
                if (dx * dx + dy * dy <= 1.0f) img.SetPixel(x, y, color);
            }
    }

    private static void DrawTriangle(Image img, int x0, int y0, int x1, int y1, int x2, int y2, Color color)
    {
        int size = img.GetWidth();
        int minX = Math.Max(0, Math.Min(x0, Math.Min(x1, x2)));
        int maxX = Math.Min(size - 1, Math.Max(x0, Math.Max(x1, x2)));
        int minY = Math.Max(0, Math.Min(y0, Math.Min(y1, y2)));
        int maxY = Math.Min(size - 1, Math.Max(y0, Math.Max(y1, y2)));
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float d1 = (x - x2) * (y0 - y2) - (x0 - x2) * (y - y2);
                float d2 = (x - x2) * (y1 - y2) - (x1 - x2) * (y - y2);
                float d3 = (x - x0) * (y1 - y0) - (x1 - x0) * (y - y0);
                bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
                bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
                if (!(hasNeg && hasPos)) img.SetPixel(x, y, color);
            }
    }
}
