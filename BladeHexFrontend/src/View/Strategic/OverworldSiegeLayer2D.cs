// OverworldSiegeLayer2D.cs
// 围城视觉层 — 集中 POI IsUnderSiege 的表现和点击入口
//
// 设计目标:
//   - 围城 marker、攻守双方信息
//   - 助攻/助守入口
//   - 统一野战和围城的 hover/click 交互模型
//   - 玩家加入围城不依赖只在附近轮询 Prompt
//   - 点击被围城 POI 可以稳定出现加入选择
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;

namespace BladeHex.View.Strategic;

/// <summary>
/// 围城视觉层。
///
/// 管线位置:
///   ViewProjectionSnapshot.Sieges → SiegeLayer.Sync(snapshot) → marker 节点
///
/// 职责:
///   - 根据 SiegeView 创建/更新/回收围城 marker
///   - 攻守双方信息展示
///   - hover 命中测试
///   - click 返回 JoinOpportunity（助攻/助守）
///   - 统一与 BattlefieldLayer 的交互模型
///
/// 不负责:
///   - POI 常规渲染（由 POIRenderer2D 处理）
///   - 实际进入战斗场景
///   - AI 围城结算（由 SiegeProcessor 处理）
/// </summary>
public sealed partial class OverworldSiegeLayer2D : Node2D
{
    // ========================================
    // 常量
    // ========================================

    private const float SiegeMarkerSize = 52.0f;

    /// <summary>围城 marker 的圆形 hover/click 命中半径，略大于视觉占位符。</summary>
    public const float MarkerHitRadius = 36.0f;

    private struct SiegeVisualRef
    {
        public Node2D Container;
        public Sprite2D BaseSprite;
        public ColorRect AttackerBar;
        public ColorRect DefenderBar;
        public Label IconLabel;
        public Label InfoLabel;
        public SiegeView Siege;
    }

    // ========================================
    // 内部状态
    // ========================================

    private readonly Dictionary<OverworldPOI, SiegeVisualRef> _visualMap = new();
    private static Texture2D? _siegeTexture;

    /// <summary>最近一次 hover 命中的围城 POI</summary>
    public OverworldPOI? HoveredSiegePoi { get; private set; }

    /// <summary>最近一次 hover 命中的围城视图</summary>
    public SiegeView? HoveredSiege { get; private set; }

    // ========================================
    // 同步入口
    // ========================================

    /// <summary>
    /// 根据投影快照同步围城 marker。
    /// </summary>
    public void Sync(List<SiegeView> sieges)
    {
        var visiblePois = new HashSet<OverworldPOI>();

        foreach (var siege in sieges)
        {
            visiblePois.Add(siege.Poi);

            if (_visualMap.TryGetValue(siege.Poi, out var visual))
            {
                visual.Siege = siege;
                visual.Container.Position = siege.Position;
                ApplyVisual(ref visual, siege);
                _visualMap[siege.Poi] = visual;
            }
            else
            {
                visual = CreateVisual(siege);
                visual.Container.Position = siege.Position;
                AddChild(visual.Container);
                _visualMap[siege.Poi] = visual;
                OverworldDiagnostics.Log(Prefix.Battlefield,
                    $"siege_marker created: {siege.PoiName}");
            }
        }

        // 回收不再被围城的 POI marker
        var toRemove = new List<OverworldPOI>();
        foreach (var kvp in _visualMap)
        {
            if (!visiblePois.Contains(kvp.Key))
            {
                kvp.Value.Container.QueueFree();
                toRemove.Add(kvp.Key);
                OverworldDiagnostics.Log(Prefix.Battlefield,
                    $"siege_marker removed: {kvp.Key.PoiName}");
            }
        }
        foreach (var poi in toRemove)
            _visualMap.Remove(poi);
    }

    // ========================================
    // 命中测试
    // ========================================

    /// <summary>
    /// 检测鼠标位置是否命中某个围城 marker。
    /// </summary>
    public OverworldPOI? HitTest(Vector2 mouseWorldPos)
    {
        foreach (var kvp in _visualMap)
        {
            if (kvp.Value.Container.Position.DistanceTo(mouseWorldPos) <= MarkerHitRadius)
            {
                HoveredSiegePoi = kvp.Key;
                HoveredSiege = kvp.Value.Siege;
                return kvp.Key;
            }
        }

        HoveredSiegePoi = null;
        HoveredSiege = null;
        return null;
    }

    /// <summary>
    /// 在指定位置查找可加入的围城，生成 JoinOpportunity。
    /// </summary>
    public JoinOpportunity? QueryJoinAtPosition(
        Vector2 worldPos, List<OverworldEntity> entities,
        List<OverworldPOI> pois, string playerFaction, float radius = MarkerHitRadius)
    {
        // 优先使用 WarBattleJoinService 的 Siege 查询
        return WarBattleJoinService.Query(
            playerPos: worldPos,
            entities: entities,
            pois: pois,
            playerFaction: playerFaction,
            joinRadius: radius);
    }

    /// <summary>清除所有 marker（场景切换时调用）</summary>
    public void ClearAll()
    {
        foreach (var kvp in _visualMap)
            kvp.Value.Container.QueueFree();
        _visualMap.Clear();
        HoveredSiegePoi = null;
        HoveredSiege = null;
    }

    /// <summary>当前可见围城数量（用于诊断）</summary>
    public int VisibleCount => _visualMap.Count;

    // ========================================
    // 视觉创建
    // ========================================

    private SiegeVisualRef CreateVisual(SiegeView siege)
    {
        var container = new Node2D
        {
            Name = $"Siege_{siege.PoiName}",
            ZIndex = 96
        };

        // 底座
        var baseSprite = new Sprite2D
        {
            Name = "Base",
            Texture = GetSiegeTexture(),
            Centered = true,
            Scale = new Vector2(SiegeMarkerSize / 8f, SiegeMarkerSize / 8f),
            Modulate = new Color(0.12f, 0.08f, 0.06f, 0.9f)
        };
        container.AddChild(baseSprite);

        // 攻方色条
        var attackerBar = new ColorRect
        {
            Name = "AttackerBar",
            Position = new Vector2(-SiegeMarkerSize * 0.5f, -SiegeMarkerSize * 0.5f),
            Size = new Vector2(SiegeMarkerSize * 0.5f, SiegeMarkerSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        container.AddChild(attackerBar);

        // 守方色条
        var defenderBar = new ColorRect
        {
            Name = "DefenderBar",
            Position = new Vector2(0, -SiegeMarkerSize * 0.5f),
            Size = new Vector2(SiegeMarkerSize * 0.5f, SiegeMarkerSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        container.AddChild(defenderBar);

        // 围城图标
        var icon = new Label
        {
            Name = "Icon",
            Text = "\U0001f3f0",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(-SiegeMarkerSize * 0.5f, -SiegeMarkerSize * 0.5f - 2),
            Size = new Vector2(SiegeMarkerSize, SiegeMarkerSize + 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        icon.AddThemeFontSizeOverride("font_size", 22);
        icon.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.32f));
        container.AddChild(icon);

        // 信息标签
        var info = new Label
        {
            Name = "Info",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-60, -SiegeMarkerSize * 0.5f - 22),
            Size = new Vector2(120, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        info.AddThemeFontSizeOverride("font_size", 12);
        info.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.48f));
        container.AddChild(info);

        var visual = new SiegeVisualRef
        {
            Container = container,
            BaseSprite = baseSprite,
            AttackerBar = attackerBar,
            DefenderBar = defenderBar,
            IconLabel = icon,
            InfoLabel = info,
            Siege = siege
        };
        ApplyVisual(ref visual, siege);
        return visual;
    }

    private static void ApplyVisual(ref SiegeVisualRef visual, SiegeView siege)
    {
        visual.AttackerBar.Color = OverworldBattlefieldLayer2D.GetSideColor(siege.AttackerRelation);
        visual.DefenderBar.Color = OverworldBattlefieldLayer2D.GetSideColor(siege.DefenderRelation);
        visual.InfoLabel.Text = $"\u56f4\u57ce D{siege.SiegeDays}";
    }

    // ========================================
    // 纹理
    // ========================================

    private static Texture2D GetSiegeTexture()
    {
        if (_siegeTexture == null)
        {
            var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            img.Fill(Colors.White);
            _siegeTexture = ImageTexture.CreateFromImage(img);
        }
        return _siegeTexture;
    }

    /// <summary>日志前缀（复用 OverworldDiagnostics）</summary>
    private static class Prefix
    {
        public const string Battlefield = "[OverworldBattlefield]";
    }
}
