// CharacterRenderNode.cs
// 角色渲染节点 — 骨骼动画版本
// 使用 UpperBodySkeleton 骨骼系统替代旧的平铺 sprite 方案
// 保留所有公共 API 接口兼容性
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.Data;
using BladeHex.View.Unit;
using BladeHex.View.Unit.Skeleton;
using BladeHex.View.Unit.Skeleton.Editor;
using BladeHex.View.Unit.Slots;
using static BladeHex.View.Unit.Slots.SlotConfigTable;

namespace BladeHex.Combat;

[GlobalClass]
public partial class CharacterRenderNode : Node3D
{
    // ========================================
    // 信号
    // ========================================

    [Signal] public delegate void HpUpdatedEventHandler(int currentHp, int maxHp);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void EquipmentSlotChangedEventHandler(int slot);

    // ========================================
    // 常量
    // ========================================

    private const float SelectionRingRadius = 40.0f;
    private const float SelectionRingHeight = 5.0f;
    private const float HpLabelYGap = 20.0f;
    private const float HpLabelPixelSize = 3.0f;
    private const float HpBarYGap = 15.0f;
    private const float HpBarWidth = 60.0f;
    private const float HpBarHeight = 4.0f;
    private const float TurnIndicatorYGap = 10.0f;
    private const float StatusIconSize = 16.0f;
    private const float StatusIconSpacing = 20.0f;
    private const float DeathFadeDuration = 1.0f;
    private const float HitFlashDuration = 0.5f;

    // ========================================
    // 外部引用
    // ========================================

    public Unit? UnitRef { get; private set; }

    // ========================================
    // 骨骼系统
    // ========================================

    private UpperBodySkeleton? _skeleton;
    private Node3D? _bodyRoot;
    private Sprite3D? _basePedestal;
    private static Texture2D? _basePedestalTexture;

    // 自定义动画播放状态
    private AnimClip? _currentClip;
    private float _animTime;
    private bool _animPlaying;
    private WeaponAnimCategory _weaponCategory = WeaponAnimCategory.Slash;
    private string _currentAnimName = "idle";

    // HUD 元素
    private Label3D? _hpLabel;
    private MeshInstance3D? _hpBarBg;
    private MeshInstance3D? _hpBarFg;
    private Node3D? _statusContainer;
    private MeshInstance3D? _selectionRing;
    private MeshInstance3D? _turnIndicator;

    // 渲染状态
    private int _currentHp;
    private int _maxHp = 1;
    private float _cachedBodyHeight = 120.0f;
    private float _cachedPixelSize = 1.5f;
    private bool _isSelected;
    private bool _isActiveTurn;
    private bool _isDead;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        Visible = false;
    }

    public override void _Process(double delta)
    {
        // 骨骼动画驱动
        if (_animPlaying && _currentClip != null)
        {
            _animTime += (float)delta;
            if (_currentClip.Loop)
            {
                if (_currentClip.Duration > 0)
                    _animTime %= _currentClip.Duration;
            }
            else if (_animTime >= _currentClip.Duration)
            {
                _animTime = _currentClip.Duration;
                _animPlaying = false;
                // 非循环动画结束后回到 idle
                PlayAnimation("idle");
                return;
            }

            ApplyAnimFrame(_animTime);
        }

        // 选中环脉动
        if (_isSelected && _selectionRing != null)
        {
            float t = (float)(Time.GetTicksMsec() / 1000.0) % 2.0f;
            _selectionRing.Scale = new Vector3(
                1.0f + 0.1f * Mathf.Sin(t * Mathf.Tau), 1.0f,
                1.0f + 0.1f * Mathf.Sin(t * Mathf.Tau));
            if (_turnIndicator != null && _isActiveTurn)
            {
                float baseY = _cachedBodyHeight * _cachedPixelSize + HpLabelYGap + HpLabelPixelSize * 15.0f + TurnIndicatorYGap;
                _turnIndicator.Position = _turnIndicator.Position with { Y = baseY + 3.0f * Mathf.Sin(t * Mathf.Pi * 3.0f) };
            }
        }
    }

    // ========================================
    // 初始化
    // ========================================

    public void Setup(Unit unit)
    {
        UnitRef = unit;
        if (unit.Data == null)
        {
            GD.PushWarning("CharacterRenderNode.Setup: Unit.Data 为空");
            return;
        }

        _currentHp = unit.CurrentHp;
        _maxHp = unit.GetMaxHp();
        _isDead = _currentHp <= 0;

        // 确定武器动画类别
        var weapon = unit.Data.PrimaryMainHand as WeaponData;
        if (weapon != null)
            _weaponCategory = WeaponAnimCategoryUtil.FromSubtype(weapon.Subtype);

        BuildBodyRoot();
        BuildSkeleton();
        LoadEquipment();
        ApplyEquipmentOffsets();
        BuildHud();

        Visible = true;
        SetProcess(false); // 默认不跑 _Process，播放动画或选中时开启

        // 自动播放 idle
        PlayAnimation("idle");
    }

    private void BuildBodyRoot()
    {
        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);

        // 底座
        var tex = LoadBasePedestalTexture();
        if (tex != null)
        {
            _basePedestal = new Sprite3D
            {
                Name = "BasePedestal",
                Texture = tex,
                PixelSize = 0.5f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                RotationDegrees = new Vector3(-90, 0, 0),
                Position = new Vector3(0, 2.0f, 0),
                AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
                SortingOffset = 1.0f,
            };
            _bodyRoot.AddChild(_basePedestal);
        }
    }

    private static Texture2D? LoadBasePedestalTexture()
    {
        if (_basePedestalTexture != null) return _basePedestalTexture;
        _basePedestalTexture = GD.Load<Texture2D>("res://assets/generated_ui_icons/unit_base_steel.png");
        return _basePedestalTexture;
    }

    private void BuildSkeleton()
    {
        var config = BoneConfig.Standard; // TODO: 根据种族/体型选择
        _skeleton = new UpperBodySkeleton();
        _skeleton.Build(_bodyRoot!, config);
        _cachedPixelSize = config.PixelSize;
        _cachedBodyHeight = config.TorsoHeight + config.HeadOffsetY;
    }

    // ========================================
    // 换装 API
    // ========================================

    public void SetSlotTexture(ItemData.EquipSlot slot, Texture2D texture)
    {
        var sprite = GetSpriteForSlot(slot);
        if (sprite == null) return;

        if (texture != null)
        {
            sprite.Texture = texture;
            sprite.Visible = true;
            sprite.Scale = Vector2.One; // 1:1 像素，不缩放
        }
        else
        {
            sprite.Visible = false;
        }
        EmitSignal(SignalName.EquipmentSlotChanged, (int)slot);
    }

    public void SetSlotFrames(ItemData.EquipSlot slot, SpriteFrames frames)
    {
        // 取第一帧作为贴图
        if (frames == null || frames.GetFrameCount("default") == 0) return;
        var tex = frames.GetFrameTexture("default", 0);
        if (tex != null)
            SetSlotTexture(slot, tex);
    }

    public void ClearSlot(ItemData.EquipSlot slot)
    {
        if (!IsSlotSwappable(slot)) return;
        var sprite = GetSpriteForSlot(slot);
        if (sprite != null) sprite.Visible = false;
    }

    public AnimatedSprite3D? GetLayer(ItemData.EquipSlot slot)
    {
        // 向后兼容：返回 null
        return null;
    }

    private Sprite2D? GetSpriteForSlot(ItemData.EquipSlot slot)
    {
        if (_skeleton == null) return null;
        return _skeleton.GetSlotSprite(slot);
    }

    private void LoadEquipment()
    {
        var data = UnitRef!.Data!;
        var resolution = CharacterPresenter.Resolve(data, !UnitRef.UsingPrimaryWeapon);
        _cachedBodyHeight = resolution.BodyTextureHeight;

        foreach (var (slot, slotData) in resolution.Slots)
        {
            if (!slotData.HasContent) continue;
            var tex = slotData.Texture ?? slotData.Frames?.GetFrameTexture("default", 0);
            if (tex != null)
                SetSlotTexture(slot, tex);
        }

        // Body 占位符着色
        if (resolution.BodyIsPlaceholder && _skeleton != null)
            _skeleton.SpriteBody.Modulate = resolution.PlaceholderModulate;
    }

    public void RefreshAllEquipment()
    {
        if (UnitRef?.Data == null || _skeleton == null) return;
        _skeleton.SpriteBody.Visible = false;
        _skeleton.SpriteCostume.Visible = false;
        _skeleton.SpriteHead.Visible = false;
        _skeleton.SpriteHelmet.Visible = false;
        _skeleton.SpriteHands.Visible = false;
        _skeleton.SpriteWeapon.Visible = false;
        LoadEquipment();
        ApplyEquipmentOffsets();
    }

    private void ApplyEquipmentOffsets()
    {
        if (_skeleton == null) return;
        foreach (var slot in EquipmentOffsetConfig.EditableSlots)
        {
            EquipmentOffsetConfig config;
            if (slot == ItemData.EquipSlot.Weapon)
                config = EquipmentOffsetConfig.GetWeapon(_weaponCategory, _currentAnimName);
            else
                config = EquipmentOffsetConfig.Get(slot);

            if (config.OffsetX == 0 && config.OffsetY == 0
                && Mathf.IsEqualApprox(config.Scale, 1.0f)
                && Mathf.IsEqualApprox(config.Rotation, 0f))
                continue; // 全默认值，跳过

            var sprite = _skeleton.GetSlotSprite(slot);
            if (sprite == null || !sprite.Visible) continue;

            sprite.Offset = new Vector2(config.OffsetX, config.OffsetY);

            // 旋转（武器）
            if (EquipmentOffsetConfig.SupportsRotation(slot))
            {
                sprite.RotationDegrees = config.Rotation;
            }

            // 缩放：用户手动设置的缩放倍率
            if (!Mathf.IsEqualApprox(config.Scale, 1.0f))
            {
                sprite.Scale = new Vector2(config.Scale, config.Scale);
            }

            // 水平翻转
            if (config.FlipH)
            {
                sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
            }
        }
    }

    // ========================================
    // 动画
    // ========================================

    public void PlayAnimation(string animName)
    {
        if (_isDead && animName != "die") return;

        // 映射旧动画名
        string resolved = animName switch
        {
            "default" => "idle",
            "attack" => _weaponCategory is WeaponAnimCategory.Bow or WeaponAnimCategory.Crossbow or WeaponAnimCategory.Throw
                ? "attack_ranged" : "attack_melee",
            _ => animName,
        };

        _currentAnimName = resolved;

        // 尝试加载自定义动画 JSON
        var clip = AnimClipSerializer.Load(resolved, _weaponCategory);
        if (clip != null)
        {
            _currentClip = clip;
            _animTime = 0;
            _animPlaying = true;
            SetProcess(true);
            return;
        }

        // 回退：无自定义动画时不播放（AnimPlayer 已移除）
        // 新方案完全依赖 AnimClip JSON 系统
        _currentClip = null;
        _animPlaying = false;
        SetProcess(_isSelected);
    }

    private void ApplyAnimFrame(float time)
    {
        if (_currentClip == null || _skeleton == null) return;
        var pose = AnimClipInterpolator.Sample(_currentClip, time);

        foreach (var (boneName, p) in pose)
        {
            Node2D? node = boneName switch
            {
                "Torso" => _skeleton.BoneTorso,
                "Head" => _skeleton.BoneHead,
                "ArmL" => _skeleton.BoneArmL,
                "ArmR" => _skeleton.BoneArmR,
                "ForearmL" => _skeleton.BoneForearmL,
                "ForearmR" => _skeleton.BoneForearmR,
                "Weapon" => _skeleton.BoneWeapon,
                _ => null,
            };
            if (node == null) continue;

            node.RotationDegrees = p.RotationZ;

            if (boneName == "Torso")
                node.Position = new Vector2(0, -p.PositionY); // 2D: Y 向上为负
        }

        // 叠加部件偏移配置（含武器）
        ApplyEquipmentOffsets();
    }

    public void PlayHit()
    {
        if (_isDead) return;
        FlashAll(new Color(1.5f, 1.5f, 1.5f));
        PlayAnimation("hit");
        Schedule(HitFlashDuration, () => { if (!_isDead) PlayAnimation("idle"); });
    }

    public void PlayAttackLunge(Vector3 direction)
    {
        if (_isDead || _bodyRoot == null) return;
        float lungeDistance = 20.0f * _cachedPixelSize;
        var offset = direction.Normalized() * lungeDistance;

        var tween = GetTree().CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_bodyRoot, "position", offset, 0.12f);
        tween.TweenProperty(_bodyRoot, "position", Vector3.Zero, 0.18f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }

    public void PlaySelectBounce()
    {
        if (_isDead || _bodyRoot == null) return;
        float bounceHeight = 12.0f * _cachedPixelSize;
        var up = new Vector3(0, bounceHeight, 0);

        var tween = GetTree().CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_bodyRoot, "position", up, 0.1f);
        tween.TweenProperty(_bodyRoot, "position", Vector3.Zero, 0.15f)
            .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
    }

    public void PlayDodgeBack(Vector3 attackerDirection)
    {
        if (_isDead || _bodyRoot == null) return;
        float dodgeDistance = 15.0f * _cachedPixelSize;
        var offset = -attackerDirection.Normalized() * dodgeDistance;
        offset.Y = 0;

        var tween = GetTree().CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_bodyRoot, "position", offset, 0.1f);
        tween.TweenProperty(_bodyRoot, "position", Vector3.Zero, 0.25f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }

    public void PlayDeath()
    {
        _isDead = true;
        PlayAnimation("die");
        FadeAll(DeathFadeDuration);
        if (_hpBarBg != null) _hpBarBg.Visible = false;
        if (_hpBarFg != null) _hpBarFg.Visible = false;
        if (_hpLabel != null) _hpLabel.Visible = false;
        if (_statusContainer != null) _statusContainer.Visible = false;
        EmitSignal(SignalName.Died);
    }

    // ========================================
    // HP / 状态
    // ========================================

    public void UpdateHp(int current, int maximum)
    {
        _currentHp = current;
        _maxHp = Mathf.Max(1, maximum);
        EmitSignal(SignalName.HpUpdated, _currentHp, _maxHp);
    }

    public void UpdateStatusEffects(Godot.Collections.Array<Godot.Collections.Dictionary> effects)
    {
        if (_statusContainer == null) return;
        foreach (var child in _statusContainer.GetChildren()) child.QueueFree();
        for (int i = 0; i < effects.Count; i++)
            _statusContainer.AddChild(MakeStatusIcon(effects[i], i));
    }

    public void UpdateStatusEffects(Godot.Collections.Array effects)
    {
        if (_statusContainer == null) return;
        foreach (var child in _statusContainer.GetChildren()) child.QueueFree();
        for (int i = 0; i < effects.Count; i++)
            _statusContainer.AddChild(MakeStatusIconFromVariant(effects[i], i));
    }

    // ========================================
    // 选中 / 回合
    // ========================================

    public void SetSelected(bool on)
    {
        _isSelected = on;
        if (_selectionRing != null) _selectionRing.Visible = on;
        SetProcess(on || _animPlaying);
    }

    public void SetActiveTurn(bool on)
    {
        _isActiveTurn = on;
        if (_turnIndicator != null) _turnIndicator.Visible = on;
    }

    // ========================================
    // 朝向
    // ========================================

    private int _facing = 0;

    public void SetFacing(int facing)
    {
        _facing = ((facing % 6) + 6) % 6;
        bool facingLeft = _facing >= 2 && _facing <= 4;
        _skeleton?.SetFacing(facingLeft);
    }

    // ========================================
    // HUD
    // ========================================

    private static readonly PackedScene _hudScene = GD.Load<PackedScene>("res://BladeHexFrontend/src/View/Unit/UnitHud.tscn");

    private void BuildHud()
    {
        float topY = _cachedBodyHeight * _cachedPixelSize;

        var hudInstance = _hudScene.Instantiate<Node3D>();
        hudInstance.Name = "Hud";
        AddChild(hudInstance);

        _hpLabel = hudInstance.GetNode<Label3D>("%HpLabel");
        _hpBarBg = hudInstance.GetNode<MeshInstance3D>("%HpBarBg");
        _hpBarFg = hudInstance.GetNode<MeshInstance3D>("%HpBarFg");
        _statusContainer = hudInstance.GetNode<Node3D>("%StatusContainer");
        _selectionRing = hudInstance.GetNode<MeshInstance3D>("%SelectionRing");
        _turnIndicator = hudInstance.GetNode<MeshInstance3D>("%TurnIndicator");

        if (_hpLabel != null) _hpLabel.Visible = false;
        if (_hpBarBg != null) _hpBarBg.Visible = false;
        if (_hpBarFg != null) _hpBarFg.Visible = false;

        ApplyHudLayout(topY);
    }

    private void ApplyHudLayout(float topY)
    {
        if (_hpLabel != null) _hpLabel.Position = new Vector3(0, topY + HpLabelYGap, 0);

        float barY = topY + HpBarYGap;
        if (_hpBarBg != null)
        {
            _hpBarBg.Mesh = new QuadMesh { Size = new Vector2(HpBarWidth, HpBarHeight) };
            _hpBarBg.Position = new Vector3(0, barY, 0);
            _hpBarBg.MaterialOverride = MakeHudMaterial(new Color(0.2f, 0.2f, 0.2f, 0.8f));
        }
        if (_hpBarFg != null)
        {
            _hpBarFg.Mesh = new QuadMesh { Size = new Vector2(HpBarWidth, HpBarHeight) };
            _hpBarFg.Position = new Vector3(0, barY, -0.1f);
            _hpBarFg.MaterialOverride = MakeHudMaterial(new Color(0.2f, 0.8f, 0.2f));
        }
        if (_statusContainer != null)
            _statusContainer.Position = new Vector3(0, topY + HpBarYGap + HpBarHeight + 5.0f, 0);
        if (_selectionRing != null)
        {
            _selectionRing.Mesh = new CylinderMesh { TopRadius = SelectionRingRadius, BottomRadius = SelectionRingRadius, Height = SelectionRingHeight };
            _selectionRing.Position = new Vector3(0, SelectionRingHeight / 2.0f, 0);
            _selectionRing.MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1.0f, 0.9f, 0.2f, 0.6f),
            };
        }
        if (_turnIndicator != null)
        {
            _turnIndicator.Mesh = new QuadMesh { Size = new Vector2(12, 12) };
            _turnIndicator.Position = new Vector3(0, topY + HpLabelYGap + HpLabelPixelSize * 15.0f + TurnIndicatorYGap, 0);
            _turnIndicator.MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                AlbedoColor = new Color(0.2f, 1.0f, 0.4f),
            };
        }
    }

    // ========================================
    // 视觉效果
    // ========================================

    private void FlashAll(Color color)
    {
        if (_skeleton == null) return;
        var sprites = new Sprite2D[] { _skeleton.SpriteBody, _skeleton.SpriteCostume, _skeleton.SpriteHead,
            _skeleton.SpriteHelmet, _skeleton.SpriteHands, _skeleton.SpriteWeapon, _skeleton.SpriteShield };
        foreach (var sprite in sprites)
        {
            if (!sprite.Visible) continue;
            var orig = sprite.Modulate;
            sprite.Modulate = color;
            Schedule(0.1f, () => { if (GodotObject.IsInstanceValid(sprite)) sprite.Modulate = orig; });
        }
    }

    private void FadeAll(float duration)
    {
        if (_skeleton == null) return;
        var sprites = new Sprite2D[] { _skeleton.SpriteBody, _skeleton.SpriteCostume, _skeleton.SpriteHead,
            _skeleton.SpriteHelmet, _skeleton.SpriteHands, _skeleton.SpriteWeapon, _skeleton.SpriteShield };
        foreach (var sprite in sprites)
        {
            if (!sprite.Visible) continue;
            var tw = CreateTween();
            var s = sprite;
            tw.TweenProperty(s, "modulate:a", 0.0, duration);
            tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(s)) s.Visible = false; }));
        }
    }

    // ========================================
    // 工具
    // ========================================

    private void Schedule(float delay, System.Action callback)
    {
        var timer = GetTree().CreateTimer(delay);
        timer.Timeout += callback;
    }

    private static StandardMaterial3D MakeHudMaterial(Color color) => new()
    {
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
        RenderPriority = 10,
        AlbedoColor = color,
    };

    private Sprite3D MakeStatusIcon(Godot.Collections.Dictionary effect, int index)
    {
        var icon = new Sprite3D();
        var tex = new PlaceholderTexture2D { Size = new Vector2(StatusIconSize, StatusIconSize) };
        icon.Texture = tex;
        icon.PixelSize = 0.5f;
        icon.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        icon.Offset = new Vector2(0, StatusIconSize / 2.0f);
        icon.Position = new Vector3((index - 2.0f) * StatusIconSpacing, 0, 0);
        var id = effect.TryGetValue("id", out var idVar) ? idVar.AsString() : "";
        icon.Modulate = StatusColor(id);
        return icon;
    }

    private Sprite3D MakeStatusIconFromVariant(Variant effectVar, int index)
    {
        var icon = new Sprite3D();
        var tex = new PlaceholderTexture2D { Size = new Vector2(StatusIconSize, StatusIconSize) };
        icon.Texture = tex;
        icon.PixelSize = 0.5f;
        icon.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        icon.Offset = new Vector2(0, StatusIconSize / 2.0f);
        icon.Position = new Vector3((index - 2.0f) * StatusIconSpacing, 0, 0);
        if (effectVar.VariantType == Variant.Type.Dictionary)
        {
            var dict = effectVar.AsGodotDictionary();
            var id = dict.TryGetValue("id", out var idVar) ? idVar.AsString() : "";
            icon.Modulate = StatusColor(id);
        }
        return icon;
    }

    private static Color StatusColor(string id) => id switch
    {
        "burning" => new Color(1.0f, 0.4f, 0.1f),
        "freeze" or "frozen" => new Color(0.3f, 0.6f, 1.0f),
        "poison" or "poisoned" => new Color(0.4f, 0.8f, 0.2f),
        "entangled" or "web" => new Color(0.6f, 0.4f, 0.2f),
        "stun" or "stunned" => new Color(0.9f, 0.9f, 0.2f),
        "charmed" => new Color(1.0f, 0.5f, 0.8f),
        "bleed" or "bleeding" => new Color(0.8f, 0.1f, 0.1f),
        "shield" or "magic_shield" => new Color(0.3f, 0.5f, 1.0f),
        "blessing" => new Color(1.0f, 1.0f, 0.7f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };
}
