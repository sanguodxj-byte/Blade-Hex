using Godot;

namespace BladeHex.View.AssetSystem;

internal static class SpriteFramesFileLoader
{
    public static SpriteFrames? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!path.StartsWith("res://") && !path.StartsWith("uid://") && !path.StartsWith("user://"))
            return null;

        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<SpriteFrames>(path);
    }
}
