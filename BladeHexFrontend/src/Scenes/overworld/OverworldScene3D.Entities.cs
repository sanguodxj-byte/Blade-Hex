// OverworldScene3D.Entities.cs
// 实体管理 + 遭遇系统 + 战斗入口
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Events;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
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

    /// <summary>声望追踪器</summary>
    private ReputationTracker? _reputationTracker;

    /// <summary>封地管理器</summary>
    private FiefManager? _fiefManager;

    private bool _encounterActive = false;
    private readonly Dictionary<OverworldEntity, Node3D> _entityMeshMap = new();
    private OverworldEntity? _lastEncounteredEntity;

    /// <summary>初始化实体管理器</summary>
    private void InitEntities()
    {
        EntityMgr = new OverworldEntityManager();
        EntityMgr.Name = "EntityManager";
        AddChild(EntityMgr);

        // 设置寻路
        EntityMgr.SetHexNavigation(_grid, _astar);
        if (_chunkManager != null)
        {
            var chunkAstar = new ChunkAStar();
            EntityMgr.SetChunkNavigation(_chunkManager, chunkAstar);
        }

        // 加载 POI 到实体管理器（POI 数据驱动实体生成）
        var poisArray = new Godot.Collections.Array();
        foreach (var poi in WorldPois) poisArray.Add(poi);
        var entitiesArray = new Godot.Collections.Array();
        EntityMgr.LoadWorld(poisArray, entitiesArray);

        // 设置玩家信息
        EntityMgr.UpdatePlayerPosition(_playerPixelPos);
        EntityMgr.PlayerLevel = PlayerUnitData?.Level ?? 1;

        // --- 特殊角色加载到 DormantPool ---
        if (_worldSpecialCharacters.Count > 0)
        {
            foreach (var entity in _worldSpecialCharacters)
                EntityMgr.StoreToDormantPool(entity);
            GD.Print($"[OverworldScene3D] 特殊角色已加载到 DormantPool: {_worldSpecialCharacters.Count} 个");
        }

        // --- ZoneOfControlManager 初始化 ---
        _zocManager = new ZoneOfControlManager();
        _zocManager.Initialize(WorldPois);
        // 注入到 MovementSpeedComponent
        if (PlayerParty?.SpeedComponent != null)
        {
            PlayerParty.SpeedComponent.ZocManagerRef = _zocManager;
            // "player" 不匹配任何 POI 的 OwningFaction → 所有非 neutral POI 的 ZoC 对玩家生效
            // 玩家加入势力后应更新为对应 nationId 以免受友方 ZoC 惩罚
            PlayerParty.SpeedComponent.PlayerFaction = "player";
        }

        // --- ReputationTracker 初始化 ---
        _reputationTracker = new ReputationTracker();

        // --- FiefManager 初始化 ---
        _fiefManager = new FiefManager(_reputationTracker);

        // --- RecruitService 初始化 ---
        _recruitService = new RecruitService();
        _recruitService.Initialize(WorldPois, _worldNations, _worldSeed);

        // --- QuestManager 初始化 ---
        _questManager = new QuestManager();
        _questManager.Name = "QuestManager";
        AddChild(_questManager);

        // --- 每日事件订阅（FiefManager 结算）---
        EventBus.Instance?.Subscribe(EventBus.Signals.DayPassed, OnDayPassedFief);

        GD.Print($"[OverworldScene3D] 实体管理器: POI={WorldPois.Count}, 初始实体={EntityMgr.Entities.Count}");
        GD.Print($"[OverworldScene3D] 子系统: ZoC={_zocManager != null}, Recruit={_recruitService != null}, Quest={_questManager != null}, Fief={_fiefManager != null}");
    }

    /// <summary>每日结算：封地收入 + 声望</summary>
    private void OnDayPassedFief(Godot.Collections.Dictionary _)
    {
        if (_fiefManager == null || _fiefManager.FiefCount == 0) return;

        var report = _fiefManager.ProcessAllFiefs();
        if (report.GoldEarned > 0 && EconomyMgr != null)
            EconomyMgr.AddGold(report.GoldEarned);

        GD.Print($"[Fief] 每日结算: +{report.GoldEarned}金, 食物+{report.FoodProduced}/-{report.FoodConsumed}");
    }

    /// <summary>每帧更新实体</summary>
    private void UpdateEntities(float dt)
    {
        if (EntityMgr == null) return;

        EntityMgr.UpdatePlayerPosition(_playerPixelPos);

        // 同步玩家等级（影响遭遇敌方等级缩放）
        if (PlayerParty?.Roster?.Leader != null)
            EntityMgr.PlayerLevel = PlayerParty.Roster.Leader.Level;

        // 时间流逝时 tick 实体移动和生成
        if (_playerMoving && !IsTimePaused)
            EntityMgr.TickMovement(dt);

        // 同步实体 3D 视觉
        SyncEntityVisuals();

        // 遭遇检测
        if (!_encounterActive && _playerMoving)
            CheckEncounters();

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
        // 基础概率检定
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
                // 船只耐久损失
                if (PlayerParty?.CurrentShip != null)
                {
                    PlayerParty.CurrentShip.TakeDamage(encounter.DurabilityDamage);
                    GD.Print($"[SeaEncounter] 船只受损: -{encounter.DurabilityDamage} 耐久");
                }
                break;

            case SeaEncounterType.Flotsam:
                // 获得金币和物品
                if (EconomyMgr != null && encounter.GoldReward > 0)
                    EconomyMgr.AddGold(encounter.GoldReward);
                break;

            case SeaEncounterType.PirateAttack:
            case SeaEncounterType.SeaMonster:
                // 触发战斗 — 生成临时敌方实体
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
                // 商船遭遇 — 显示交互选项（简化为 toast 提示）
                _toast?.Show("商船出现！可以交易或继续航行。");
                break;
        }
    }

    /// <summary>同步实体 3D 视觉位置（O(n) 优化）</summary>
    private void SyncEntityVisuals()
    {
        // 天气影响视野范围
        float visionRange = 3000.0f * WeatherVisionFactor;
        var visible = EntityMgr.GetVisibleEntities(_playerPixelPos, visionRange);
        var visibleSet = new HashSet<OverworldEntity>(visible);

        // 移除不再可见/已死亡的
        var toRemove = new List<OverworldEntity>();
        foreach (var kvp in _entityMeshMap)
        {
            if (!GodotObject.IsInstanceValid(kvp.Key) || !kvp.Key.IsAlive || !visibleSet.Contains(kvp.Key))
            {
                kvp.Value.QueueFree();
                _lightSystem?.RemoveEntityLight(kvp.Key);
                toRemove.Add(kvp.Key);
            }
            else
            {
                kvp.Value.Position = CoordConverter.PixelToWorld3D(kvp.Key.Position) + new Vector3(0, 0.35f, 0);
                _lightSystem?.UpdateEntityLightPosition(kvp.Key);
            }
        }
        foreach (var e in toRemove) _entityMeshMap.Remove(e);

        // 为新实体创建视觉
        foreach (var entity in visible)
        {
            if (!entity.IsAlive || _entityMeshMap.ContainsKey(entity)) continue;

            // 容器节点（mesh + label 一起移动）
            var container = new Node3D();
            container.Name = $"Entity_{entity.EntityName}";

            var mesh = new MeshInstance3D();
            mesh.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = entity.IsHostileToPlayer ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.3f, 0.7f, 0.3f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mesh.MaterialOverride = mat;
            container.AddChild(mesh);

            // 名称标签 — 悬浮在球体头上
            var label = new Label3D();
            label.Text = entity.EntityName;
            label.FontSize = 72;
            label.PixelSize = 0.01f;
            label.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
            label.Position = new Vector3(0, 0.6f, 0);
            label.Modulate = entity.IsHostileToPlayer ? new Color(1.0f, 0.8f, 0.8f) : new Color(0.8f, 1.0f, 0.8f);
            label.OutlineModulate = new Color(0.0f, 0.0f, 0.0f);
            label.OutlineSize = 14;
            label.NoDepthTest = true;
            label.RenderPriority = 100;
            container.AddChild(label);

            container.Position = CoordConverter.PixelToWorld3D(entity.Position) + new Vector3(0, 0.35f, 0);
            AddChild(container);
            _entityMeshMap[entity] = container;

            // 添加实体光源
            _lightSystem?.AddEntityLight(entity);
        }
    }

    // ========================================
    // 遭遇检测 + 追逃机制
    // ========================================

    private const float ENCOUNTER_DIST = 120.0f;  // 触发遭遇的距离（像素）
    private const float CHASE_WARN_DIST = 400.0f; // 追击警告距离
    private OverworldEntity? _chasingEntity;       // 正在追击玩家的实体
    private bool _fleeAttempted = false;           // 本次追击是否已尝试逃跑

    private void CheckEncounters()
    {
        var encountered = EntityMgr.CheckPlayerEncounters(_playerPixelPos);
        if (encountered != null)
        {
            // 天气影响遭遇概率
            if (WeatherEncounterFactor < 1.0f)
            {
                float roll = (float)GD.Randf();
                if (roll > WeatherEncounterFactor)
                {
                    GD.Print($"[Weather] 天气掩护，避免了与 {encountered.EntityName} 的遭遇");
                    return;
                }
            }

            // 追逃判定：比较玩家速度和敌人速度
            float playerSpeed = PlayerParty?.SpeedComponent?.CalculateSpeed(_playerPixelPos) ?? PlayerMoveSpeed;
            float enemySpeed = encountered.MoveSpeed;

            // 逃跑成功率 = 玩家速度 / (玩家速度 + 敌人速度)
            // 速度快的一方更容易追上/逃脱
            float fleeChance = playerSpeed / (playerSpeed + enemySpeed);

            // 如果玩家在移动中（不是静止被追上），有机会逃跑
            if (_playerMoving && !_fleeAttempted)
            {
                _fleeAttempted = true;
                float fleeRoll = (float)GD.Randf();
                if (fleeRoll < fleeChance)
                {
                    // 逃跑成功！敌人暂时放弃追击
                    _toast?.Show($"成功甩开了 {encountered.EntityName}！");
                    encountered.CurrentAIState = OverworldEntity.AIState.Returning;
                    _chasingEntity = null;
                    _fleeAttempted = false;
                    return;
                }
                else
                {
                    _toast?.Show($"被 {encountered.EntityName} 追上了！");
                }
            }

            _encounterActive = true;
            _playerMoving = false;
            _lastEncounteredEntity = encountered;
            _chasingEntity = null;
            _fleeAttempted = false;
            IsTimePaused = true;

            var enemyNode = new OverworldEnemy();
            enemyNode.SetupFromEntity(encountered);
            enemyNode.Visible = false;
            AddChild(enemyNode);

            _interactionMgr?.TriggerInteraction(enemyNode);
        }
        else
        {
            // 检查是否有敌人在追击范围内（警告）
            _chasingEntity = null;
            if (EntityMgr != null)
            {
                foreach (var entity in EntityMgr.Entities)
                {
                    if (!entity.IsAlive || !entity.IsHostileToPlayer) continue;
                    if (entity.CurrentAIState != OverworldEntity.AIState.Chasing) continue;
                    float dist = _playerPixelPos.DistanceTo(entity.Position);
                    if (dist < CHASE_WARN_DIST)
                    {
                        _chasingEntity = entity;
                        break;
                    }
                }
            }

            // 离开追击范围后重置逃跑标记
            if (_chasingEntity == null)
                _fleeAttempted = false;
        }
    }

    // ========================================
    // 战斗入口
    // ========================================

    private void TriggerCombatWithEntity(OverworldEntity entity)
    {
        GD.Print($"[OverworldScene3D] 与 {entity.EntityName} 进入战斗");

        // 获取战斗坐标
        var coord = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);

        // 创建战斗上下文
        var ctx = BattleContext.CreateFromEncounter(
            attacker: null, // 玩家方
            defender: entity,
            poi: null,
            grid: _grid,
            coord: coord
        );
        ctx.Seed = (int)GD.Randi();

        // 进入战斗
        EnterCombatScene(ctx);
    }

    /// <summary>缓存 SceneTree 引用（用于从场景树移出后仍能操作）</summary>
    private SceneTree? _cachedTree;

    private void EnterCombatScene(BattleContext ctx)
    {
        try
        {
            // 将当前天气写入 GlobalState 供战斗场景读取
            WriteWeatherToGlobalState();

            // 缓存 SceneTree 引用（移出后 GetTree() 返回 null）
            _cachedTree = GetTree();

            // 播放过渡动画
            var transition = new BladeHex.View.Transitions.CombatTransition();
            AddChild(transition);

            transition.Play(
                _camera,
                _overworldUi?.TopPanel,
                _overworldUi?.BottomPanel,
                _minimap,
                () => ExecuteCombatSwitch(ctx));
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[OverworldScene3D] 进入战斗异常: {ex.Message}");
            _encounterActive = false;
        }
    }

    private void ExecuteCombatSwitch(BattleContext ctx)
    {
        try
        {
            // 释放相机控制权
            if (_camera != null)
                _camera.Current = false;

            // 实例化战斗场景
            var combatSceneScript = GD.Load<CSharpScript>("res://BladeHexFrontend/src/Scenes/combat/CombatScene.cs");
            if (combatSceneScript == null)
            {
                GD.PrintErr("[OverworldScene3D] 无法加载 CombatScene.cs");
                _encounterActive = false;
                return;
            }

            var obj = combatSceneScript.New();
            var combatScene = obj.AsGodotObject() as BladeHex.Scenes.CombatScene;
            if (combatScene == null)
            {
                GD.PrintErr("[OverworldScene3D] CombatScene 实例化失败");
                _encounterActive = false;
                return;
            }

            combatScene.BattleContextRef = ctx;
            combatScene.PlayerRoster = PlayerParty.Roster;

            // 从遭遇实体生成敌方单位数据
            if (_lastEncounteredEntity != null)
                combatScene.EncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(_lastEncounteredEntity);

            combatScene.CombatFinished += (victory) => OnCombatFinished(victory, combatScene);

            // 隔离大地图：从场景树移出
            _cachedTree!.Root.RemoveChild(this);
            _cachedTree.Root.AddChild(combatScene);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[OverworldScene3D] ExecuteCombatSwitch 异常: {ex.Message}");
            _encounterActive = false;
        }
    }

    private void OnCombatFinished(bool victory, BladeHex.Scenes.CombatScene combatScene)
    {
        GD.Print($"[OverworldScene3D] 战斗结束: victory={victory}");

        // 处理战斗结果
        if (victory && _lastEncounteredEntity != null)
        {
            _lastEncounteredEntity.IsAlive = false;
            EntityMgr?.RemoveEntity(_lastEncounteredEntity);
            if (_entityMeshMap.TryGetValue(_lastEncounteredEntity, out var mesh))
            {
                mesh.QueueFree();
                _entityMeshMap.Remove(_lastEncounteredEntity);
            }
        }

        // 缓存战利品（恢复到树中之后再打开 UI）
        BladeHex.Strategic.BattleOutcome? outcome = combatScene.LastBattleOutcome;

        // 先恢复大地图到场景树
        combatScene.QueueFree();
        if (_cachedTree != null)
        {
            _cachedTree.Root.AddChild(this);
            _cachedTree = null;
        }
        Visible = true;
        _encounterActive = false;
        _lastEncounteredEntity = null;

        // 恢复相机
        if (_camera != null)
            _camera.Current = true;

        // 大地图已恢复，现在安全打开战利品 UI
        if (victory && outcome != null && _overworldUi != null)
        {
            if (outcome.GoldGranted > 0)
                EconomyMgr?.AddGold(outcome.GoldGranted);

            if (outcome.LootItems.Count > 0)
                _overworldUi.OpenPartyLoot(outcome.LootItems, outcome.GoldGranted, outcome.XpGranted);
        }
    }
}
