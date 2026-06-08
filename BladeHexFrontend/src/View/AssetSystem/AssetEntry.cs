using System.Collections.Generic;

namespace BladeHex.View.AssetSystem;

public sealed class AssetEntry
{
    public required string Id { get; init; }
    public required AssetKind Kind { get; init; }
    public required string Path { get; init; }
    public string FallbackId { get; init; } = "";
    public string SourceId { get; init; } = "built_in";
    public bool IsModded { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}
