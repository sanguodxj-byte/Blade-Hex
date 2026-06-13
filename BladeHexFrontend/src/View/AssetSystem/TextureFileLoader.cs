using Godot;

namespace BladeHex.View.AssetSystem;

internal static class TextureFileLoader
{
    public static Texture2D? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (path.StartsWith("uid://") || path.StartsWith("res://"))
        {
            string absolutePath = ProjectSettings.GlobalizePath(path);
            if (IsImageFilePath(path) && System.IO.File.Exists(absolutePath))
                return LoadImageFromFile(absolutePath);

            if (ResourceLoader.Exists(path))
            {
                var texture = ResourceLoader.Load<Texture2D>(path);
                if (texture != null)
                    return texture;
            }

            return LoadImageFromFile(absolutePath);
        }

        if (path.StartsWith("user://"))
            return LoadImageFromFile(ProjectSettings.GlobalizePath(path));

        if (System.IO.File.Exists(path))
            return LoadImageFromFile(path);

        return null;
    }

    private static bool IsImageFilePath(string path)
    {
        return path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webp", System.StringComparison.OrdinalIgnoreCase);
    }

    private static Texture2D? LoadImageFromFile(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !System.IO.File.Exists(absolutePath))
            return null;

        Image? image = null;
        try
        {
            byte[] data = System.IO.File.ReadAllBytes(absolutePath);
            if (data.Length >= 4)
            {
                image = new Image();
                var error = Error.FileUnrecognized;
                if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                    error = image.LoadJpgFromBuffer(data);
                else if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                    error = image.LoadPngFromBuffer(data);
                else if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46)
                    error = image.LoadWebpFromBuffer(data);

                if (error != Error.Ok)
                    image = Image.LoadFromFile(absolutePath);
            }
            else
            {
                image = Image.LoadFromFile(absolutePath);
            }
        }
        catch
        {
            image = Image.LoadFromFile(absolutePath);
        }

        return image == null || image.GetWidth() <= 0 ? null : ImageTexture.CreateFromImage(image);
    }
}
