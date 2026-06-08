// OverworldScene2D.Entities.cs
// 实体管理 + 遭遇系统 + 战斗入口 — 从 OverworldScene3D.Entities.cs 迁移
// 使用 Sprite2D + Label 替代 MeshInstance3D + Label3D
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Strategic.Hero;
using BladeHex.Strategic.SubParty;
using BladeHex.Data;
using BladeHex.Events;
using BladeHex.Scenes;
using BladeHex.View.Quest;
using BladeHex.View.UI.Overworld;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 实体系统
    // ========================================

    /// <summary>ZoC 管理器 — 敌对 POI 控制区</summary>
    private ZoneOfControlManager? _zocManager;

    /// <summary>招募服务 — 城镇酒馆招募</summary>
    private RecruitService? _recruitService;

    /// <summary>任务管理器</summary>
    private QuestManager? _questManager;

    /// <summary>委托生成器 — 城镇布告栏动态任务</summary>
    private QuestGenerator? _questGenerator;

    /// <summary>大地图委托目标视觉，key = QuestId。</summary>
    private readonly Dictionary<string, QuestTargetVisual> _questTargetVisuals = new();

    /// <summary>封地管理器</summary>
    private FiefManager? _fiefManager;

    private bool _encounterActive = false;
    private readonly Dictionary<OverworldEntity, EntityVisualRef> _entitySpriteMap = new();
    private Vector2 _lastPlayerPosForEntities = new(float.MinValue, float.MinValue);
    private OverworldEntity? _lastEncounteredEntity;

    /// <summary>用于 OverworldHostility.AreHostile 判定的玩家代理实体</summary>
    private static readonly OverworldEntity _hostilityProxy = new()
    {
        EntityName = "PlayerProxy",
        Faction = "player",
        IsHostileToPlayer = false,
        IsAlive = true,
        EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
    };

    // ========================================
    // 追逃机制
    // ========================================

    private const float ENCOUNTER_DIST = 120.0f;  // 触发遭遇的距离（像素）
    private const float QUEST_TARGET_DIST = 95.0f; // 触发委托目标的距离（像素）
    private const float CHASE_WARN_DIST = 520.0f;  // 追击警告距离（略大于遭遇距离，给玩家反应窗口）
    private const float FLEE_CHANCE_MIN = 0.18f;   // 再慢也有少量突围机会
    private const float FLEE_CHANCE_MAX = 0.82f;   // 再快也不能必定甩开
    private const double CHASE_WARNING_COOLDOWN_SEC = 2.5;
    private OverworldEntity? _chasingEntity;       // 正在追击玩家的实体
    private OverworldEntity? _lastWarnedChasingEntity;
    private double _lastChaseWarningTime = -999.0;
    private bool _fleeAttempted = false;           // 本次追击是否已尝试逃跑

    // ========================================
    // 战斗过渡
    // ========================================

    private SceneTree? _cachedTree;

    // ========================================
    // 实体 2D 精灵纹理（1x1 白色，运行时着色 + 缩放）
    // ========================================

    private static Texture2D? _entityTexture;
    private const float ENTITY_SPRITE_SIZE = 24.0f; // 实体精灵直径（像素）
    private const float ENTITY_LABEL_FONT_SIZE = 14.0f;

    // ========================================
    // LOD (Level of Detail) 配置
    // ========================================

    private const float LodFullDetail = 0.5f;   // Zoom >= 此值：显示完整细节（精灵 + 标签）
    private const float LodDotOnly = 0.3f;      // Zoom < 此值：只显示小点，隐藏标签
    private const float LodDotScale = 0.4f;     // Dot-only 模式下的精灵缩放比例

    /// <summary>LOD 级别枚举</summary>
    private enum EntityLOD : byte { FullDetail = 0, DotOnly = 1, Hidden = 2 }

    /// <summary>实体视觉组件引用，用于 LOD 切换时更新</summary>
    private struct EntityVisualRef
    {
        public Node2D Container;
        public Sprite2D Sprite;
        public Label Label;
        public EntityLOD CurrentLOD;

        public EntityVisualRef(Node2D container, Sprite2D sprite, Label label)
        {
            Container = container;
            Sprite = sprite;
            Label = label;
            CurrentLOD = EntityLOD.FullDetail;
        }
    }

    private static Texture2D GetEntityTexture()
    {
        if (_entityTexture == null)
        {
            var img = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            img.Fill(Colors.White);
            _entityTexture = ImageTexture.CreateFromImage(img);
        }
        return _entityTexture;
    }

    /// <summary>初始化实体管理器</summary>
    private void InitEntities()
    {
        EntityMgr = new OverworldEntityManager();
        EntityMgr.Name = "EntityManager";
        EntityMgr.Nations = _worldNations!;
        AddChild(EntityMgr);

        // 订阅 SubParty 战败 7 天后归队
        EntityMgr.SubPartyRejoined += OnSubPartyRejoined;

        // 设置寻路
        EntityMgr.SetHexNavigation(_grid, _astar);
        if (_chunkManager != null)
        {
            var chunkAstar = new ChunkAStar();
            EntityMgr.SetChunkNavigation(_chunkManager, chunkAstar);
        }

        // 加载 POI 到实体管理器
        var poisArray = new Godot.Collections.Array();
        foreach (var poi in WorldPois) poisArray.Add(poi);
        var entitiesArray = new Godot.Collections.Array();
        EntityMgr.LoadWorld(poisArray, entitiesArray);

        // 设置玩家信息
        EntityMgr.UpdatePlayerPosition(_playerPixelPos);
        EntityMgr.PlayerLevel = PlayerUnitData?.Level ?? 1;
        EntityMgr.PlayerRaceId = PlayerRaceId;

        // 写入静态字段供 OverworldEnemy 在 NpcProfile 生成时判断同/异族
        OverworldEnemy.PlayerRaceIdStatic = PlayerRaceId;

        // --- 特殊角色加载到 DormantPool ---
        if (_worldSpecialCharacters.Count > 0)
        {
            foreach (var entity in _worldSpecialCharacters)
                EntityMgr.StoreToDormantPool(entity);
            GD.Print($"[OverworldScene2D] 特殊角色已加载到 DormantPool: {_worldSpecialCharacters.Count} 个");
        }

        // --- ZoneOfControlManager 初始化 ---
        _zocManager = new ZoneOfControlManager();
        _zocManager.Initialize(WorldPois);
        EntityMgr.SetZoneOfControl(_zocManager);
        if (PlayerParty?.SpeedComponent != null)
        {
            PlayerParty.SpeedComponent.ZocManagerRef = _zocManager;
            PlayerParty.SpeedComponent.PlayerFaction = "player";
        }

        // --- ReputationTracker 初始化 ---
        // 注意：ReputationTracker 已在主类中声明，此处确保非 null
        _reputationTracker ??= new ReputationTracker();

        // --- FiefManager 初始化 ---
        _fiefManager = new FiefManager(_reputationTracker);

        // --- RecruitService 初始化 ---
        _recruitService = new RecruitService();
        _recruitService.Initialize(WorldPois, _worldNations, _worldSeed);

        // --- QuestManager 初始化 ---
        _questManager = new QuestManager();
        _questManager.Name = "QuestManager";
        AddChild(_questManager);
        _questManager.QuestTargetSpawned += OnQuestTargetSpawned;
        _questManager.QuestTargetCleared += OnQuestTargetCleared;

        // --- QuestGenerator 初始化 ---
        _questGenerator = new QuestGenerator();
        _questGenerator.Initialize(WorldPois, _worldSeed);

        // --- 每日事件订阅（FiefManager 结算）---
        EventBus.Instance?.Subscribe(EventBus.Signals.DayPassed, OnDayPassedFief);

        GD.Print($"[OverworldScene2D] 实体管理器: POI={WorldPois.Count}, 初始实体={EntityMgr.Entities.Count}");
        GD.Print($"[OverworldScene2D] 子系统: ZoC={_zocManager != null}, Recruit={_recruitService != null}, Quest={_questManager != null}, Fief={_fiefManager != null}");

        // --- 发现日志初始化（新游戏时根据出身国家自动填充）---
        var gs2 = BladeHex.Data.Globals.StateOrNull;
        if (gs2 != null && !gs2.Save.IsLoadingSave)
        {
            string homeFaction = FindPlayerHomeFaction();
            if (!string.IsNullOrEmpty(homeFaction))
            {
                EntityMgr.Journal.InitializeFromOriginFaction(homeFaction, WorldPois);
                GD.Print($"[OverworldScene2D] 发现日志: 出身国家 '{homeFaction}' 领土 POI 已自动加入");
            }
        }

        // --- 读档恢复：还原实体 AI 行为状态 ---
        if (gs2 != null && gs2.Save.IsLoadingSave && !string.IsNullOrEmpty(gs2.Save.CurrentSaveId))
        {
            try
            {
                var saveData = new BladeHex.Data.SaveManager().LoadGame(gs2.Save.CurrentSaveId);
                if (saveData?.World?.Entities != null && saveData.World.Entities.Count > 0)
                {
                    int restoredCount = 0;
                    var restoredEntities = new HashSet<OverworldEntity>();
                    foreach (var saveEntity in saveData.World.Entities)
                    {
                        var existing = FindEntityForSaveRestore(saveEntity, restoredEntities, allowFallback: false);
                        if (existing == null)
                        {
                            var created = CreateEntityFromSaveData(saveEntity);
                            EntityMgr.Entities.Add(created);
                            EntityMgr.Spatial.Add(created);
                        }
                    }

                    foreach (var saveEntity in saveData.World.Entities)
                    {
                        var entity = FindEntityForSaveRestore(saveEntity, restoredEntities, allowFallback: true);
                        if (entity != null)
                        {
                            BladeHex.Data.SaveManager.RestoreEntityState(entity, saveEntity, EntityMgr.Pois, EntityMgr.Entities);
                            restoredEntities.Add(entity);
                            restoredCount++;
                            EntityMgr.Spatial.Rebuild(EntityMgr.Entities);
                        }
                    }
                    GD.Print($"[OverworldScene2D] 读档恢复实体AI状态: {restoredCount}/{EntityMgr.Entities.Count} 个");
                }
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[OverworldScene2D] 读档恢复实体AI状态失败: {ex.Message}");
            }
        }
    }

    /// <summary>每日结算：封地收入 + 声望 + Companion独立带队结算</summary>
    private OverworldEntity? FindEntityForSaveRestore(
        EntitySaveData saveEntity,
        HashSet<OverworldEntity> restoredEntities,
        bool allowFallback)
    {
        OverworldEntity? fallback = null;
        int fallbackCount = 0;
        bool hasSavedType = Enum.TryParse<OverworldEntity.EntityType>(saveEntity.EntityType, out var savedType);
        var savedPos = new Vector2(saveEntity.PosX, saveEntity.PosY);

        foreach (var entity in EntityMgr.Entities)
        {
            if (restoredEntities.Contains(entity)) continue;
            if (entity.EntityName != saveEntity.EntityName) continue;
            if (hasSavedType && entity.EntityTypeEnum != savedType) continue;

            fallback ??= entity;
            fallbackCount++;

            if (!string.IsNullOrEmpty(saveEntity.HeroId) && entity.HeroId == saveEntity.HeroId)
                return entity;

            if (entity.Faction == saveEntity.Faction && entity.Position.DistanceTo(savedPos) < 1.0f)
                return entity;
        }

        return allowFallback && fallbackCount == 1 ? fallback : null;
    }

    private static OverworldEntity CreateEntityFromSaveData(EntitySaveData saveEntity)
    {
        var entity = new OverworldEntity
        {
            EntityName = saveEntity.EntityName,
            Position = new Vector2(saveEntity.PosX, saveEntity.PosY),
            Faction = saveEntity.Faction,
            IsAlive = saveEntity.IsAlive,
            HeroId = saveEntity.HeroId ?? "",
            IsHostileToPlayer = saveEntity.IsHostileToPlayer ?? saveEntity.Faction != "player",
        };

        if (Enum.TryParse<OverworldEntity.EntityType>(saveEntity.EntityType, out var savedType))
            entity.EntityTypeEnum = savedType;

        return entity;
    }

    private void OnDayPassedFief(Godot.Collections.Dictionary _)
    {
        // 1. 正常的封地结算
        if (_fiefManager != null && _fiefManager.FiefCount > 0)
        {
            var report = _fiefManager.ProcessAllFiefs(WorldPois, EconomyMgr, EntityMgr?.WorldEngine);
            if (report.GoldEarned > 0 && EconomyMgr != null)
                EconomyMgr.AddGold(report.GoldEarned);
            GD.Print($"[Fief] 每日结算: +{report.GoldEarned}金 (作坊被动收益: +{report.WorkshopIncome}金), 食物+{report.FoodProduced}/-{report.FoodConsumed}");
        }

        // 2. 独立带队小队 (SubParty) 每日 Tick 战败检测与归队计时
        if (EntityMgr != null && EconomyMgr != null)
        {
            var toRecall = new List<SubParty>();
            foreach (var sp in EntityMgr.SubParties.GetAll())
            {
                var entity = sp.OverworldEntityRef;

                if (entity != null && !entity.IsAlive)
                {
                    if (sp.TaskStartDay == 0)
                    {
                        sp.TaskStartDay = EconomyMgr.DaysPassed;
                        _toast?.Show($"【战报】部下 {sp.LeaderUnitName} 战败，正寻找机会脱身归队...");
                    }

                    if (EconomyMgr.DaysPassed - sp.TaskStartDay >= 7)
                    {
                        foreach (var member in sp.Members)
                        {
                            PlayerParty?.Roster?.Add(member);
                        }
                        toRecall.Add(sp);
                        _toast?.Show($"【归队】Companion {sp.LeaderUnitName} 顺利脱身并已重回队伍！");
                    }
                }
            }

            foreach (var sp in toRecall)
            {
                EntityMgr.SubParties.Remove(sp.SubPartyId);
            }
        }

        // 3. 消费 subparty_victory 大捷新闻事件
        if (EntityMgr != null && EntityMgr.WorldEngine != null && EconomyMgr != null)
        {
            foreach (var news in EntityMgr.WorldEngine.NewsQueue.ToList())
            {
                if (news.Type == "subparty_victory" && news.Day == EconomyMgr.DaysPassed)
                {
                    EconomyMgr.AddGold(300);
                    _toast?.Show(news.Description);

                    foreach (var sp in EntityMgr.SubParties.GetAll())
                    {
                        if (news.Description.Contains(sp.LeaderUnitName))
                        {
                            var leaderUnit = sp.Members.Find(m => m.UnitName == sp.LeaderUnitName);
                            if (leaderUnit != null)
                            {
                                leaderUnit.Xp += 200;
                                GD.Print($"[SubParty] Companion {sp.LeaderUnitName} 剿匪大捷, 获得 200 XP!");
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>每帧更新实体</summary>
    private void UpdateEntities(float dt)
    {
        if (EntityMgr == null) return;

        EntityMgr.UpdatePlayerPosition(_playerPixelPos);

        // 同步玩家等级（影响遭遇敌方等级缩放）与战力
        if (PlayerParty?.Roster?.Leader != null)
        {
            EntityMgr.PlayerLevel = PlayerParty.Roster.Leader.Level;
        }
        if (PlayerParty?.Roster != null)
        {
            EntityMgr.PlayerCombatPower = PlayerParty.Roster.CalculateCombatPower();
        }

        // 时间流逝时 tick 实体移动和生成
        if ((_playerMoving || IsWaiting) && !IsTimePaused)
            EntityMgr.TickMovement(dt);

        // 同步实体 2D 视觉
        SyncEntityVisuals();

        // MVP 战事中途加入轮询 — 优先于普通遭遇检测，避免战场加入提示被吞
        UpdateBattleJoinQuery(dt);

        // 若未触发战场加入，执行委托目标和遭遇检测
        if (!_encounterActive && (_playerMoving || IsWaiting))
        {
            // 委托目标检测优先于普通实体遭遇
            CheckQuestTargets();

            // 遭遇检测
            CheckEncounters();
        }

        // 海上遭遇消费
        if (PlayerParty != null && PlayerParty.SeaEncounterPending)
        {
            PlayerParty.SeaEncounterPending = false;
            ProcessSeaEncounter();
        }
    }

    /// <summary>处理海上遭遇</summary>
    private void ProcessSeaEncounter()
    {
        float roll = (float)GD.Randf();
        if (roll > SeaEncounterTable.BaseEncounterChance) return;

        var rng = new System.Random((int)GD.Randi());
        int playerLevel = PlayerUnitData?.Level ?? 1;
        var encounter = SeaEncounterTable.GenerateEncounter(rng, playerLevel);

        GD.Print($"[SeaEncounter] {encounter.Type}: {encounter.Description}");
        _toast?.Show(encounter.Description);

        switch (encounter.Type)
        {
            case SeaEncounterType.Storm:
                if (PlayerParty?.CurrentShip != null)
                {
                    PlayerParty.CurrentShip.TakeDamage(encounter.DurabilityDamage);
                    GD.Print($"[SeaEncounter] 船只受损: -{encounter.DurabilityDamage} 耐久");
                }
                break;

            case SeaEncounterType.Flotsam:
                if (EconomyMgr != null && encounter.GoldReward > 0)
                    EconomyMgr.AddGold(encounter.GoldReward);
                break;

            case SeaEncounterType.PirateAttack:
            case SeaEncounterType.SeaMonster:
                var seaEnemy = new OverworldEntity();
                seaEnemy.EntityName = encounter.Type == SeaEncounterType.PirateAttack ? "海盗" : "海怪";
                seaEnemy.EntityTypeEnum = encounter.Type == SeaEncounterType.PirateAttack
                    ? OverworldEntity.EntityType.PirateCrew
                    : OverworldEntity.EntityType.EpicMonster;
                seaEnemy.Position = _playerPixelPos;
                seaEnemy.IsHostileToPlayer = true;
                TriggerCombatWithEntity(seaEnemy);
                break;

            case SeaEncounterType.MerchantShip:
                _toast?.Show("商船出现！可以交易或继续航行。");
                break;
        }
    }

    private void OnQuestTargetSpawned(QuestTargetSite site)
    {
        if (site == null || string.IsNullOrEmpty(site.QuestId)) return;

        OnQuestTargetCleared(site.QuestId);

        var visual = new QuestTargetVisual { Name = $"QuestTarget_{site.QuestId}" };
        visual.Setup(site);
        visual.ZIndex = 95;
        AddChild(visual);
        _questTargetVisuals[site.QuestId] = visual;

        GD.Print($"[Quest] 目标点已生成: {site.SiteName} ({site.QuestId}) at {site.WorldPosition}");
    }

    private void OnQuestTargetCleared(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;

        if (_questTargetVisuals.TryGetValue(questId, out var visual))
        {
            if (GodotObject.IsInstanceValid(visual))
            {
                visual.MarkCleared();
                visual.QueueFree();
            }
            _questTargetVisuals.Remove(questId);
        }
    }

    private void CheckQuestTargets()
    {
        if (_questManager == null || _questManager.ActiveTargetSites.Count == 0) return;

        foreach (var site in _questManager.ActiveTargetSites.Values.ToList())
        {
            if (site == null || site.IsCleared) continue;
            if (_playerPixelPos.DistanceTo(site.WorldPosition) > QUEST_TARGET_DIST) continue;

            ResolveQuestTarget(site);
            break;
        }
    }

    private void ResolveQuestTarget(QuestTargetSite site)
    {
        var quest = _questManager?.GetActiveQuest(site.QuestId);
        if (quest == null) return;

        StopPlayerMovementForEncounter();
        site.IsVisibleToPlayer = true;

        switch (quest.questType)
        {
            case QuestData.QuestType.Extermination:
            case QuestData.QuestType.Bounty:
            case QuestData.QuestType.Defense:
            case QuestData.QuestType.Emergency:
                TriggerCombatWithQuestTarget(site, quest);
                break;
            default:
                CompleteQuestTargetWithoutCombat(site, quest);
                break;
        }
    }

    private void StopPlayerMovementForEncounter()
    {
        PlayerParty?.StopNavAgent();
        _currentPath = null;
        _pathIndex = 0;
        _playerMoving = false;
        _cameraFollowing = false;
        ClearPathPreview();
        IsTimePaused = true;
        _encounterActive = true;
        IsWaiting = false;
    }

    private void CompleteQuestTargetWithoutCombat(QuestTargetSite site, QuestData quest)
    {
        site.IsCleared = true;
        _questManager?.UpdateQuestProgress(quest.QuestId, Math.Max(1, quest.TargetCount));
        _toast?.Show($"委托目标已完成：{quest.QuestName}。返回 {quest.IssuerName} 领取奖励。");
        IsTimePaused = false;
        _encounterActive = false;
    }

    private void TriggerCombatWithQuestTarget(QuestTargetSite site, QuestData quest)
    {
        GD.Print($"[Quest] 进入委托目标战斗: {quest.QuestName} ({quest.QuestId})");

        var coord = HexOverworldTile.PixelToAxial(site.WorldPosition.X, site.WorldPosition.Y);
        HexOverworldTile? tile = _mapAccess.GetActiveTile(coord.X, coord.Y);
        var terrain = tile?.Terrain ?? HexOverworldTile.TerrainType.Plains;

        var ctx = BattleContext.Create(
            terrain,
            ResolveQuestBattleSize(quest),
            BattleContext.EngagementType.Normal,
            (int)GD.Randi());
        ctx.QuestId = quest.QuestId;
        ctx.OverworldGrid = _grid;
        ctx.EncounterCoord = coord;
        ctx.EncounterPosition = new Vector2I((int)site.WorldPosition.X, (int)site.WorldPosition.Y);

        _lastEncounteredEntity = null;
        EnterCombatScene(ctx);
    }

    private static BattleContext.BattleSize ResolveQuestBattleSize(QuestData quest) => quest.difficulty switch
    {
        QuestData.QuestDifficulty.Easy => BattleContext.BattleSize.Mercenary,
        QuestData.QuestDifficulty.Medium => BattleContext.BattleSize.Knight,
        QuestData.QuestDifficulty.Hard => BattleContext.BattleSize.Lord,
        QuestData.QuestDifficulty.Boss => BattleContext.BattleSize.Stronghold,
        _ => BattleContext.BattleSize.Mercenary,
    };

    private QuestData? CompleteQuestBattleTarget(string questId)
    {
        if (_questManager == null) return null;

        var quest = _questManager.GetActiveQuest(questId);
        if (quest == null) return null;

        if (_questManager.GetActiveTargetSite(questId) is QuestTargetSite site)
            site.IsCleared = true;

        _questManager.UpdateQuestProgress(questId, Math.Max(1, quest.TargetCount));
        return quest;
    }

    /// <summary>同步实体 2D 视觉位置（增量渲染 + 消抖优化）</summary>
    private void SyncEntityVisuals()
    {
        if (EntityMgr == null) return;

        // 1. 玩家移动距离小于 50px 时跳过大范围可见性同步，仅推进在场实体的微移动
        float pDist = _playerPixelPos.DistanceTo(_lastPlayerPosForEntities);
        if (pDist < 50f)
        {
            float quickZoom = _camera?.Zoom.X ?? 1.0f;
            // 使用临时列表避免 foreach 期间修改字典
            var keys = new List<OverworldEntity>(_entitySpriteMap.Keys);
            foreach (var key in keys)
            {
                if (!GodotObject.IsInstanceValid(key) || !key.IsAlive) continue;
                var vis = _entitySpriteMap[key];
                vis.Container.Position = key.Position;
                ApplyEntityLOD(ref vis, key, quickZoom);
                _entitySpriteMap[key] = vis;
            }
            return;
        }
        _lastPlayerPosForEntities = _playerPixelPos;

        // 2. 气象影响后的可视视野
        float visionRange = 3000.0f * WeatherVisionFactor;
        var visible = EntityMgr.GetVisibleEntities(_playerPixelPos, visionRange);
        var visibleSet = new HashSet<OverworldEntity>(visible);

        // 3. 移除不再可见的实体视觉
        var toRemove = new List<OverworldEntity>();
        foreach (var kvp in _entitySpriteMap)
        {
            if (!GodotObject.IsInstanceValid(kvp.Key) || !kvp.Key.IsAlive || !visibleSet.Contains(kvp.Key))
            {
                kvp.Value.Container.QueueFree();
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var e in toRemove) _entitySpriteMap.Remove(e);

        // 4. 新显示 / 更新坐标 + LOD
        float zoom = _camera?.Zoom.X ?? 1.0f;
        foreach (var entity in visible)
        {
            if (!entity.IsAlive) continue;

            if (_entitySpriteMap.TryGetValue(entity, out var visualRef))
            {
                visualRef.Container.Position = entity.Position;
                ApplyEntityLOD(ref visualRef, entity, zoom);
            }
            else
            {
                visualRef = CreateEntityVisual(entity);
                visualRef.Container.Position = entity.Position;
                AddChild(visualRef.Container);
                ApplyEntityLOD(ref visualRef, entity, zoom);
                _entitySpriteMap[entity] = visualRef;
            }
        }
    }

    /// <summary>为实体创建 2D 视觉节点（Sprite2D 着色 + Label 文字）</summary>
    private EntityVisualRef CreateEntityVisual(OverworldEntity entity)
    {
        var container = new Node2D();
        container.Name = $"Entity_{entity.EntityName}";
        container.ZIndex = 90;

        // --- 精灵（圆形近似：缩放后的白色方块） ---
        var sprite = new Sprite2D();
        sprite.Name = "Sprite";
        sprite.Texture = GetEntityTexture();
        sprite.Centered = true;
        sprite.Scale = new Vector2(ENTITY_SPRITE_SIZE / 4f, ENTITY_SPRITE_SIZE / 4f);

        // 染色：玩家=蓝, 敌对=红, 友方=绿
        if (entity.Faction == "player")
            sprite.Modulate = new Color(0.2f, 0.4f, 0.8f);
        else
            sprite.Modulate = OverworldHostility.AreHostile(entity, _hostilityProxy) ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.3f, 0.7f, 0.3f);

        container.AddChild(sprite);

        // --- 标签 ---
        var label = new Label();
        label.Name = "Label";
        label.HorizontalAlignment = HorizontalAlignment.Center;

        // 图标 + 文字
        if (entity.Faction == "player")
        {
            label.Text = $"🛡 {entity.EntityName}";
            label.Modulate = new Color(0.6f, 0.8f, 1.0f);
        }
        else if (entity.IsMarshal && !string.IsNullOrEmpty(entity.ArmyId))
        {
            label.Text = $"⚔ {entity.EntityName}";
            float hue = (Math.Abs(entity.Faction.GetHashCode()) % 360) / 360.0f;
            label.Modulate = Color.FromHsv(hue, 0.9f, 1.0f);
        }
        else
        {
            label.Text = entity.EntityName;
            label.Modulate = OverworldHostility.AreHostile(entity, _hostilityProxy) ? new Color(1.0f, 0.8f, 0.8f) : new Color(0.8f, 1.0f, 0.8f);
        }

        // 字体大小
        var fontSettings = new LabelSettings();
        fontSettings.FontSize = entity.IsMarshal && !string.IsNullOrEmpty(entity.ArmyId)
            ? (int)(ENTITY_LABEL_FONT_SIZE * 1.15f)
            : (int)ENTITY_LABEL_FONT_SIZE;
        label.LabelSettings = fontSettings;

        // 标签位于精灵上方
        label.Position = new Vector2(-40, -ENTITY_SPRITE_SIZE * 0.5f - 18);
        label.Size = new Vector2(80, 20);

        container.AddChild(label);

        return new EntityVisualRef(container, sprite, label);
    }

    // ========================================
    // LOD (Level of Detail) 系统
    // ========================================

    /// <summary>判断实体是否为"essential"（在极远缩放时仍然可见）</summary>
    private static bool IsEntityEssential(OverworldEntity entity)
    {
        // 玩家阵营始终可见
        if (entity.Faction == "player") return true;
        // 元帅（军团领袖）始终可见
        if (entity.IsMarshal && !string.IsNullOrEmpty(entity.ArmyId)) return true;
        // 命名角色始终可见
        if (entity.IsNamedCharacter) return true;
        return false;
    }

    /// <summary>根据当前缩放级别应用 LOD 到实体视觉</summary>
    private static void ApplyEntityLOD(ref EntityVisualRef visual, OverworldEntity entity, float zoom)
    {
        EntityLOD targetLOD;

        if (zoom < LodDotOnly)
        {
            // 极远缩放：隐藏非essential实体
            targetLOD = IsEntityEssential(entity) ? EntityLOD.DotOnly : EntityLOD.Hidden;
        }
        else if (zoom < LodFullDetail)
        {
            // 中等缩放：只显示小点
            targetLOD = EntityLOD.DotOnly;
        }
        else
        {
            // 近缩放：完整细节
            targetLOD = EntityLOD.FullDetail;
        }

        // LOD 未变化时跳过更新
        if (visual.CurrentLOD == targetLOD) return;
        visual.CurrentLOD = targetLOD;

        switch (targetLOD)
        {
            case EntityLOD.FullDetail:
                visual.Container.Visible = true;
                visual.Sprite.Scale = new Vector2(ENTITY_SPRITE_SIZE / 4f, ENTITY_SPRITE_SIZE / 4f);
                visual.Label.Visible = true;
                break;

            case EntityLOD.DotOnly:
                visual.Container.Visible = true;
                visual.Sprite.Scale = new Vector2(
                    ENTITY_SPRITE_SIZE * LodDotScale / 4f,
                    ENTITY_SPRITE_SIZE * LodDotScale / 4f);
                visual.Label.Visible = false;
                break;

            case EntityLOD.Hidden:
                visual.Container.Visible = false;
                break;
        }
    }

    // ========================================
    // 遭遇检测 + 追逃机制
    // ========================================

    private void CheckEncounters()
    {
        if (EntityMgr == null) return;

        var encountered = EntityMgr.CheckPlayerEncounters(_playerPixelPos);
        if (encountered != null)
        {
            // --- 动态激活百科条目与遭遇日志录入 ---
            if (EntityMgr.Journal != null)
            {
                // 1. 如果是领主，自动录入具名英雄图鉴
                if (encountered.EntityTypeEnum == OverworldEntity.EntityType.LordArmy && !string.IsNullOrEmpty(encountered.HeroId))
                {
                    if (EntityMgr.Journal.EncounterLord(encountered.HeroId))
                    {
                        _toast?.Show($"👑 百科已更新: 结识了领主【{encountered.EntityName}】！");
                        BladeHex.UI.Tutorial.TutorialManager.Instance?.Trigger("encounter_lord");
                    }
                }
                // 2. 如果是中立冒险者，记录日志
                else if (encountered.EntityTypeEnum == OverworldEntity.EntityType.Adventurer)
                {
                    if (EntityMgr.Journal.EncounterAdventurer(encountered.EntityName))
                    {
                        _toast?.Show($"🧭 百科已更新: 遇见了冒险者队伍【{encountered.EntityName}】！");
                    }
                }
                // 3. 其它生物或野怪遭遇图鉴录入
                else
                {
                    var enemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(encountered);
                    if (enemies != null && enemies.Count > 0)
                    {
                        foreach (var enemy in enemies)
                        {
                            var templateId = enemy.EnemyTemplateId;
                            if (string.IsNullOrEmpty(templateId)) continue;

                            if (templateId.StartsWith("legend_"))
                            {
                                if (EntityMgr.Journal.EncounterLegendary(templateId))
                                {
                                    _toast?.Show($"🐉 百科已更新: 遭遇了传说生物【{enemy.UnitName}】！");
                                }
                            }
                            else
                            {
                                if (EntityMgr.Journal.EncounterCreature(templateId))
                                {
                                    _toast?.Show($"👾 百科已更新: 遭遇了【{enemy.UnitName}】！");
                                }
                            }
                        }
                    }
                }
            }

            // 友方/中立实体：直接打开交互
            if (!OverworldHostility.AreHostile(encountered, _hostilityProxy))
            {
                // Companion 实体 (Faction == "player") → 直接解散归队
                if (encountered.Faction == "player")
                {
                    var sp = EntityMgr.SubParties.GetAll().Find(s => s.LeaderUnitName == encountered.EntityName);
                    if (sp != null)
                    {
                        foreach (var member in sp.Members)
                        {
                            PlayerParty?.Roster?.Add(member);
                        }
                        EntityMgr.SubParties.Remove(sp.SubPartyId);
                        encountered.IsAlive = false;
                        _toast?.Show($"【归队】部下 {encountered.EntityName} 已顺利带领队伍重返兵团！");

                        if (_entitySpriteMap.TryGetValue(encountered, out var spriteNode))
                        {
                            spriteNode.Container.QueueFree();
                            _entitySpriteMap.Remove(encountered);
                        }
                        return;
                    }
                }

                _encounterActive = true;
                _playerMoving = false;
                _lastEncounteredEntity = encountered;
                _chasingEntity = null;
                _fleeAttempted = false;
                IsTimePaused = true;

                CleanupCurrentEnemyNode();
                var friendlyNode = new OverworldEnemy();
                friendlyNode.SetupFromEntity(encountered);
                friendlyNode.Visible = false;
                AddChild(friendlyNode);
                _currentEnemyNode = friendlyNode;

                _interactionMgr?.TriggerInteraction(friendlyNode);
                return;
            }

            // 天气影响遭遇概率（仅敌对实体）
            if (WeatherEncounterFactor < 1.0f)
            {
                float roll = (float)GD.Randf();
                if (roll > WeatherEncounterFactor)
                {
                    GD.Print($"[Weather] 天气掩护，避免了与 {encountered.EntityName} 的遭遇");
                    return;
                }
            }

            // 追逃判定：使用双方有效速度，并限制概率区间，形成更稳定的骑砍式追逃手感。
            float playerSpeed = GetPlayerEffectiveSpeed();
            float enemySpeed = GetEntityEffectiveSpeed(encountered);
            float fleeChance = CalculateFleeChance(playerSpeed, enemySpeed);

            if (_playerMoving && !_fleeAttempted)
            {
                _fleeAttempted = true;
                float fleeRoll = (float)GD.Randf();
                if (fleeRoll < fleeChance)
                {
                    _toast?.Show($"🏇 你甩开了 {encountered.EntityName}！（突围率 {fleeChance:P0}）");
                    MarkEntityLostPlayer(encountered);
                    _chasingEntity = null;
                    _fleeAttempted = false;
                    return;
                }
                else
                {
                    _toast?.Show($"⚔ {encountered.EntityName} 追上了你！（突围率 {fleeChance:P0}）");
                }
            }

            _encounterActive = true;
            _playerMoving = false;
            _lastEncounteredEntity = encountered;
            _chasingEntity = null;
            _fleeAttempted = false;
            IsTimePaused = true;

            CleanupCurrentEnemyNode();
            var enemyNode = new OverworldEnemy();
            enemyNode.SetupFromEntity(encountered);
            enemyNode.Visible = false;
            AddChild(enemyNode);
            _currentEnemyNode = enemyNode;

            _interactionMgr?.TriggerInteraction(enemyNode);
        }
        else
        {
            // 检查是否有敌人在追击范围内（警告）：选择最近追兵并节流提示，避免刷屏。
            _chasingEntity = FindNearestChasingEntity();
            if (_chasingEntity != null)
                MaybeShowChaseWarning(_chasingEntity);
            else
                _fleeAttempted = false;
        }
    }

    private float GetPlayerEffectiveSpeed()
    {
        return PlayerParty?.SpeedComponent?.CalculateSpeed(_playerPixelPos) ?? PlayerMoveSpeed;
    }

    private float GetEntityEffectiveSpeed(OverworldEntity entity)
    {
        if (EntityMgr?.SimCtx != null)
        {
            return EntitySpeedCalculator.CalculateSpeed(
                entity,
                entity.Position,
                EntityMgr.SimCtx.TerrainQuery,
                EntityMgr.SimCtx.ZocManager);
        }
        return entity.MoveSpeed;
    }

    private static float CalculateFleeChance(float playerSpeed, float enemySpeed)
    {
        float safePlayerSpeed = Math.Max(1.0f, playerSpeed);
        float safeEnemySpeed = Math.Max(1.0f, enemySpeed);
        float raw = safePlayerSpeed / (safePlayerSpeed + safeEnemySpeed);
        return Mathf.Clamp(raw, FLEE_CHANCE_MIN, FLEE_CHANCE_MAX);
    }

    private void MarkEntityLostPlayer(OverworldEntity entity)
    {
        entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
        entity.ChaseTarget = null;
        entity.CurrentTacticalTarget = null;
        entity.LastIntentSummary = "跟丢了玩家队伍";
        entity.IsMoving = false;
        entity.Path.Clear();
    }

    private OverworldEntity? FindNearestChasingEntity()
    {
        if (EntityMgr == null) return null;

        OverworldEntity? nearest = null;
        float nearestDist = CHASE_WARN_DIST;
        foreach (var entity in EntityMgr.Entities)
        {
            if (!entity.IsAlive || !OverworldHostility.AreHostile(entity, _hostilityProxy)) continue;
            if (entity.CurrentAIState != OverworldEntity.AIState.Chasing) continue;

            float dist = _playerPixelPos.DistanceTo(entity.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = entity;
            }
        }
        return nearest;
    }

    private void MaybeShowChaseWarning(OverworldEntity entity)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (_lastWarnedChasingEntity == entity && now - _lastChaseWarningTime < CHASE_WARNING_COOLDOWN_SEC)
            return;

        _lastWarnedChasingEntity = entity;
        _lastChaseWarningTime = now;
        float enemySpeed = GetEntityEffectiveSpeed(entity);
        float playerSpeed = GetPlayerEffectiveSpeed();
        string speedHint = enemySpeed > playerSpeed ? "对方速度更快，继续逃跑风险很高" : "你速度占优，有机会甩开";
        _toast?.Show($"👁 {entity.EntityName} 正在追击你。{speedHint}");
    }

    // ========================================
    // 战斗入口
    // ========================================

    private void TriggerCombatWithEntity(OverworldEntity entity)
    {
        GD.Print($"[OverworldScene2D] 与 {entity.EntityName} 进入战斗");

        var ctx = BattleContextFactory.CreatePlayerVsEntity(
            defender: entity,
            grid: _grid,
            playerPixelPosition: _playerPixelPos,
            seed: (int)GD.Randi());
        ApplyBattleTerrainFromMapAccess(ctx, entity.Position);

        EnterCombatScene(ctx);
    }

    private void ApplyBattleTerrainFromMapAccess(BattleContext ctx, Vector2 pixelPosition)
    {
        var axial = HexOverworldTile.PixelToAxial(pixelPosition.X, pixelPosition.Y);
        var tile = _mapAccess.GetActiveTile(axial.X, axial.Y)
            ?? _mapAccess.GetKnownTileFromCache(axial.X, axial.Y);

        if (tile != null)
        {
            ctx.Terrain = tile.Terrain;
            ctx.EncounterCoord = tile.Coord;
        }
    }

    private void EnterCombatScene(BattleContext ctx)
    {
        try
        {
            // 缓存 SceneTree 引用（移出后 GetTree() 返回 null）
            _cachedTree = GetTree();

            // 播放过渡动画（2D 版使用 CombatEntranceTransition，不需要 Camera3D）
            var transition = new BladeHex.View.Transitions.CombatEntranceTransition();
            GetTree().Root.AddChild(transition);

            transition.Play(
                _overworldUi?.BottomPanel,
                _overworldUi?.TopPanel,
                _minimap?.Panel,
                () => ExecuteCombatSwitch(ctx));
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[OverworldScene2D] 进入战斗异常: {ex.Message}");
            _encounterActive = false;
        }
    }

    private void ExecuteCombatSwitch(BattleContext ctx)
    {
        try
        {
            var combatSceneScript = GD.Load<CSharpScript>("res://BladeHexFrontend/src/Scenes/combat/CombatScene.cs");
            if (combatSceneScript == null)
            {
                GD.PrintErr("[OverworldScene2D] 无法加载 CombatScene.cs");
                _encounterActive = false;
                return;
            }

            var obj = combatSceneScript.New();
            var combatScene = obj.AsGodotObject() as CombatScene;
            if (combatScene == null)
            {
                GD.PrintErr("[OverworldScene2D] CombatScene 实例化失败");
                _encounterActive = false;
                return;
            }

            combatScene.BattleContextRef = ctx;
            combatScene.PlayerRoster = PlayerParty.Roster;

            if (!string.IsNullOrEmpty(ctx.QuestId) && _questManager?.GetActiveTargetSite(ctx.QuestId) is QuestTargetSite site)
                combatScene.EncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromQuestTarget(site, PlayerParty?.Roster?.Leader?.Level ?? 1);
            // 普通大地图实体战斗不再由 OverworldScene2D 反查 _lastEncounteredEntity 生成敌军；
            // CombatScene 会从 BattleContextRef.DefenderDeployment 统一生成。

            combatScene.CombatFinished += (bool victory) => OnCombatFinished(victory, combatScene);

            // 隔离大地图：从场景树移出
            _cachedTree!.Root.RemoveChild(this);
            _cachedTree.Root.AddChild(combatScene);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[OverworldScene2D] ExecuteCombatSwitch 异常: {ex.Message}");
            _encounterActive = false;
        }
    }

    private void OnCombatFinished(bool victory, CombatScene combatScene)
    {
        GD.Print($"[OverworldScene2D] 战斗结束: victory={victory}");

        var ctx = combatScene.BattleContextRef;

        // 如果胜利，且打败的是传说生物（包括大地图 EpicMonster 以及海上遭遇的 EpicMonster “海怪”）
        if (victory && combatScene.EncounterEnemies != null && EntityMgr?.Journal != null)
        {
            foreach (var enemy in combatScene.EncounterEnemies)
            {
                var templateId = enemy.EnemyTemplateId;
                if (string.IsNullOrEmpty(templateId)) continue;

                if (templateId.StartsWith("legend_"))
                {
                    if (EntityMgr.Journal.DefeatLegendary(templateId))
                    {
                        _toast?.Show($"🏆 传奇战绩! 百科已完全解锁传说生物【{enemy.UnitName}】的绝密情报!");
                    }
                }
            }
        }

        // 1. 中途加入战事的回写逻辑
        if (ctx != null && ctx.WarJoinOppRef != null)
        {
            var opp = ctx.WarJoinOppRef;
            var playerNation = new PlayerNationResolver().GetCurrent(_reputationTracker!, EconomyMgr.DaysPassed);

            if (opp.Type == WarBattleType.Siege)
            {
                var poi = opp.DefenderPoi!;
                if (ctx.PlayerJoinedAsAttacker)
                {
                    if (victory)
                    {
                        PoiTransferService.Apply(poi, opp.Attacker.Faction, opp.Attacker, EconomyMgr.DaysPassed, EntityMgr?.WorldEngine, playerNearby: true);
                        if (!string.IsNullOrEmpty(playerNation))
                            EntityMgr?.WorldEngine.Influence.Add(playerNation, 30, "玩家助攻战役成功攻陷POI");
                    }
                    else
                    {
                        opp.Attacker.CurrentAIState = OverworldEntity.AIState.Fleeing;
                        opp.Attacker.SiegeTarget = null;
                        poi.EndSiege();
                    }
                }
                else
                {
                    if (victory)
                    {
                        opp.Attacker.CurrentAIState = OverworldEntity.AIState.Fleeing;
                        opp.Attacker.SiegeTarget = null;
                        poi.EndSiege();
                        if (!string.IsNullOrEmpty(playerNation))
                            EntityMgr?.WorldEngine.Influence.Add(playerNation, 30, "玩家助守战役击退攻城敌军");
                    }
                    else
                    {
                        PoiTransferService.Apply(poi, opp.Attacker.Faction, opp.Attacker, EconomyMgr.DaysPassed, EntityMgr?.WorldEngine, playerNearby: true);
                        _playerPixelPos += new Vector2(150, 150);
                    }
                }
            }
            else // 野战
            {
                var enemy = ctx.PlayerJoinedAsAttacker ? opp.DefenderEntity! : opp.Attacker;
                var ally = ctx.PlayerJoinedAsAttacker ? opp.Attacker : opp.DefenderEntity!;

                if (victory)
                {
                    enemy.IsAlive = false;
                    EntityMgr?.RemoveEntity(enemy);
                    if (_entitySpriteMap.TryGetValue(enemy, out var sprite))
                    {
                        sprite.Container.QueueFree();
                        _entitySpriteMap.Remove(enemy);
                    }
                    if (!string.IsNullOrEmpty(playerNation))
                        EntityMgr?.WorldEngine.Influence.Add(playerNation, 15, "玩家参与野外会战大获全胜");
                }
                else
                {
                    ally.CurrentAIState = OverworldEntity.AIState.Fleeing;
                    _playerPixelPos += new Vector2(100, 100);
                }
            }
        }

        // 2. 默认遭遇战处理。优先使用 BattleContext.DefenderEntity，_lastEncounteredEntity 仅作旧入口 fallback。
        var resolvedEncounterEntity = ctx?.DefenderEntity ?? _lastEncounteredEntity;
        if (victory && resolvedEncounterEntity != null && (ctx == null || ctx.WarJoinOppRef == null))
        {
            if (resolvedEncounterEntity.EntityTypeEnum == OverworldEntity.EntityType.LordArmy &&
                resolvedEncounterEntity.IsNamedCharacter && !string.IsNullOrEmpty(resolvedEncounterEntity.HeroId) &&
                EntityMgr != null)
            {
                var playerEntity = new OverworldEntity { EntityName = "玩家", HeroId = "player", Faction = "player" };
                double roll = new System.Random().NextDouble();
                if (roll < 0.8)
                {
                    CapturedSystem.Capture(
                        resolvedEncounterEntity,
                        playerEntity,
                        EconomyMgr != null ? EconomyMgr.DaysPassed : 1,
                        EntityMgr.Heroes,
                        EntityMgr.Prisoners,
                        EntityMgr.Relations,
                        EntityMgr.Pois);
                    _toast?.Show($"成功俘虏了 {resolvedEncounterEntity.EntityName}！");
                }
                else
                {
                    resolvedEncounterEntity.IsAlive = false;
                    EntityMgr.Heroes.MarkDead(resolvedEncounterEntity.HeroId, EconomyMgr != null ? EconomyMgr.DaysPassed : 1);
                    _toast?.Show($"{resolvedEncounterEntity.EntityName} 战死了！");
                }
            }
            else
            {
                resolvedEncounterEntity.IsAlive = false;
            }

            EntityMgr?.RemoveEntity(resolvedEncounterEntity);
            if (_entitySpriteMap.TryGetValue(resolvedEncounterEntity, out var sprite2))
            {
                sprite2.Container.QueueFree();
                _entitySpriteMap.Remove(resolvedEncounterEntity);
            }
        }

        // 缓存战利品
        BattleOutcome? outcome = combatScene.LastBattleOutcome;
        QuestData? completedQuest = null;

        if (victory && ctx != null && !string.IsNullOrEmpty(ctx.QuestId))
            completedQuest = CompleteQuestBattleTarget(ctx.QuestId);

        // 恢复大地图到场景树
        combatScene.QueueFree();
        if (_cachedTree != null)
        {
            _cachedTree.Root.AddChild(this);
            _cachedTree = null;
        }
        Visible = true;
        _encounterActive = false;
        _lastEncounteredEntity = null;
        IsTimePaused = false;

        if (completedQuest != null)
            _toast?.Show($"委托完成：{completedQuest.QuestName}。返回 {completedQuest.IssuerName} 领取奖励。");

        // 大地图已恢复，打开战利品 UI
        if (victory && outcome != null && _overworldUi != null)
        {
            if (outcome.GoldGranted > 0)
                EconomyMgr?.AddGold(outcome.GoldGranted);

            if (outcome.LootItems.Count > 0)
                _overworldUi.OpenPartyLoot(outcome.LootItems, outcome.GoldGranted, outcome.XpGranted);
        }

        // 竞技场奖金处理
        if (_pendingArenaPrize > 0)
        {
            if (victory)
            {
                EconomyMgr?.AddGold(_pendingArenaPrize);
                GD.Print($"[Arena] 竞技场胜利! 获得奖金 {_pendingArenaPrize} 金币");
            }
            else
            {
                GD.Print("[Arena] 竞技场失败，报名费已损失");
            }
            _pendingArenaPrize = 0;
        }

        // 锦标赛战斗结果处理
        if (_pendingTournamentState != null)
        {
            bool isTournament = _pendingTournamentState.ContainsKey("is_tournament") && _pendingTournamentState["is_tournament"].AsBool();
            if (isTournament)
            {
                int round = _pendingTournamentState.ContainsKey("round") ? _pendingTournamentState["round"].AsInt32() : 0;
                if (victory)
                    GD.Print($"[Tournament] 锦标赛第{round + 1}轮胜利!");
                else
                    GD.Print("[Tournament] 锦标赛淘汰!");
            }
            _pendingTournamentState = null;
        }

        // 3. 玩家战败被俘弹窗
        if (!victory && resolvedEncounterEntity != null &&
            resolvedEncounterEntity.EntityTypeEnum == OverworldEntity.EntityType.LordArmy &&
            resolvedEncounterEntity.IsNamedCharacter && !string.IsNullOrEmpty(resolvedEncounterEntity.HeroId))
        {
            var dialog = new PlayerCapturedDialog();
            int currentGold = EconomyMgr != null ? EconomyMgr.Gold : 0;
            var captorName = resolvedEncounterEntity.EntityName;
            var captorHeroId = resolvedEncounterEntity.HeroId;

            dialog.Setup(
                captorName,
                currentGold,
                onPayRansom: () =>
                {
                    EconomyMgr?.AddGold(-5000);
                    EntityMgr?.Relations.Adjust("player", captorHeroId, 5);
                    EntityMgr?.WorldEngine.AddNews(
                        "hero_released",
                        $"💰 你支付 5000 金币赎金,从 {captorName} 处赎回了自己。",
                        _playerPixelPos);
                    _toast?.Show("已支付 5000 金币自释！");
                },
                onWaitEscape: () =>
                {
                    if (EconomyMgr != null)
                    {
                        for (int i = 0; i < 7; i++)
                            EconomyMgr.AdvanceDay();
                    }
                    var spawnPoi = FindNearestFriendlyPoi();
                    if (spawnPoi != null)
                    {
                        _playerPixelPos = spawnPoi.Position;
                        if (PlayerParty != null) PlayerParty.Position = spawnPoi.Position;
                    }
                    EntityMgr?.Relations.Adjust("player", captorHeroId, -5);
                    EntityMgr?.WorldEngine.AddNews(
                        "hero_released",
                        $"🕊 你在 {captorName} 的监狱中关押 7 日,趁夜成功逃脱!",
                        _playerPixelPos);
                    _toast?.Show("在监狱被关押了 7 天，终于寻得机会成功逃离！");
                }
            );
            AddChild(dialog);
        }
    }

    private OverworldPOI? FindNearestFriendlyPoi()
    {
        OverworldPOI? bestPoi = null;
        float bestDist = float.MaxValue;
        foreach (var poi in WorldPois)
        {
            if (poi.OwningFaction != "hostile" &&
                (poi.PoiTypeEnum == OverworldPOI.POIType.Castle || poi.PoiTypeEnum == OverworldPOI.POIType.Town))
            {
                float d = _playerPixelPos.DistanceTo(poi.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoi = poi;
                }
            }
        }
        return bestPoi;
    }

    // ========================================
    // SubParty 归队
    // ========================================

    /// <summary>
    /// SubParty 战败 7 天后归队回调:把 Companion 与残部塞回 PlayerParty.Roster。
    /// HP 设为 30% 表示带伤归队。
    /// </summary>
    private void OnSubPartyRejoined(SubParty sp)
    {
        if (PlayerParty == null || sp == null) return;
        var roster = PlayerParty.Roster;
        if (roster == null) return;

        int rejoinedCount = 0;
        int lostCount = 0;
        foreach (var member in sp.Members)
        {
            if (member == null) continue;
            int hp = System.Math.Max(1, (int)(member.BaseMaxHp * 0.3f));
            PartyRoster.SetCurrentHp(member, hp);

            if (roster.Add(member)) rejoinedCount++;
            else lostCount++;
        }

        var msg = lostCount > 0
            ? $"{sp.LeaderUnitName} 带 {rejoinedCount} 人归队,有 {lostCount} 人因队伍满员遣散。"
            : $"{sp.LeaderUnitName} 与 {System.Math.Max(0, rejoinedCount - 1)} 名残部成功归队!";
        _toast?.Show(msg);
        GD.Print($"[OverworldScene2D] SubParty 归队: {msg}");
    }

    // ========================================
    // 战事中途加入轮询
    // ========================================

    private float _battleJoinQueryTimer = 0.0f;
    private const float BATTLE_JOIN_QUERY_INTERVAL = 0.2f;
    private float _battleJoinPromptSilenceTimer = 0.0f;
    private JoinBattlePrompt? _battleJoinPrompt;

    /// <summary>更新战事中途加入轮询</summary>
    public void UpdateBattleJoinQuery(float dt)
    {
        // 玩家跟随军团处理
        if (PlayerParty != null && !string.IsNullOrEmpty(PlayerParty.ArmyId) && EntityMgr != null)
        {
            var army = EntityMgr.Armies.Get(PlayerParty.ArmyId);
            if (army != null && army.Marshal != null && army.Marshal.IsAlive && army.State != BladeHex.Strategic.Army.ArmyState.Disbanding)
            {
                var marshal = army.Marshal;
                float dist = _playerPixelPos.DistanceTo(marshal.Position);

                if (dist > 800.0f)
                {
                    GD.Print($"[WarBattle] 玩家远离元帅超过 800px ({dist:F0}px)，自动退出军团！");
                    PlayerParty.ArmyId = "";
                }
                else
                {
                    PlayerParty.Position = marshal.Position + new Vector2(50, -50);
                    _playerPixelPos = PlayerParty.Position;
                    PlayerParty.IsMoving = false;
                    PlayerParty.Path.Clear();

                    if (army.State == BladeHex.Strategic.Army.ArmyState.Besieging)
                    {
                        var targetPoi = EntityMgr.Pois.FirstOrDefault(p => p.PoiName == army.TargetPoiName);
                        if (targetPoi != null && targetPoi.IsUnderSiege && !_encounterActive && _battleJoinPromptSilenceTimer <= 0.0f)
                        {
                            var siegeOpp = new JoinOpportunity
                            {
                                Type = WarBattleType.Siege,
                                Attacker = marshal,
                                DefenderPoi = targetPoi,
                                Distance = dist
                            };

                            if (_battleJoinPrompt == null)
                            {
                                _battleJoinPrompt = new JoinBattlePrompt();
                                _battleJoinPrompt.JoinSelected += OnJoinBattleSelected;
                                _battleJoinPrompt.LeaveSelected += OnLeaveBattleSelected;
                                AddChild(_battleJoinPrompt);
                            }

                            if (!_battleJoinPrompt.Visible)
                                _battleJoinPrompt.ShowPrompt(siegeOpp);
                        }
                    }
                    return;
                }
            }
            else
            {
                GD.Print("[WarBattle] 军团已解散或元帅已战死，玩家自动退出军团！");
                PlayerParty.ArmyId = "";
            }
        }

        if (_battleJoinPromptSilenceTimer > 0.0f)
            _battleJoinPromptSilenceTimer -= dt;

        if (_encounterActive || IsTimePaused || _battleJoinPromptSilenceTimer > 0.0f)
        {
            if (_battleJoinPrompt != null && _battleJoinPrompt.Visible)
                _battleJoinPrompt.HidePrompt();
            return;
        }

        _battleJoinQueryTimer += dt;
        if (_battleJoinQueryTimer >= BATTLE_JOIN_QUERY_INTERVAL)
        {
            _battleJoinQueryTimer = 0.0f;
            if (EntityMgr != null)
            {
                var opp = WarBattleJoinService.Query(_playerPixelPos, EntityMgr.Entities, EntityMgr.Pois, "kingdom", EntityMgr.Armies, 250.0f, EntityMgr.WorldEngine);
                if (opp != null)
                {
                    if (opp.Type == WarBattleType.ArmyJoin && PlayerParty != null && !string.IsNullOrEmpty(PlayerParty.ArmyId))
                    {
                        if (_battleJoinPrompt != null && _battleJoinPrompt.Visible)
                            _battleJoinPrompt.HidePrompt();
                    }
                    else
                    {
                        if (_battleJoinPrompt == null)
                        {
                            _battleJoinPrompt = new JoinBattlePrompt();
                            _battleJoinPrompt.JoinSelected += OnJoinBattleSelected;
                            _battleJoinPrompt.LeaveSelected += OnLeaveBattleSelected;
                            AddChild(_battleJoinPrompt);
                        }
                        _battleJoinPrompt.ShowPrompt(opp);
                    }
                }
                else
                {
                    if (_battleJoinPrompt != null && _battleJoinPrompt.Visible)
                        _battleJoinPrompt.HidePrompt();
                }
            }
        }
    }

    private void OnLeaveBattleSelected()
    {
        _battleJoinPromptSilenceTimer = 5.0f;
        GD.Print("[WarBattle] 玩家选择暂不介入，战事加入 Prompt 沉默 5 秒");
    }

    private void OnJoinBattleSelected(JoinOpportunity opp, bool joinAttacker)
    {
        GD.Print($"[WarBattle] 玩家选择加入战事! 协助 {(joinAttacker ? "进攻方" : "防守方")}");

        IsTimePaused = true;
        _playerMoving = false;
        _encounterActive = true;

        var coord = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
        var ctx = BattleContext.CreateFromEncounter(
            attacker: opp.Attacker,
            defender: opp.DefenderEntity,
            poi: opp.DefenderPoi,
            grid: _grid,
            coord: coord
        );

        ctx.WarJoinOppRef = opp;
        ctx.PlayerJoinedAsAttacker = joinAttacker;
        ctx.Seed = (int)GD.Randi();
        var terrainPos = opp.DefenderEntity?.Position
            ?? opp.DefenderPoi?.Position
            ?? _playerPixelPos;
        ApplyBattleTerrainFromMapAccess(ctx, terrainPos);

        EnterCombatScene(ctx);
    }
}
