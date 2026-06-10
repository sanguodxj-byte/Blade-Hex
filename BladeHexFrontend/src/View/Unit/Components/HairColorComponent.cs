// HairColorComponent.cs
// 发色组件 — 对发型纹理进行 HSL 重着色，保留明暗关系和细节。
// 核心公式：new_pixel = HSL(target.H, target.S, original.L)
// 
// 使用方式：
//   var recolored = HairColorComponent.ApplyHairColor(originalTexture, targetHue, targetSat);
//
// 需要时也可直接修改 Image：
//   HairColorComponent.ApplyHairColorToImage(image, targetHue, targetSat);
using Godot;

namespace BladeHex.View.Unit.Components;

/// <summary>
/// 发色组件。接收一个发型纹理 + 目标色相/饱和度，输出重着色后的新纹理。
/// 保留原始纹理的明度和透明度，仅替换色相和饱和度。
/// </summary>
public static class HairColorComponent
{
    /// <summary>
    /// 对发型纹理进行重着色，返回新 Texture2D。
    /// 不修改输入纹理。
    /// </summary>
    /// <param name="source">原始发型纹理</param>
    /// <param name="targetHue">目标色相（0~360）</param>
    /// <param name="targetSat">目标饱和度（0~1）</param>
    /// <param name="overrideLightness">明度覆盖值（-1 = 保留原明度，0~1 = 固定明度）</param>
    /// <returns>重着色后的新纹理，若输入为空则返回 null</returns>
    public static Texture2D? Apply(Texture2D? source, float targetHue, float targetSat, float overrideLightness = -1f)
    {
        if (source == null)
            return null;

        var image = source.GetImage();
        if (image == null)
            return null;

        // 复制一份以免污染原纹理缓存
        image = (Image)image.Duplicate();
        ColorUtils.RecolorHairImage(image, targetHue, targetSat, overrideLightness);

        return ImageTexture.CreateFromImage(image);
    }
}
