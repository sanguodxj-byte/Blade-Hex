// SaveManager.cs — 统一存档管理器（JSON 格式）
//
// 历史：项目初期有 V1 二进制版本（已移除）；本类原名 SaveManagerV2，
// 在架构优化 spec R9 期间正式接管 SaveManager 名字。
// 仍保留 V1 旧存档（.dat StoreVar 格式）的迁移读取能力（LoadLegacySave / ConvertLegacyData），
// 但目前没有 UI 入口调用，需要时由 UI 层显式触发。
using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Data;

/// <summary>
/// 统一存档管理器 — JSON 格式 + 自动备份
/// </summary>
[GlobalClass]
public partial class SaveManager : Node
{
    private const string FallbackSavePath = "user://sword_and_hex_save.json";
    private const string FallbackBackupPath = "user://sword_and_hex_save_backup.json";
    private const string LegacyPath = "user://sword_and_hex_save.dat";

    // ========================================
    // 存档路径解析（按 saveId 隔离）
    // ========================================

    private static OverworldEntity? FindChaseTarget(EntitySaveData data, List<OverworldEntity> allEntities, OverworldEntity self)
    {
        if (!string.IsNullOrEmpty(data.ChaseTargetHeroId))
        {
            foreach (var candidate in allEntities)
            {
                if (ReferenceEquals(candidate, self) || !candidate.IsAlive) continue;
                if (candidate.HeroId == data.ChaseTargetHeroId)
                    return candidate;
            }
        }

        if (string.IsNullOrEmpty(data.ChaseTargetEntityName))
            return null;

        bool hasSavedType = Enum.TryParse<OverworldEntity.EntityType>(data.ChaseTargetEntityType, out var savedType);
        bool hasSavedFaction = !string.IsNullOrEmpty(data.ChaseTargetFaction);
        bool hasSavedPosition = data.ChaseTargetPosX.HasValue && data.ChaseTargetPosY.HasValue;
        var savedPos = hasSavedPosition
            ? new Vector2(data.ChaseTargetPosX!.Value, data.ChaseTargetPosY!.Value)
            : Vector2.Zero;

        OverworldEntity? fallback = null;
        int fallbackCount = 0;

        foreach (var candidate in allEntities)
        {
            if (ReferenceEquals(candidate, self) || !candidate.IsAlive) continue;
            if (candidate.EntityName != data.ChaseTargetEntityName) continue;
            if (hasSavedType && candidate.EntityTypeEnum != savedType) continue;
            if (hasSavedFaction && candidate.Faction != data.ChaseTargetFaction) continue;

            fallback ??= candidate;
            fallbackCount++;

            if (hasSavedPosition && candidate.Position.DistanceTo(savedPos) < 1.0f)
                return candidate;
        }

        return fallbackCount == 1 ? fallback : null;
    }

    /// <summary>
    /// 根据 saveId 获取玩家角色存档路径。
    /// 有 saveId → user://saves/{saveId}/player_save.json（与地图 chunk 数据同目录）
    /// 无 saveId → 向后兼容旧路径 user://sword_and_hex_save.json
    /// </summary>
    public static string GetSavePath(string? saveId)
    {
        if (string.IsNullOrEmpty(saveId)) return FallbackSavePath;
        return $"user://saves/{saveId}/player_save.json";
    }

    public static string GetBackupPath(string? saveId)
    {
        if (string.IsNullOrEmpty(saveId)) return FallbackBackupPath;
        return $"user://saves/{saveId}/player_save_backup.json";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // ========================================
    // 存档检查
    // ========================================

    /// <summary>检查是否存在有效存档（V2 JSON 或 V1 legacy）</summary>
    public bool HasSave(string? saveId = null) => FileAccess.FileExists(GetSavePath(saveId)) || FileAccess.FileExists(LegacyPath);

    /// <summary>检查是否存在 V1 旧存档（需要迁移）</summary>
    public bool HasLegacySave() => !FileAccess.FileExists(FallbackSavePath) && FileAccess.FileExists(LegacyPath);

    // ========================================
    // 保存
    // ========================================

    /// <summary>保存游戏 — 从运行时数据构建存档并写入 JSON</summary>
    public bool SaveGame(GameSaveData saveData, string? saveId = null)
    {
        saveData.Timestamp = (long)Time.GetUnixTimeFromSystem();
        string savePath = GetSavePath(saveId ?? saveData.World.SaveId);
        string backupPath = GetBackupPath(saveId ?? saveData.World.SaveId);

        // 确保 saves/{saveId} 目录存在
        EnsureParentDir(savePath);

        try
        {
            // 备份旧存档
            if (FileAccess.FileExists(savePath))
            {
                var src = FileAccess.Open(savePath, FileAccess.ModeFlags.Read);
                if (src != null)
                {
                    var content = src.GetAsText();
                    src.Close();
                    var backup = FileAccess.Open(backupPath, FileAccess.ModeFlags.Write);
                    if (backup != null) { backup.StoreString(content); backup.Close(); }
                }
            }

            // 写入新存档
            var json = JsonSerializer.Serialize(saveData, JsonOpts);
            var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(json);
                file.Close();
                GD.Print($"[SaveV2] 游戏已保存: {ProjectSettings.GlobalizePath(savePath)}");
                return true;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveV2] 保存失败: {e.Message}");
        }
        return false;
    }

    private static void EnsureParentDir(string path)
    {
        // path 格式如 user://saves/{saveId}/player_save.json
        int lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0) return;
        string dirPath = path[..lastSlash];
        // 逐级创建（DirAccess 以 user:// 为根）
        string relative = dirPath.Replace("user://", "");
        string[] parts = relative.Split('/');
        string current = "";
        var dir = DirAccess.Open("user://");
        if (dir == null) return;
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            current = string.IsNullOrEmpty(current) ? part : $"{current}/{part}";
            if (!dir.DirExists(current)) dir.MakeDir(current);
        }
    }

    // ========================================
    // 读取
    // ========================================

    /// <summary>读取存档 — 优先 V2 JSON，回退到备份；支持 saveId 隔离路径</summary>
    public GameSaveData? LoadGame(string? saveId = null)
    {
        string savePath = GetSavePath(saveId);
        string backupPath = GetBackupPath(saveId);
        if (FileAccess.FileExists(savePath))
        {
            var data = LoadFromPath(savePath);
            if (data != null) return data;
        }
        // 尝试备份
        if (FileAccess.FileExists(backupPath))
        {
            var data = LoadFromPath(backupPath);
            if (data != null) return data;
        }
        // 最终回退：旧版全局路径
        return LoadFromPath(FallbackSavePath);
    }

    /// <summary>读取 V1 旧存档并转换为 V2 格式</summary>
    public GameSaveData? LoadLegacySave()
    {
        if (!FileAccess.FileExists(LegacyPath)) return null;
        try
        {
            var file = FileAccess.Open(LegacyPath, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var raw = (Godot.Collections.Dictionary)file.GetVar();
            file.Close();
            return ConvertLegacyData(raw);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveV2] V1 旧存档读取失败: {e.Message}");
            return null;
        }
    }

    private GameSaveData? LoadFromPath(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return null;
            var json = file.GetAsText();
            file.Close();
            return JsonSerializer.Deserialize<GameSaveData>(json, JsonOpts);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveV2] 读取 {path} 失败: {e.Message}");
            return null;
        }
    }

    // ========================================
    // 删除
    // ========================================

    /// <summary>删除所有存档（V2 + 备份 + V1 legacy）</summary>
    public void DeleteSave()
    {
        DeleteFileIfExists(FallbackSavePath);
        DeleteFileIfExists(FallbackBackupPath);
        DeleteFileIfExists(LegacyPath);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    // ========================================
    // 数据构建 — 从运行时状态构建 GameSaveData
    // ========================================

    /// <summary>从 OverworldScene3D 运行时状态构建完整存档数据</summary>
    public static GameSaveData BuildSaveData(
        UnitData playerUnit,
        int playerRaceId,
        Vector2 playerPos,
        EconomyManager economy,
        OverworldEntityManager? entityMgr = null,
        int worldSeed = 0,
        int worldSize = 1,
        string? saveId = null)
    {
        var save = new GameSaveData();

        // 世界数据
        save.World.PlayerPosX = playerPos.X;
        save.World.PlayerPosY = playerPos.Y;
        save.World.Seed = worldSeed;
        save.World.WorldSize = worldSize;
        save.World.SaveId = saveId;

        // 经济数据
        save.Economy.Gold = economy.Gold;
        save.Economy.Food = economy.Food;
        save.Economy.DaysPassed = economy.DaysPassed;
        save.Economy.Month = economy.Month;
        save.Economy.Year = economy.Year;
        save.Economy.CurrentHour = (int)economy.CurrentHour;

        // 生存系统持久化字段
        save.Economy.ConsecutiveUnpaidDays = economy.WageSys.ConsecutiveUnpaidDays;
        save.Economy.ConsecutiveStarveDays = economy.FoodSys.ConsecutiveStarveDays;
        save.Economy.Tools = economy.Tools;
        save.Economy.Medicine = economy.Medicine;

        // 玩家角色
        save.Party.PlayerRaceId = playerRaceId;
        save.Party.Units.Add(SaveDataConverter.BuildUnitSaveData(playerUnit, isLeader: true));

        // 背包
        foreach (var item in economy.GetAllItems())
        {
            save.Inventory.Add(new InventoryItemSaveData
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                ItemType = item switch
                {
                    WeaponData => "weapon",
                    ArmorData => "armor",
                    ConsumableData => "consumable",
                    _ => "misc"
                },
            });
        }

        // POI 与实体
        if (entityMgr != null)
        {
            foreach (var poi in entityMgr.Pois)
            {
                save.World.Pois.Add(new PoiSaveData
                {
                    PoiName = poi.PoiName,
                    PoiType = poi.PoiTypeEnum.ToString(),
                    PosX = poi.Position.X,
                    PosY = poi.Position.Y,
                    Prosperity = poi.Prosperity,
                    GarrisonSize = poi.GarrisonCurrent,
                });
            }

            foreach (var entity in entityMgr.Entities)
            {
                var saveEntity = new EntitySaveData
                {
                    EntityName = entity.EntityName,
                    EntityType = entity.EntityTypeEnum.ToString(),
                    PosX = entity.Position.X,
                    PosY = entity.Position.Y,
                    Faction = entity.Faction,
                    IsAlive = entity.IsAlive,
                    IsHostileToPlayer = entity.IsHostileToPlayer,
                    
                    // Phase 4: 行为状态
                    AiState = (int)entity.CurrentAIState,
                    AiStrategy = (int)entity.AIStrategy,
                    TargetPosX = entity.TargetPosition.X,
                    TargetPosY = entity.TargetPosition.Y,
                    HomePosX = entity.HomePosition.X,
                    HomePosY = entity.HomePosition.Y,
                    TerritoryCenterX = entity.TerritoryCenter.X,
                    TerritoryCenterY = entity.TerritoryCenter.Y,
                    TerritoryRadius = entity.TerritoryRadius,
                    MoveSpeed = entity.MoveSpeed,
                    PartySize = entity.PartySize,
                    PartyLevel = entity.PartyLevel,
                    CombatPower = entity.CombatPower,
                    DaysAlive = entity.DaysAlive,
                    ArmyId = string.IsNullOrEmpty(entity.ArmyId) ? null : entity.ArmyId,
                    IsMarshal = entity.IsMarshal,
                    AssignedWarTargetPoiName = string.IsNullOrEmpty(entity.AssignedWarTargetPoiName) ? null : entity.AssignedWarTargetPoiName,
                    WarTargetAssignedDay = entity.WarTargetAssignedDay,
                    EngagedSinceHour = entity.EngagedSinceHour,
                    CombatDurationHours = entity.CombatDurationHours,
                    SiegeTargetPoiName = entity.SiegeTarget?.PoiName,
                    ReinforceTargetPoiName = entity.ReinforceTarget?.PoiName,
                    ChaseTargetEntityName = entity.ChaseTarget?.EntityName,
                    ChaseTargetHeroId = string.IsNullOrEmpty(entity.ChaseTarget?.HeroId) ? null : entity.ChaseTarget.HeroId,
                    ChaseTargetEntityType = entity.ChaseTarget?.EntityTypeEnum.ToString(),
                    ChaseTargetFaction = entity.ChaseTarget?.Faction,
                    ChaseTargetPosX = entity.ChaseTarget != null ? entity.ChaseTarget.Position.X : (float?)null,
                    ChaseTargetPosY = entity.ChaseTarget != null ? entity.ChaseTarget.Position.Y : (float?)null,
                    PatrolRadius = entity.PatrolRadius,
                    VisionRange = entity.VisionRange,
                    LordPersonality = (int)entity.LordPersonalityValue,
                    GarrisonSize = entity.GarrisonSize,
                    GuardedPoiName = entity.GuardedPOI?.PoiName,
                    HeroId = string.IsNullOrEmpty(entity.HeroId) ? null : entity.HeroId,
                };
                save.World.Entities.Add(saveEntity);
            }
        }

        return save;
    }

    // ========================================
    // 读档还原 — 将存档数据还原至 EconomyManager
    // ========================================
    
    /// <summary>
    /// 将 EntitySaveData 中的行为状态 100% 还原至 OverworldEntity。
    /// 旧存档（字段缺失）降级为 Idle。
    /// </summary>
    public static void RestoreEntityState(OverworldEntity entity, EntitySaveData data, List<OverworldPOI> pois, List<OverworldEntity> allEntities)
    {
        // 位置
        entity.EntityName = data.EntityName;
        if (Enum.TryParse<OverworldEntity.EntityType>(data.EntityType, out var savedEntityType))
            entity.EntityTypeEnum = savedEntityType;
        entity.Faction = data.Faction;
        entity.IsAlive = data.IsAlive;
        entity.Position = new Vector2(data.PosX, data.PosY);
        
        // AI 状态
        entity.CurrentAIState = (OverworldEntity.AIState)data.AiState;
        if (data.IsHostileToPlayer.HasValue)
            entity.IsHostileToPlayer = data.IsHostileToPlayer.Value;
        entity.AIStrategy = (AIStrategyEnum)data.AiStrategy;
        entity.TargetPosition = new Vector2(data.TargetPosX, data.TargetPosY);
        
        // 领地/基地
        entity.HomePosition = new Vector2(data.HomePosX, data.HomePosY);
        entity.TerritoryCenter = new Vector2(data.TerritoryCenterX, data.TerritoryCenterY);
        entity.TerritoryRadius = data.TerritoryRadius;
        
        // 战斗属性
        entity.MoveSpeed = data.MoveSpeed > 0 ? data.MoveSpeed : entity.MoveSpeed;
        entity.PartySize = data.PartySize > 0 ? data.PartySize : entity.PartySize;
        entity.PartyLevel = data.PartyLevel > 0 ? data.PartyLevel : entity.PartyLevel;
        entity.CombatPower = data.CombatPower > 0 ? data.CombatPower : entity.CombatPower;
        entity.DaysAlive = data.DaysAlive;
        entity.GarrisonSize = data.GarrisonSize > 0 ? data.GarrisonSize : entity.GarrisonSize;
        entity.PatrolRadius = data.PatrolRadius > 0 ? data.PatrolRadius : entity.PatrolRadius;
        entity.VisionRange = data.VisionRange > 0 ? data.VisionRange : entity.VisionRange;
        entity.LordPersonalityValue = (OverworldPOI.LordPersonality)data.LordPersonality;
        
        // 军团
        entity.ArmyId = data.ArmyId ?? "";
        entity.IsMarshal = data.IsMarshal;
        entity.AssignedWarTargetPoiName = data.AssignedWarTargetPoiName ?? "";
        entity.WarTargetAssignedDay = data.WarTargetAssignedDay;
        
        // 交战
        entity.EngagedSinceHour = data.EngagedSinceHour;
        entity.CombatDurationHours = data.CombatDurationHours;
        
        // 英雄
        entity.HeroId = data.HeroId ?? "";
        
        // ── 对象引用恢复（按名称查找） ──
        // SiegeTarget
        if (!string.IsNullOrEmpty(data.SiegeTargetPoiName))
            entity.SiegeTarget = pois.Find(p => p.PoiName == data.SiegeTargetPoiName);
        
        // ReinforceTarget
        if (!string.IsNullOrEmpty(data.ReinforceTargetPoiName))
            entity.ReinforceTarget = pois.Find(p => p.PoiName == data.ReinforceTargetPoiName);
        
        // ChaseTarget
        entity.ChaseTarget = FindChaseTarget(data, allEntities, entity);
        
        // GuardedPOI
        if (!string.IsNullOrEmpty(data.GuardedPoiName))
            entity.GuardedPOI = pois.Find(p => p.PoiName == data.GuardedPoiName);
        
        // 围攻关联恢复
        if (entity.CurrentAIState == OverworldEntity.AIState.Besieging && entity.SiegeTarget != null)
        {
            if (!entity.SiegeTarget.IsUnderSiege)
                entity.SiegeTarget.BeginSiege(entity);
        }
        
        GD.Print($"[SaveV2] 实体 {entity.EntityName} 行为状态已恢复: AIState={entity.CurrentAIState}");
    }

    /// <summary>
    /// 将 EconomySaveData 中的数据 100% 还原至 EconomyManager 实例。
    /// 包括四大资源存量、时间状态和生存系统累计天数。
    /// </summary>
    public static void RestoreEconomy(EconomyManager economy, EconomySaveData data)
    {
        economy.Gold = data.Gold;
        economy.Food = data.Food;
        economy.DaysPassed = data.DaysPassed > 0 ? data.DaysPassed : 1;
        economy.Month = data.Month > 0 ? data.Month : 1;
        economy.Year = data.Year > 0 ? data.Year : 1250;
        economy.CurrentHour = data.CurrentHour;
        economy.Tools = data.Tools;
        economy.Medicine = data.Medicine;

        // 还原生存系统累计状态（通过专用 setter 写入 private 字段）
        economy.WageSys.SetConsecutiveUnpaidDays(data.ConsecutiveUnpaidDays);
        economy.FoodSys.SetConsecutiveStarveDays(data.ConsecutiveStarveDays);

        GD.Print($"[SaveV2] 经济数据已还原: 金币={data.Gold}, 食物={data.Food:F1}, 工具={data.Tools:F1}, 药品={data.Medicine:F1}");
        GD.Print($"[SaveV2]   欠饷天数={data.ConsecutiveUnpaidDays}, 断粮天数={data.ConsecutiveStarveDays}");
    }

    // ========================================
    // V1 旧存档转换
    // ========================================

    private static GameSaveData ConvertLegacyData(Godot.Collections.Dictionary raw)
    {
        var save = new GameSaveData { Version = "2.0.0-migrated" };

        // 经济
        if (raw.ContainsKey("economy"))
        {
            var econ = raw["economy"].AsGodotDictionary();
            save.Economy.Gold = econ.ContainsKey("gold") ? econ["gold"].AsInt32() : 0;
            save.Economy.Food = econ.ContainsKey("food") ? econ["food"].AsInt32() : 0;
            save.Economy.DaysPassed = econ.ContainsKey("days") ? econ["days"].AsInt32() : 0;
            save.Economy.Month = econ.ContainsKey("month") ? econ["month"].AsInt32() : 1;
            save.Economy.Year = econ.ContainsKey("year") ? econ["year"].AsInt32() : 1;
            save.Economy.CurrentHour = econ.ContainsKey("current_hour") ? econ["current_hour"].AsInt32() : 8;
        }

        // 世界位置
        if (raw.ContainsKey("world"))
        {
            var world = raw["world"].AsGodotDictionary();
            save.World.PlayerPosX = world.ContainsKey("player_pos_x") ? world["player_pos_x"].AsSingle() : 0;
            save.World.PlayerPosY = world.ContainsKey("player_pos_y") ? world["player_pos_y"].AsSingle() : 0;
        }

        // 角色
        if (raw.ContainsKey("character"))
        {
            var ch = raw["character"].AsGodotDictionary();
            var unit = new UnitSaveData
            {
                IsLeader = true,
                UnitName = ch.ContainsKey("name") ? ch["name"].AsString() : "Unknown",
                Level = ch.ContainsKey("level") ? ch["level"].AsInt32() : 1,
                CurrentHp = ch.ContainsKey("current_hp") ? ch["current_hp"].AsInt32() : 10,
                Xp = ch.ContainsKey("xp") ? ch["xp"].AsInt32() : 0,
                Str = ch.ContainsKey("str") ? ch["str"].AsInt32() : 10,
                Dex = ch.ContainsKey("dex") ? ch["dex"].AsInt32() : 10,
                Con = ch.ContainsKey("con") ? ch["con"].AsInt32() : 10,
                Intel = ch.ContainsKey("intel") ? ch["intel"].AsInt32() : 10,
                Wis = ch.ContainsKey("wis") ? ch["wis"].AsInt32() : 10,
                Cha = ch.ContainsKey("cha") ? ch["cha"].AsInt32() : 10,
                BaseMaxHp = ch.ContainsKey("base_hp") ? ch["base_hp"].AsInt32() : 10,
                CurrentMana = ch.ContainsKey("current_mana") ? ch["current_mana"].AsInt32() : 0,
            };
            save.Party.PlayerRaceId = ch.ContainsKey("race_id") ? ch["race_id"].AsInt32() : 0;
            save.Party.Units.Add(unit);
        }

        return save;
    }
}
