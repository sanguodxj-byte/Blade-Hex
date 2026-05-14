// OverworldScene.Process.cs
// [T-602] Per-frame update logic — partial class for OverworldScene
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.View.Map;

namespace BladeHex.Scenes.Overworld;

/// <summary>
/// OverworldScene partial — per-frame update methods:
/// UpdateVisualCycle, UpdateVisibility, UpdateUIInfo,
/// CheckEncounters, CheckPoiEnter, CheckQuestTargetProximity,
/// HandleMapClick, HandleCameraWASD
/// </summary>
public partial class OverworldScene
{
    // ========================================
    // 0. Chunk 加载更新
    // ========================================

    /// <summary>
    /// Chunk 模式下每帧更新 chunk 加载/卸载和渲染
    /// </summary>
    private void UpdateChunkLoading()
    {
        if (_chunkManager == null || PlayerParty == null || HexRenderer == null) return;

        // 将玩家像素位置转换为轴向坐标
        var playerAxial = HexOverworldTile.PixelToAxial(PlayerParty.Position.X, PlayerParty.Position.Y);

        // 更新 chunk 加载（从磁盘加载新 chunk，卸载远处 chunk）
        var previousActive = new HashSet<Vector2I>(_chunkManager.ActiveChunks.Keys);
        var newChunks = _chunkManager.UpdateChunks(playerAxial.X, playerAxial.Y);

        // 渲染新加载的 chunk
        if (newChunks.Count > 0)
        {
            HexRenderer.OnChunksUpdated(newChunks);
            // 道路已在初始化阶段一次性渲染，无需增量更新
            // 预计算代价网格
            if (_costGrid != null)
            {
                foreach (var chunk in newChunks)
                    _costGrid.OnChunkLoaded(chunk);
            }
            // 路径缓存失效 — chunk 变化可能影响已缓存路径
            PlayerParty.ChunkAStar?.InvalidateCache();
        }

        // 检测卸载的 chunk 并从渲染器移除
        var currentActive = new HashSet<Vector2I>(_chunkManager.ActiveChunks.Keys);
        var unloaded = new HashSet<Vector2I>();
        foreach (var coord in previousActive)
        {
            if (!currentActive.Contains(coord))
                unloaded.Add(coord);
        }

        if (unloaded.Count > 0)
        {
            HexRenderer.OnChunksUnloaded(unloaded);
            // chunk 卸载也需要清除缓存
            PlayerParty.ChunkAStar?.InvalidateCache();
            // 释放代价网格缓存
            if (_costGrid != null)
            {
                foreach (var coord in unloaded)
                    _costGrid.OnChunkUnloaded(coord);
            }

            // 保存修改过的 chunk 到磁盘（卸载时不自动保存 — 由玩家手动保存触发 SaveWorldData）
            // ChunkManager 的内存缓存保留卸载的 chunk 数据直到手动保存
        }
    }

    // ========================================
    // 1. 昼夜视觉更新 (GD lines 986-1006)
    // ========================================

    /// <summary>
    /// Update day/night cycle via CanvasModulate.
    /// Samples TimeGradient at current hour, applies season tint,
    /// clamps to BRIGHTNESS_FLOOR.
    /// </summary>
    private void UpdateVisualCycle()
    {
        if (SceneCanvasModulate == null || EconomyMgr == null)
            return;

        float timeRatio = EconomyMgr.CurrentHour / 24.0f;
        Color baseColor = TimeGradient.Sample(timeRatio);

        // Season tint — tweaked offsets to avoid extreme darkening
        Color seasonTint = Colors.White;
        switch (EconomyMgr.GetSeason())
        {
            case EconomyManager.Season.Spring:
                seasonTint = new Color(0.97f, 1.04f, 0.97f);
                break;
            case EconomyManager.Season.Summer:
                seasonTint = new Color(1.03f, 1.03f, 0.96f);
                break;
            case EconomyManager.Season.Fall:
                seasonTint = new Color(1.06f, 0.97f, 0.88f);
                break;
            case EconomyManager.Season.Winter:
                seasonTint = new Color(0.90f, 0.97f, 1.10f);
                break;
        }

        Color finalColor = baseColor * seasonTint;

        // Brightness floor protection: no channel below BRIGHTNESS_FLOOR
        finalColor.R = Mathf.Max(finalColor.R, BRIGHTNESS_FLOOR);
        finalColor.G = Mathf.Max(finalColor.G, BRIGHTNESS_FLOOR);
        finalColor.B = Mathf.Max(finalColor.B, BRIGHTNESS_FLOOR);

        SceneCanvasModulate.Color = finalColor;
    }

    // ========================================
    // 2. 迷雾可见性更新
    // ========================================

    /// <summary>玩家对实体和建筑的可见视野半径（8格）</summary>
    private const float ENTITY_VISION_RADIUS = 8.0f * HEX_TILE_SIZE; // 8格 × 156px = 1248px

    /// <summary>
    /// Update fog vision, control POI/entity visibility based on player distance.
    /// - 地形揭示：由 FogOfWar grid 控制（已揭示的永久可见）
    /// - POI 可见性：已揭示区域的 POI 永久可见（暗色），当前视野内全亮
    /// - 实体可见性：以玩家为中心 8 格半径内可见，超出隐藏
    /// </summary>
    private void UpdateVisibility()
    {
        if (Fog == null || PlayerParty == null)
            return;

        // Update fog vision grid (揭示玩家视野内的地形格)
        Fog.UpdateVision(PlayerParty.Position);

        // Sync explored tile cache with newly revealed tiles
        HexRenderer?.SyncWithFog();

        // 更新 fog renderer
        FogRenderer?.UpdatePlayerPosition(PlayerParty.Position);
        FogRenderer?.UpdateVisionRadius(Fog.VisionRange * Fog.ScoutMultiplier);

        float visionRadiusSq = ENTITY_VISION_RADIUS * ENTITY_VISION_RADIUS;
        Vector2 playerPos = PlayerParty.Position;

        // POI 可见性：已发现的 POI 永久可见，未发现的隐藏
        for (int i = 0; i < PoiVisuals.Count; i++)
        {
            var visual = PoiVisuals[i];
            if (!GodotObject.IsInstanceValid(visual)) continue;

            if (Fog.DisableFog)
            {
                visual.Visible = true;
                visual.Modulate = Colors.White;
                continue;
            }

            bool discovered = _discoveredPoiIndices.Contains(i);

            // 玩家视野内的 POI 自动发现（永久）
            if (!discovered)
            {
                float distSq = playerPos.DistanceSquaredTo(visual.Position);
                if (distSq <= visionRadiusSq)
                {
                    _discoveredPoiIndices.Add(i);
                    discovered = true;
                }
            }

            visual.Visible = discovered;

            // 当前视野内全亮，已发现但不在视野内稍暗
            if (discovered)
            {
                float distSq = playerPos.DistanceSquaredTo(visual.Position);
                bool inVision = distSq <= visionRadiusSq;
                visual.Modulate = inVision ? Colors.White : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // 实体（敌人/NPC）可见性：仅当前视野 8 格半径内可见
        foreach (var (entity, visual) in _entityVisuals)
        {
            if (!GodotObject.IsInstanceValid(visual)) continue;
            if (Fog.DisableFog)
            {
                visual.Visible = true;
            }
            else
            {
                float distSq = playerPos.DistanceSquaredTo(entity.Position);
                visual.Visible = distSq <= visionRadiusSq;
            }
        }

        // Sync fog renderer texture (dirty-pixel diff)
        FogRenderer?.UpdateFog();
    }

    // ========================================
    // 3. UI 信息更新 (GD lines 1027-1044)
    // Defined in OverworldScene.Combat.cs — reuse UpdateUIInfo()
    // ========================================

    // UpdateUIInfo() is already defined in OverworldScene.Combat.cs

    // ========================================
    // 4. 遭遇检查 (GD lines 940-955)
    // ========================================

    /// <summary>
    /// Check if player has encountered an AI entity.
    /// Delegates to EntityMgr.CheckPlayerEncounters().
    /// On encounter: tries InteractionManager via CreateEnemyFromEntity;
    /// falls back to TriggerCombatWithEntity (both in Combat.cs).
    /// </summary>
    private void CheckEncounters()
    {
        // 遭遇检测已统一由 UpdateEncounterVisuals() 处理（Encounter.cs）
        // 此方法保留为空以避免破坏 _Process 调用链
        // 所有遭遇触发逻辑在 Encounter.cs 的 UpdateEncounterVisuals 中
    }

    // ========================================
    // 5. POI 进入检测 (GD lines 957-968)
    // ========================================

    /// <summary>
    /// Check if player has entered a POI tile.
    /// Triggers only when player steps onto the POI's hex tile from outside.
    /// Uses axial coordinate matching instead of distance.
    /// </summary>
    private void CheckPoiEnter()
    {
        if (EntityMgr == null || PlayerParty == null)
            return;

        // 如果刚离开城镇，给一个冷却期（防止立即重新触发）
        if (_poiLeaveCooldown > 0)
        {
            _poiLeaveCooldown -= 1;
            return;
        }

        // 获取玩家当前所在的 hex tile 坐标
        var playerAxial = HexOverworldTile.PixelToAxial(PlayerParty.Position.X, PlayerParty.Position.Y);

        // 检查玩家是否站在某个 POI 的 tile 上
        OverworldPOI? steppedPoi = null;
        foreach (var poi in WorldPois)
        {
            var poiAxial = HexOverworldTile.PixelToAxial(poi.Position.X, poi.Position.Y);
            if (playerAxial == poiAxial)
            {
                steppedPoi = poi;
                break;
            }
        }

        if (steppedPoi != null && !_poiEntered && IsPlayerMoving())
        {
            _poiEntered = true;

            // 声望检查：是否允许进入该势力的城镇
            if (!string.IsNullOrEmpty(steppedPoi.OwningFaction)
                && steppedPoi.OwningFaction != "neutral"
                && !_reputationTracker.CanEnterTown(steppedPoi.OwningFaction))
            {
                GD.Print($"[Reputation] {steppedPoi.PoiName} 拒绝你进入！(声望: {_reputationTracker.GetReputation(steppedPoi.OwningFaction)})");
                _poiEntered = false;
                return;
            }

            StopPlayer();

            var town = CreateTownFromPoi(steppedPoi);
            if (town != null && InteractionMgr != null)
            {
                InteractionMgr.TriggerInteraction(town);
            }
            else
            {
                if (UI is BladeHex.View.UI.Overworld.OverworldUI overworldUi)
                    overworldUi.EmitSignal(BladeHex.View.UI.Overworld.OverworldUI.SignalName.MenuOpened, "poi_" + steppedPoi.PoiName);
            }
        }
        else if (steppedPoi == null && _poiEntered)
        {
            // 玩家离开了 POI 格子，重置标志
            _poiEntered = false;
        }
    }

    // ========================================
    // 6. 任务目标点接近检测 (GD lines 1250-1278)
    // ========================================

    /// <summary>
    /// Iterate quest target visuals, check distance to player,
    /// update visibility based on fog, and trigger approach event
    /// via OnPlayerReachedQuestTarget (Combat.cs).
    /// Only one target per frame (first that matches).
    /// </summary>
    private void CheckQuestTargetProximity()
    {
        if (PlayerParty == null)
            return;

        Vector2 playerPos = PlayerParty.Position;

        foreach (var visual in _questTargetVisuals)
        {
            if (!GodotObject.IsInstanceValid(visual))
                continue;

            var targetSite = visual.Get("target_site").As<QuestTargetSite>();
            if (targetSite == null || targetSite.IsCleared)
                continue;

            float dist = playerPos.DistanceTo(targetSite.WorldPosition);

            // Update fog-based visibility
            if (Fog != null)
            {
                visual.Visible = Fog.IsRevealed(
                    targetSite.WorldPosition.X,
                    targetSite.WorldPosition.Y);
            }

            // Proximity check — trigger when within approach distance
            if (dist < QUEST_TARGET_APPROACH_DIST)
            {
                string qid = targetSite.QuestId;
                if (qid != _lastApproachedQuestId)
                {
                    _lastApproachedQuestId = qid;
                    OnPlayerReachedQuestTarget(targetSite);
                }
                return; // One target per frame
            }
        }

        // Reset when player leaves all target ranges
        if (_lastApproachedQuestId != "")
        {
            var questMgr = QuestMgr as QuestManager;
            if (questMgr != null &&
                questMgr.ActiveTargetSites.TryGetValue(_lastApproachedQuestId, out var lastSite))
            {
                if (playerPos.DistanceTo(lastSite.WorldPosition) >= QUEST_TARGET_APPROACH_DIST)
                {
                    _lastApproachedQuestId = "";
                }
            }
        }
    }

    // ========================================
    // 7. 地图点击移动 (GD lines 844-865)
    // ========================================

    /// <summary>
    /// Handle left-click on the overworld map.
    /// Priority: POI → QuestTarget → Entity → free move.
    /// Chunk 模式下使用 ChunkManager 做可通行检查（HexGrid 在 chunk 模式下为空）。
    /// </summary>
    private void HandleMapClick(Vector2 clickPos)
    {
        if (PlayerParty == null)
            return;

        // 左键点击寻路 = 启用镜头跟随玩家
        RecenterCameraOnPlayer();
        _isFollowingPlayer = true;

        // Priority 1: POI (town / village / castle)
        var clickedPoi = FindNearestPoiInRange(clickPos, 300.0f);
        if (clickedPoi != null)
        {
            // 从玩家方向接近 POI，在 POI 边缘找可通行点（而非固定偏移）
            var approachTarget = FindApproachPosition(clickedPoi.Position, 80.0f);
            MovePlayerTo(approachTarget);
            return;
        }

        // Priority 2: Quest target site
        var clickedTarget = FindNearestQuestTargetInRange(clickPos, 400.0f);
        if (clickedTarget != null)
        {
            var approachTarget = FindApproachPosition(clickedTarget.WorldPosition, 60.0f);
            MovePlayerTo(approachTarget);
            return;
        }

        // Priority 3: AI entity
        var clickedEntity = FindNearestEntityInRange(clickPos, 300.0f);
        if (clickedEntity != null)
        {
            // 实体会移动，直接寻路到其当前位置
            MovePlayerTo(clickedEntity.Position);
            return;
        }

        // Priority 4: Free move to click position
        MovePlayerTo(clickPos);
    }

    /// <summary>
    /// 从玩家方向接近目标点，在目标附近找可通行位置。
    /// 优先在玩家→目标方向的反向偏移处找可通行 tile，
    /// 如果失败则 BFS 搜索最近可通行 tile。
    /// </summary>
    private Vector2 FindApproachPosition(Vector2 targetPos, float approachDist)
    {
        if (PlayerParty == null) return targetPos;

        // 方向：从目标朝向玩家（玩家接近方向）
        Vector2 dir = (PlayerParty.Position - targetPos).Normalized();
        if (dir.LengthSquared() < 0.01f)
            dir = Vector2.Right; // 玩家就在目标上，随便选个方向

        // 尝试在接近方向上找可通行点
        Vector2 candidate = targetPos + dir * approachDist;

        if (IsTilePassableAtPixel(candidate))
            return candidate;

        // 尝试 6 个方向（60° 间隔）
        for (int i = 1; i <= 5; i++)
        {
            float angle = i * Mathf.Pi / 3.0f;
            Vector2 rotated = dir.Rotated(angle);
            candidate = targetPos + rotated * approachDist;
            if (IsTilePassableAtPixel(candidate))
                return candidate;
        }

        // 所有方向都失败，使用 BFS 找最近可通行 tile
        var passable = FindNearestPassablePixel(targetPos);
        return passable ?? targetPos;
    }

    /// <summary>检查像素位置的 tile 是否可通行（优先 ChunkManager，回退 HexGrid）</summary>
    private bool IsTilePassableAtPixel(Vector2 pixel)
    {
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(pixel.X, pixel.Y);
            var tile = _chunkManager.GetTile(axial.X, axial.Y);
            // tile == null 表示未加载，视为可通行（ChunkAStar 会处理边界）
            return tile == null || tile.IsPassable;
        }
        if (HexGrid != null)
        {
            var tile = HexGrid.GetTileAtPixel(pixel.X, pixel.Y);
            return tile != null && tile.IsPassable;
        }
        return true;
    }

    /// <summary>BFS 搜索像素位置附近最近的可通行 tile 像素坐标</summary>
    private Vector2? FindNearestPassablePixel(Vector2 pixel)
    {
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(pixel.X, pixel.Y);
            var visited = new HashSet<Vector2I> { axial };
            var queue = new Queue<Vector2I>();
            queue.Enqueue(axial);

            int maxSearch = 36;
            while (queue.Count > 0 && maxSearch-- > 0)
            {
                var current = queue.Dequeue();
                for (int dir = 0; dir < 6; dir++)
                {
                    var neighbor = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);

                    var tile = _chunkManager.GetTile(neighbor.X, neighbor.Y);
                    if (tile != null && tile.IsPassable)
                        return HexOverworldTile.AxialToPixel(neighbor.X, neighbor.Y);

                    if (tile != null)
                        queue.Enqueue(neighbor);
                }
            }
            return null;
        }
        if (HexGrid != null)
        {
            var tile = HexGrid.FindPassableNearPixel(pixel.X, pixel.Y, 10);
            return tile?.PixelPos;
        }
        return null;
    }

    // ========================================
    // 7.5 ZoC 进出检测
    // ========================================

    /// <summary>
    /// 检测玩家部队进出敌对控制区，发出信号通知 UI。
    /// 每帧检查当前位置是否在 ZoC 内，与上一帧状态对比。
    /// </summary>
    private void CheckZocTransition()
    {
        if (_zocManager == null || PlayerParty == null) return;

        string playerFaction = PlayerParty.SpeedComponent?.PlayerFaction ?? "";
        if (string.IsNullOrEmpty(playerFaction)) return;

        var axial = HexOverworldTile.PixelToAxial(PlayerParty.Position.X, PlayerParty.Position.Y);
        bool isInZoc = _zocManager.IsInHostileZoc(axial.X, axial.Y, playerFaction);

        if (isInZoc && !_wasInHostileZoc)
        {
            // 进入 ZoC
            bool hasResistance = PlayerParty.SpeedComponent?.PlayerFaction != null; // simplified check
            float penalty = ZoneOfControlManager.ZocPenalty;
            _zocManager.EmitSignal(ZoneOfControlManager.SignalName.EnteredZoc, "hostile", penalty);
            GD.Print($"[ZoC] 进入敌对控制区 — 移速 ×{penalty}");
        }
        else if (!isInZoc && _wasInHostileZoc)
        {
            // 离开 ZoC
            _zocManager.EmitSignal(ZoneOfControlManager.SignalName.LeftZoc, "hostile");
            GD.Print("[ZoC] 离开敌对控制区");
        }

        _wasInHostileZoc = isInZoc;
    }

    // ========================================
    // 8. 相机 WASD 控制 (GD lines 973-981)
    // ========================================

    /// <summary>
    /// WASD camera movement relative to zoom level.
    /// </summary>
    private void HandleCameraWASD(float delta)
    {
        float camSpeed = 1000.0f * delta / MainCamera.Zoom.X;
        Vector2 moveVec = Vector2.Zero;

        if (Input.IsKeyPressed(Key.W)) moveVec.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) moveVec.Y += 1;
        if (Input.IsKeyPressed(Key.A)) moveVec.X -= 1;
        if (Input.IsKeyPressed(Key.D)) moveVec.X += 1;

        if (moveVec.Length() > 0)
        {
            // 任意键盘平移会暂时关闭平滑，取消跟随玩家
            MainCamera.PositionSmoothingEnabled = false;
            MainCamera.Position += moveVec.Normalized() * camSpeed;
            _isFollowingPlayer = false;
        }
    }

    /// <summary>
    /// 把摄像机平滑回到玩家位置（左键寻路时调用一次）。
    /// </summary>
    private void RecenterCameraOnPlayer()
    {
        if (MainCamera == null || PlayerParty == null) return;
        MainCamera.PositionSmoothingEnabled = true;
        MainCamera.PositionSmoothingSpeed = 8.0f;
        MainCamera.Position = PlayerParty.Position;
    }

    // ========================================
    // 查找辅助
    // ========================================

    /// <summary>
    /// Find nearest POI within maxDist from position.
    /// </summary>
    private OverworldPOI? FindNearestPoiInRange(Vector2 pos, float maxDist)
    {
        OverworldPOI? closest = null;
        float closestDist = maxDist;

        foreach (var poi in WorldPois)
        {
            float d = pos.DistanceTo(poi.Position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = poi;
            }
        }

        return closest;
    }

    /// <summary>
    /// Find nearest active quest target site within maxDist from position.
    /// </summary>
    private QuestTargetSite? FindNearestQuestTargetInRange(Vector2 pos, float maxDist)
    {
        var questMgr = QuestMgr as QuestManager;
        if (questMgr == null) return null;

        QuestTargetSite? closest = null;
        float closestDist = maxDist;

        foreach (var kvp in questMgr.ActiveTargetSites)
        {
            var site = kvp.Value;
            if (site.IsCleared) continue;

            float d = pos.DistanceTo(site.WorldPosition);
            if (d < closestDist)
            {
                closestDist = d;
                closest = site;
            }
        }

        return closest;
    }

    /// <summary>
    /// Find nearest alive AI entity within maxDist from position.
    /// </summary>
    private OverworldEntity? FindNearestEntityInRange(Vector2 pos, float maxDist)
    {
        OverworldEntity? closest = null;
        float closestDist = maxDist;

        foreach (var entity in WorldEntities)
        {
            if (!entity.IsAlive) continue;

            float d = pos.DistanceTo(entity.Position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = entity;
            }
        }

        return closest;
    }
}
