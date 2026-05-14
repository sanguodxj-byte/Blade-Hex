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
    private const string CliffTextureDir = "res://src/assets/tiles/battle_ground/cliffs";
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

        // 强制占位符模式：始终使用纯色纹理
        if (ForceUsePlaceholder)
        {
            var dummy = CreateSolidColorTexture(profile.DominantColor);
            mat.SetShaderParameter("top_texture", dummy);
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

    /// <summary>获取给定战斗地形的侧面（悬崖）材质</summary>
    public ShaderMaterial GetCliffMaterial(BattleCellData.TerrainType terrainType)
    {
        var profile = BattleTerrainBridge.GetProfile(terrainType);
        return GetCliffMaterialByKey(profile.BattleCliffKey);
    }

    /// <summary>按 cliff key 直接获取侧面材质（共享跨地形）</summary>
    public ShaderMaterial GetCliffMaterialByKey(string cliffKey)
    {
        if (_cliffMaterials.TryGetValue(cliffKey, out var existing))
            return existing;

        var mat = new ShaderMaterial();
        _cliffShader ??= GD.Load<Shader>(CliffShaderPath);
        if (_cliffShader != null)
        {
            mat.Shader = _cliffShader;
        }

        string path = $"{CliffTextureDir}/{cliffKey}.png";
        if (ResourceLoader.Exists(path))
        {
            mat.SetShaderParameter("cliff_texture", GD.Load<Texture2D>(path));
        }
        else
        {
            // 占位：用 profile-free 的灰褐色
            mat.SetShaderParameter("cliff_texture", CreateSolidColorTexture(new Color(0.45f, 0.38f, 0.32f)));
        }

        _cliffMaterials[cliffKey] = mat;
        return mat;
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

    /// <summary>创建 2×2 纯色占位贴图（缺纹理时用）</summary>
    private static Texture2D CreateSolidColorTexture(Color color)
    {
        var img = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }
}
