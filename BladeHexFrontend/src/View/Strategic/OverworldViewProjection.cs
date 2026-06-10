// OverworldViewProjection.cs
// View Projection — 从 Simulation/Core 层投影出只读视图数据
//
// 设计目标:
//   - 只读 OverworldEntityManager 和 OverworldSimulationContext
//   - 输出 MapEntityView / BattlefieldView / SiegeView / PoiView
//   - 主场景不直接遍历实体决定每种视觉语义
//   - Engaged 实体通过 BattlefieldView 集中表达，不再在主场景到处特殊判断
//   - 新增一种地图表现时优先改 Projection，而不是改主场景主循环
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;

namespace BladeHex.View.Strategic;

/// <summary>实体视图数据 — 由 Projection 生成，供 EntityLayer 消费</summary>
public readonly struct MapEntityView
{
    public OverworldEntity Entity { get; }
    public Vector2 Position { get; }
    public Color FactionColor { get; }
    public Color LabelColor { get; }
    public string DisplayText { get; }
    public bool IsEssential { get; }
    public bool IsVisible { get; }

    public MapEntityView(
        OverworldEntity entity, Vector2 position, Color factionColor,
        Color labelColor, string displayText, bool isEssential, bool isVisible)
    {
        Entity = entity;
        Position = position;
        FactionColor = factionColor;
        LabelColor = labelColor;
        DisplayText = displayText;
        IsEssential = isEssential;
        IsVisible = isVisible;
    }
}

/// <summary>战场视图数据 — 由 Projection 生成，供 BattlefieldLayer 消费</summary>
public readonly struct BattlefieldView
{
    public string Key { get; }
    public string BattlefieldId { get; }
    public OverworldEntity Attacker { get; }
    public OverworldEntity Defender { get; }
    public Vector2 Position { get; }
    public PlayerBattleRelation AttackerRelation { get; }
    public PlayerBattleRelation DefenderRelation { get; }
    public string AttackerName { get; }
    public string DefenderName { get; }
    public float AttackerPower { get; }
    public float DefenderPower { get; }
    public float AttackerTotalPower { get; }
    public float DefenderTotalPower { get; }
    public float Progress { get; }

    // ── NvN 全参与者名称（供 Layer 查询完整 JoinOpportunity）──
    public string[] AttackerNames { get; }
    public string[] DefenderNames { get; }
    public OverworldEntity[] AttackerEntities { get; }
    public OverworldEntity[] DefenderEntities { get; }

    public BattlefieldView(
        string key, string battlefieldId, OverworldEntity attacker, OverworldEntity defender,
        Vector2 position, PlayerBattleRelation attackerRelation,
        PlayerBattleRelation defenderRelation, string attackerName,
        string defenderName, float attackerPower, float defenderPower,
        float attackerTotalPower, float defenderTotalPower, float progress,
        string[]? attackerNames = null, string[]? defenderNames = null,
        OverworldEntity[]? attackerEntities = null, OverworldEntity[]? defenderEntities = null)
    {
        Key = key;
        BattlefieldId = battlefieldId;
        Attacker = attacker;
        Defender = defender;
        Position = position;
        AttackerRelation = attackerRelation;
        DefenderRelation = defenderRelation;
        AttackerName = attackerName;
        DefenderName = defenderName;
        AttackerPower = attackerPower;
        DefenderPower = defenderPower;
        AttackerTotalPower = attackerTotalPower;
        DefenderTotalPower = defenderTotalPower;
        Progress = progress;
        AttackerNames = attackerNames ?? [];
        DefenderNames = defenderNames ?? [];
        AttackerEntities = attackerEntities ?? [];
        DefenderEntities = defenderEntities ?? [];
    }
}

/// <summary>围城视图数据 — 由 Projection 生成，供 SiegeLayer 消费</summary>
public readonly struct SiegeView
{
    public OverworldPOI Poi { get; }
    public Vector2 Position { get; }
    public string PoiName { get; }
    public string AttackerFaction { get; }
    public string DefenderFaction { get; }
    public int AttackerCount { get; }
    public int DefenderGarrison { get; }
    public int SiegeDays { get; }
    public PlayerBattleRelation AttackerRelation { get; }
    public PlayerBattleRelation DefenderRelation { get; }

    public SiegeView(
        OverworldPOI poi, Vector2 position, string poiName,
        string attackerFaction, string defenderFaction,
        int attackerCount, int defenderGarrison, int siegeDays,
        PlayerBattleRelation attackerRelation, PlayerBattleRelation defenderRelation)
    {
        Poi = poi;
        Position = position;
        PoiName = poiName;
        AttackerFaction = attackerFaction;
        DefenderFaction = defenderFaction;
        AttackerCount = attackerCount;
        DefenderGarrison = defenderGarrison;
        SiegeDays = siegeDays;
        AttackerRelation = attackerRelation;
        DefenderRelation = defenderRelation;
    }
}

/// <summary>POI 视图数据 — 由 Projection 生成，供 PoiLayer/主场景消费</summary>
public readonly struct PoiView
{
    public OverworldPOI Poi { get; }
    public Vector2 Position { get; }
    public string DisplayName { get; }
    public string Faction { get; }
    public bool IsUnderSiege { get; }
    public OverworldPOI.POIType PoiType { get; }

    public PoiView(OverworldPOI poi, Vector2 position, string displayName,
        string faction, bool isUnderSiege, OverworldPOI.POIType poiType)
    {
        Poi = poi;
        Position = position;
        DisplayName = displayName;
        Faction = faction;
        IsUnderSiege = isUnderSiege;
        PoiType = poiType;
    }
}

/// <summary>
/// 所有视图数据的快照容器。每帧由 Projection 生成一次。
/// </summary>
public sealed class ViewProjectionSnapshot
{
    public List<MapEntityView> Entities { get; } = new();
    public List<BattlefieldView> Battlefields { get; } = new();
    public List<SiegeView> Sieges { get; } = new();
    public List<PoiView> Pois { get; } = new();

    /// <summary>战场参与者集合 — EntityLayer 据此隐藏已在战场中显示的实体</summary>
    public HashSet<OverworldEntity> BattlefieldParticipants { get; } = new();
}

/// <summary>
/// 大地图 View Projection — 从 Core 层投影出只读视图数据。
///
/// 管线位置:
///   OverworldSimulation.Tick → OverworldEntityManager → OverworldViewProjection.Project() → ViewProjectionSnapshot → Layers
///
/// 职责:
///   - 读取 OverworldEntityManager 的实体和 POI 列表
///   - 识别 Engaged 实体对，生成 BattlefieldView
///   - 识别 IsUnderSiege POI，生成 SiegeView
///   - 为可见实体生成 MapEntityView（颜色、文字、LOD 标记）
///   - 不创建任何 Godot 节点
/// </summary>
public sealed class OverworldViewProjection
{
    private string _playerFaction;

    public OverworldViewProjection(string playerFaction)
    {
        _playerFaction = playerFaction;
    }

    /// <summary>更新玩家阵营（阵营可能随王国建立变化）</summary>
    public void SetPlayerFaction(string faction) => _playerFaction = faction;

/// <summary>
    /// 从 EntityManager 和可见性查询结果投影出完整视图快照。
    /// 优先使用 BattlefieldRegistry 快照（NvN 多方战场），降级至 EngagedWith pair 扫描。
    /// </summary>
    /// <param name="entityMgr">实体管理器（提供 Entities、Pois）</param>
    /// <param name="visibleEntities">当前视野内可见的实体列表</param>
    /// <param name="playerPos">玩家世界像素位置</param>
    /// <param name="visionRange">当前视野范围（受气象影响后）</param>
    /// <param name="registry">战场注册表（提供 BattlefieldSnapshot 快照，可空）</param>
    /// <param name="battleResolver">战斗结算器（供 registry 查询快照，可空）</param>
    /// <param name="currentGameHour">当前游戏小时数（用于计算进度）</param>
    public ViewProjectionSnapshot Project(
        OverworldEntityManager entityMgr,
        List<OverworldEntity> visibleEntities,
        Vector2 playerPos,
        float visionRange,
        BattlefieldRegistry? registry = null,
        BattleResolver? battleResolver = null,
        float currentGameHour = 0f)
    {
        var snapshot = new ViewProjectionSnapshot();

        // 1. 识别野战战场（优先 Registry 快照，降级 EngagedWith pair）
        BuildBattlefieldViews(entityMgr.Entities, registry, battleResolver, playerPos,
            visionRange, currentGameHour, snapshot);

        // 2. 识别围城 POI
        BuildSiegeViews(entityMgr.Entities, entityMgr.Pois, snapshot);

        // 3. 投影可见实体（排除已在战场中显示的实体）
        BuildEntityViews(visibleEntities, snapshot);

        // 4. 投影 POI（供 tooltip 等使用）
        BuildPoiViews(entityMgr.Pois, playerPos, visionRange, snapshot);

        return snapshot;
    }

    // ========================================
    // 战场投影
    // ========================================

    private void BuildBattlefieldViews(
        List<OverworldEntity> allEntities,
        BattlefieldRegistry? registry,
        BattleResolver? battleResolver,
        Vector2 playerPos,
        float visionRange,
        float currentGameHour,
        ViewProjectionSnapshot snapshot)
    {
        var seenKeys = new HashSet<string>();

        // ── 主路径：BattlefieldRegistry 快照（支持 NvN 多方战场）──
        if (registry != null && battleResolver != null)
        {
            try
            {
                var snapshots = registry.GetSnapshots(battleResolver, currentGameHour);
                foreach (var bf in snapshots)
                {
                    if (playerPos.DistanceTo(bf.Position) > visionRange) continue;
                    if (bf.Attackers.Count == 0 || bf.Defenders.Count == 0) continue;

                    if (!seenKeys.Add(bf.Id)) continue;

                    // 全参与者追踪
                    var atkEntities = new List<OverworldEntity>();
                    var defEntities = new List<OverworldEntity>();
                    foreach (var p in bf.Attackers)
                    {
                        var e = FindAliveEntity(allEntities, p);
                        if (e != null) { atkEntities.Add(e); snapshot.BattlefieldParticipants.Add(e); }
                    }
                    foreach (var p in bf.Defenders)
                    {
                        var e = FindAliveEntity(allEntities, p);
                        if (e != null) { defEntities.Add(e); snapshot.BattlefieldParticipants.Add(e); }
                    }

                    // 主攻/主守：取战力最高者作为渲染锚点
                    OverworldEntity? primaryAtk = atkEntities.OrderByDescending(e => e.CombatPower * e.PartySize).FirstOrDefault();
                    OverworldEntity? primaryDef = defEntities.OrderByDescending(e => e.CombatPower * e.PartySize).FirstOrDefault();
                    if (primaryAtk == null || primaryDef == null) continue;

                    float atkTotal = atkEntities.Sum(e => e.CombatPower * e.PartySize);
                    float defTotal = defEntities.Sum(e => e.CombatPower * e.PartySize);

                    string[] atkNames = bf.Attackers.Select(p => p.EntityName).ToArray();
                    string[] defNames = bf.Defenders.Select(p => p.EntityName).ToArray();

                    snapshot.Battlefields.Add(new BattlefieldView(
                        key: bf.Id,
                        battlefieldId: bf.Id,
                        attacker: primaryAtk,
                        defender: primaryDef,
                        position: bf.Position,
                        attackerRelation: GetRelationToPlayer(primaryAtk),
                        defenderRelation: GetRelationToPlayer(primaryDef),
                        attackerName: primaryAtk.EntityName,
                        defenderName: primaryDef.EntityName,
                        attackerPower: primaryAtk.CombatPower * primaryAtk.PartySize,
                        defenderPower: primaryDef.CombatPower * primaryDef.PartySize,
                        attackerTotalPower: atkTotal,
                        defenderTotalPower: defTotal,
                        progress: bf.Progress,
                        attackerNames: atkNames,
                        defenderNames: defNames,
                        attackerEntities: atkEntities.ToArray(),
                        defenderEntities: defEntities.ToArray()
                    ));
                }
            }
            catch (System.Exception ex)
            {
                OverworldDiagnostics.LogFallbackThrottled("[Projection]", "registry", $"Registry fallback: {ex.Message}");
            }
        }

        // ── 降级路径：EngagedWith pair 扫描（补漏未在 registry 登记的交战实体）──
        var fallbackGroups = allEntities
            .Where(IsEntityInFieldBattle)
            .GroupBy(e => string.IsNullOrEmpty(e.BattlefieldId) ? GetFieldBattleKey(e, e.EngagedWith!) : e.BattlefieldId)
            .ToList();

        foreach (var group in fallbackGroups)
        {
            var participants = group
                .Where(e => !snapshot.BattlefieldParticipants.Contains(e))
                .ToList();
            if (participants.Count == 0) continue;

            var seed = participants[0];
            var attackers = participants
                .Where(e => !OverworldHostility.AreHostile(seed, e))
                .ToList();
            var defenders = participants
                .Where(e => OverworldHostility.AreHostile(seed, e))
                .ToList();

            if (attackers.Count == 0 || defenders.Count == 0)
            {
                var first = participants.First();
                var opponent = first.EngagedWith;
                if (!IsEntityInFieldBattle(opponent)) continue;
                attackers = [first];
                defenders = [opponent!];
            }

            string key = group.Key;
            if (!seenKeys.Add(key)) continue;

            Vector2 pos = Vector2.Zero;
            foreach (var participant in participants)
                pos += participant.Position;
            pos /= participants.Count;
            if (playerPos.DistanceTo(pos) > visionRange) continue;

            foreach (var participant in participants)
                snapshot.BattlefieldParticipants.Add(participant);

            var primaryAtk = attackers.OrderByDescending(e => e.CombatPower * e.PartySize).FirstOrDefault();
            var primaryDef = defenders.OrderByDescending(e => e.CombatPower * e.PartySize).FirstOrDefault();
            if (primaryAtk == null || primaryDef == null) continue;

            float atkPower = attackers.Sum(e => e.CombatPower * e.PartySize);
            float defPower = defenders.Sum(e => e.CombatPower * e.PartySize);

            snapshot.Battlefields.Add(new BattlefieldView(
                key: key,
                battlefieldId: key.StartsWith("fb_") ? "" : key,
                attacker: primaryAtk,
                defender: primaryDef,
                position: pos,
                attackerRelation: GetRelationToPlayer(primaryAtk),
                defenderRelation: GetRelationToPlayer(primaryDef),
                attackerName: primaryAtk.EntityName,
                defenderName: primaryDef.EntityName,
                attackerPower: primaryAtk.CombatPower * primaryAtk.PartySize,
                defenderPower: primaryDef.CombatPower * primaryDef.PartySize,
                attackerTotalPower: atkPower,
                defenderTotalPower: defPower,
                progress: 0f,
                attackerNames: attackers.Select(e => e.EntityName).ToArray(),
                defenderNames: defenders.Select(e => e.EntityName).ToArray(),
                attackerEntities: attackers.ToArray(),
                defenderEntities: defenders.ToArray()
            ));
        }
    }

    private static OverworldEntity? FindAliveEntity(List<OverworldEntity> entities, BattlefieldParticipantView participant)
    {
        if (participant.Entity.IsAlive && entities.Contains(participant.Entity))
            return participant.Entity;

        return FindAliveEntity(entities, participant.EntityName);
    }

    private static OverworldEntity? FindAliveEntity(List<OverworldEntity> entities, string name)
    {
        return entities.FirstOrDefault(e => e.EntityName == name && e.IsAlive);
    }

    // ========================================
    // 围城投影
    // ========================================

    private void BuildSiegeViews(
        List<OverworldEntity> allEntities, List<OverworldPOI> pois,
        ViewProjectionSnapshot snapshot)
    {
        foreach (var poi in pois)
        {
            if (!poi.IsUnderSiege) continue;

            var attackers = allEntities
                .Where(e => e.IsAlive
                    && e.CurrentAIState == OverworldEntity.AIState.Besieging
                    && e.SiegeTarget == poi)
                .ToList();

            int attackerCount = attackers.Count;
            string attackerFaction = attackers.FirstOrDefault()?.Faction ?? "unknown";

            snapshot.Sieges.Add(new SiegeView(
                poi: poi,
                position: poi.Position,
                poiName: poi.PoiName,
                attackerFaction: attackerFaction,
                defenderFaction: poi.OwningFaction,
                attackerCount: attackerCount,
                defenderGarrison: poi.GarrisonCurrent,
                siegeDays: poi.SiegeDays,
                attackerRelation: GetRelationToPlayerFaction(attackerFaction),
                defenderRelation: GetRelationToPlayerFaction(poi.OwningFaction)
            ));
        }
    }

    // ========================================
    // 实体投影
    // ========================================

    private void BuildEntityViews(
        List<OverworldEntity> visibleEntities,
        ViewProjectionSnapshot snapshot)
    {
        foreach (var entity in visibleEntities)
        {
            if (!entity.IsAlive) continue;
            // 已在战场中显示的实体不在普通实体层出现
            if (snapshot.BattlefieldParticipants.Contains(entity)) continue;

            bool isEssential = entity.Faction == "player"
                || (entity.IsMarshal && !string.IsNullOrEmpty(entity.ArmyId))
                || entity.IsNamedCharacter;

            string displayText = BuildEntityDisplayText(entity);
            Color factionColor = BuildEntityFactionColor(entity);
            Color labelColor = BuildEntityLabelColor(entity);

            snapshot.Entities.Add(new MapEntityView(
                entity: entity,
                position: entity.Position,
                factionColor: factionColor,
                labelColor: labelColor,
                displayText: displayText,
                isEssential: isEssential,
                isVisible: true
            ));
        }
    }

    // ========================================
    // POI 投影
    // ========================================

    private void BuildPoiViews(
        List<OverworldPOI> pois, Vector2 playerPos, float visionRange,
        ViewProjectionSnapshot snapshot)
    {
        foreach (var poi in pois)
        {
            if (playerPos.DistanceTo(poi.Position) > visionRange) continue;

            snapshot.Pois.Add(new PoiView(
                poi: poi,
                position: poi.Position,
                displayName: poi.PoiName,
                faction: poi.OwningFaction,
                isUnderSiege: poi.IsUnderSiege,
                poiType: poi.PoiTypeEnum
            ));
        }
    }

    // ========================================
    // 辅助方法
    // ========================================

    private static bool IsEntityInFieldBattle(OverworldEntity? entity)
    {
        return entity != null
            && entity.IsAlive
            && entity.CurrentAIState == OverworldEntity.AIState.Engaged
            && entity.EngagedWith != null;
    }

    private static string GetFieldBattleKey(OverworldEntity a, OverworldEntity b)
    {
        int hashA = a.GetHashCode();
        int hashB = b.GetHashCode();
        return hashA < hashB ? $"fb_{hashA}_{hashB}" : $"fb_{hashB}_{hashA}";
    }

    private string BuildEntityDisplayText(OverworldEntity entity)
    {
        if (entity.Faction == "player")
            return $"\U0001f6e1 {entity.EntityName}";
        if (entity.IsMarshal && !string.IsNullOrEmpty(entity.ArmyId))
            return $"\u2694 {entity.EntityName}";
        return entity.EntityName;
    }

    private Color BuildEntityFactionColor(OverworldEntity entity)
    {
        if (entity.Faction == "player")
            return new Color(0.2f, 0.4f, 0.8f);
        return IsHostile(entity) ? new Color(0.8f, 0.2f, 0.2f) : new Color(0.3f, 0.7f, 0.3f);
    }

    private Color BuildEntityLabelColor(OverworldEntity entity)
    {
        if (entity.Faction == "player")
            return new Color(0.6f, 0.8f, 1.0f);
        if (entity.IsMarshal && !string.IsNullOrEmpty(entity.ArmyId))
        {
            float hue = (System.Math.Abs(entity.Faction.GetHashCode()) % 360) / 360.0f;
            return Color.FromHsv(hue, 0.9f, 1.0f);
        }
        return IsHostile(entity) ? new Color(1.0f, 0.8f, 0.8f) : new Color(0.8f, 1.0f, 0.8f);
    }

    private bool IsHostile(OverworldEntity entity)
    {
        if (entity.Faction == _playerFaction) return false;
        if (OverworldHostility.IsPlayerFaction(_playerFaction))
            return entity.IsHostileToPlayer || OverworldHostility.IsIntrinsicHostileFaction(entity.Faction);
        if (OverworldHostility.IsPlayerFaction(entity.Faction))
            return OverworldHostility.IsIntrinsicHostileFaction(_playerFaction);
        return OverworldHostility.IsIntrinsicHostileFaction(entity.Faction)
            || OverworldHostility.IsIntrinsicHostileFaction(_playerFaction);
    }

    private PlayerBattleRelation GetRelationToPlayer(OverworldEntity entity)
    {
        if (entity.Faction == _playerFaction) return PlayerBattleRelation.Friendly;
        if (IsHostile(entity)) return PlayerBattleRelation.Hostile;
        return PlayerBattleRelation.Neutral;
    }

    private PlayerBattleRelation GetRelationToPlayerFaction(string faction)
    {
        if (faction == _playerFaction) return PlayerBattleRelation.Friendly;
        bool isHostile;
        if (OverworldHostility.IsPlayerFaction(_playerFaction))
            isHostile = OverworldHostility.IsIntrinsicHostileFaction(faction);
        else if (OverworldHostility.IsPlayerFaction(faction))
            isHostile = OverworldHostility.IsIntrinsicHostileFaction(_playerFaction);
        else
            isHostile = OverworldHostility.IsIntrinsicHostileFaction(faction)
                || OverworldHostility.IsIntrinsicHostileFaction(_playerFaction);
        return isHostile ? PlayerBattleRelation.Hostile : PlayerBattleRelation.Neutral;
    }
}

/// <summary>玩家与实体的战场关系</summary>
public enum PlayerBattleRelation
{
    Friendly,
    Neutral,
    Hostile
}
