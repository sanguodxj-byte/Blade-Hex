// OverworldBattlefieldLayer2D.cs
// 战场视觉层 — 接收 BattlefieldView 列表，负责战场 marker、hover、click 加入入口
//
// 设计目标:
//   - 两个实体合并为一个战场 marker
//   - 左侧 A（攻击方）、右侧 B（防御方）
//   - 背景按玩家关系：友方绿、中立黑、敌对红
//   - 支持 hover 命中测试
//   - 支持 click 返回 JoinOpportunity
//   - Engaged 中的两个实体不会同时显示为普通实体
//   - Layer 不直接调用 EnterCombatScene
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;

namespace BladeHex.View.Strategic;

/// <summary>
/// 战场视觉层。
///
/// 管线位置:
///   ViewProjectionSnapshot.Battlefields → BattlefieldLayer.Sync(snapshot) → marker 节点
///
/// 职责:
///   - 根据 BattlefieldView 创建/更新/回收战场 marker 节点
///   - 双方颜色（友方绿/中立黑/敌对红）
///   - hover 命中测试
///   - click 返回 JoinOpportunity（不直接触发战斗）
///
/// 不负责:
///   - 普通实体视觉
///   - 实际进入战斗场景
///   - AI 状态修改
/// </summary>
public sealed partial class OverworldBattlefieldLayer2D : Node2D
{
    // ========================================
    // 常量
    // ========================================

    private const float MarkerSize = 44.0f;

    /// <summary>战场 marker 的圆形 hover/click 命中半径，略大于视觉占位符。</summary>
    public const float MarkerHitRadius = 32.0f;

    private struct BattlefieldVisualRef
    {
        public Node2D Container;
        public Sprite2D BaseSprite;
        public ColorRect LeftSide;
        public ColorRect RightSide;
        public Label IconLabel;
        public Label NameLabel;
        public BattlefieldView Battle;
    }

    // ========================================
    // 内部状态
    // ========================================

    private readonly Dictionary<string, BattlefieldVisualRef> _visualMap = new();
    private static Texture2D? _battlefieldTexture;

    /// <summary>最近一次 hover 命中的战场 key（用于 tooltip 联动）</summary>
    public string? HoveredBattlefieldKey { get; private set; }

    /// <summary>最近一次 hover 命中的战场视图</summary>
    public BattlefieldView? HoveredBattlefield { get; private set; }

    // ========================================
    // 同步入口
    // ========================================

    /// <summary>
    /// 根据投影快照同步战场 marker。
    /// </summary>
    public void Sync(List<BattlefieldView> battles)
    {
        var visibleKeys = new HashSet<string>();

        foreach (var battle in battles)
        {
            visibleKeys.Add(battle.Key);

            if (_visualMap.TryGetValue(battle.Key, out var visual))
            {
                visual.Battle = battle;
                visual.Container.Position = battle.Position;
                ApplyVisual(ref visual, battle);
                _visualMap[battle.Key] = visual;
            }
            else
            {
                visual = CreateVisual(battle);
                visual.Container.Position = battle.Position;
                AddChild(visual.Container);
                _visualMap[battle.Key] = visual;
                OverworldDiagnostics.LogViewBattlefieldCreated(battle.Key);
            }
        }

        // 回收不可见战场
        var toRemove = new List<string>();
        foreach (var kvp in _visualMap)
        {
            if (!visibleKeys.Contains(kvp.Key))
            {
                kvp.Value.Container.QueueFree();
                toRemove.Add(kvp.Key);
                OverworldDiagnostics.LogViewBattlefieldRemoved(kvp.Key);
            }
        }
        foreach (string key in toRemove)
            _visualMap.Remove(key);
    }

    // ========================================
    // 命中测试
    // ========================================

    /// <summary>
    /// 检测鼠标位置是否命中某个战场 marker。
    /// 命中时设置 HoveredBattlefield 和 HoveredBattlefieldKey。
    /// </summary>
    /// <returns>命中的战场 key，或 null</returns>
    public string? HitTest(Vector2 mouseWorldPos)
    {
        foreach (var kvp in _visualMap)
        {
            if (kvp.Value.Container.Position.DistanceTo(mouseWorldPos) <= MarkerHitRadius)
            {
                HoveredBattlefieldKey = kvp.Key;
                HoveredBattlefield = kvp.Value.Battle;
                return kvp.Key;
            }
        }

        HoveredBattlefieldKey = null;
        HoveredBattlefield = null;
        return null;
    }

    /// <summary>
    /// 在指定位置查找可加入的战场，生成 JoinOpportunity（支持 NvN 多方战场）。
    /// 返回 null 表示该位置没有可加入的战场。
    /// 通过 allEntities 查找全参与者实体引用以填充 Attackers/Defenders 列表。
    /// </summary>
    public JoinOpportunity? QueryJoinAtPosition(Vector2 worldPos, float radius = MarkerHitRadius,
        List<OverworldEntity>? allEntities = null)
    {
        foreach (var kvp in _visualMap)
        {
            var battle = kvp.Value.Battle;
            float dist = worldPos.DistanceTo(battle.Position);
            if (dist > radius) continue;

            var opp = new JoinOpportunity
            {
                Type = WarBattleType.FieldBattle,
                BattlefieldId = battle.BattlefieldId,
                Attacker = battle.Attacker,
                DefenderEntity = battle.Defender,
                Distance = dist,
                WorldPosition = battle.Position,
                Attackers = new List<OverworldEntity>(),
                Defenders = new List<OverworldEntity>(),
                AttackerTotalPower = battle.AttackerTotalPower,
                DefenderTotalPower = battle.DefenderTotalPower,
            };

            // 优先使用投影保留下来的实体引用；名称只作为旧 fallback。
            if (battle.AttackerEntities.Length > 0 || battle.DefenderEntities.Length > 0)
            {
                foreach (var entity in battle.AttackerEntities)
                {
                    if (entity.IsAlive && (allEntities == null || allEntities.Contains(entity)))
                        opp.Attackers.Add(entity);
                }
                foreach (var entity in battle.DefenderEntities)
                {
                    if (entity.IsAlive && (allEntities == null || allEntities.Contains(entity)))
                        opp.Defenders.Add(entity);
                }
            }
            else if (allEntities != null)
            {
                foreach (var name in battle.AttackerNames)
                {
                    var e = allEntities.FirstOrDefault(e => e.EntityName == name && e.IsAlive);
                    if (e != null) opp.Attackers.Add(e);
                }
                foreach (var name in battle.DefenderNames)
                {
                    var e = allEntities.FirstOrDefault(e => e.EntityName == name && e.IsAlive);
                    if (e != null) opp.Defenders.Add(e);
                }
            }
            else
            {
                // 无实体列表时退化为 primary pair
                opp.Attackers.Add(battle.Attacker);
                if (battle.Defender != null) opp.Defenders.Add(battle.Defender);
            }

            return opp;
        }
        return null;
    }

    /// <summary>清除所有 marker（场景切换时调用）</summary>
    public void ClearAll()
    {
        foreach (var kvp in _visualMap)
            kvp.Value.Container.QueueFree();
        _visualMap.Clear();
        HoveredBattlefieldKey = null;
        HoveredBattlefield = null;
    }

    /// <summary>当前可见战场数量（用于诊断）</summary>
    public int VisibleCount => _visualMap.Count;

    // ========================================
    // 视觉创建
    // ========================================

    private BattlefieldVisualRef CreateVisual(BattlefieldView battle)
    {
        var container = new Node2D
        {
            Name = $"Battlefield_{battle.Key}",
            ZIndex = 95
        };

        var baseSprite = new Sprite2D
        {
            Name = "Base",
            Texture = GetBattlefieldTexture(),
            Centered = true,
            Scale = new Vector2(MarkerSize / 8f, MarkerSize / 8f),
            Modulate = new Color(0.08f, 0.08f, 0.09f, 0.86f)
        };
        container.AddChild(baseSprite);

        var left = new ColorRect
        {
            Name = "LeftSide",
            Position = new Vector2(-MarkerSize * 0.5f, -MarkerSize * 0.5f),
            Size = new Vector2(MarkerSize * 0.5f, MarkerSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        container.AddChild(left);

        var right = new ColorRect
        {
            Name = "RightSide",
            Position = new Vector2(0, -MarkerSize * 0.5f),
            Size = new Vector2(MarkerSize * 0.5f, MarkerSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        container.AddChild(right);

        var icon = new Label
        {
            Name = "Icon",
            Text = "\u2694",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(-MarkerSize * 0.5f, -MarkerSize * 0.5f - 2),
            Size = new Vector2(MarkerSize, MarkerSize + 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        icon.AddThemeFontSizeOverride("font_size", 24);
        icon.AddThemeColorOverride("font_color", new Color(1.0f, 0.86f, 0.42f));
        container.AddChild(icon);

        var label = new Label
        {
            Name = "Name",
            Text = "\u4ea4\u6218",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-54, -MarkerSize * 0.5f - 22),
            Size = new Vector2(108, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.52f));
        container.AddChild(label);

        var visual = new BattlefieldVisualRef
        {
            Container = container,
            BaseSprite = baseSprite,
            LeftSide = left,
            RightSide = right,
            IconLabel = icon,
            NameLabel = label,
            Battle = battle
        };
        ApplyVisual(ref visual, battle);
        return visual;
    }

    private static void ApplyVisual(ref BattlefieldVisualRef visual, BattlefieldView battle)
    {
        visual.LeftSide.Color = GetSideColor(battle.AttackerRelation);
        visual.RightSide.Color = GetSideColor(battle.DefenderRelation);
        visual.NameLabel.Text = "\u4ea4\u6218";
    }

    /// <summary>按玩家关系返回战场侧颜色：友方绿、中立黑、敌对红</summary>
    public static Color GetSideColor(PlayerBattleRelation relation)
    {
        return relation switch
        {
            PlayerBattleRelation.Friendly => new Color(0.2f, 0.6f, 0.3f, 0.85f),
            PlayerBattleRelation.Hostile => new Color(0.7f, 0.15f, 0.15f, 0.85f),
            _ => new Color(0.15f, 0.15f, 0.18f, 0.85f),
        };
    }

    // ========================================
    // 纹理
    // ========================================

    private static Texture2D GetBattlefieldTexture()
    {
        if (_battlefieldTexture == null)
        {
            var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            img.Fill(Colors.White);
            _battlefieldTexture = ImageTexture.CreateFromImage(img);
        }
        return _battlefieldTexture;
    }
}
