// TextureScaleConfig.cs
// 投射物纹理缩放配置 — 管理投射物在 3D 场景中的世界尺寸
//
// 注意：装备纹理不再使用此配置。装备在 SubViewport 内 1:1 像素显示，不缩放。
// 投射物是直接的 Sprite3D（不走 SubViewport），所以仍需要 PixelSize 计算。
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Combat;

/// <summary>
/// 投射物纹理缩放配置。
/// <para>定义投射物在世界空间中的目标尺寸，根据实际贴图像素尺寸计算 Sprite3D.PixelSize。</para>
/// </summary>
public static class TextureScaleConfig
{
    // ========================================
    // 投射物目标世界尺寸（最长边，世界单位）
    // ========================================

    private static readonly Dictionary<string, float> ProjectileWorldSize = new()
    {
        { "arrow",          40.0f },
        { "crossbow_bolt",  35.0f },
        { "throwing_knife", 28.0f },
        { "throwing_axe",   36.0f },
        { "fireball",       48.0f },
        { "magic_bolt",     32.0f },
        { "ice_shard",      36.0f },
        { "lightning",      44.0f },
    };

    /// <summary>投射物默认世界尺寸（未配置类型的 fallback）</summary>
    private const float DefaultProjectileWorldSize = 36.0f;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 计算投射物的 Sprite3D.PixelSize。
    /// </summary>
    public static float GetProjectilePixelSize(string projectileType, Vector2 texturePixels)
    {
        if (texturePixels.X <= 0 || texturePixels.Y <= 0) return 0.3f;

        float targetSize = ProjectileWorldSize.GetValueOrDefault(projectileType, DefaultProjectileWorldSize);
        float maxPixel = Mathf.Max(texturePixels.X, texturePixels.Y);
        return targetSize / maxPixel;
    }

    /// <summary>
    /// 获取投射物的目标世界尺寸（最长边）。
    /// </summary>
    public static float GetProjectileTargetSize(string projectileType)
    {
        return ProjectileWorldSize.GetValueOrDefault(projectileType, DefaultProjectileWorldSize);
    }

    /// <summary>
    /// 从 Texture2D 计算投射物 PixelSize。
    /// </summary>
    public static float GetProjectilePixelSize(string projectileType, Texture2D? texture)
    {
        if (texture == null) return 0.3f;
        return GetProjectilePixelSize(projectileType, new Vector2(texture.GetWidth(), texture.GetHeight()));
    }
}
