// CombatMaterialManager.cs
// 战斗场景 3D 材质管理器 — HD-2D 风格（3D 六棱柱 + 贴 2D 写实纹理）
//
// 职责：
// - 按 TerrainType 缓存顶面 ShaderMaterial（tileable + unshaded + 假光）
// - 按 cliff key 缓存侧面 ShaderMaterial
// - 按 coverType 缓存掩体材质
//
// 纹理路径契约（美术资产见 docs/battle-map-texture-spec.md）：
//   顶面：res://src/assets/tiles/battle_ground/tops/{BattleTopKey}_{variant}.png
//   侧面：res://src/assets/tiles/battle_ground/cliffs/{BattleCliffKey}.png
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 战斗场景材质管理器 — 单例
/// </summary>
[GlobalClass]
public partial class CombatMaterialManager : RefCounted
{
    // ========================================
    // 常量
    // ========================================

    private const string TopTextureDir = "res://src/assets/tiles/battle_ground/tops";
    private const string CliffTextureDir = "res://assets/tiles/battle_ground/cliffs";
    private const string LegacyTextureDir = "res://src/assets/tiles/hex_terrain";

    private const string TopShaderPath = "res://src/assets/shaders/battle_ground_top.gdshader";
    private const string CliffShaderPath = "res://src/assets/shaders/battle_ground_cliff.gdshader";

    // ========================================
    // 单例
    // ========================================

    private static CombatMaterialManager? _instance;
    public static CombatMaterialManager Instance => _instance ??= new CombatMaterialManager();
    public static CombatMaterialManager GetInstance() => Instance;

    // ========================================
    // 缓存
    // ========================================

    /// <summary>顶面材质缓存：key = $"{terrainInt}"</summary>
    private readonly Dictionary<string, ShaderMaterial> _topMaterials = new();

    /// <summary>侧面材质缓存：key = cliffKey</summary>
    private readonly Dictionary<string, ShaderMaterial> _cliffMaterials = new();

    private Shader? _topShader;
    private Shader? _cliffShader;

    // ========================================
    // 旧接口（向后兼容，委托到顶面材质）
    // ========================================

    /// <summary>[兼容] 按 (TerrainType, Elevation) 取材质——新架构下 elevation 由 shader 内部处理，这里只返回顶面材质</summary>
    public ShaderMaterial GetMaterial(BattleCellData.TerrainType terrainType, int elevation)
    {
        return GetTopMaterial(terrainType);
    }

    // ========================================
    // 顶面
    // ========================================

    /// <summary>获取给定战斗地形的顶面材质（tileable + unshaded）</summary>
    /// <summary>强制使用占位符纹理（调试模式）</summary>
    public static bool ForceUsePlaceholder = true;

    public ShaderMaterial GetTopMaterial(BattleCellData.TerrainType terrainType)
    {
        string key = ((int)terrainType).ToString();
        if (_topMaterials.TryGetValue(key, out var existing))
            return existing;

        var mat = new ShaderMaterial();
        _topShader ??= GD.Load<Shader>(TopShaderPath);
        if (_topShader != null)
        {
            mat.Shader = _topShader;
        }

        var profile = BattleTerrainBridge.GetProfile(terrainType);

        // 强制占位符模式：用程序化精细纹理(三色调色板 + 图案)
        if (ForceUsePlaceholder)
        {
            var tex = CreateProceduralTileTexture(
                profile.DominantColor, profile.PaletteDark, profile.PaletteLight, profile.PatternType);
            mat.SetShaderParameter("top_texture", tex);
        }
        else
        {
            var tex = LoadTopTexture(profile);
            if (tex != null)
            {
                mat.SetShaderParameter("top_texture", tex);
            }
            else
            {
                var dummy = CreateSolidColorTexture(profile.DominantColor);
                mat.SetShaderParameter("top_texture", dummy);
            }
        }

        _topMaterials[key] = mat;

        // Stochastic tiling 参数:消除平铺纹理重复
        mat.SetShaderParameter("use_stochastic", true);
        mat.SetShaderParameter("blend_sharpness", 6.0f);
        mat.SetShaderParameter("color_variance", 0.06f);

        // 设置侧面颜色(shader 内部用法线 Y 区分顶面/侧面)
        var sideTerrainType = (BattleCellData.TerrainType)int.Parse(key);
        var (sideBase, _, _) = GetCliffColors(sideTerrainType, profile);
        mat.SetShaderParameter("side_color", new Godot.Vector3(sideBase.R, sideBase.G, sideBase.B));

        // 设置侧面贴图（如果有）
        string cliffPath = $"{CliffTextureDir}/{profile.BattleCliffKey}.png";
        bool cliffExists = ResourceLoader.Exists(cliffPath);
        if (!cliffExists)
        {
            // 尝试 .jpeg 后缀
            cliffPath = $"{CliffTextureDir}/{profile.BattleCliffKey}.jpeg";
            cliffExists = ResourceLoader.Exists(cliffPath);
        }
        if (cliffExists)
        {
            var sideTex = GD.Load<Texture2D>(cliffPath);
            if (sideTex != null)
            {
                mat.SetShaderParameter("side_texture", sideTex);
                mat.SetShaderParameter("use_side_texture", true);
                GD.Print($"[CombatMaterialManager] 侧面贴图加载成功: {cliffPath}");
            }
        }
        else
        {
            GD.Print($"[CombatMaterialManager] 侧面贴图未找到: {CliffTextureDir}/{profile.BattleCliffKey}.*");
        }

        return mat;
    }

    private static Texture2D? LoadTopTexture(TerrainVisualProfile profile)
    {
        // 优先级：新路径 battle_ground/tops/{key}_0.png → 旧路径（兼容，不保证写实）
        string p0 = $"{TopTextureDir}/{profile.BattleTopKey}_0.png";
        if (ResourceLoader.Exists(p0))
            return GD.Load<Texture2D>(p0);

        string pLegacy = $"{LegacyTextureDir}/{profile.OverworldKey}_0.png";
        if (ResourceLoader.Exists(pLegacy))
            return GD.Load<Texture2D>(pLegacy);

        return null;
    }

    // ========================================
    // 侧面（cliff）
    // ========================================

    /// <summary>获取给定战斗地形的侧面（悬崖）材质 — 颜色根据地形类型变化</summary>
    public ShaderMaterial GetCliffMaterial(BattleCellData.TerrainType terrainType)
    {
        var profile = BattleTerrainBridge.GetProfile(terrainType);
        string cacheKey = $"cliff_{(int)terrainType}";

        if (_cliffMaterials.TryGetValue(cacheKey, out var existing))
            return existing;

        var mat = new ShaderMaterial();
        _cliffShader ??= GD.Load<Shader>(CliffShaderPath);
        if (_cliffShader != null)
            mat.Shader = _cliffShader;

        // 优先加载真实纹理
        string path = $"{CliffTextureDir}/{profile.BattleCliffKey}.png";
        if (ResourceLoader.Exists(path))
        {
            mat.SetShaderParameter("cliff_texture", GD.Load<Texture2D>(path));
        }
        else
        {
            // 程序化侧面纹理:从顶面颜色派生暗色调(模拟泥土/岩石/根系)
            var (baseC, darkC, lightC) = GetCliffColors(terrainType, profile);
            var cliffTex = CreateProceduralTileTexture(baseC, darkC, lightC);
            mat.SetShaderParameter("cliff_texture", cliffTex);
        }

        _cliffMaterials[cacheKey] = mat;
        return mat;
    }

    /// <summary>按 cliff key 直接获取侧面材质（向后兼容）</summary>
    public ShaderMaterial GetCliffMaterialByKey(string cliffKey)
    {
        if (_cliffMaterials.TryGetValue(cliffKey, out var existing))
            return existing;

        var mat = new ShaderMaterial();
        _cliffShader ??= GD.Load<Shader>(CliffShaderPath);
        if (_cliffShader != null)
            mat.Shader = _cliffShader;

        string path = $"{CliffTextureDir}/{cliffKey}.png";
        if (ResourceLoader.Exists(path))
        {
            mat.SetShaderParameter("cliff_texture", GD.Load<Texture2D>(path));
        }
        else
        {
            var cliffTex = CreateProceduralTileTexture(
                new Color(0.40f, 0.34f, 0.28f),
                new Color(0.28f, 0.23f, 0.18f),
                new Color(0.48f, 0.42f, 0.35f));
            mat.SetShaderParameter("cliff_texture", cliffTex);
        }

        _cliffMaterials[cliffKey] = mat;
        return mat;
    }

    /// <summary>根据地形类型派生侧面颜色(比顶面暗 30-40%,偏向泥土/岩石色)</summary>
    private static (Color baseC, Color darkC, Color lightC) GetCliffColors(
        BattleCellData.TerrainType terrainType, TerrainVisualProfile profile)
    {
        // 基础策略:取顶面 DominantColor 暗化 + 偏棕/灰
        var topColor = profile.DominantColor;

        return terrainType switch
        {
            // 草地/平原 → 泥土棕
            BattleCellData.TerrainType.Plains or
            BattleCellData.TerrainType.Grassland or
            BattleCellData.TerrainType.Savanna or
            BattleCellData.TerrainType.LuckyGrass =>
                (new Color(0.38f, 0.30f, 0.20f),
                 new Color(0.25f, 0.18f, 0.12f),
                 new Color(0.45f, 0.36f, 0.26f)),

            // 森林/密林 → 深棕(树根/腐殖土)
            BattleCellData.TerrainType.Forest or
            BattleCellData.TerrainType.DenseForest =>
                (new Color(0.30f, 0.22f, 0.14f),
                 new Color(0.18f, 0.12f, 0.08f),
                 new Color(0.38f, 0.28f, 0.18f)),

            // 水域 → 深蓝灰岩
            BattleCellData.TerrainType.ShallowWater or
            BattleCellData.TerrainType.DeepWater =>
                (new Color(0.22f, 0.28f, 0.35f),
                 new Color(0.14f, 0.18f, 0.25f),
                 new Color(0.30f, 0.35f, 0.42f)),

            // 山地/丘陵 → 灰岩
            BattleCellData.TerrainType.Hills or
            BattleCellData.TerrainType.Mountain =>
                (new Color(0.42f, 0.40f, 0.38f),
                 new Color(0.28f, 0.26f, 0.24f),
                 new Color(0.52f, 0.50f, 0.47f)),

            // 沙地 → 砂岩黄
            BattleCellData.TerrainType.Sand =>
                (new Color(0.55f, 0.48f, 0.35f),
                 new Color(0.40f, 0.34f, 0.24f),
                 new Color(0.62f, 0.55f, 0.42f)),

            // 雪地 → 冻土灰白
            BattleCellData.TerrainType.Snow =>
                (new Color(0.55f, 0.56f, 0.58f),
                 new Color(0.40f, 0.42f, 0.44f),
                 new Color(0.65f, 0.67f, 0.70f)),

            // 沼泽 → 暗绿泥
            BattleCellData.TerrainType.Swamp or
            BattleCellData.TerrainType.PoisonMushroom =>
                (new Color(0.25f, 0.30f, 0.20f),
                 new Color(0.15f, 0.20f, 0.12f),
                 new Color(0.32f, 0.38f, 0.26f)),

            // 道路 → 压实泥土
            BattleCellData.TerrainType.Road =>
                (new Color(0.42f, 0.36f, 0.28f),
                 new Color(0.30f, 0.25f, 0.18f),
                 new Color(0.50f, 0.44f, 0.35f)),

            // 墙/废墟 → 石砖灰
            BattleCellData.TerrainType.Wall or
            BattleCellData.TerrainType.Ruins =>
                (new Color(0.38f, 0.36f, 0.34f),
                 new Color(0.25f, 0.24f, 0.22f),
                 new Color(0.48f, 0.46f, 0.43f)),

            // 默认 → 通用泥土
            _ => (new Color(0.40f, 0.34f, 0.28f),
                  new Color(0.28f, 0.23f, 0.18f),
                  new Color(0.48f, 0.42f, 0.35f)),
        };
    }

    // ========================================
    // 掩体（遗留）
    // ========================================

    private StandardMaterial3D? _halfCoverMat;
    private StandardMaterial3D? _fullCoverMat;

    /// <summary>获取掩体材质（coverType: 0=无, 1=半掩体, 2=全掩体）</summary>
    public StandardMaterial3D GetCoverMaterial(int coverType)
    {
        if (coverType == 1)
        {
            _halfCoverMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.5f, 0.2f) };
            return _halfCoverMat;
        }
        _fullCoverMat ??= new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.4f) };
        return _fullCoverMat;
    }

    // ========================================
    // 工具
    // ========================================

    /// <summary>
    /// 程序化生成 128×128 tileable 占位纹理。
    /// 用低频 value noise(不是 sin 波)避免摩尔纹,只做柔和的明暗变化。
    /// 不同地形靠三色调色板区分,不靠图案。
    /// </summary>
    private static Texture2D CreateSolidColorTexture(Color color)
    {
        return CreateProceduralTileTexture(color, color * 0.82f, color * 1.12f);
    }

    private static Texture2D CreateProceduralTileTexture(Color baseColor, Color darkColor, Color lightColor, int _patternType = 0)
    {
        const int size = 128;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        baseColor.A = 1f; darkColor.A = 1f; lightColor.A = 1f;

        // 用 hash-based value noise(低频,tileable)
        // 先生成一张 16×16 的随机值表,然后双线性插值到 128×128 → 柔和无摩尔纹
        const int gridSize = 16;
        var grid = new float[gridSize, gridSize];
        int seed = baseColor.GetHashCode();
        var rng = new System.Random(seed);
        for (int gy = 0; gy < gridSize; gy++)
            for (int gx = 0; gx < gridSize; gx++)
                grid[gx, gy] = (float)rng.NextDouble();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 映射到 grid 坐标(tileable:wrap around)
                float gxf = (float)x / size * gridSize;
                float gyf = (float)y / size * gridSize;

                int gx0 = ((int)gxf) % gridSize;
                int gy0 = ((int)gyf) % gridSize;
                int gx1 = (gx0 + 1) % gridSize;
                int gy1 = (gy0 + 1) % gridSize;

                float fx = gxf - (int)gxf;
                float fy = gyf - (int)gyf;

                // Smoothstep 插值(消除线性插值的棱角)
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);

                float v00 = grid[gx0, gy0];
                float v10 = grid[gx1, gy0];
                float v01 = grid[gx0, gy1];
                float v11 = grid[gx1, gy1];

                float value = Mathf.Lerp(
                    Mathf.Lerp(v00, v10, fx),
                    Mathf.Lerp(v01, v11, fx),
                    fy);

                // 三色混合
                Color c;
                if (value < 0.35f)
                    c = darkColor.Lerp(baseColor, value / 0.35f);
                else if (value < 0.65f)
                    c = baseColor;
                else
                    c = baseColor.Lerp(lightColor, (value - 0.65f) / 0.35f);

                img.SetPixel(x, y, c);
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
