using Godot;

namespace BladeHex.View.AssetSystem;

[GlobalClass]
public partial class AssetCatalogCheck : Node
{
    public override void _Ready()
    {
        AssetCatalog.Reload();
        var issues = AssetCatalogValidator.ValidateLoadedCatalog();

        GD.Print($"[AssetCatalogCheck] entries={AssetCatalog.Count} issues={issues.Count}");

        foreach (var issue in issues)
            GD.PushWarning($"[AssetCatalogCheck] {issue}");

        GetTree().Quit(issues.Count == 0 ? 0 : 1);
    }
}
