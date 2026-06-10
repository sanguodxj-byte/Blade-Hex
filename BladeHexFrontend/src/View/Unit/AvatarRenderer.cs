using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.AssetSystem;
using BladeHex.View.Unit.Components;

namespace BladeHex.View.Unit;

public static class AvatarRenderer
{
    public enum Layer
    {
        Head,
        Hair,
        Decoration,
    }

    public static string GetPartPath(string partType, string race, string gender, int index)
    {
        return CharacterPartTextureResolver.GetLegacyPath(partType, race, gender, index);
    }

    public static Texture2D? LoadPartTexture(string partType, string race, string gender, int index)
    {
        return CharacterPartTextureResolver.Load(partType, race, gender, index);
    }

    public static Texture2D? RenderToTexture(AvatarData avatar)
    {
        var images = CollectLayerImages(avatar);
        if (images.Count == 0)
            return null;

        int maxW = 1;
        int maxH = 1;
        foreach (var img in images)
        {
            maxW = Mathf.Max(maxW, img.GetWidth());
            maxH = Mathf.Max(maxH, img.GetHeight());
        }

        var composite = Image.CreateEmpty(maxW, maxH, false, Image.Format.Rgba8);
        composite.Fill(new Color(0, 0, 0, 0));

        foreach (var img in images)
        {
            int ox = (maxW - img.GetWidth()) / 2;
            int oy = (maxH - img.GetHeight()) / 2;
            composite.BlendRect(
                img,
                new Rect2I(0, 0, img.GetWidth(), img.GetHeight()),
                new Vector2I(ox, oy));
        }

        return ImageTexture.CreateFromImage(composite);
    }

    public static Texture2D? RenderToSquare(AvatarData avatar, int size = 128)
    {
        var images = CollectLayerImages(avatar);
        if (images.Count == 0)
            return null;

        int maxW = 1;
        int maxH = 1;
        foreach (var img in images)
        {
            maxW = Mathf.Max(maxW, img.GetWidth());
            maxH = Mathf.Max(maxH, img.GetHeight());
        }

        float scale = Mathf.Min((float)size / maxW, (float)size / maxH);

        var composite = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        composite.Fill(new Color(0, 0, 0, 0));

        foreach (var img in images)
        {
            var scaledImg = (Image)img.Duplicate();
            scaledImg.Resize(
                Mathf.Max(1, (int)(img.GetWidth() * scale)),
                Mathf.Max(1, (int)(img.GetHeight() * scale)));

            int ox = (size - scaledImg.GetWidth()) / 2;
            int oy = (size - scaledImg.GetHeight()) / 2;
            composite.BlendRect(
                scaledImg,
                new Rect2I(0, 0, scaledImg.GetWidth(), scaledImg.GetHeight()),
                new Vector2I(ox, oy));
        }

        return ImageTexture.CreateFromImage(composite);
    }

    public static AvatarView2D BuildView(AvatarData avatar)
    {
        var view = new AvatarView2D();
        view.Setup(avatar);
        return view;
    }

    private static List<Image> CollectLayerImages(AvatarData avatar)
    {
        var list = new List<Image>(3);
        string race = avatar.RaceString;
        string gender = avatar.Gender;

        var headTex = LoadPartTexture("head", race, gender, avatar.HeadIndex);
        if (headTex != null)
        {
            var img = headTex.GetImage();
            if (img != null)
                list.Add(img);
        }

        if (avatar.HasHair)
        {
            var hairTex = LoadPartTexture("hair", race, gender, avatar.HairIndex);
            if (hairTex != null)
            {
                var img = hairTex.GetImage();
                if (img != null)
                {
                    // 若指定了自定义发色，对发型层进行重着色
                    if (avatar.HasCustomHairColor)
                        ColorUtils.RecolorHairImage(img, avatar.HairColorHue, avatar.HairColorSat, avatar.HairColorLightness);
                    list.Add(img);
                }
            }
        }

        int decorationIndex = GetDecorationIndex(avatar);
        if (decorationIndex > 0)
        {
            var decorationTex = LoadPartTexture("decoration", race, gender, decorationIndex);
            if (decorationTex != null)
            {
                var img = decorationTex.GetImage();
                if (img != null)
                    list.Add(img);
            }
        }

        return list;
    }

    public static int GetDecorationIndex(AvatarData avatar)
    {
        if (string.IsNullOrWhiteSpace(avatar.DecorationId))
            return 0;

        string id = avatar.DecorationId.Trim();
        int underscore = id.LastIndexOf('_');
        string token = underscore >= 0 ? id[(underscore + 1)..] : id;
        return int.TryParse(token, out int index) ? Mathf.Max(0, index) : 0;
    }
}

[GlobalClass]
public partial class AvatarView2D : Node2D
{
    private const string LayerPrefix = "AvatarLayer_";

    public AvatarData? AvatarDataRef { get; private set; }

    private readonly Godot.Collections.Dictionary<AvatarRenderer.Layer, Sprite2D> _sprites = new();

    public void Setup(AvatarData avatar)
    {
        AvatarDataRef = avatar;
        Rebuild();
    }

    public void RefreshTextures()
    {
        if (AvatarDataRef == null)
            return;

        var avatar = AvatarDataRef;
        ApplyTexture(
            AvatarRenderer.Layer.Head,
            AvatarRenderer.LoadPartTexture("head", avatar.RaceString, avatar.Gender, avatar.HeadIndex),
            true);

        if (avatar.HasHair)
        {
            var hairTex = AvatarRenderer.LoadPartTexture("hair", avatar.RaceString, avatar.Gender, avatar.HairIndex);
            if (hairTex != null && avatar.HasCustomHairColor)
                hairTex = HairColorComponent.Apply(hairTex, avatar.HairColorHue, avatar.HairColorSat, avatar.HairColorLightness) ?? hairTex;
            ApplyTexture(AvatarRenderer.Layer.Hair, hairTex, true);
        }
        else
        {
            HideLayer(AvatarRenderer.Layer.Hair);
        }

        int decorationIndex = AvatarRenderer.GetDecorationIndex(avatar);
        if (decorationIndex > 0)
            ApplyLayer(AvatarRenderer.Layer.Decoration, "decoration", avatar.RaceString, avatar.Gender, decorationIndex);
        else
            HideLayer(AvatarRenderer.Layer.Decoration);
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            if (child is Sprite2D sprite && sprite.Name.ToString().StartsWith(LayerPrefix))
            {
                RemoveChild(sprite);
                sprite.QueueFree();
            }
        }

        _sprites.Clear();
        RefreshTextures();
    }

    private void EnsureLayer(AvatarRenderer.Layer layer)
    {
        if (_sprites.ContainsKey(layer))
            return;

        var sprite = new Sprite2D
        {
            Name = LayerPrefix + layer.ToString(),
            Centered = true,
        };
        _sprites[layer] = sprite;
        AddChild(sprite);
    }

    private void ApplyLayer(
        AvatarRenderer.Layer layer,
        string partType,
        string race,
        string gender,
        int index,
        bool forceVisible = false)
    {
        ApplyTexture(layer, AvatarRenderer.LoadPartTexture(partType, race, gender, index), forceVisible);
    }

    private void ApplyTexture(AvatarRenderer.Layer layer, Texture2D? tex, bool forceVisible = false)
    {
        if (tex == null && !forceVisible)
            return;

        EnsureLayer(layer);
        var sprite = _sprites[layer];
        sprite.Texture = tex;
        sprite.Visible = tex != null;
    }

    private void HideLayer(AvatarRenderer.Layer layer)
    {
        if (_sprites.TryGetValue(layer, out var sprite))
            sprite.Visible = false;
    }
}
