using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace BladeHex.View.AssetSystem;

public static class AssetCatalog
{
    private const string BuiltInCatalogPath = "res://assets/catalog/built_in_assets.json";
    private const string BuiltInRelativeBase = "res://assets";
    private const string ModsDir = "user://mods";

    private static readonly Dictionary<(AssetKind Kind, string Id), AssetEntry> Entries = new();

    public static bool IsInitialized { get; private set; }
    public static int Count => Entries.Count;

    public static void Initialize()
    {
        if (IsInitialized)
            return;

        LoadCatalogFile(BuiltInCatalogPath, BuiltInRelativeBase, "built_in", isModded: false);
        LoadModCatalogs();
        IsInitialized = true;
    }

    public static void Reload()
    {
        Clear();
        Initialize();
    }

    public static void Clear()
    {
        Entries.Clear();
        IsInitialized = false;
    }

    public static void Register(AssetEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
            return;

        Entries[(entry.Kind, NormalizeId(entry.Id))] = entry;
    }

    public static bool TryGet(AssetKind kind, string? id, out AssetEntry entry)
    {
        Initialize();
        entry = null!;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return Entries.TryGetValue((kind, NormalizeId(id)), out entry!);
    }

    public static bool TryGetPath(AssetKind kind, string? id, out string path)
    {
        path = "";
        if (!TryGet(kind, id, out var entry))
            return false;

        path = entry.Path;
        return !string.IsNullOrWhiteSpace(path);
    }

    public static IReadOnlyCollection<AssetEntry> GetAll()
    {
        Initialize();
        return Entries.Values;
    }

    public static void LoadCatalogFile(string catalogPath, string relativeBasePath, string sourceId, bool isModded)
    {
        if (!FileAccess.FileExists(catalogPath))
            return;

        using var file = FileAccess.Open(catalogPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return;

        string json = file.GetAsText();
        if (string.IsNullOrWhiteSpace(json))
            return;

        CatalogDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CatalogDocument>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AssetCatalog] Failed to parse catalog {catalogPath}: {ex.Message}");
            return;
        }

        if (document?.Assets == null)
            return;

        foreach (var entry in document.Assets)
        {
            if (string.IsNullOrWhiteSpace(entry.Id)
                || string.IsNullOrWhiteSpace(entry.Kind)
                || string.IsNullOrWhiteSpace(entry.Path)
                || !TryParseKind(entry.Kind, out var kind))
            {
                continue;
            }

            Register(new AssetEntry
            {
                Id = entry.Id,
                Kind = kind,
                Path = ResolvePath(relativeBasePath, entry.Path),
                FallbackId = entry.FallbackId ?? "",
                SourceId = string.IsNullOrWhiteSpace(entry.SourceId) ? sourceId : entry.SourceId!,
                IsModded = isModded,
                Tags = entry.Tags ?? [],
            });
        }
    }

    private static void LoadModCatalogs()
    {
        using var mods = DirAccess.Open(ModsDir);
        if (mods == null)
            return;

        var modRoots = new List<ModSource>();
        mods.ListDirBegin();
        for (string modId = mods.GetNext(); !string.IsNullOrEmpty(modId); modId = mods.GetNext())
        {
            if (!mods.CurrentIsDir() || modId.StartsWith('.') || modId.StartsWith('_'))
                continue;

            string modRoot = $"{ModsDir}/{modId}";
            var manifest = LoadModManifest($"{modRoot}/manifest.json");
            if (!manifest.Enabled)
                continue;

            modRoots.Add(new ModSource(modId, modRoot, manifest.LoadOrder));
        }
        mods.ListDirEnd();

        modRoots.Sort((left, right) =>
        {
            int order = left.LoadOrder.CompareTo(right.LoadOrder);
            return order != 0 ? order : string.CompareOrdinal(left.ModId, right.ModId);
        });

        foreach (var mod in modRoots)
            LoadCatalogFile($"{mod.RootPath}/assets.json", mod.RootPath, mod.ModId, isModded: true);
    }

    private static string NormalizeId(string id)
    {
        return id.Trim().ToLowerInvariant();
    }

    private static string ResolvePath(string relativeBasePath, string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith("res://")
            || normalized.StartsWith("user://")
            || normalized.StartsWith("uid://")
            || System.IO.Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        return $"{relativeBasePath.TrimEnd('/')}/{normalized}";
    }

    private static bool TryParseKind(string value, out AssetKind kind)
    {
        string normalized = value.Trim().Replace("-", "_").ToLowerInvariant();
        string enumName = normalized switch
        {
            "icon" => nameof(AssetKind.Icon),
            "portrait" => nameof(AssetKind.Portrait),
            "unit_sprite" => nameof(AssetKind.UnitSprite),
            "character_part" => nameof(AssetKind.CharacterPart),
            "equipment_texture" => nameof(AssetKind.EquipmentTexture),
            "sfx" => nameof(AssetKind.Sfx),
            "bgm" => nameof(AssetKind.Bgm),
            "ambient" => nameof(AssetKind.Ambient),
            "vfx" => nameof(AssetKind.Vfx),
            "campaign_illustration" => nameof(AssetKind.CampaignIllustration),
            "poi_illustration" => nameof(AssetKind.PoiIllustration),
            "origin_illustration" => nameof(AssetKind.OriginIllustration),
            "ui_texture" => nameof(AssetKind.UiTexture),
            "fog_illustration" => nameof(AssetKind.FogIllustration),
            "projectile_texture" => nameof(AssetKind.ProjectileTexture),
            "map_texture" => nameof(AssetKind.MapTexture),
            "sprite_frames" => nameof(AssetKind.SpriteFrames),
            "shader" => nameof(AssetKind.Shader),
            "packed_scene" => nameof(AssetKind.PackedScene),
            "scene" => nameof(AssetKind.PackedScene),
            "animation" => nameof(AssetKind.Animation),
            "material" => nameof(AssetKind.Material),
            _ => value,
        };

        return Enum.TryParse(enumName, ignoreCase: true, out kind);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class CatalogDocument
    {
        public List<CatalogEntryDto>? Assets { get; set; }
    }

    private sealed class CatalogEntryDto
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Path { get; set; } = "";

        [JsonPropertyName("fallback_id")]
        public string? FallbackId { get; set; }

        [JsonPropertyName("source_id")]
        public string? SourceId { get; set; }

        public List<string>? Tags { get; set; }
    }

    private sealed record ModSource(string ModId, string RootPath, int LoadOrder);

    private sealed class ModManifestDto
    {
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("load_order")]
        public int LoadOrder { get; set; }
    }

    private static ModManifestDto LoadModManifest(string manifestPath)
    {
        if (!FileAccess.FileExists(manifestPath))
            return new ModManifestDto();

        using var file = FileAccess.Open(manifestPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return new ModManifestDto();

        string json = file.GetAsText();
        if (string.IsNullOrWhiteSpace(json))
            return new ModManifestDto();

        try
        {
            return JsonSerializer.Deserialize<ModManifestDto>(json, JsonOptions) ?? new ModManifestDto();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AssetCatalog] Failed to parse mod manifest {manifestPath}: {ex.Message}");
            return new ModManifestDto();
        }
    }
}
