// FogOverlay3D.cs
// 3D 大地图迷雾覆盖层 — 使用纹理（羊皮纸/古地图风格）填充未探索区域
// 作为 MeshInstance3D 平面覆盖在地图上方，通过 shader 控制透明度
using Godot;
using System;

namespace BladeHex.View.Environment;

/// <summary>
/// 3D 迷雾覆盖层 — 在未探索区域显示纹理化迷雾。
/// 
/// 工作原理：
/// 1. 一个全地图大小的平面 Mesh 悬浮在地面上方
/// 2. Shader 读取迷雾状态纹理（R通道=透明度）
/// 3. 未探索区域显示羊皮纸纹理，已揭示区域完全透明
/// 4. 视野边缘有柔和渐变过渡
/// 
/// 纹理槽位：
/// - albedo_texture: 羊皮纸/古地图纹理（平铺）
/// - fog_mask: 动态生成的迷雾状态纹理（每帧更新脏区域）
/// </summary>
[GlobalClass]
public partial class FogOverlay3D : MeshInstance3D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>羊皮纸纹理路径（可替换为任何平铺纹理）</summary>
    [Export] public string ParchmentTexturePath { get; set; } = "";

    /// <summary>迷雾平面悬浮高度（Y 轴偏移）</summary>
    [Export] public float OverlayHeight { get; set; } = 0.8f;

    /// <summary>未探索区域不透明度</summary>
    [Export] public float UnexploredOpacity { get; set; } = 0.92f;

    /// <summary>已揭示但不在视野内的区域不透明度（轻微暗化）</summary>
    [Export] public float RevealedOpacity { get; set; } = 0.0f;

    /// <summary>视野边缘渐变宽度（fog cell 数）</summary>
    [Export] public int EdgeFadeWidth { get; set; } = 3;

    /// <summary>纹理平铺缩放（越大纹理越密）</summary>
    [Export] public float TextureTilingScale { get; set; } = 0.005f;

    // ========================================
    // 内部状态
    // ========================================

    private ShaderMaterial? _material;
    private ImageTexture? _fogMaskTexture;
    private Image? _fogMaskImage;
    private BladeHex.Strategic.FogOfWar? _fogData;

    private int _maskW;
    private int _maskH;
    private bool _dirty = true;
    private int _updateFrameSkip = 0;

    // 世界尺寸（像素）
    private float _worldWidthPx;
    private float _worldHeightPx;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 初始化迷雾覆盖层
    /// </summary>
    /// <param name="fogData">迷雾数据源</param>
    /// <param name="worldWidthPx">世界宽度（像素）</param>
    /// <param name="worldHeightPx">世界高度（像素）</param>
    public void Initialize(BladeHex.Strategic.FogOfWar fogData, float worldWidthPx, float worldHeightPx)
    {
        _fogData = fogData;
        _worldWidthPx = worldWidthPx;
        _worldHeightPx = worldHeightPx;
        _maskW = fogData.GridW;
        _maskH = fogData.GridH;

        // 创建迷雾 mask 纹理（R=不透明度，单通道足够）
        _fogMaskImage = Image.CreateEmpty(_maskW, _maskH, false, Image.Format.R8);
        _fogMaskImage.Fill(new Color(1, 0, 0, 1)); // 初始全不透明（未探索）
        _fogMaskTexture = ImageTexture.CreateFromImage(_fogMaskImage);

        // 创建覆盖平面
        CreateOverlayMesh(worldWidthPx, worldHeightPx);

        // 初始更新（全量扫描，确保已揭示领土正确显示）
        FullUpdateFogMask();

        GD.Print($"[FogOverlay3D] 初始化: {_maskW}×{_maskH} mask, world={worldWidthPx:F0}×{worldHeightPx:F0}px");
    }

    private void CreateOverlayMesh(float worldWPx, float worldHPx)
    {
        // 将像素尺寸转换为 3D 世界尺寸
        var worldSize = BladeHex.View.Map.CoordConverter.PixelToWorld3D(new Vector2(worldWPx, worldHPx));
        float meshW = worldSize.X + 20.0f; // 加边距
        float meshH = worldSize.Z + 20.0f;

        var plane = new PlaneMesh();
        plane.Size = new Vector2(meshW, meshH);
        plane.SubdivideWidth = 0;
        plane.SubdivideDepth = 0;
        Mesh = plane;

        // 位置：世界中心，悬浮在地面上方
        Position = new Vector3(worldSize.X * 0.5f, OverlayHeight, worldSize.Z * 0.5f);

        // 创建 shader material
        _material = new ShaderMaterial();
        var shader = CreateFogShader();
        _material.Shader = shader;

        // 设置 uniform
        _material.SetShaderParameter("fog_mask", _fogMaskTexture!);
        _material.SetShaderParameter("unexplored_opacity", UnexploredOpacity);
        _material.SetShaderParameter("revealed_opacity", RevealedOpacity);
        _material.SetShaderParameter("tiling_scale", TextureTilingScale);

        // 加载羊皮纸纹理（如果有）
        if (!string.IsNullOrEmpty(ParchmentTexturePath))
        {
            var tex = GD.Load<Texture2D>(ParchmentTexturePath);
            if (tex != null)
                _material.SetShaderParameter("parchment_texture", tex);
        }

        // 如果没有外部纹理，生成程序化羊皮纸
        if (string.IsNullOrEmpty(ParchmentTexturePath))
        {
            var proceduralTex = GenerateProceduralParchment();
            _material.SetShaderParameter("parchment_texture", proceduralTex);
        }

        MaterialOverride = _material;
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        // 每 3 帧更新一次 mask（性能优化）
        _updateFrameSkip++;
        if (_updateFrameSkip < 3) return;
        _updateFrameSkip = 0;

        if (_dirty && _fogData != null)
        {
            UpdateFogMask();
            _dirty = false;
        }
    }

    /// <summary>标记迷雾数据已变化，需要刷新 mask</summary>
    public void MarkDirty()
    {
        _dirty = true;
    }

    // ========================================
    // Mask 更新（增量：只更新玩家视野附近的脏区域）
    // ========================================

    private Vector2I _lastPlayerCell = new(-1, -1);

    /// <summary>全量更新 mask（初始化或 reveal_all 时调用）</summary>
    public void FullUpdateFogMask()
    {
        if (_fogData == null || _fogMaskImage == null || _fogMaskTexture == null) return;

        for (int gy = 0; gy < _maskH; gy++)
        {
            for (int gx = 0; gx < _maskW; gx++)
            {
                byte state = _fogData.ExploredGrid[gy, gx];
                float opacity = state == (byte)BladeHex.Strategic.FogOfWar.FogState.Unexplored ? 1.0f : 0.0f;
                _fogMaskImage.SetPixel(gx, gy, new Color(opacity, 0, 0, 1));
            }
        }

        _fogMaskTexture.Update(_fogMaskImage);
    }

    private void UpdateFogMask()
    {
        if (_fogData == null || _fogMaskImage == null || _fogMaskTexture == null) return;

        // 计算需要更新的区域（玩家视野半径 + 边距）
        // 只更新上次和本次视野覆盖的矩形区域
        int rangeCells = (int)(_fogData.VisionRange / _fogData.CellSize) + 4;

        // 获取当前玩家 cell（通过最近一次 InVision 的中心推断）
        Vector2I currentCell = FindVisionCenter();

        int minGx = Mathf.Max(0, currentCell.X - rangeCells);
        int maxGx = Mathf.Min(_maskW - 1, currentCell.X + rangeCells);
        int minGy = Mathf.Max(0, currentCell.Y - rangeCells);
        int maxGy = Mathf.Min(_maskH - 1, currentCell.Y + rangeCells);

        // 也包含上次位置的区域（确保离开的区域也被更新）
        if (_lastPlayerCell.X >= 0)
        {
            minGx = Mathf.Min(minGx, Mathf.Max(0, _lastPlayerCell.X - rangeCells));
            maxGx = Mathf.Max(maxGx, Mathf.Min(_maskW - 1, _lastPlayerCell.X + rangeCells));
            minGy = Mathf.Min(minGy, Mathf.Max(0, _lastPlayerCell.Y - rangeCells));
            maxGy = Mathf.Max(maxGy, Mathf.Min(_maskH - 1, _lastPlayerCell.Y + rangeCells));
        }

        _lastPlayerCell = currentCell;

        // 增量更新脏区域
        for (int gy = minGy; gy <= maxGy; gy++)
        {
            for (int gx = minGx; gx <= maxGx; gx++)
            {
                byte state = _fogData.ExploredGrid[gy, gx];
                float opacity = state == (byte)BladeHex.Strategic.FogOfWar.FogState.Unexplored ? 1.0f : 0.0f;
                _fogMaskImage.SetPixel(gx, gy, new Color(opacity, 0, 0, 1));
            }
        }

        _fogMaskTexture.Update(_fogMaskImage);
    }

    /// <summary>找到当前视野中心 cell（从 FogData 的 InVision 区域推断）</summary>
    private Vector2I FindVisionCenter()
    {
        if (_fogData == null) return Vector2I.Zero;

        // 快速方法：扫描中间行找第一个 InVision cell
        int midY = _maskH / 2;
        for (int gy = midY - 20; gy <= midY + 20; gy++)
        {
            if (gy < 0 || gy >= _maskH) continue;
            for (int gx = 0; gx < _maskW; gx++)
            {
                if (_fogData.ExploredGrid[gy, gx] == (byte)BladeHex.Strategic.FogOfWar.FogState.InVision)
                    return new Vector2I(gx, gy);
            }
        }

        // Fallback：地图中心
        return new Vector2I(_maskW / 2, _maskH / 2);
    }

    // ========================================
    // Shader 生成
    // ========================================

    private static Shader CreateFogShader()
    {
        var shader = new Shader();
        shader.Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded;

uniform sampler2D fog_mask : filter_linear, repeat_disable;
uniform sampler2D parchment_texture : filter_linear_mipmap, repeat_enable;
uniform float unexplored_opacity : hint_range(0.0, 1.0) = 0.92;
uniform float revealed_opacity : hint_range(0.0, 1.0) = 0.0;
uniform float tiling_scale = 0.005;
uniform float edge_softness = 0.15;

void fragment() {
    // 采样迷雾 mask（UV 对应世界坐标归一化）
    float fog_value = texture(fog_mask, UV).r;
    
    // 柔化边缘（避免锯齿）
    float alpha = smoothstep(0.0, edge_softness, fog_value);
    alpha *= unexplored_opacity;
    
    // 羊皮纸纹理（世界坐标平铺）
    vec2 tiled_uv = UV * vec2(textureSize(fog_mask, 0)) * tiling_scale;
    vec3 parchment_color = texture(parchment_texture, tiled_uv).rgb;
    
    // 边缘染色（迷雾边缘略深，模拟古地图边缘烧焦效果）
    float edge_darken = smoothstep(0.0, 0.3, fog_value) * (1.0 - smoothstep(0.7, 1.0, fog_value));
    parchment_color *= mix(1.0, 0.75, edge_darken * 0.5);
    
    ALBEDO = parchment_color;
    ALPHA = alpha;
}
";
        return shader;
    }

    // ========================================
    // 程序化羊皮纸纹理
    // ========================================

    /// <summary>生成程序化羊皮纸纹理（256×256，暖黄色 + 噪声纤维）</summary>
    private static ImageTexture GenerateProceduralParchment()
    {
        const int size = 256;
        var img = Image.CreateEmpty(size, size, true, Image.Format.Rgb8);

        var noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Seed = 42;
        noise.Frequency = 0.02f;
        noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        noise.FractalOctaves = 4;

        var noiseDetail = new FastNoiseLite();
        noiseDetail.NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular;
        noiseDetail.Seed = 123;
        noiseDetail.Frequency = 0.05f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 基础暖黄色
                float baseR = 0.82f;
                float baseG = 0.72f;
                float baseB = 0.55f;

                // 大尺度色调变化
                float n1 = (noise.GetNoise2D(x, y) + 1.0f) * 0.5f;
                // 细节纤维纹理
                float n2 = (noiseDetail.GetNoise2D(x * 2, y * 2) + 1.0f) * 0.5f;

                float variation = (n1 - 0.5f) * 0.12f + (n2 - 0.5f) * 0.06f;

                float r = Mathf.Clamp(baseR + variation, 0.0f, 1.0f);
                float g = Mathf.Clamp(baseG + variation * 0.8f, 0.0f, 1.0f);
                float b = Mathf.Clamp(baseB + variation * 0.5f, 0.0f, 1.0f);

                img.SetPixel(x, y, new Color(r, g, b));
            }
        }

        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>设置自定义羊皮纸纹理</summary>
    public void SetParchmentTexture(Texture2D texture)
    {
        _material?.SetShaderParameter("parchment_texture", texture);
    }

    /// <summary>设置未探索区域不透明度</summary>
    public void SetUnexploredOpacity(float opacity)
    {
        UnexploredOpacity = opacity;
        _material?.SetShaderParameter("unexplored_opacity", opacity);
    }

    /// <summary>获取迷雾 mask 纹理（供其他组件共享，如插画层）</summary>
    public ImageTexture? GetFogMaskTexture() => _fogMaskTexture;
}
