// ChunkManager.cs
// Chunk 生命周期管理 — 加载/卸载/序列化
// 以玩家位置为中心，维护活跃 chunk 集合
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// Chunk 管理器 — 管理活跃 chunk 的加载、卸载和持久化
/// </summary>
[GlobalClass]
public partial class ChunkManager : RefCounted
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>加载范围（以玩家所在 chunk 为中心的六边形半径）</summary>
    public int LoadRadius { get; set; } = 2;

    /// <summary>卸载阈值（超出此半径的 chunk 进入休眠）</summary>
    public int UnloadRadius { get; set; } = 3;

    // ========================================
    // 运行时数据
    // ========================================

    /// <summary>当前活跃的 chunk: ChunkCoord → ChunkData</summary>
    public Dictionary<Vector2I, ChunkData> ActiveChunks { get; private set; } = new();

    /// <summary>已生成的 chunk 索引（含休眠 chunk）</summary>
    public HashSet<Vector2I> GeneratedChunkCoords { get; private set; } = new();

    /// <summary>内存 chunk 缓存（新游戏时持有全部世界数据，避免磁盘 IO）</summary>
    private Dictionary<Vector2I, ChunkData>? _memoryCache;

    /// <summary>获取所有已知 chunk（内存缓存优先，否则返回 ActiveChunks）</summary>
    public IReadOnlyDictionary<Vector2I, ChunkData> AllKnownChunks
        => (IReadOnlyDictionary<Vector2I, ChunkData>?)_memoryCache ?? ActiveChunks;

    /// <summary>Chunk 生成器</summary>
    public ChunkGenerator? Generator { get; set; }

    /// <summary>河流/道路印章器（用于 chunk 生成后标记河流/道路）</summary>
    public Strategic.RiverRoadStamper? RiverRoadStamper { get; set; }

    /// <summary>玩家当前所在的 chunk 坐标</summary>
    public Vector2I PlayerChunk { get; private set; } = new(-999, -999);

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 用世界种子初始化
    /// </summary>
    public void Initialize(int worldSeed, int worldWidth = 1024, int worldHeight = 768)
    {
        Generator = new ChunkGenerator();
        Generator.Initialize(worldSeed, worldWidth, worldHeight);
    }

    /// <summary>
    /// 将全部 chunk 数据加载到内存缓存（新游戏时使用，避免磁盘 IO）。
    /// 之后 LoadOrGenerateChunk 会优先从缓存取。
    /// </summary>
    public void LoadIntoMemory(Dictionary<Vector2I, ChunkData> allChunks)
    {
        _memoryCache = allChunks;
        foreach (var coord in allChunks.Keys)
            GeneratedChunkCoords.Add(coord);
    }

    /// <summary>尝试从内存缓存获取 chunk（供预渲染使用）</summary>
    public bool TryGetFromCache(Vector2I coord, out ChunkData chunk)
    {
        chunk = null!;
        if (_memoryCache != null && _memoryCache.TryGetValue(coord, out var cached))
        {
            chunk = cached;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 将内存缓存中的全部 chunk 保存到磁盘（玩家手动保存时调用）
    /// </summary>
    public int SaveAllToDisk(string saveId)
    {
        if (_memoryCache == null || _memoryCache.Count == 0)
            return ChunkPersistence.SaveModifiedChunks(saveId, ActiveChunks.Values);

        return ChunkPersistence.SaveAllChunks(saveId, _memoryCache);
    }

    // ========================================
    // 每帧更新
    // ========================================

    /// <summary>
    /// 更新 chunk 加载状态（在玩家移动时调用）
    /// </summary>
    /// <param name="playerWorldQ">玩家全局 q 坐标</param>
    /// <param name="playerWorldR">玩家全局 r 坐标</param>
    /// <returns>新加载的 chunk 列表</returns>
    public List<ChunkData> UpdateChunks(int playerWorldQ, int playerWorldR)
    {
        var newChunks = new List<ChunkData>();

        var newPlayerChunk = ChunkData.WorldToChunk(playerWorldQ, playerWorldR);

        // 玩家未移动到新 chunk，无需更新
        if (newPlayerChunk == PlayerChunk) return newChunks;

        PlayerChunk = newPlayerChunk;

        // 1. 计算需要加载的 chunk 集合
        var requiredChunks = GetChunksInRadius(PlayerChunk, LoadRadius);

        // 2. 加载新 chunk
        foreach (var coord in requiredChunks)
        {
            if (!ActiveChunks.ContainsKey(coord))
            {
                var chunk = LoadOrGenerateChunk(coord);
                ActiveChunks[coord] = chunk;
                newChunks.Add(chunk);
            }
        }

        // 3. 卸载超出范围的 chunk
        var unloadThreshold = GetChunksInRadius(PlayerChunk, UnloadRadius);
        var toUnload = new List<Vector2I>();
        foreach (var coord in ActiveChunks.Keys)
        {
            if (!unloadThreshold.Contains(coord))
                toUnload.Add(coord);
        }

        foreach (var coord in toUnload)
        {
            var chunk = ActiveChunks[coord];
            chunk.IsActive = false;
            // 保留在索引中（已生成标记），但从活跃池移除
            ActiveChunks.Remove(coord);
        }

        return newChunks;
    }

    // ========================================
    // Chunk 加载/生成
    // ========================================

    /// <summary>存档 ID（读档时设置，用于从磁盘加载 chunk）</summary>
    public string? SaveId { get; set; }

    /// <summary>
    /// 加载 chunk 优先级：内存缓存 → 磁盘存档 → 重新生成
    /// </summary>
    private ChunkData LoadOrGenerateChunk(Vector2I chunkCoord)
    {
        // 1. 内存缓存（新游戏时全部 chunk 在内存中）
        if (_memoryCache != null && _memoryCache.TryGetValue(chunkCoord, out var cached))
        {
            cached.IsActive = true;
            GeneratedChunkCoords.Add(chunkCoord);
            return cached;
        }

        // 2. 从磁盘加载（读档路径 — 恢复已保存的状态）
        if (!string.IsNullOrEmpty(SaveId))
        {
            var loaded = ChunkPersistence.LoadChunk(SaveId, chunkCoord);
            if (loaded != null)
            {
                loaded.IsActive = true;
                GeneratedChunkCoords.Add(chunkCoord);
                return loaded;
            }
        }

        // 3. 重新生成（磁盘无数据时的回退）
        var chunk = Generator!.Generate(chunkCoord.X, chunkCoord.Y);
        RiverRoadStamper?.StampOnChunk(chunk);

        chunk.IsActive = true;
        GeneratedChunkCoords.Add(chunkCoord);
        return chunk;
    }

    // ========================================
    // 六边形范围计算
    // ========================================

    /// <summary>
    /// 获取以 center 为中心、radius 为半径的所有 chunk 坐标
    /// 注意：chunk 坐标是矩形排布（不是 hex），所以用切比雪夫距离（方形）
    /// </summary>
    public static HashSet<Vector2I> GetChunksInRadius(Vector2I center, int radius)
    {
        var result = new HashSet<Vector2I>();

        // chunk 坐标系是矩形：用切比雪夫距离（方形邻居）
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                result.Add(new Vector2I(center.X + dx, center.Y + dy));
            }
        }

        return result;
    }

    // ========================================
    // 瓦片访问（便捷方法）
    // ========================================

    /// <summary>获取指定全局坐标的瓦片（从活跃 chunk 中查找）</summary>
    public HexOverworldTile? GetTile(int worldQ, int worldR)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);
        if (ActiveChunks.TryGetValue(chunkCoord, out var chunk))
            return chunk.GetTile(worldQ, worldR);
        return null;
    }

    /// <summary>获取指定全局坐标的瓦片（活跃 chunk + 内存缓存，用于小地图等全图查询）</summary>
    public HexOverworldTile? GetTileAnywhere(int worldQ, int worldR)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);
        if (ActiveChunks.TryGetValue(chunkCoord, out var chunk))
            return chunk.GetTile(worldQ, worldR);
        if (_memoryCache != null && _memoryCache.TryGetValue(chunkCoord, out var cached))
            return cached.GetTile(worldQ, worldR);
        return null;
    }

    /// <summary>获取指定全局坐标所在的活跃 chunk</summary>
    public ChunkData? GetChunkAt(int worldQ, int worldR)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);
        return ActiveChunks.GetValueOrDefault(chunkCoord);
    }

    /// <summary>判断指定全局坐标是否在已加载的 chunk 中</summary>
    public bool IsLoaded(int worldQ, int worldR)
    {
        var chunkCoord = ChunkData.WorldToChunk(worldQ, worldR);
        return ActiveChunks.ContainsKey(chunkCoord);
    }

    // ========================================
    // 序列化（存档）
    // ========================================

    /// <summary>序列化所有活跃 chunk（用于存档）</summary>
    public Godot.Collections.Dictionary SerializeActiveChunks()
    {
        var chunksData = new Godot.Collections.Dictionary();
        foreach (var kv in ActiveChunks)
        {
            chunksData[$"{kv.Key.X},{kv.Key.Y}"] = kv.Value.Serialize();
        }

        var generatedCoords = new Godot.Collections.Array();
        foreach (var coord in GeneratedChunkCoords)
        {
            generatedCoords.Add(new Vector2I(coord.X, coord.Y));
        }

        return new Godot.Collections.Dictionary
        {
            ["player_chunk"] = new Vector2I(PlayerChunk.X, PlayerChunk.Y),
            ["active_chunks"] = chunksData,
            ["generated_coords"] = generatedCoords,
        };
    }

    /// <summary>反序列化（读档）</summary>
    public void DeserializeChunks(Godot.Collections.Dictionary data)
    {
        ActiveChunks.Clear();
        GeneratedChunkCoords.Clear();

        if (data.ContainsKey("player_chunk"))
        {
            var pc = (Vector2I)data["player_chunk"];
            PlayerChunk = pc;
        }

        if (data.ContainsKey("generated_coords") && data["generated_coords"].Obj is Godot.Collections.Array coords)
        {
            foreach (var c in coords)
            {
                var coord = (Vector2I)c;
                GeneratedChunkCoords.Add(coord);
            }
        }

        if (data.ContainsKey("active_chunks") && data["active_chunks"].Obj is Godot.Collections.Dictionary chunks)
        {
            foreach (var key in chunks.Keys)
            {
                string keyStr = (string)key;
                var parts = keyStr.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int cq) && int.TryParse(parts[1], out int cr))
                {
                    var chunk = ChunkData.Deserialize((Godot.Collections.Dictionary)chunks[key]);
                    chunk.IsActive = true;
                    ActiveChunks[new Vector2I(cq, cr)] = chunk;
                }
            }
        }
    }
}
