// ChunkPersistence.cs
// Chunk 磁盘持久化 — 读写单个 chunk 到文件
// 世界生成后所有 chunk 序列化到磁盘，运行时按需加载/卸载
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Map;

using FileAccess = Godot.FileAccess;

/// <summary>
/// Chunk 磁盘持久化 — 管理 chunk 数据的文件读写
/// 存档结构: user://saves/{saveId}/chunks/chunk_{q}_{r}.dat
/// </summary>
public static class ChunkPersistence
{
    /// <summary>chunk 文件目录名</summary>
    private const string ChunksDirName = "chunks";

    // ========================================
    // 路径工具
    // ========================================

    /// <summary>获取存档根目录</summary>
    public static string GetSaveDir(string saveId)
    {
        return $"user://saves/{saveId}";
    }

    /// <summary>获取 chunk 目录</summary>
    public static string GetChunksDir(string saveId)
    {
        return $"{GetSaveDir(saveId)}/{ChunksDirName}";
    }

    /// <summary>获取单个 chunk 文件路径</summary>
    public static string GetChunkPath(string saveId, Vector2I coord)
    {
        return $"{GetChunksDir(saveId)}/chunk_{coord.X}_{coord.Y}.dat";
    }

    // ========================================
    // 读取
    // ========================================

    /// <summary>
    /// 从磁盘加载单个 chunk
    /// </summary>
    /// <returns>ChunkData 或 null（文件不存在时）</returns>
    public static ChunkData? LoadChunk(string saveId, Vector2I coord)
    {
        string path = GetChunkPath(saveId, coord);

        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[ChunkPersistence] 无法打开 chunk 文件: {path}, error={FileAccess.GetOpenError()}");
            return null;
        }

        string json = file.GetAsText();
        var parsed = Json.ParseString(json);
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PrintErr($"[ChunkPersistence] chunk 文件格式错误: {path}");
            return null;
        }

        return ChunkData.Deserialize((Godot.Collections.Dictionary)parsed);
    }

    // ========================================
    // 写入
    // ========================================

    /// <summary>
    /// 保存单个 chunk 到磁盘
    /// </summary>
    public static bool SaveChunk(string saveId, ChunkData chunk)
    {
        string dir = GetChunksDir(saveId);
        EnsureDirectoryExists(dir);

        string path = GetChunkPath(saveId, chunk.ChunkCoord);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[ChunkPersistence] 无法写入 chunk 文件: {path}, error={FileAccess.GetOpenError()}");
            return false;
        }

        var data = chunk.Serialize();
        string json = Json.Stringify(data);
        file.StoreString(json);
        return true;
    }

    /// <summary>
    /// 批量保存修改过的 chunk
    /// </summary>
    public static int SaveModifiedChunks(string saveId, IEnumerable<ChunkData> chunks)
    {
        int count = 0;
        foreach (var chunk in chunks)
        {
            if (SaveChunk(saveId, chunk))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 保存所有 chunk（世界生成完成后调用）
    /// </summary>
    public static int SaveAllChunks(string saveId, Dictionary<Vector2I, ChunkData> allChunks)
    {
        string dir = GetChunksDir(saveId);
        EnsureDirectoryExists(dir);

        int count = 0;
        foreach (var (coord, chunk) in allChunks)
        {
            if (SaveChunk(saveId, chunk))
                count++;
        }

        GD.Print($"[ChunkPersistence] 保存 {count}/{allChunks.Count} chunks 到 {dir}");
        return count;
    }

    // ========================================
    // 世界元数据
    // ========================================

    /// <summary>
    /// 保存世界元数据（种子、尺寸、国家列表等）
    /// </summary>
    public static bool SaveWorldMeta(string saveId, Godot.Collections.Dictionary meta)
    {
        string dir = GetSaveDir(saveId);
        EnsureDirectoryExists(dir);

        string path = $"{dir}/world_meta.json";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return false;

        file.StoreString(Json.Stringify(meta, "\t"));
        return true;
    }

    /// <summary>
    /// 加载世界元数据
    /// </summary>
    public static Godot.Collections.Dictionary? LoadWorldMeta(string saveId)
    {
        string path = $"{GetSaveDir(saveId)}/world_meta.json";
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return null;

        return (Godot.Collections.Dictionary)parsed;
    }

    // ========================================
    // 查询
    // ========================================

    /// <summary>检查指定 chunk 是否已保存到磁盘</summary>
    public static bool HasChunk(string saveId, Vector2I coord)
    {
        return FileAccess.FileExists(GetChunkPath(saveId, coord));
    }

    /// <summary>检查存档是否存在</summary>
    public static bool HasSave(string saveId)
    {
        return DirAccess.DirExistsAbsolute(
            ProjectSettings.GlobalizePath(GetSaveDir(saveId)));
    }

    // ========================================
    // 工具
    // ========================================

    /// <summary>确保目录存在</summary>
    private static void EnsureDirectoryExists(string path)
    {
        // Godot 的 DirAccess 使用 user:// 路径
        var dir = DirAccess.Open("user://");
        if (dir == null) return;

        // 从 user:// 根开始逐级创建
        string relativePath = path.Replace("user://", "");
        string[] parts = relativePath.Split('/');
        string current = "";

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            current = string.IsNullOrEmpty(current) ? part : $"{current}/{part}";
            if (!dir.DirExists(current))
                dir.MakeDir(current);
        }
    }

    /// <summary>删除存档（清理所有 chunk 文件）</summary>
    public static void DeleteSave(string saveId)
    {
        string dir = GetSaveDir(saveId);
        string globalPath = ProjectSettings.GlobalizePath(dir);

        if (!DirAccess.DirExistsAbsolute(globalPath)) return;

        // 递归删除
        var access = DirAccess.Open(dir);
        if (access == null) return;

        // 删除 chunks 子目录
        string chunksDir = $"{dir}/{ChunksDirName}";
        var chunksAccess = DirAccess.Open(chunksDir);
        if (chunksAccess != null)
        {
            chunksAccess.ListDirBegin();
            string fileName = chunksAccess.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!chunksAccess.CurrentIsDir())
                    chunksAccess.Remove(fileName);
                fileName = chunksAccess.GetNext();
            }
            chunksAccess.ListDirEnd();
        }

        GD.Print($"[ChunkPersistence] 存档已删除: {saveId}");
    }

    // ========================================
    // POI 持久化
    // ========================================

    /// <summary>获取 POI 文件路径</summary>
    private static string GetPoisPath(string saveId)
    {
        return $"{GetSaveDir(saveId)}/world_pois.json";
    }

    /// <summary>
    /// 保存所有 POI 到磁盘
    /// </summary>
    public static bool SavePois(string saveId, List<BladeHex.Strategic.OverworldPOI> pois)
    {
        string dir = GetSaveDir(saveId);
        EnsureDirectoryExists(dir);

        string path = GetPoisPath(saveId);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[ChunkPersistence] 无法写入 POI 文件: {path}, error={FileAccess.GetOpenError()}");
            return false;
        }

        var poiArray = new Godot.Collections.Array();
        foreach (var poi in pois)
            poiArray.Add(poi.Serialize());

        file.StoreString(Json.Stringify(poiArray, "\t"));
        GD.Print($"[ChunkPersistence] 保存 {pois.Count} 个 POI 到 {path}");
        return true;
    }

    /// <summary>
    /// 从磁盘加载所有 POI
    /// </summary>
    public static List<BladeHex.Strategic.OverworldPOI>? LoadPois(string saveId)
    {
        string path = GetPoisPath(saveId);
        if (!FileAccess.FileExists(path)) return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Array)
        {
            GD.PrintErr($"[ChunkPersistence] POI 文件格式错误: {path}");
            return null;
        }

        var poiArray = (Godot.Collections.Array)parsed;
        var result = new List<BladeHex.Strategic.OverworldPOI>();

        foreach (var item in poiArray)
        {
            if (item.VariantType != Variant.Type.Dictionary) continue;
            var data = (Godot.Collections.Dictionary)item;
            var poi = DeserializePoi(data);
            if (poi != null)
                result.Add(poi);
        }

        GD.Print($"[ChunkPersistence] 加载 {result.Count} 个 POI 从 {path}");
        return result;
    }

    /// <summary>检查 POI 文件是否存在</summary>
    public static bool HasPois(string saveId)
    {
        return FileAccess.FileExists(GetPoisPath(saveId));
    }

    /// <summary>
    /// 从 Dictionary 反序列化单个 POI
    /// </summary>
    private static BladeHex.Strategic.OverworldPOI? DeserializePoi(Godot.Collections.Dictionary data)
    {
        var poi = new BladeHex.Strategic.OverworldPOI();

        if (data.TryGetValue("poi_name", out var name))
            poi.PoiName = name.AsString();
        if (data.TryGetValue("poi_type", out var type))
            poi.PoiTypeEnum = (BladeHex.Strategic.OverworldPOI.POIType)type.AsInt32();
        if (data.TryGetValue("position_x", out var px) && data.TryGetValue("position_y", out var py))
            poi.Position = new Vector2((float)px.AsDouble(), (float)py.AsDouble());
        if (data.TryGetValue("owning_faction", out var faction))
            poi.OwningFaction = faction.AsString();
        if (data.TryGetValue("prosperity", out var prosperity))
            poi.Prosperity = prosperity.AsInt32();

        // Footprint / Scale 字段（旧存档没有 → 用默认 solo + rotation 0）
        if (data.TryGetValue("center_hex_q", out var chq) && data.TryGetValue("center_hex_r", out var chr))
            poi.CenterHex = new Vector2I(chq.AsInt32(), chr.AsInt32());
        if (data.TryGetValue("footprint_template", out var fpTpl))
            poi.FootprintTemplateName = fpTpl.AsString();
        if (data.TryGetValue("footprint_rotation", out var fpRot))
            poi.FootprintRotation = fpRot.AsInt32();
        // 重建占用 hex 缓存
        try { poi.RebuildOccupiedHexes(); }
        catch (System.Exception) { /* 模板未注册，留 OccupiedHexes 为空 */ }

        // 外族聚落字段
        if (data.TryGetValue("settlement_race", out var race))
            poi.SettlementRaceValue = (BladeHex.Strategic.OverworldPOI.SettlementRace)race.AsInt32();
        if (data.TryGetValue("threat_level", out var threat))
            poi.ThreatLevel = (float)threat.AsDouble();
        if (data.TryGetValue("raid_interval_days", out var raidInterval))
            poi.RaidIntervalDays = raidInterval.AsInt32();
        if (data.TryGetValue("max_raiding_parties", out var maxRaid))
            poi.MaxRaidingParties = maxRaid.AsInt32();

        // 巢穴字段
        if (data.TryGetValue("lair_type", out var lairType))
            poi.LairTypeValue = (BladeHex.Strategic.OverworldPOI.LairType)lairType.AsInt32();
        if (data.TryGetValue("lair_level", out var lairLevel))
            poi.LairLevel = lairLevel.AsInt32();
        if (data.TryGetValue("is_cleared", out var cleared))
            poi.IsCleared = cleared.AsBool();

        // 设施
        if (data.TryGetValue("has_tavern", out var tavern))
            poi.HasTavern = tavern.AsBool();
        if (data.TryGetValue("has_shop", out var shop))
            poi.HasShop = shop.AsBool();
        if (data.TryGetValue("has_blacksmith", out var smith))
            poi.HasBlacksmith = smith.AsBool();
        if (data.TryGetValue("has_quest_board", out var quest))
            poi.HasQuestBoard = quest.AsBool();
        if (data.TryGetValue("has_barracks", out var barracks))
            poi.HasBarracks = barracks.AsBool();

        // 城堡防御
        if (data.TryGetValue("castle_defense_level", out var defLevel))
            poi.CastleDefenseLevel = defLevel.AsInt32();
        if (data.TryGetValue("garrison_max", out var garMax))
            poi.GarrisonMax = garMax.AsInt32();
        if (data.TryGetValue("garrison_current", out var garCur))
            poi.GarrisonCurrent = garCur.AsInt32();
        if (data.TryGetValue("lord_personality", out var personality))
            poi.LordPersonalityValue = (BladeHex.Strategic.OverworldPOI.LordPersonality)personality.AsInt32();

        // 运行时状态
        if (data.TryGetValue("days_since_last_raid", out var daysSince))
            poi.DaysSinceLastRaid = daysSince.AsInt32();
        if (data.TryGetValue("active_raiding_parties", out var activeRaid))
            poi.ActiveRaidingParties = activeRaid.AsInt32();
        if (data.TryGetValue("is_under_siege", out var siege))
            poi.IsUnderSiege = siege.AsBool();
        if (data.TryGetValue("siege_days", out var siegeDays))
            poi.SiegeDays = siegeDays.AsInt32();
        if (data.TryGetValue("last_attacked_day", out var lastAtk))
            poi.LastAttackedDay = lastAtk.AsInt32();

        return poi;
    }

    // ========================================
    // 河流骨架持久化
    // ========================================

    /// <summary>获取河流骨架文件路径</summary>
    private static string GetSkeletonPath(string saveId)
    {
        return $"{GetSaveDir(saveId)}/road_skeleton.json";
    }

    /// <summary>保存河流骨架到磁盘</summary>
    public static bool SaveSkeleton(string saveId, RiverRoadSkeleton skeleton)
    {
        string path = GetSkeletonPath(saveId);
        EnsureDirectoryExists(path);

        var data = skeleton.Serialize();
        string json = Json.Stringify(data, "  ");

        var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return false;
        file.StoreString(json);
        file.Close();

        GD.Print($"[ChunkPersistence] 河流骨架已保存: {skeleton.RiverPaths.Count} 条河流");
        return true;
    }

    /// <summary>从磁盘加载河流骨架（兼容旧存档含 roads 字段）</summary>
    public static RiverRoadSkeleton? LoadSkeleton(string saveId)
    {
        string path = GetSkeletonPath(saveId);
        if (!FileAccess.FileExists(path)) return null;

        var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;

        string json = file.GetAsText();
        file.Close();

        var parsed = Json.ParseString(json);
        if (parsed.VariantType != Variant.Type.Dictionary) return null;

        var skeleton = RiverRoadSkeleton.Deserialize(parsed.AsGodotDictionary());
        GD.Print($"[ChunkPersistence] 河流骨架已加载: {skeleton.RiverPaths.Count} 条河流");
        return skeleton;
    }

    /// <summary>检查骨架文件是否存在</summary>
    public static bool HasSkeleton(string saveId)
    {
        return FileAccess.FileExists(GetSkeletonPath(saveId));
    }
}
