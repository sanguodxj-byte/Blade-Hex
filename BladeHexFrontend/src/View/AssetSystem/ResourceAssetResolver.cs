using Godot;

namespace BladeHex.View.AssetSystem;

public static class ResourceAssetResolver
{
    public static T? LoadPath<T>(string path) where T : Resource
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!path.StartsWith("res://") && !path.StartsWith("uid://") && !path.StartsWith("user://"))
            return null;

        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<T>(path);
    }
}
