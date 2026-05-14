// OverworldScene.Encounter.cs
// 大地图遭遇视觉系统 — 把 OverworldEntityManager 的 Entities 同步成 OverworldEnemy 节点
//
// 职责：
// - 监听 EntitySpawned / EntityRemoved 信号，创建/销毁 OverworldEnemy 视觉节点
// - 每帧把实体逻辑位置同步到视觉节点
// - 检测玩家与实体距离，靠近时触发战斗
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene
{
    // ========================================
    // 实体视觉节点字典：OverworldEntity → OverworldEnemy
    // ========================================
    private readonly Dictionary<OverworldEntity, OverworldEnemy> _entityVisuals = new();

    /// <summary>
    /// 初始化遭遇视觉系统（连接 EntityMgr 信号）
    /// 由 _Ready 或 InitEntityManager 调用
    /// </summary>
    private void InitEncounterVisuals()
    {
        if (EntityMgr == null) return;
        EntityMgr.EntitySpawned += OnEntitySpawned;
        EntityMgr.EntityRemoved += OnEntityRemovedVisual;
    }

    /// <summary>
    /// 每帧调用：同步实体位置到视觉节点 + 检查遭遇触发
    /// </summary>
    private void UpdateEncounterVisuals()
    {
        if (EntityMgr == null) return;

        // 1. 同步位置
        foreach (var (entity, visual) in _entityVisuals)
        {
            if (!GodotObject.IsInstanceValid(visual)) continue;
            if (!entity.IsAlive)
            {
                visual.QueueFree();
                continue;
            }
            visual.Position = entity.Position;
        }

        // 2. 清理无效节点
        var toRemove = new List<OverworldEntity>();
        foreach (var (entity, visual) in _entityVisuals)
        {
            if (!GodotObject.IsInstanceValid(visual) || !entity.IsAlive)
                toRemove.Add(entity);
        }
        foreach (var e in toRemove) _entityVisuals.Remove(e);

        // 3. 检查遭遇触发 → 弹出交互面板（迎战/逃跑）
        if (PlayerParty != null && !_encounterTriggering && !IsTimePaused)
        {
            var encountered = EntityMgr.CheckPlayerEntityEncounter();
            if (encountered != null)
            {
                _encounterTriggering = true;
                ShowEncounterInteraction(encountered);
            }
        }
        // 如果遭遇锁定中但实体已不在范围内（被击杀/逃跑），重置
        else if (_encounterTriggering && _pendingEncounterEntity != null)
        {
            if (!_pendingEncounterEntity.IsAlive)
            {
                _encounterTriggering = false;
                _pendingEncounterEntity = null;
            }
        }
    }

    /// <summary>
    /// 弹出遭遇交互面板（类骑砍：显示敌方信息 + 攻击/逃跑选项）
    /// 通过已有的 InteractionManager → InteractionPanel 流程
    /// </summary>
    private void ShowEncounterInteraction(OverworldEntity entity)
    {
        GD.Print($"[OverworldScene] 遭遇接近: {entity.EntityName} ({entity.GetTypeName()})");

        if (PlayerParty != null)
            PlayerParty.IsMoving = false;

        // 创建 OverworldEnemy 视觉节点用于交互系统（如果还没有）
        OverworldEnemy? enemyNode = null;
        if (_entityVisuals.TryGetValue(entity, out var existing))
        {
            enemyNode = existing;
        }
        else
        {
            enemyNode = new OverworldEnemy();
            enemyNode.SetupFromEntity(entity);
            enemyNode.Position = entity.Position;
            AddChild(enemyNode);
            _entityVisuals[entity] = enemyNode;
        }

        // 记录当前遭遇实体（CombatRequested 回调时用）
        _pendingEncounterEntity = entity;

        // 通过 InteractionManager 触发交互面板
        if (InteractionMgr != null)
        {
            InteractionMgr.TriggerInteraction(enemyNode);
        }
        else
        {
            BladeHex.Debug.GameLog.Err("[OverworldScene] InteractionMgr 为 null，无法触发遭遇交互");
            _encounterTriggering = false;
        }
    }

    /// <summary>
    /// 当交互面板关闭（用户选了 Leave 或面板被关闭）时重置遭遇锁
    /// </summary>
    public void OnEncounterInteractionClosed()
    {
        _encounterTriggering = false;
        _pendingEncounterEntity = null;
    }

    /// <summary>是否正在触发遭遇战（防止重复触发）</summary>
    private bool _encounterTriggering = false;

    /// <summary>遭遇视觉创建</summary>
    private void OnEntitySpawned(OverworldEntity entity)
    {
        if (entity == null || _entityVisuals.ContainsKey(entity)) return;

        var visual = new OverworldEnemy();
        visual.SetupFromEntity(entity);
        visual.Position = entity.Position;
        visual.DisplayName = entity.EntityName;
        visual.IsHostile = entity.IsHostileToPlayer;
        visual.EnemyType = (int)entity.EntityTypeEnum;
        visual.ZIndex = 7; // 实体在 POI(5) 之上，玩家(10) 之下

        AddChild(visual);
        _entityVisuals[entity] = visual;

        GD.Print($"[OverworldScene] 实体生成: {entity.EntityName} @ ({entity.Position.X:F0}, {entity.Position.Y:F0})");
    }

    /// <summary>遭遇视觉销毁</summary>
    private void OnEntityRemovedVisual(OverworldEntity entity)
    {
        if (entity == null) return;
        if (_entityVisuals.TryGetValue(entity, out var visual))
        {
            if (GodotObject.IsInstanceValid(visual))
                visual.QueueFree();
            _entityVisuals.Remove(entity);
        }
    }

    /// <summary>触发遭遇战 — 切到战斗场景</summary>
    private void TriggerEncounterCombat(OverworldEntity entity)
    {
        GD.Print($"[OverworldScene] 触发遭遇战: {entity.EntityName} ({entity.GetTypeName()})");

        // 暂停玩家移动
        if (PlayerParty != null)
            PlayerParty.IsMoving = false;

        // 用 EncounterUnitFactory 从实体生成敌方 UnitData 列表
        _pendingEncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(entity);
        _pendingEncounterEntity = entity;

        // 构造 BattleContext
        var ctx = new BattleContext();
        ctx.EncounterCoord = HexOverworldTile.PixelToAxial(entity.Position.X, entity.Position.Y);

        EnterCombatSceneFromCs(ctx);
    }

    /// <summary>待传给战斗场景的敌方单位列表</summary>
    private System.Collections.Generic.List<BladeHex.Data.UnitData>? _pendingEncounterEnemies;

    /// <summary>触发本次遭遇的实体（战斗结束后从世界移除）</summary>
    private OverworldEntity? _pendingEncounterEntity;

    /// <summary>
    /// C# 版本的进入战斗场景方法（取代 GD 的 _enter_combat_scene）
    /// </summary>
    private void EnterCombatSceneFromCs(BattleContext ctx)
    {
        BladeHex.Debug.GameLog.Info("[OverworldScene] EnterCombatSceneFromCs 开始...");

        if (PlayerParty == null)
        {
            BladeHex.Debug.GameLog.Err("[OverworldScene] PlayerParty 为 null，无法进入战斗");
            return;
        }

        try
        {
            // 隐藏整个大地图场景 + 停止天气粒子
            Visible = false;
            ProcessMode = ProcessModeEnum.Disabled;
            if (UI != null) UI.Visible = false;  // CanvasLayer 不会随父节点隐藏，需手动控制
            _weatherParticles2D?.StopAll();
            if (_weatherParticles2D != null) _weatherParticles2D.Visible = false;

            // 传递天气状态到 GlobalState（供战斗场景读取）
            var gs = GetNodeOrNull<BladeHex.Data.GlobalState>("/root/GlobalState");
            if (gs != null && WeatherMgr != null)
            {
                gs.CurrentWeatherType = (int)WeatherMgr.CurrentWeather;
                gs.CurrentWeatherIntensity = WeatherMgr.GetEffectiveIntensity();
            }

            BladeHex.Debug.GameLog.Info("[OverworldScene] 加载 CombatScene 脚本...");
            var combatSceneScript = GD.Load<CSharpScript>("res://BladeHexFrontend/src/Scenes/combat/CombatScene.cs");
            if (combatSceneScript == null)
            {
                BladeHex.Debug.GameLog.Err("[OverworldScene] 无法加载 CombatScene.cs — 文件不存在或编译失败");
                RestoreOverworldAfterError();
                return;
            }

            BladeHex.Debug.GameLog.Info("[OverworldScene] 实例化 CombatScene...");
            var obj = combatSceneScript.New();
            if (obj.Obj == null)
            {
                BladeHex.Debug.GameLog.Err("[OverworldScene] CombatScene.New() 返回 null — 脚本实例化失败");
                RestoreOverworldAfterError();
                return;
            }

            var combatScene = obj.AsGodotObject() as BladeHex.Scenes.CombatScene;
            if (combatScene == null)
            {
                BladeHex.Debug.GameLog.Err("[OverworldScene] CombatScene 类型转换失败");
                RestoreOverworldAfterError();
                return;
            }

            combatScene.BattleContextRef = ctx;
            combatScene.PlayerRoster = PlayerParty.Roster;
            combatScene.EncounterEnemies = _pendingEncounterEnemies;

            combatScene.CombatFinished += (victory) => OnEncounterCombatFinished(victory, combatScene);

            BladeHex.Debug.GameLog.Info("[OverworldScene] 添加 CombatScene 到场景树...");
            GetTree().Root.AddChild(combatScene);
            BladeHex.Debug.GameLog.Info("[OverworldScene] CombatScene 已添加 ✓");
        }
        catch (System.Exception ex)
        {
            BladeHex.Debug.GameLog.Exception("[OverworldScene] EnterCombatSceneFromCs 异常", ex);
            RestoreOverworldAfterError();
        }
    }

    /// <summary>战斗场景加载失败时恢复大地图</summary>
    private void RestoreOverworldAfterError()
    {
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
        if (UI != null) UI.Visible = true;
        _encounterTriggering = false;
        IsTimePaused = false;
        GD.Print("[OverworldScene] 已恢复大地图（战斗加载失败）");
    }

    /// <summary>遭遇战结束回调</summary>
    private void OnEncounterCombatFinished(bool victory, BladeHex.Scenes.CombatScene combatScene)
    {
        GD.Print($"[OverworldScene] 遭遇战结束，胜利={victory}");

        // 应用战斗结果
        var outcome = combatScene.LastBattleOutcome;
        if (outcome != null && PlayerParty != null && PlayerParty.Roster != null)
        {
            // 回写存活/阵亡到 Roster
            PlayerParty.Roster.ApplyBattleResult(outcome.SurvivorHp, outcome.DeadUnitNames);

            if (outcome.DeadUnitNames.Count > 0)
                GD.Print($"[OverworldScene] 阵亡: {string.Join(", ", outcome.DeadUnitNames)}");

            // 队长阵亡 → 游戏结束
            if (!PlayerParty.Roster.IsLeaderAlive)
                GD.Print("[OverworldScene] 队长阵亡！游戏结束");

            // 经验和金币
            if (outcome.GoldGranted > 0 && EconomyMgr != null)
            {
                EconomyMgr.AddGold(outcome.GoldGranted);
                GD.Print($"[OverworldScene] 获得金币: {outcome.GoldGranted}");
            }
            if (outcome.XpGranted > 0)
            {
                var alive = PlayerParty.Roster.GetDeployableMembers();
                if (alive.Count > 0)
                {
                    int xpEach = outcome.XpGranted / alive.Count;
                    foreach (var m in alive) m.Xp += xpEach;
                    GD.Print($"[OverworldScene] 获得经验: {outcome.XpGranted} (每人 {xpEach})");
                }
            }

            // 战利品收入背包
            if (outcome.LootEntries.Count > 0 && PlayerParty?.Inventory != null)
            {
                int added = PlayerParty.Inventory.AddAll(outcome.LootEntries, PlayerParty.Roster.Count);
                GD.Print($"[OverworldScene] 战利品入包: {added}/{outcome.LootEntries.Count} 件");
            }
        }

        // 移除被击败的遭遇实体
        if (victory && _pendingEncounterEntity != null && EntityMgr != null)
        {
            // 声望：击败敌方势力的实体 → 对该势力的对手加声望
            string defeatedFaction = _pendingEncounterEntity.Faction;
            if (!string.IsNullOrEmpty(defeatedFaction) && defeatedFaction != "neutral")
            {
                // 击败敌对势力 → 所有与该势力敌对的国家 +1 声望
                // 简化：对所有非该势力的主要国家 +1
                if (_worldNations != null)
                {
                    foreach (var n in _worldNations)
                    {
                        if (n.Id != defeatedFaction && n.IsMajorNation)
                            _reputationTracker.OnEnemyDefeated(n.Id);
                    }
                }
            }

            _pendingEncounterEntity.IsAlive = false;
            EntityMgr.Entities.Remove(_pendingEncounterEntity);
            EntityMgr.EmitSignal(OverworldEntityManager.SignalName.EntityRemoved, _pendingEncounterEntity);
        }

        // 检查委托完成
        CheckQuestCompletionAfterCombat(victory);

        // 释放战斗场景
        combatScene.QueueFree();

        // 显示战斗结算面板（玩家确认后才恢复大地图）
        ShowBattleResultPanel(victory, outcome);
    }

    /// <summary>战斗结算面板</summary>
    private Node? _battleResultPanel;

    /// <summary>显示战斗结算面板</summary>
    private void ShowBattleResultPanel(bool victory, BattleOutcome? outcome)
    {
        if (_battleResultPanel == null)
        {
            // BattleResultPanel no longer exists — skip display
            GD.PushWarning("BattleResultPanel: no longer exists, battle result display skipped.");
        }

        if (_battleResultPanel != null && outcome != null)
        {
            _battleResultPanel.Call("show_result", victory, outcome);
        }
        else
        {
            // 面板加载失败，直接恢复
            OnBattleResultAcknowledged();
        }
    }

    /// <summary>玩家确认战斗结果后恢复大地图</summary>
    private void OnBattleResultAcknowledged()
    {
        _pendingEncounterEntity = null;
        _pendingEncounterEnemies = null;

        // 恢复大地图场景 + UI + 天气粒子
        Visible = true;
        ProcessMode = ProcessModeEnum.Inherit;
        if (UI != null) UI.Visible = true;
        if (_weatherParticles2D != null)
        {
            _weatherParticles2D.Visible = true;
            // 恢复当前天气
            if (WeatherMgr != null && WeatherMgr.CurrentWeather != BladeHex.View.Environment.WeatherType.Clear)
                _weatherParticles2D.SetWeather(WeatherMgr.CurrentWeather, WeatherMgr.GetEffectiveIntensity());
        }

        UpdateUIInfo();
        _encounterTriggering = false;
    }
}
