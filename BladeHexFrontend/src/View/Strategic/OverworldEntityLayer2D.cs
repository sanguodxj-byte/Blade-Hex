// OverworldEntityLayer2D.cs
// 实体视觉层 — 接收 MapEntityView 列表，负责普通实体创建、复用、LOD、位置同步
//
// 设计目标:
//   - 不处理战斗入口、不处理 tooltip 文本、不处理 AI 状态修改
//   - 只接收投影数据，创建/更新/回收 Godot 节点
//   - 支持 LOD：FullDetail / DotOnly / Hidden
//   - 单元/场景测试可以直接喂 view data 验证节点创建数量
using Godot;
using System.Collections.Generic;
using BladeHex.Strategic;

namespace BladeHex.View.Strategic;

/// <summary>
/// 实体视觉层。
///
/// 管线位置:
///   ViewProjectionSnapshot.Entities → EntityLayer.Sync(snapshot) → Sprite2D + Label 节点
///
/// 职责:
///   - 根据 MapEntityView 创建/更新/回收 Sprite2D + Label 节点
///   - LOD 切换
///   - 位置同步
///
/// 不负责:
///   - 战场 marker（由 OverworldBattlefieldLayer2D 处理）
///   - 围城 marker（由 OverworldSiegeLayer2D 处理）
///   - tooltip 文字
///   - AI 状态修改
///   - 战斗入口
/// </summary>
public sealed partial class OverworldEntityLayer2D : Node2D
{
    // ========================================
    // LOD 配置
    // ========================================

    private const float LodFullDetail = 0.5f;
    private const float LodDotOnly = 0.3f;
    private const float LodDotScale = OverworldInteractionHitRules.EntityDotScale;
    private const float EntitySpriteSize = OverworldInteractionHitRules.EntitySpriteSize;
    private const float EntityLabelFontSize = OverworldInteractionHitRules.EntityLabelFontSize;

    private enum EntityLOD : byte { FullDetail = 0, DotOnly = 1, Hidden = 2 }

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

    // ========================================
    // 内部状态
    // ========================================

    private readonly Dictionary<OverworldEntity, EntityVisualRef> _visualMap = new();
    private static Texture2D? _entityTexture;

    // ========================================
    // 同步入口
    // ========================================

    /// <summary>
    /// 根据投影快照同步实体视觉。
    /// 创建新可见实体的节点、更新已有节点位置/LOD、回收不可见实体。
    /// </summary>
    /// <param name="entityViews">本帧的实体视图列表</param>
    /// <param name="zoom">当前相机缩放级别</param>
    public void Sync(List<MapEntityView> entityViews, float zoom)
    {
        var visibleSet = new HashSet<OverworldEntity>();

        foreach (var view in entityViews)
        {
            if (!view.IsVisible || !GodotObject.IsInstanceValid(view.Entity) || !view.Entity.IsAlive)
                continue;

            visibleSet.Add(view.Entity);

            if (_visualMap.TryGetValue(view.Entity, out var visualRef))
            {
                // 更新位置
                visualRef.Container.Position = view.Position;
                ApplyLOD(ref visualRef, view, zoom);
                _visualMap[view.Entity] = visualRef;
            }
            else
            {
                // 创建新视觉
                visualRef = CreateVisual(view);
                visualRef.Container.Position = view.Position;
                AddChild(visualRef.Container);
                ApplyLOD(ref visualRef, view, zoom);
                _visualMap[view.Entity] = visualRef;
            }
        }

        // 回收不再可见的实体
        var toRemove = new List<OverworldEntity>();
        foreach (var kvp in _visualMap)
        {
            if (!GodotObject.IsInstanceValid(kvp.Key) || !kvp.Key.IsAlive || !visibleSet.Contains(kvp.Key))
            {
                kvp.Value.Container.QueueFree();
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var e in toRemove)
            _visualMap.Remove(e);
    }

    /// <summary>快速增量同步（玩家移动 < 50px 时使用，跳过大范围可见性重建）</summary>
    public void QuickSync(List<MapEntityView> entityViews, float zoom)
    {
        foreach (var view in entityViews)
        {
            if (!_visualMap.TryGetValue(view.Entity, out var visualRef)) continue;
            if (!GodotObject.IsInstanceValid(view.Entity) || !view.Entity.IsAlive) continue;

            visualRef.Container.Position = view.Position;
            ApplyLOD(ref visualRef, view, zoom);
            _visualMap[view.Entity] = visualRef;
        }
    }

    /// <summary>隐藏已在战场中显示的实体（由 BattlefieldLayer 告知）</summary>
    public void HideBattlefieldParticipants(HashSet<OverworldEntity> participants)
    {
        foreach (var entity in participants)
        {
            if (_visualMap.TryGetValue(entity, out var visualRef))
                visualRef.Container.Visible = false;
        }
    }

    /// <summary>清除所有视觉节点（场景切换时调用）</summary>
    public void ClearAll()
    {
        foreach (var kvp in _visualMap)
            kvp.Value.Container.QueueFree();
        _visualMap.Clear();
    }

    /// <summary>当前可见实体数量（用于诊断）</summary>
    public int VisibleCount => _visualMap.Count;

    // ========================================
    // 视觉创建
    // ========================================

    private EntityVisualRef CreateVisual(MapEntityView view)
    {
        var container = new Node2D
        {
            Name = $"Entity_{view.Entity.EntityName}",
            ZIndex = 90
        };

        var texture = GetEntityTexture();
        var sprite = new Sprite2D
        {
            Name = "Sprite",
            Texture = texture,
            Centered = true,
            Scale = OverworldInteractionHitRules.SpriteScaleForTexture(texture, EntitySpriteSize),
            Modulate = view.FactionColor
        };
        container.AddChild(sprite);

        var label = new Label
        {
            Name = "Label",
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = view.DisplayText,
            Modulate = view.LabelColor,
            Position = new Vector2(-40, -EntitySpriteSize * 0.5f - 18),
            Size = new Vector2(80, 20),
        };
        var fontSettings = new LabelSettings();
        bool isMarshal = view.Entity.IsMarshal && !string.IsNullOrEmpty(view.Entity.ArmyId);
        fontSettings.FontSize = isMarshal
            ? (int)(EntityLabelFontSize * 1.15f)
            : (int)EntityLabelFontSize;
        label.LabelSettings = fontSettings;
        container.AddChild(label);

        return new EntityVisualRef(container, sprite, label);
    }

    // ========================================
    // LOD
    // ========================================

    private static void ApplyLOD(ref EntityVisualRef visual, MapEntityView view, float zoom)
    {
        EntityLOD targetLOD;

        if (zoom < LodDotOnly)
            targetLOD = view.IsEssential ? EntityLOD.DotOnly : EntityLOD.Hidden;
        else if (zoom < LodFullDetail)
            targetLOD = EntityLOD.DotOnly;
        else
            targetLOD = EntityLOD.FullDetail;

        if (visual.CurrentLOD == targetLOD) return;
        visual.CurrentLOD = targetLOD;

        switch (targetLOD)
        {
            case EntityLOD.FullDetail:
                visual.Container.Visible = true;
                visual.Sprite.Scale = OverworldInteractionHitRules.SpriteScaleForTexture(visual.Sprite.Texture, EntitySpriteSize);
                visual.Label.Visible = true;
                break;

            case EntityLOD.DotOnly:
                visual.Container.Visible = true;
                visual.Sprite.Scale = OverworldInteractionHitRules.SpriteScaleForTexture(
                    visual.Sprite.Texture,
                    EntitySpriteSize * LodDotScale);
                visual.Label.Visible = false;
                break;

            case EntityLOD.Hidden:
                visual.Container.Visible = false;
                break;
        }
    }

    // ========================================
    // 纹理
    // ========================================

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
}
