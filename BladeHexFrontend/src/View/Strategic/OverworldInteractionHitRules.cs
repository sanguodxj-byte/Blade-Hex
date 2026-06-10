using Godot;

namespace BladeHex.View.Strategic;

/// <summary>
/// Shared overworld marker sizes and hit radii for visual, hover, and click logic.
/// Keep these values in sync so the player can trust what the cursor is targeting.
/// </summary>
public static class OverworldInteractionHitRules
{
    public const float EntitySpriteSize = 40.0f;
    public const float EntityDotScale = 0.4f;
    public const float EntityLabelFontSize = 14.0f;
    public const float EntityHitRadius = 40.0f;
    public const float PoiHitRadius = 128.0f;

    public static Vector2 SpriteScaleForTexture(Texture2D? texture, float targetPixelSize)
    {
        float sourceSize = texture != null
            ? Mathf.Max(1.0f, Mathf.Max(texture.GetWidth(), texture.GetHeight()))
            : 1.0f;
        float scale = targetPixelSize / sourceSize;
        return new Vector2(scale, scale);
    }
}
