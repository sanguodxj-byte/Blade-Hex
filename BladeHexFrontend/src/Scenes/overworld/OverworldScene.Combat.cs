// OverworldScene.Combat.cs
// [T-601] OverworldScene — 世界事件 + 任务目标点 partial class
// Combat entry (EnterCombatScene, OnCombatFinished, TriggerCombatWithEntity,
// HandleQuestTargetReached, etc.) lives in OverworldScene.Process.cs.
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld;

// World event callbacks and quest target lifecycle
public partial class OverworldScene : Node2D
{
    // ========================================
    // 世界事件回调
    // ========================================

    /// <summary>
    /// 村庄被袭击事件
    /// 对应 _on_village_attacked (line 1192)
    /// </summary>
    private void OnVillageAttacked(OverworldPOI village, OverworldEntity attacker)
    {
        GD.Print($"[世界事件] {village.PoiName} 被 {attacker.EntityName} 袭击！繁荣度降至 {village.Prosperity}");
    }

    /// <summary>
    /// 围攻开始事件
    /// 对应 _on_siege_started (line 1195)
    /// </summary>
    private void OnSiegeStarted(OverworldPOI target, OverworldEntity attacker)
    {
        GD.Print($"[围攻开始] {attacker.EntityName} 开始围攻 {target.PoiName}");
    }

    /// <summary>
    /// 围攻结果事件
    /// 对应 _on_siege_resolved (line 1198)
    /// </summary>
    private void OnSiegeResolved(OverworldPOI target, bool attackerWon, OverworldEntity attacker)
    {
        if (attackerWon)
            GD.Print($"[围攻结果] {attacker.EntityName} 攻占了 {target.PoiName}！");
        else
            GD.Print($"[围攻结果] {target.PoiName} 守住了 {attacker.EntityName} 的围攻");
        UpdateUIInfo();
    }

    /// <summary>
    /// 回援到达事件
    /// 对应 _on_reinforcement_arrived (line 1205)
    /// </summary>
    private void OnReinforcementArrived(OverworldPOI targetPoi, OverworldEntity reinforcer)
    {
        GD.Print($"[回援] {reinforcer.EntityName} 赶到 {targetPoi.PoiName} 支援");
    }

    /// <summary>
    /// AI战斗事件
    /// 对应 _on_ai_battle (line 1208)
    /// </summary>
    private void OnAiBattle(OverworldEntity attacker, OverworldEntity defender, bool attackerWon)
    {
        var winner = attackerWon ? attacker.EntityName : defender.EntityName;
        GD.Print($"[AI战斗] {attacker.EntityName} vs {defender.EntityName} → {winner} 获胜");
    }

    /// <summary>
    /// POI被占领事件
    /// 对应 _on_poi_captured (line 1212)
    /// </summary>
    private void OnPoiCaptured(OverworldPOI poi, string newFaction, OverworldEntity captor)
    {
        GD.Print($"[领土变更] {poi.PoiName} 被 {captor.EntityName} 攻占，归属: {newFaction}");

        // 更新 ZoC — 旧阵营的控制区消失，新阵营的控制区生效
        if (_zocManager != null)
        {
            string oldFaction = poi.OwningFaction;
            _zocManager.OnPoiFactionChanged(poi, oldFaction);

            // 更新寻路代价网格
            if (_costGrid != null)
            {
                // 清除旧 ZoC 代价
                var oldTiles = _zocManager.GetZocTiles(poi.PoiName);
                if (oldTiles.Count > 0)
                    _costGrid.ClearZocRegion(oldTiles);

                // 确定玩家阵营
                string playerFaction = PlayerParty?.SpeedComponent?.PlayerFaction ?? "";

                // 如果新阵营对玩家敌对，写入新 ZoC 代价
                if (!string.IsNullOrEmpty(newFaction) && newFaction != "neutral" && newFaction != playerFaction)
                {
                    var newTiles = _zocManager.GetZocTiles(poi.PoiName);
                    if (newTiles.Count > 0)
                        _costGrid.UpdateZocRegion(newTiles, ZoneOfControlManager.ZocPathfindingMultiplier);
                }
            }

            // 路径缓存失效
            PlayerParty?.ChunkAStar?.InvalidateCache();
        }

        // 更新 POI 视觉颜色以反映新势力归属
        UpdatePoiVisualColor(poi, newFaction);
    }

    /// <summary>
    /// 实体被移除事件
    /// 对应 _on_entity_removed (line 1216)
    /// </summary>
    private void OnEntityRemoved(OverworldEntity entity)
    {
        // 实体被移除，可能需要更新UI
    }

    /// <summary>更新 POI 视觉颜色（势力变更时调用）</summary>
    private void UpdatePoiVisualColor(OverworldPOI poi, string newFaction)
    {
        int idx = WorldPois.IndexOf(poi);
        if (idx < 0 || idx >= PoiVisuals.Count) return;

        var visual = PoiVisuals[idx];
        if (!IsInstanceValid(visual)) return;

        // 根据新势力确定颜色
        Color factionColor = newFaction switch
        {
            "player" or "human" => new Color(0.2f, 0.5f, 0.9f),   // 蓝色 — 玩家/人类
            "hostile" => new Color(0.85f, 0.2f, 0.2f),             // 红色 — 敌对
            "bandit" => new Color(0.7f, 0.4f, 0.2f),              // 橙色 — 山贼
            "monster" => new Color(0.6f, 0.2f, 0.6f),             // 紫色 — 怪物
            "neutral" => new Color(0.6f, 0.6f, 0.5f),             // 灰色 — 中立
            _ => new Color(0.5f, 0.5f, 0.5f),
        };

        // 更新 Polygon2D 或 Sprite 的颜色
        var polygon = visual.GetNodeOrNull<Polygon2D>("Polygon");
        if (polygon != null)
        {
            polygon.Color = factionColor;
            return;
        }

        // 回退：直接 Modulate 整个节点
        visual.Modulate = factionColor;
    }

    // ========================================
    // 任务目标点系统
    // ========================================

    /// <summary>
    /// 任务目标点生成回调 — 在大地图上渲染目标标记
    /// 对应 _on_quest_target_spawned (lines 1226-1231)
    /// </summary>
    private void OnQuestTargetSpawned(QuestTargetSite targetSite)
    {
        var visual = new BladeHex.View.Quest.QuestTargetVisual();
        AddChild(visual);
        visual.Setup(targetSite);
        _questTargetVisuals.Add(visual);

        GD.Print($"[OverworldScene] 渲染任务目标点: {targetSite.SiteName} ({targetSite.GetSiteTypeName()})");
    }

    /// <summary>
    /// 任务目标点清理回调 — 移除目标标记 (淡出后释放)
    /// 对应 _on_quest_target_cleared (lines 1235-1246)
    /// </summary>
    private void OnQuestTargetCleared(string questId)
    {
        for (int i = _questTargetVisuals.Count - 1; i >= 0; i--)
        {
            var visual = _questTargetVisuals[i];
            if (!GodotObject.IsInstanceValid(visual)) continue;

            var site = visual.Get("target_site").As<QuestTargetSite>();
            if (site != null && site.QuestId == questId)
            {
                if (visual.HasMethod("MarkCleared"))
                    visual.Call("MarkCleared");

                // 延迟淡出后释放节点
                var tween = CreateTween();
                tween.TweenProperty(visual, "modulate:a", 0.0, 1.0);
                tween.TweenCallback(Callable.From(visual.QueueFree));
                _questTargetVisuals.RemoveAt(i);
                break;
            }
        }
        _lastApproachedQuestId = "";
    }

    // ========================================
    // 战斗 / 交互包装器 — 供 Process.cs 调用
    // ========================================

    /// <summary>
    /// 从 OverworldEntity 创建 OverworldEnemy Node2D 包装器
    /// 供 Process.cs CheckEncounters() 调用
    /// </summary>
    private Node2D? CreateEnemyFromEntity(OverworldEntity entity)
    {
        var enemy = new OverworldEnemy();
        enemy.SetupFromEntity(entity);
        return enemy;
    }

    /// <summary>
    /// 与指定实体触发战斗
    /// 供 Process.cs CheckEncounters() 调用
    /// </summary>
    private void TriggerCombatWithEntity(OverworldEntity entity)
    {
        GD.Print($"[OverworldScene] 与 {entity.EntityName} 进入战斗");
        StopPlayer();

        // 生成敌方单位列表
        _pendingEncounterEnemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(entity);
        _pendingEncounterEntity = entity;

        // 构造 BattleContext
        var ctx = new BattleContext();
        ctx.EncounterCoord = HexOverworldTile.PixelToAxial(entity.Position.X, entity.Position.Y);

        // 尝试从实体位置获取地形信息
        HexOverworldTile? tile = null;
        if (_chunkManager != null)
        {
            tile = _chunkManager.GetTile(ctx.EncounterCoord.X, ctx.EncounterCoord.Y);
        }
        else if (HexGrid != null)
        {
            tile = HexGrid.GetTileAtPixel(entity.Position.X, entity.Position.Y);
        }
        if (tile != null)
            ctx.Terrain = tile.Terrain;

        // 设置战斗规模（根据实体队伍大小）
        ctx.Size = entity.PartySize switch
        {
            <= 4 => BattleContext.BattleSize.Mercenary,
            <= 8 => BattleContext.BattleSize.Knight,
            <= 16 => BattleContext.BattleSize.Lord,
            _ => BattleContext.BattleSize.Stronghold,
        };

        ctx.Seed = (int)GD.Randi();

        EnterCombatSceneFromCs(ctx);
    }

    /// <summary>
    /// 从 OverworldPOI 创建 OverworldTown Node2D 包装器
    /// 供 Process.cs CheckPoiEnter() 调用
    /// </summary>
    private Node2D? CreateTownFromPoi(OverworldPOI poi)
    {
        var town = new OverworldTown();
        town.TownName = poi.PoiName;
        town.Position = poi.Position;
        town.Prosperity = poi.Prosperity;
        town.Garrison = poi.GarrisonCurrent;

        // 根据POI类型初始化对应设施
        switch (poi.PoiTypeEnum)
        {
            case OverworldPOI.POIType.Village: town.SetupVillageFacilities(); break;
            case OverworldPOI.POIType.Port: town.SetupPortFacilities(); break;
            case OverworldPOI.POIType.Castle: town.SetupCastleFacilities(); break;
            case OverworldPOI.POIType.Outpost: town.SetupOutpostFacilities(); break;
            case OverworldPOI.POIType.Tavern: town.SetupTavernFacilities(); break;
            case OverworldPOI.POIType.Mine: town.SetupMineFacilities(); break;
            case OverworldPOI.POIType.Shrine: town.SetupShrineFacilities(); break;
            default: town.SetupDefaultFacilities(); break;
        }

        return town;
    }

    /// <summary>
    /// 玩家接近任务目标点时的处理
    /// 供 Process.cs CheckQuestTargetProximity() 调用
    /// </summary>
    private void OnPlayerReachedQuestTarget(QuestTargetSite targetSite)
    {
        GD.Print($"[OverworldScene] 接近任务目标: {targetSite.SiteName} ({targetSite.GetSiteTypeName()})");
        StopPlayer();

        if (targetSite.IsCleared)
        {
            GD.Print($"[OverworldScene] 目标点已清除，跳过: {targetSite.SiteName}");
            return;
        }

        // 查找对应的活跃委托
        var quest = FindActiveQuest(targetSite.QuestId);
        if (quest != null)
        {
            // 委托存在 — 委托给 Quest.cs 的完整处理流程（伏击事件 + 战斗/完成）
            OnPlayerReachedQuestTargetCs(quest);
        }
        else
        {
            // 没有对应的活跃委托（可能已过期或数据不一致）— 直接标记清除
            GD.Print($"[OverworldScene] 未找到对应委托 {targetSite.QuestId}，直接清除目标点");
            targetSite.IsCleared = true;

            var questMgr = QuestMgr as QuestManager;
            questMgr?.EmitSignal(QuestManager.SignalName.QuestTargetCleared, targetSite.QuestId);
        }
    }

    // ========================================
    // UI 信息更新
    // ========================================

    /// <summary>
    /// 刷新 UI 面板信息
    /// 被 _Process / InitUI / 世界事件处理调用
    /// </summary>
    private void UpdateUIInfo()
    {
        if (UI == null || PlayerUnitData == null) return;
        if (UI is BladeHex.View.UI.Overworld.OverworldUI overworldUi)
        {
            overworldUi.UpdateInfo(PlayerUnitData, EconomyMgr);

            // 更新右上角地形显示
            if (PlayerParty != null)
            {
                var terrainInfo = GetTerrainAtPlayer();
                overworldUi.UpdateTerrainDisplay(terrainInfo.Name, terrainInfo.Color);
            }

        }
    }

    /// <summary>获取玩家当前位置的地形信息</summary>
    private (string Name, Color Color) GetTerrainAtPlayer()
    {
        if (PlayerParty == null) return ("未知", Colors.Gray);

        var pos = PlayerParty.Position;
        HexOverworldTile? tile = null;

        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(pos.X, pos.Y);
            tile = _chunkManager.GetTile(axial.X, axial.Y);
        }
        else if (HexGrid != null)
        {
            tile = HexGrid.GetTileAtPixel(pos.X, pos.Y);
        }

        if (tile == null) return ("未知", Colors.Gray);

        int tt = (int)tile.Terrain;
        string name = tt switch
        {
            0 => "深水",
            1 => "浅水",
            2 => "沙地",
            3 => "平原",
            4 => "草地",
            5 => "森林",
            6 => "密林",
            7 => "丛林",
            8 => "针叶林",
            9 => "沼泽",
            10 => "湿地",
            11 => "草原",
            12 => "荒地",
            13 => "岩地",
            14 => "丘陵",
            15 => "山地",
            16 => "雪山",
            17 => "雪原",
            18 => "冰原",
            19 => "道路",
            20 => "河流",
            _ => "未知",
        };

        Color color = tt switch
        {
            0 => new Color(0.1f, 0.2f, 0.6f),
            1 => new Color(0.2f, 0.4f, 0.8f),
            2 => new Color(0.9f, 0.85f, 0.5f),
            3 => new Color(0.5f, 0.8f, 0.4f),
            4 => new Color(0.4f, 0.75f, 0.35f),
            5 => new Color(0.2f, 0.6f, 0.2f),
            6 => new Color(0.1f, 0.4f, 0.1f),
            7 => new Color(0.15f, 0.5f, 0.15f),
            8 => new Color(0.2f, 0.45f, 0.3f),
            9 or 10 => new Color(0.4f, 0.5f, 0.3f),
            11 => new Color(0.75f, 0.7f, 0.35f),
            12 => new Color(0.6f, 0.5f, 0.35f),
            13 => new Color(0.55f, 0.5f, 0.4f),
            14 => new Color(0.6f, 0.55f, 0.35f),
            15 or 16 => new Color(0.5f, 0.45f, 0.4f),
            17 or 18 => new Color(0.85f, 0.9f, 0.95f),
            19 => new Color(0.7f, 0.6f, 0.4f),
            20 => new Color(0.2f, 0.4f, 0.8f),
            _ => Colors.Gray,
        };

        return (name, color);
    }
}
