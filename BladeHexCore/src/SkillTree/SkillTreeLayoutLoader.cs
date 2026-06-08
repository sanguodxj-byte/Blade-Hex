using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BladeHex.Strategic;

public static class SkillTreeLayoutLoader
{
    public const string DefaultLayoutPath = "BladeHexCore/src/SkillTree/skill_tree_layout.json";
    public const string DefaultContentPath = "BladeHexCore/src/SkillTree/skill_tree_content.json";

    public static bool TryLoadDefault(Dictionary<string, SkillNodeData> nodes, out string error)
    {
        string? layoutPath = ResolveDataPath(DefaultLayoutPath);
        string? contentPath = ResolveDataPath(DefaultContentPath);
        if (layoutPath == null)
        {
            error = $"Missing {DefaultLayoutPath}";
            return false;
        }

        if (contentPath == null)
        {
            error = $"Missing {DefaultContentPath}";
            return false;
        }

        return TryLoad(layoutPath, contentPath, nodes, out error);
    }

    public static bool TryLoad(string layoutPath, string contentPath, Dictionary<string, SkillNodeData> nodes, out string error)
    {
        try
        {
            using var layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutPath));
            using var contentDoc = JsonDocument.Parse(File.ReadAllText(contentPath));

            var contentById = ReadContent(contentDoc.RootElement);
            var pools = ReadPools(contentDoc.RootElement);

            nodes.Clear();
            foreach (var nodeElement in layoutDoc.RootElement.GetProperty("nodes").EnumerateArray())
            {
                string id = ReadRequiredString(nodeElement, "id");
                var node = new SkillNodeData
                {
                    NodeId = id,
                    CurrentRegion = ParseRegion(ReadRequiredString(nodeElement, "region")),
                    CurrentNodeType = ParseNodeType(ReadRequiredString(nodeElement, "type")),
                    ExplicitTiles = ReadTiles(nodeElement),
                    Depth = ReadInt(nodeElement, "depth", 0),
                    IsBridge = ReadBool(nodeElement, "isBridge", false),
                };

                if (node.ExplicitTiles.Length > 0)
                    node.GridPosition = node.ExplicitTiles[0];

                if (contentById.TryGetValue(id, out var content))
                    ApplyContent(node, content);
                else
                    ApplyDefaultContent(node);

                if (node.CurrentContentMode == SkillNodeData.ContentMode.RandomAttribute && node.StatBonuses.Count == 0)
                    node.StatBonuses = RollRandomAttribute(node, pools);

                nodes[id] = node;
            }

            error = "";
            return true;
        }
        catch (Exception ex)
        {
            nodes.Clear();
            error = ex.Message;
            return false;
        }
    }

    public static (bool ok, string message) Validate(Dictionary<string, SkillNodeData> nodes, int radius)
    {
        if (!nodes.ContainsKey(SkillTreeData.StartNodeId))
            return (false, "missing start node");

        var owner = new Dictionary<Vector2I, string>();
        foreach (var (id, node) in nodes)
        {
            var tiles = SkillNodeShape.GetTiles(node);
            if (id != SkillTreeData.StartNodeId && tiles.Length != node.GetRequiredTileCount())
                return (false, $"{id} has {tiles.Length} tiles but costs {node.GetRequiredTileCount()}");

            if (!TilesAreConnected(tiles))
                return (false, $"{id} tiles are not edge-connected");

            foreach (var tile in tiles)
            {
                if (!SkillTreeCoord.IsTileInsideHex(tile, radius))
                    return (false, $"{id} tile {tile} is outside radius {radius}");
                if (owner.TryGetValue(tile, out var other))
                    return (false, $"{id} overlaps {other} at {tile}");
                owner[tile] = id;
            }
        }

        var reached = new HashSet<string> { SkillTreeData.StartNodeId };
        var queue = new Queue<string>();
        queue.Enqueue(SkillTreeData.StartNodeId);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (var tile in SkillNodeShape.GetTiles(nodes[current]))
            {
                foreach (var neighbor in SkillTreeCoord.GetTileNeighbors(tile))
                {
                    if (!owner.TryGetValue(neighbor, out var other) || other == current)
                        continue;
                    if (reached.Add(other))
                        queue.Enqueue(other);
                }
            }
        }

        if (reached.Count != nodes.Count)
            return (false, $"start reaches {reached.Count}/{nodes.Count} nodes");

        return (true, "");
    }

    private static bool TilesAreConnected(Vector2I[] tiles)
    {
        if (tiles.Length <= 1)
            return true;

        var set = new HashSet<Vector2I>(tiles);
        var seen = new HashSet<Vector2I> { tiles[0] };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(tiles[0]);
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            foreach (var neighbor in SkillTreeCoord.GetTileNeighbors(tile))
            {
                if (set.Contains(neighbor) && seen.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        return seen.Count == tiles.Length;
    }

    private static Dictionary<string, JsonElement> ReadContent(JsonElement root)
    {
        var result = new Dictionary<string, JsonElement>();
        if (!root.TryGetProperty("nodes", out var nodesElement))
            return result;

        foreach (var node in nodesElement.EnumerateArray())
            result[ReadRequiredString(node, "id")] = node.Clone();
        return result;
    }

    private static Dictionary<SkillNodeData.Region, List<RandomPoolEntry>> ReadPools(JsonElement root)
    {
        var result = new Dictionary<SkillNodeData.Region, List<RandomPoolEntry>>();
        if (!root.TryGetProperty("randomPools", out var poolsElement))
            return result;

        foreach (var poolProp in poolsElement.EnumerateObject())
        {
            var region = ParseRegion(poolProp.Name);
            var entries = new List<RandomPoolEntry>();
            foreach (var entry in poolProp.Value.EnumerateArray())
            {
                entries.Add(new RandomPoolEntry(
                    ReadRequiredString(entry, "stat"),
                    ReadNumber(entry, "min", 0),
                    ReadNumber(entry, "max", 0),
                    ReadInt(entry, "weight", 1)));
            }
            result[region] = entries;
        }

        return result;
    }

    private static void ApplyContent(SkillNodeData node, JsonElement content)
    {
        node.NodeName = ReadString(content, "name", node.NodeId);
        node.NodeSubtitle = ReadString(content, "subtitle", "");
        node.Description = ReadString(content, "description", "");
        node.SkillEffect = ReadString(content, "effect", "");
        node.IsActiveSkill = ReadBool(content, "isActiveSkill", false);
        node.RequiredLevel = ReadInt(content, "requiredLevel", 0);
        node.KeystoneCost = ReadString(content, "keystoneCost", "");
        node.StatBonuses = ReadDictionary(content, "statBonuses");
        node.CostBonuses = ReadDictionary(content, "costBonuses");
        node.FigureName = ReadString(content, "figureName", "");
        node.FigureTemplate = ReadString(content, "figureTemplate", "");
        node.CurrentContentMode = ParseContentMode(ReadString(content, "contentMode", "fixed"));
        node.RandomSeed = ReadInt(content, "seed", StableSeed(node.NodeId));
    }

    private static void ApplyDefaultContent(SkillNodeData node)
    {
        node.NodeName = node.NodeId;
        node.NodeSubtitle = "";
        node.Description = "";
        node.RandomSeed = StableSeed(node.NodeId);
        if (node.CurrentNodeType == SkillNodeData.NodeType.Small || node.CurrentNodeType == SkillNodeData.NodeType.Pip)
            node.CurrentContentMode = SkillNodeData.ContentMode.RandomAttribute;
    }

    private static Godot.Collections.Dictionary RollRandomAttribute(
        SkillNodeData node,
        Dictionary<SkillNodeData.Region, List<RandomPoolEntry>> pools)
    {
        var dict = new Godot.Collections.Dictionary();
        if (!pools.TryGetValue(node.CurrentRegion, out var entries) || entries.Count == 0)
            return dict;

        var rng = new Random(node.RandomSeed);
        var main = Pick(entries, rng);
        dict[main.Stat] = ToVariantValue(main.Roll(rng, useMinimum: node.CurrentNodeType == SkillNodeData.NodeType.Pip));

        if (node.CurrentNodeType == SkillNodeData.NodeType.Small && rng.NextDouble() < 0.10d)
        {
            var secondary = Pick(entries, rng);
            if (!dict.ContainsKey(secondary.Stat))
                dict[secondary.Stat] = ToVariantValue(secondary.Roll(rng, useMinimum: true));
        }

        return dict;
    }

    private static RandomPoolEntry Pick(List<RandomPoolEntry> entries, Random rng)
    {
        int total = 0;
        foreach (var entry in entries)
            total += Math.Max(0, entry.Weight);

        int roll = rng.Next(Math.Max(1, total));
        foreach (var entry in entries)
        {
            roll -= Math.Max(0, entry.Weight);
            if (roll < 0)
                return entry;
        }

        return entries[0];
    }

    private static Variant ToVariantValue(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.0001d)
            return (int)Math.Round(value);
        return (float)value;
    }

    private static string? ResolveDataPath(string relativePath)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "..", relativePath),
        };

        foreach (var candidate in candidates)
        {
            string full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        return null;
    }

    private static Vector2I[] ReadTiles(JsonElement nodeElement)
    {
        var tiles = new List<Vector2I>();
        foreach (var tileElement in nodeElement.GetProperty("tiles").EnumerateArray())
        {
            int q = tileElement[0].GetInt32();
            int r = tileElement[1].GetInt32();
            int t = tileElement[2].GetInt32();
            tiles.Add(SkillTreeCoord.EncodeTile(q, r, t));
        }

        return tiles.ToArray();
    }

    private static SkillNodeData.NodeType ParseNodeType(string value) => value.ToLowerInvariant() switch
    {
        "start" => SkillNodeData.NodeType.Start,
        "small" => SkillNodeData.NodeType.Small,
        "pip" => SkillNodeData.NodeType.Pip,
        "big" => SkillNodeData.NodeType.Big,
        "active" => SkillNodeData.NodeType.Big,
        "passive" => SkillNodeData.NodeType.Big,
        "keystone" => SkillNodeData.NodeType.Keystone,
        "giant" => SkillNodeData.NodeType.Giant,
        "apex" => SkillNodeData.NodeType.Giant,
        _ => throw new InvalidDataException($"Unknown skill node type '{value}'"),
    };

    private static SkillNodeData.Region ParseRegion(string value) => value.ToLowerInvariant() switch
    {
        "str" => SkillNodeData.Region.Str,
        "dex" => SkillNodeData.Region.Dex,
        "con" => SkillNodeData.Region.Con,
        "int" => SkillNodeData.Region.Int,
        "wis" => SkillNodeData.Region.Wis,
        "cha" => SkillNodeData.Region.Cha,
        "transition" => SkillNodeData.Region.Transition,
        "trans" => SkillNodeData.Region.Transition,
        "none" => SkillNodeData.Region.None,
        "start" => SkillNodeData.Region.None,
        _ => throw new InvalidDataException($"Unknown skill region '{value}'"),
    };

    private static SkillNodeData.ContentMode ParseContentMode(string value) => value.ToLowerInvariant() switch
    {
        "randomattribute" => SkillNodeData.ContentMode.RandomAttribute,
        "random_attribute" => SkillNodeData.ContentMode.RandomAttribute,
        "random" => SkillNodeData.ContentMode.RandomAttribute,
        _ => SkillNodeData.ContentMode.Fixed,
    };

    private static Godot.Collections.Dictionary ReadDictionary(JsonElement element, string propertyName)
    {
        var result = new Godot.Collections.Dictionary();
        if (!element.TryGetProperty(propertyName, out var dictElement) || dictElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in dictElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Number when prop.Value.TryGetInt32(out int i) => i,
                JsonValueKind.Number => (float)prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => prop.Value.ToString(),
            };
        }

        return result;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            throw new InvalidDataException($"Missing required property '{propertyName}'");
        return value.GetString() ?? "";
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static bool ReadBool(JsonElement element, string propertyName, bool fallback)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetBoolean()
            : fallback;
    }

    private static int ReadInt(JsonElement element, string propertyName, int fallback)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetInt32()
            : fallback;
    }

    private static double ReadNumber(JsonElement element, string propertyName, double fallback)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetDouble()
            : fallback;
    }

    private static int StableSeed(string text)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in text)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private sealed record RandomPoolEntry(string Stat, double Min, double Max, int Weight)
    {
        public double Roll(Random rng, bool useMinimum)
        {
            if (useMinimum || Math.Abs(Max - Min) < 0.0001d)
                return Min;

            if (Math.Abs(Min - Math.Round(Min)) < 0.0001d && Math.Abs(Max - Math.Round(Max)) < 0.0001d)
                return rng.Next((int)Math.Round(Min), (int)Math.Round(Max) + 1);

            return Min + rng.NextDouble() * (Max - Min);
        }
    }
}
