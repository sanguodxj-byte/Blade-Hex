// CharacterView2D.cs
// 2D 角色多层渲染节点（与 CharacterRenderNode 的 3D 版本对称）。
// 用于：大地图（OverworldParty / OverworldEnemy）、UI 头像（CharacterAvatarControl 内）。
//
// 资源解析委托给 CharacterPresenter，槽位锚点由 SlotConfigTable 提供（与 3D 共享）。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.Unit.Slots;
using static BladeHex.View.Unit.Slots.SlotConfigTable;

namespace BladeHex.View.Unit;

/// <summary>2D 多层角色视图 — 6 层 Sprite2D / AnimatedSprite2D 合成换装。</summary>
[GlobalClass]
public partial class CharacterView2D : Node2D
{
    private const string LayerNodePrefix = "Layer_";

    /// <summary>外部数据引用（可空）— Setup() 时记录</summary>
    public UnitData? UnitDataRef { get; private set; }

    private readonly Dictionary<int, AnimatedSprite2D> _layers = new();
    private string _currentAnim = "default";
    private bool _useSecondaryWeapon;
    private float _bodyHeight = 120.0f;

    /// <summary>整体缩放（1.0 = 原始大小，UI 预览常用 0.4 ~ 0.8）</summary>
    [Export] public float ContentScale { get; set; } = 1.0f;

    /// <summary>3D 锚点偏移在 2D 投影时的 Y 系数（3D Y 向上 = 2D Y 向上=负方向）</summary>
    private const float YProjScale = -1.0f;

    /// <summary>3D 锚点偏移在 2D 投影时的 X 系数</summary>
    private const float XProjScale = 1.0f;

    public override void _Ready()
    {
        // 不在 _Ready 自动构建 — 等 Setup() 显式调用
    }

    /// <summary>
    /// 用 UnitData 初始化所有层。可重复调用以刷新整个视图。
    /// </summary>
    public void Setup(UnitData data, bool useSecondaryWeapon = false)
    {
        UnitDataRef = data;
        _useSecondaryWeapon = useSecondaryWeapon;
        Rebuild();
    }

    /// <summary>从已有 UnitData 重新解析并刷新所有层（不重建节点，复用 SpriteFrames）</summary>
    public void RefreshAll()
    {
        if (UnitDataRef != null) Rebuild();
    }

    /// <summary>在所有可见层上播放动画（如 "default" / "move" / "hit" / "die"）</summary>
    public void PlayAnimation(string animName)
    {
        _currentAnim = animName;
        foreach (var sprite in _layers.Values)
        {
            if (!sprite.Visible) continue;
            if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation(animName))
                sprite.Play(animName);
            else if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation("default"))
                sprite.Play("default");
        }
    }

    /// <summary>切换主副手武器（与 Unit.UsingPrimaryWeapon 同步）</summary>
    public void SetUsingSecondaryWeapon(bool useSecondary)
    {
        if (_useSecondaryWeapon == useSecondary) return;
        _useSecondaryWeapon = useSecondary;
        if (UnitDataRef != null) Rebuild();
    }

    /// <summary>身体高度（外部 HUD 可用来定位 HP 条等）</summary>
    public float BodyHeight => _bodyHeight * ContentScale;

    // ===========================================================
    // 内部
    // ===========================================================

    private void Rebuild()
    {
        if (UnitDataRef == null) return;

        // 确保所有槽位的 Sprite 节点已存在
        EnsureLayers();

        var resolution = CharacterPresenter.Resolve(UnitDataRef, _useSecondaryWeapon);
        _bodyHeight = resolution.BodyTextureHeight;

        // 应用每层
        foreach (var cfg in GetAllSlotConfigs())
        {
            var sprite = _layers[cfg.SlotIndex];
            if (resolution.Slots.TryGetValue(cfg.Slot, out var slotData) && slotData.HasContent)
            {
                ApplySlot(sprite, cfg, slotData);
                sprite.Visible = true;
            }
            else
            {
                // 该层无内容 — Body 永远显示（占位），其他层隐藏
                sprite.Visible = cfg.Slot == ItemData.EquipSlot.Body;
            }
        }

        // 占位符着色
        if (resolution.BodyIsPlaceholder && _layers.TryGetValue((int)ItemData.EquipSlot.Body, out var bodySprite))
            bodySprite.Modulate = resolution.PlaceholderModulate;

        // 整体缩放
        Scale = new Vector2(ContentScale, ContentScale);

        // 触发当前动画播放
        PlayAnimation(_currentAnim);
    }

    private void EnsureLayers()
    {
        foreach (var cfg in GetAllSlotConfigs())
        {
            if (_layers.ContainsKey(cfg.SlotIndex)) continue;
            var sprite = new AnimatedSprite2D
            {
                Name = LayerNodePrefix + GetSlotName(cfg.Slot),
                Visible = false,
            };
            ApplyAnchorTo(sprite, cfg);
            _layers[cfg.SlotIndex] = sprite;
            AddChild(sprite);
        }
    }

    private static void ApplyAnchorTo(AnimatedSprite2D sprite, SlotRenderConfig cfg)
    {
        // 3D 锚点投到 2D（Y 翻转）。SortOffset 通过 ZIndex 实现
        sprite.Position = new Vector2(
            cfg.AnchorOffset.X * XProjScale,
            cfg.AnchorOffset.Y * YProjScale);
        sprite.ZIndex = cfg.ZOrder;
        // 像素角色锚点底部对齐（脚在 0,0）
        sprite.Centered = true;
    }

    private void ApplySlot(AnimatedSprite2D sprite, SlotRenderConfig cfg, CharacterSlotResolution slotData)
    {
        // 复用既有 SpriteFrames（避免 GC 压力）
        SpriteFrames frames;
        if (slotData.Frames != null)
        {
            frames = slotData.Frames;
        }
        else if (slotData.Texture != null)
        {
            frames = sprite.SpriteFrames;
            if (frames == null || frames.GetAnimationNames().Length != 1 || !frames.HasAnimation("default"))
            {
                frames = new SpriteFrames();
                frames.SetAnimationLoop("default", true);
                frames.SetAnimationSpeed("default", 1.0);
            }
            else
            {
                frames.Clear("default");
            }
            frames.AddFrame("default", slotData.Texture);
        }
        else
        {
            sprite.Visible = false;
            return;
        }

        sprite.SpriteFrames = frames;
        sprite.Modulate = slotData.Modulate;

        // 让贴图底端对齐到该层的锚点位置（精灵脚踩锚点，向上绘制）
        float texHeight = cfg.DefaultSize.Y;
        if (frames.GetFrameCount("default") > 0)
        {
            var firstFrame = frames.GetFrameTexture("default", 0);
            if (firstFrame != null) texHeight = firstFrame.GetHeight();
        }
        sprite.Offset = new Vector2(0, -texHeight / 2.0f);
    }
}
