using Godot;
using System;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图上的敌军队伍实体 — 扩展支持 NPCProfile，区分人形/非人形交互
/// 视觉用 CharacterView2D（与玩家队伍 / 战斗 / UI 头像共用同一套换装系统）
/// </summary>
[GlobalClass]
public partial class OverworldEnemy : Node2D, IOverworldMapEntity
{
    /// <summary>玩家种族 ID（由 OverworldScene3D 在初始化时写入；用于决定 NPC 同/异族态度）</summary>
    public static int PlayerRaceIdStatic { get; set; } = 0;

    private BladeHex.View.Unit.CharacterView2D? _characterView;
    private Polygon2D? _fallbackPoly;
    private Label _nameLabel = null!;

    // ========================================
    // NavigationAgent2D 集成
    // ========================================

    /// <summary>导航代理</summary>
    private NavigationAgent2D? _navAgent;

    /// <summary>是否使用 NavigationAgent2D 移动</summary>
    private bool _useNavAgent = false;

    /// <summary>访问占位多边形（用于改色/缩放）</summary>
    public Polygon2D? VisualPoly => _fallbackPoly;

    // ========================================
    // 交互系统扩展
    // ========================================

    // NPC档案（为null则视为非人形生物）
    [Export] public NPCProfile? NpcProfile { get; set; } = null;
    [Export] public bool IsHostile { get; set; } = true;
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string DescriptionText { get; set; } = "";
    [Export] public int EnemyType { get; set; } = 1; // 默认 BEAST=1

    /// <summary>外部可注入的"代表角色数据"（领主/具名 NPC 时由 OverworldScene3D 设置）</summary>
    public UnitData? RepresentativeUnit { get; set; }

    public override void _Ready()
    {
        ZIndex = 90;
        SetupVisuals();
        SetupNavigationAgent();
    }

    /// <summary>设置 NavigationAgent2D</summary>
    private void SetupNavigationAgent()
    {
        _navAgent = new NavigationAgent2D();
        _navAgent.Name = "NavAgent";
        
        // 配置 agent 参数
        _navAgent.PathDesiredDistance = 10.0f;
        _navAgent.TargetDesiredDistance = 20.0f;
        _navAgent.PathMaxDistance = 50.0f;
        
        // 启用避障（NPC 互相避让）
        _navAgent.AvoidanceEnabled = true;
        _navAgent.Radius = 15.0f;
        _navAgent.MaxSpeed = 200.0f;
        _navAgent.NeighborDistance = 100.0f;
        _navAgent.MaxNeighbors = 5;
        
        AddChild(_navAgent);
    }

    /// <summary>设置 NavigationAgent2D 目标位置</summary>
    public void SetNavTarget(Vector2 target)
    {
        if (_navAgent == null)
            return;
        
        _navAgent.TargetPosition = target;
        _useNavAgent = true;
    }

    /// <summary>停止 NavigationAgent2D 移动</summary>
    public void StopNavAgent()
    {
        _useNavAgent = false;
        
        if (_navAgent != null)
            _navAgent.TargetPosition = Position;
    }

    public override void _Process(double delta)
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = GetDisplayNameWithStatus();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_useNavAgent || _navAgent == null || EntityRef == null)
            return;
        
        // 检查导航是否完成
        if (_navAgent.IsNavigationFinished())
        {
            _useNavAgent = false;
            return;
        }
        
        // 检查地图是否已同步
        if (NavigationServer2D.MapGetIterationId(_navAgent.GetNavigationMap()) == 0)
            return;
        
        // 获取下一个路径点
        Vector2 nextPos = _navAgent.GetNextPathPosition();
        Vector2 direction = (nextPos - Position).Normalized();
        
        // 计算速度
        float speed = EntityRef.MoveSpeed;
        if (EntityRef.CurrentAIState == OverworldEntity.AIState.Chasing)
            speed *= 1.1f;
        
        // 移动
        float moveDelta = speed * (float)delta;
        Position = Position.MoveToward(Position + direction * moveDelta, moveDelta);
        
        // 同步位置到 EntityRef
        EntityRef.Position = Position;
    }

    private string GetDisplayNameWithStatus()
    {
        string name = GetDisplayName();
        if (EntityRef == null) return name;

        string statusIcon = EntityRef.CurrentAIState switch
        {
            OverworldEntity.AIState.Besieging => " ⚔️围攻",
            OverworldEntity.AIState.Chasing => " 🎯追击",
            OverworldEntity.AIState.Fleeing => " 🏃溃退",
            OverworldEntity.AIState.MovingToTarget => " 🧭行军",
            _ => ""
        };

        if (!string.IsNullOrEmpty(statusIcon))
        {
            return $"{name}[{statusIcon}]";
        }
        return name;
    }

    private void SetupVisuals()
    {
        _characterView = new BladeHex.View.Unit.CharacterView2D
        {
            Name = "CharacterView2D",
            ContentScale = 0.55f,
            Visible = false,
        };
        AddChild(_characterView);

        // 占位三角（无角色数据时显示）
        _fallbackPoly = new Polygon2D();
        float radius = 15.0f;
        _fallbackPoly.Polygon = new Vector2[]
        {
            new(0, radius),
            new(radius * 0.7f, -radius * 0.5f),
            new(-radius * 0.7f, -radius * 0.5f)
        };
        _fallbackPoly.Color = NpcProfile != null
            ? new Color(0.9f, 0.7f, 0.2f) // 黄色=人形NPC
            : new Color(0.9f, 0.2f, 0.2f); // 红色=非人形
        AddChild(_fallbackPoly);

        // 名称标签
        _nameLabel = new Label();
        _nameLabel.Text = GetDisplayNameWithStatus();
        _nameLabel.Position = new Vector2(-40, -25);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.CustomMinimumSize = new Vector2(80, 20);
        _nameLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_nameLabel);

        SyncVisualFromUnit();
    }

    /// <summary>从 RepresentativeUnit 同步视觉</summary>
    public void SyncVisualFromUnit()
    {
        if (_characterView == null || _fallbackPoly == null) return;

        if (RepresentativeUnit != null)
        {
            _characterView.Setup(RepresentativeUnit);
            _characterView.Visible = true;
            _fallbackPoly.Visible = false;
        }
        else
        {
            _characterView.Visible = false;
            _fallbackPoly.Visible = true;
        }
    }

    public void PlayAnim(string animName)
    {
        _characterView?.PlayAnimation(animName);
    }

    public void PlaceAt(float px, float py) => Position = new Vector2(px, py);

    // ========================================
    // 交互辅助
    // ========================================

    public string GetEntityType()
    {
        if (NpcProfile != null) return "humanoid";
        if (EnemyType == 0) return "humanoid"; // HUMANOID=0
        return "nonhumanoid";
    }

    public string GetDisplayName()
    {
        if (NpcProfile != null) return NpcProfile.npcName;
        return string.IsNullOrEmpty(DisplayName) ? "未知敌人" : DisplayName;
    }

    public string GetDescription()
    {
        if (NpcProfile != null) return NpcProfile.GetDescription();
        if (!string.IsNullOrEmpty(DescriptionText)) return DescriptionText;
        string eName = GetDisplayName();
        int count = EntityRef?.PartySize ?? 1;
        string eType = EntityRef?.EntityTypeEnum.ToString() ?? "未知";
        return InteractionDescriptions.GetEnemyDescription(eName, IsHostile, count, eType);
    }

    /// <summary>从 OverworldEntity 数据初始化</summary>
    public void SetupFromEntity(OverworldEntity entity)
    {
        DisplayName = entity.EntityName;
        Position = entity.Position;
        IsHostile = entity.IsHostileToPlayer;
        EnemyType = (int)entity.EntityTypeEnum;
        EntityRef = entity;

        // 根据实体类型给"人形 NPC"自动生成 NpcProfile
        // —— 用以让 InteractionManager 走 humanoid 分支（交谈/交易/招募/袭击/离开）
        // 若已外部预设 NpcProfile（具名 NPC 等），不覆盖
        if (NpcProfile == null)
        {
            NpcProfile = TryCreateNpcProfileFromEntity(entity);
        }

        // 构造代表角色用于 2D 多层渲染：用 EncounterUnitFactory 取队伍中第一个单位
        try
        {
            var enemies = EncounterUnitFactory.BuildEnemyUnitsFromEntity(entity);
            if (enemies.Count > 0)
            {
                RepresentativeUnit = enemies[0];
                SyncVisualFromUnit();
            }
        }
        catch (Exception)
        {
            // 工厂失败不阻塞实体生成 — 保留三角占位
        }
    }

    /// <summary>根据实体类型创建对应 NpcProfile（仅人形/拟人实体）。返回 null 表示视为非人形（怪兽/巨龙等）</summary>
    private static NPCProfile? TryCreateNpcProfileFromEntity(OverworldEntity entity)
    {
        // 巨型怪物 / 龙 / 魔像 — 不是 humanoid
        if (entity.EntityTypeEnum == OverworldEntity.EntityType.EpicMonster) return null;

        // RaidingParty 中的非人形种族（兽人/哥布林等也算 humanoid，但部分种族如食人魔不是）
        // 这里宽松处理：所有 RaidingParty/Bandit/Robber/Pirate/Adventurer/Caravan/LordArmy 都视作人形
        var npcType = entity.EntityTypeEnum switch
        {
            OverworldEntity.EntityType.Adventurer => NPCProfile.NpcType.Adventurer,
            OverworldEntity.EntityType.Caravan => NPCProfile.NpcType.Merchant,
            OverworldEntity.EntityType.LordArmy => NPCProfile.NpcType.LordArmy,
            OverworldEntity.EntityType.RaidingParty => NPCProfile.NpcType.RaidingParty,
            OverworldEntity.EntityType.BanditParty => NPCProfile.NpcType.BanditParty,
            OverworldEntity.EntityType.RobberParty => NPCProfile.NpcType.RobberParty,
            OverworldEntity.EntityType.PirateCrew => NPCProfile.NpcType.PirateCrew,
            _ => NPCProfile.NpcType.Traveler,
        };

        // 阵营态度：
        //  - 敌对 → Hostile
        //  - 商队 → Friendly（中立但欢迎玩家）
        //  - 冒险者：同族 Friendly，异族 Neutral
        //  - 其他非敌对 → Neutral
        NPCProfile.Attitude attitude;
        if (entity.IsHostileToPlayer)
        {
            attitude = NPCProfile.Attitude.Hostile;
        }
        else if (npcType == NPCProfile.NpcType.Adventurer)
        {
            bool sameRace = entity.RaceId >= 0 && entity.RaceId == PlayerRaceIdStatic;
            attitude = sameRace ? NPCProfile.Attitude.Friendly : NPCProfile.Attitude.Neutral;
        }
        else if (npcType == NPCProfile.NpcType.Merchant)
        {
            attitude = NPCProfile.Attitude.Friendly;
        }
        else
        {
            attitude = NPCProfile.Attitude.Neutral;
        }

        return new NPCProfile
        {
            npcName = entity.EntityName,
            npcType = npcType,
            attitude = attitude,
            faction = entity.Faction,
            relation = entity.IsHostileToPlayer ? -50 : (attitude == NPCProfile.Attitude.Friendly ? 20 : 0),
            gold = entity.GoldCarried,
        };
    }

    /// <summary>原始 OverworldEntity 引用（用于战斗时生成敌方单位）</summary>
    public OverworldEntity? EntityRef { get; set; }
}
