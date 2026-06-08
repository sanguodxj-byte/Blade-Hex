using Godot;
using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public static class AssetCatalogValidator
{
    public static IReadOnlyList<string> ValidateLoadedCatalog()
    {
        var issues = new List<string>();

        foreach (var entry in AssetCatalog.GetAll())
        {
            if (!PathExists(entry.Path))
                issues.Add($"Missing path: {entry.Kind}:{entry.Id} -> {entry.Path}");

            if (!string.IsNullOrWhiteSpace(entry.FallbackId)
                && !AssetCatalog.TryGet(entry.Kind, entry.FallbackId, out _))
            {
                issues.Add($"Missing fallback: {entry.Kind}:{entry.Id} -> {entry.FallbackId}");
            }
        }

        DetectFallbackCycles(issues);
        return issues;
    }

    public static void LogLoadedCatalogIssues()
    {
        foreach (var issue in ValidateLoadedCatalog())
            GD.PushWarning($"[AssetCatalogValidator] {issue}");
    }

    private static void DetectFallbackCycles(List<string> issues)
    {
        foreach (var entry in AssetCatalog.GetAll())
        {
            var seen = new HashSet<string>();
            var current = entry;

            while (!string.IsNullOrWhiteSpace(current.FallbackId))
            {
                string key = $"{current.Kind}|{current.Id}";
                if (!seen.Add(key))
                {
                    issues.Add($"Fallback cycle: {key}");
                    break;
                }

                if (!AssetCatalog.TryGet(current.Kind, current.FallbackId, out current))
                    break;
            }
        }
    }

    private static bool PathExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (path.StartsWith("res://") || path.StartsWith("uid://"))
        {
            if (ResourceLoader.Exists(path))
                return true;

            return FileAccess.FileExists(ProjectSettings.GlobalizePath(path));
        }

        if (path.StartsWith("user://"))
            return FileAccess.FileExists(path) || FileAccess.FileExists(ProjectSettings.GlobalizePath(path));

        return FileAccess.FileExists(path) || System.IO.File.Exists(path);
    }
}
