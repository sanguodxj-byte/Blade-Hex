// CharacterRenderNode.cs
// 角色渲染节点 — 封装单个角色的完整视觉表示
// 作为 Unit 的子节点，自动跟随位置
// 6层分部位渲染（头/头盔/身体/服装/手甲/武器），各有独立锚点
// 所有层统一使用 AnimatedSprite3D（单帧即静态，多帧即动画，无需销毁重建）
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.Data;
using BladeHex.View.Unit;
using BladeHex.View.Unit.Slots;
using static BladeHex.View.Unit.Slots.SlotConfigTable;

namespace BladeHex.Combat;

[GlobalClass]
public partial class CharacterRenderNode : Node3D
{
    // ========================================
    // 信号 — 供 Bus 中转或外部直接监听
    // ========================================

    [Signal] public delegate void HpUpdatedEventHandler(int currentHp, int maxHp);
    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void EquipmentSlotChangedEventHandler(int slot);

    // ========================================
    // 常量
    // ========================================

    private const float HpBarWidth = 60.0f;
    private const float HpBarHeight = 4.0f;
    private const float HpBarYGap = 15.0f;
    private const float HpLabelPixelSize = 3.0f;
    private const float HpLabelYGap = 20.0f;
    private const float SelectionRingRadius = 40.0f;
    private const float SelectionRingHeight = 5.0f;
    private const float TurnIndicatorYGap = 10.0f;
    private const float StatusIconSize = 16.0f;
    private const float StatusIconSpacing = 20.0f;
    private const float DeathFadeDuration = 1.0f;
    private const float HitFlashDuration = 0.5f;
    private const string LayerNodePrefix = "Layer_";

    // ========================================
    // 外部引用
    // ========================================

    public Unit? UnitRef { get; private set; }

    // ========================================
    // 渲染层 — key = slot int, value = AnimatedSprite3D
    // ========================================

    private readonly Dictionary<int, AnimatedSprite3D> _layers = new();
    private Node3D? _bodyRoot;

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
    private float _cachedPixelSize = 1.0f;
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

    // ========================================
    // 初始化 — 由 Unit 在 _ready 中调用
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

        BuildBodyRoot();
        BuildAllLayers();
        LoadEquipment();
        BuildHud();

        Visible = true;
    }

    // ========================================
    // 部位层构建
    // ========================================

    private void BuildBodyRoot()
    {
        _bodyRoot = new Node3D();
        _bodyRoot.Name = "BodyRoot";
        AddChild(_bodyRoot);
    }

    private void BuildAllLayers()
    {
        foreach (var cfg in GetAllSlotConfigs())
            BuildLayer(cfg);
        SetupBodyContent();
    }

    private void BuildLayer(SlotRenderConfig cfg)
    {
        var sprite = new AnimatedSprite3D();
        int slotIdx = cfg.SlotIndex;
        sprite.Name = LayerNodePrefix + GetSlotName(cfg.Slot);
        sprite.PixelSize = cfg.PixelSize;
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        sprite.Position = cfg.AnchorOffset with { Z = cfg.SortOffset };
        // AlphaCut = OpaquePrepass:让透明像素正确写 z-buffer,避免分层 sprite 互相穿透/被遮挡
        // 这是 HD-2D / 多 sprite 角色的标准做法
        sprite.AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass;
        sprite.Visible = false;
        _layers[slotIdx] = sprite;
        _bodyRoot!.AddChild(sprite);
    }

    /// <summary>为 BODY 层加载基础内容（角色本体）</summary>
    private void SetupBodyContent()
    {
        var data = UnitRef!.Data!;
        var sprite = _layers[(int)ItemData.EquipSlot.Body];

        // 通过 CharacterPresenter 解析 body 资源（与 2D / UI 共享同一套解析）
        var resolution = CharacterPresenter.Resolve(data, !UnitRef.UsingPrimaryWeapon);
        _cachedBodyHeight = resolution.BodyTextureHeight;

        if (resolution.Slots.TryGetValue(ItemData.EquipSlot.Body, out var bodySlot))
        {
            // 优先 SpriteFrames
            if (bodySlot.Frames != null)
            {
                sprite.SpriteFrames = bodySlot.Frames;
            }
            else if (bodySlot.Texture != null)
            {
                var frames = new SpriteFrames();
                frames.SetAnimationSpeed("default", 1.0);
                frames.SetAnimationLoop("default", true);
                frames.AddFrame("default", bodySlot.Texture);
                sprite.SpriteFrames = frames;
            }

            if (resolution.BodyIsPlaceholder)
            {
                // 程序化人体占位与装备 sprite 在同一像素坐标系下,不需要额外放大。
                sprite.Modulate = resolution.PlaceholderModulate;
            }

            sprite.Offset = new Vector2(0, _cachedBodyHeight / 2.0f);
            sprite.Play("default");
            sprite.Visible = true;
            _cachedPixelSize = sprite.PixelSize;
        }
    }

    // ========================================
    // 换装 API — 无销毁重建，直接替换 SpriteFrames / Texture
    // ========================================

    /// <summary>设置指定部位的外观纹理（复用已有 SpriteFrames，避免 GC 压力）</summary>
    public void SetSlotTexture(ItemData.EquipSlot slot, Texture2D texture)
    {
        int slotIdx = (int)slot;
        if (!_layers.TryGetValue(slotIdx, out var sprite)) return;

        var frames = sprite.SpriteFrames;
        // 如果已有 SpriteFrames 且只有 default 动画，复用之
        if (frames != null && frames.GetAnimationNames().Length == 1 && frames.HasAnimation("default"))
        {
            frames.Clear("default");
            frames.AddFrame("default", texture);
        }
        else
        {
            frames = new SpriteFrames();
            // SpriteFrames 构造时已含 "default" 动画
            frames.SetAnimationSpeed("default", 1.0);
            frames.SetAnimationLoop("default", true);
            frames.AddFrame("default", texture);
            sprite.SpriteFrames = frames;
        }

        var cfg = GetSlotConfig(slot);
        sprite.Offset = new Vector2(0, texture != null ? texture.GetHeight() / 2.0f : cfg.DefaultSize.Y / 2.0f);
        sprite.Play("default");
        sprite.Visible = texture != null;
        EmitSignal(SignalName.EquipmentSlotChanged, slotIdx);
    }

    /// <summary>设置指定部位的外观序列帧</summary>
    public void SetSlotFrames(ItemData.EquipSlot slot, SpriteFrames frames)
    {
        int slotIdx = (int)slot;
        if (!_layers.TryGetValue(slotIdx, out var sprite)) return;

        sprite.SpriteFrames = frames;

        var cfg = GetSlotConfig(slot);
        float texHeight = cfg.DefaultSize.Y;
        if (frames != null && frames.GetFrameCount("default") > 0)
        {
            var tex = frames.GetFrameTexture("default", 0);
            if (tex != null)
                texHeight = tex.GetHeight();
        }
        sprite.Offset = new Vector2(0, texHeight / 2.0f);
        sprite.Play("default");
        sprite.Visible = true;
        EmitSignal(SignalName.EquipmentSlotChanged, slotIdx);
    }

    /// <summary>清除指定部位外观（Body 层不可清除）</summary>
    public void ClearSlot(ItemData.EquipSlot slot)
    {
        if (!IsSlotSwappable(slot)) return;
        int slotIdx = (int)slot;
        if (_layers.TryGetValue(slotIdx, out var sprite))
            sprite.Visible = false;
    }

    /// <summary>获取指定部位的渲染节点</summary>
    public AnimatedSprite3D? GetLayer(ItemData.EquipSlot slot)
    {
        _layers.TryGetValue((int)slot, out var sprite);
        return sprite;
    }

    // ========================================
    // 装备加载 — 单一映射，Bus 通过此接口驱动
    // ========================================

    /// <summary>从 UnitData 装备槽位加载所有外观</summary>
    private void LoadEquipment()
    {
        var data = UnitRef!.Data!;
        // 通过 Presenter 统一解析（与 2D / UI 共享）
        var resolution = CharacterPresenter.Resolve(data, !UnitRef.UsingPrimaryWeapon);
        foreach (var (slot, slotData) in resolution.Slots)
        {
            if (slot == ItemData.EquipSlot.Body) continue; // body 已由 SetupBodyContent 处理
            if (!slotData.HasContent) continue;
            if (slotData.Frames != null)
                SetSlotFrames(slot, slotData.Frames);
            else if (slotData.Texture != null)
                SetSlotTexture(slot, slotData.Texture);
        }
    }

    /// <summary>全量刷新装备外观（重新从 UnitData 读取）</summary>
    public void RefreshAllEquipment()
    {
        if (UnitRef?.Data == null) return;
        // 先清除可换装层
        foreach (var kvp in _layers)
        {
            if (IsSlotSwappable((ItemData.EquipSlot)kvp.Key))
                kvp.Value.Visible = false;
        }
        LoadEquipment();
    }

    // ========================================
    // 动画 — 同步所有 AnimatedSprite3D 层
    // ========================================

    public void PlayAnimation(string animName)
    {
        if (_isDead) return;
        foreach (var sprite in _layers.Values)
        {
            if (!sprite.Visible) continue;
            if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation(animName))
                sprite.Play(animName);
            else if (sprite.SpriteFrames != null && sprite.SpriteFrames.HasAnimation("default"))
                sprite.Play("default");
        }
    }

    public void PlayHit()
    {
        if (_isDead) return;
        FlashAll(new Color(1.5f, 1.5f, 1.5f));
        PlayAnimation("hit");
        Schedule(HitFlashDuration, () => { if (!_isDead) PlayAnimation("default"); });
    }

    /// <summary>
    /// 攻击微动画 — 将 _bodyRoot 朝目标方向突进 20px 后弹回。
    /// direction 为攻击者指向目标的归一化方向向量（世界 XZ 平面）。
    /// </summary>
    public void PlayAttackLunge(Vector3 direction)
    {
        if (_isDead || _bodyRoot == null) return;
        // 20px 偏移量（像素单位 × pixelSize 转世界坐标）
        float lungeDistance = 20.0f * _cachedPixelSize;
        var offset = direction.Normalized() * lungeDistance;

        var tween = GetTree().CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_bodyRoot, "position", offset, 0.12f);
        tween.TweenProperty(_bodyRoot, "position", Vector3.Zero, 0.18f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>选中微动画 — 向上弹跳一下</summary>
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

    /// <summary>闪避微动画 — 向攻击者反方向后退一步再回来</summary>
    public void PlayDodgeBack(Vector3 attackerDirection)
    {
        if (_isDead || _bodyRoot == null) return;
        float dodgeDistance = 15.0f * _cachedPixelSize;
        // 反方向后退
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
    // HP 显示
    // ========================================

    public void UpdateHp(int current, int maximum)
    {
        _currentHp = current;
        _maxHp = Mathf.Max(1, maximum);
        // 旧 HP HUD 已由 UnitHealthBarComponent 接管,这里只更新内部状态 + 信号,不动视觉
        EmitSignal(SignalName.HpUpdated, _currentHp, _maxHp);
    }

    // ========================================
    // 状态效果图标
    // ========================================

    public void UpdateStatusEffects(Godot.Collections.Array<Godot.Collections.Dictionary> effects)
    {
        if (_statusContainer == null) return;
        foreach (var child in _statusContainer.GetChildren())
            child.QueueFree();
        for (int i = 0; i < effects.Count; i++)
        {
            var icon = MakeStatusIcon(effects[i], i);
            if (icon != null)
                _statusContainer.AddChild(icon);
        }
    }

    // 保留 Variant 签名供 Bus 从 兼容调用
    public void UpdateStatusEffects(Godot.Collections.Array effects)
    {
        if (_statusContainer == null) return;
        foreach (var child in _statusContainer.GetChildren())
            child.QueueFree();
        for (int i = 0; i < effects.Count; i++)
        {
            var icon = MakeStatusIconFromVariant(effects[i], i);
            if (icon != null)
                _statusContainer.AddChild(icon);
        }
    }

    // ========================================
    // 选中 / 回合
    // ========================================

    public void SetSelected(bool on)
    {
        _isSelected = on;
        if (_selectionRing != null)
        {
            _selectionRing.Visible = on;
            SetProcess(on);
        }
    }

    public void SetActiveTurn(bool on)
    {
        _isActiveTurn = on;
        if (_turnIndicator != null)
            _turnIndicator.Visible = on;
    }

    // ========================================
    // 朝向 — 6 方向中,2/3/4 = 西(向左),0/1/5 = 东(向右,默认)
    // ========================================

    private int _facing = 0;

    /// <summary>
    /// 设置角色朝向(0-5,六边形 6 方向)。
    /// 内部判定"朝右(默认)"还是"朝左",对所有 sprite 层执行 FlipH 与 X 锚点镜像,
    /// 让武器/盾自动出现在面向的那一侧。
    /// </summary>
    public void SetFacing(int facing)
    {
        _facing = ((facing % 6) + 6) % 6;
        bool facingLeft = _facing >= 2 && _facing <= 4;
        ApplyFacingToLayers(facingLeft);
    }

    private void ApplyFacingToLayers(bool facingLeft)
    {
        foreach (var kvp in _layers)
        {
            int slotIdx = kvp.Key;
            var sprite = kvp.Value;
            var cfg = SlotConfigTable.GetSlotConfig((ItemData.EquipSlot)slotIdx);

            // FlipH 实现整张贴图水平翻转(在 Billboard.FixedY 下对 Sprite3D 仍然有效)
            sprite.FlipH = facingLeft;

            // 锚点的 X 也要镜像 — 让"原本在右手位置的武器"跑到左手位置
            float ax = facingLeft ? -cfg.AnchorOffset.X : cfg.AnchorOffset.X;
            sprite.Position = new Vector3(ax, cfg.AnchorOffset.Y, cfg.SortOffset);
        }
    }

    // ========================================
    // HUD 构建 — 从 UnitHud.tscn 实例化
    // ========================================

    private static readonly PackedScene _hudScene = GD.Load<PackedScene>("res://BladeHexFrontend/src/View/Unit/UnitHud.tscn");

    private void BuildHud()
    {
        float topY = _cachedBodyHeight * _cachedPixelSize;

        // 实例化 HUD 场景
        var hudInstance = _hudScene.Instantiate<Node3D>();
        hudInstance.Name = "Hud";
        AddChild(hudInstance);

        // 通过 unique_name_in_owner 绑定节点
        _hpLabel = hudInstance.GetNode<Label3D>("%HpLabel");
        _hpBarBg = hudInstance.GetNode<MeshInstance3D>("%HpBarBg");
        _hpBarFg = hudInstance.GetNode<MeshInstance3D>("%HpBarFg");
        _statusContainer = hudInstance.GetNode<Node3D>("%StatusContainer");
        _selectionRing = hudInstance.GetNode<MeshInstance3D>("%SelectionRing");
        _turnIndicator = hudInstance.GetNode<MeshInstance3D>("%TurnIndicator");

        // 隐藏旧 HP 条/Label — 血量显示已由 UnitHealthBarComponent 接管(在角色下方)
        if (_hpLabel != null) _hpLabel.Visible = false;
        if (_hpBarBg != null) _hpBarBg.Visible = false;
        if (_hpBarFg != null) _hpBarFg.Visible = false;

        // 应用运行时动态参数（选中环、状态图标等仍需要位置）
        ApplyHudLayout(topY);
    }

    /// <summary>根据角色身高动态调整 HUD 元素位置和材质</summary>
    private void ApplyHudLayout(float topY)
    {
        // HP Label
        if (_hpLabel != null)
            _hpLabel.Position = new Vector3(0, topY + HpLabelYGap, 0);

        // HP Bar — 背景 + 前景
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

        // Status Container
        if (_statusContainer != null)
            _statusContainer.Position = new Vector3(0, topY + HpBarYGap + HpBarHeight + 5.0f, 0);

        // Selection Ring
        if (_selectionRing != null)
        {
            _selectionRing.Mesh = new CylinderMesh
            {
                TopRadius = SelectionRingRadius,
                BottomRadius = SelectionRingRadius,
                Height = SelectionRingHeight,
            };
            _selectionRing.Position = new Vector3(0, SelectionRingHeight / 2.0f, 0);
            var mat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(1.0f, 0.9f, 0.2f, 0.6f),
            };
            _selectionRing.MaterialOverride = mat;
        }

        // Turn Indicator
        if (_turnIndicator != null)
        {
            _turnIndicator.Mesh = new QuadMesh { Size = new Vector2(12, 12) };
            _turnIndicator.Position = new Vector3(0, topY + HpLabelYGap + HpLabelPixelSize * 15.0f + TurnIndicatorYGap, 0);
            var mat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                AlbedoColor = new Color(0.2f, 1.0f, 0.4f),
            };
            _turnIndicator.MaterialOverride = mat;
        }
    }

    private static StandardMaterial3D MakeHudMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 10,
            AlbedoColor = color,
        };
    }

    private Sprite3D MakeStatusIcon(Godot.Collections.Dictionary effect, int index)
    {
        var icon = new Sprite3D();
        var tex = new PlaceholderTexture2D();
        tex.Size = new Vector2(StatusIconSize, StatusIconSize);
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
        var tex = new PlaceholderTexture2D();
        tex.Size = new Vector2(StatusIconSize, StatusIconSize);
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

    // ========================================
    // 视觉效果 — 统一操作所有层
    // ========================================

    private void FlashAll(Color color)
    {
        foreach (var sprite in _layers.Values)
        {
            if (!sprite.Visible) continue;
            var orig = sprite.Modulate;
            sprite.Modulate = color;
            Schedule(0.1f, () =>            {
                if (GodotObject.IsInstanceValid(sprite))
                    sprite.Modulate = orig;
            });
        }
    }

    private void FadeAll(float duration)
    {
        foreach (var sprite in _layers.Values)
        {
            if (!sprite.Visible) continue;
            var tw = CreateTween();
            var capturedSprite = sprite;
            tw.TweenProperty(capturedSprite, "modulate:a", 0.0, duration);
            tw.TweenCallback(Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(capturedSprite))
                    capturedSprite.Visible = false;
            }));
        }
    }

    // ========================================
    // _process — 仅选中时运行
    // ========================================

    public override void _Process(double delta)
    {
        if (!_isSelected || _selectionRing == null)
        {
            SetProcess(false);
            return;
        }
        float t = (float)(Time.GetTicksMsec() / 1000.0) % 2.0f;
        _selectionRing.Scale = new Vector3(
            1.0f + 0.1f * Mathf.Sin(t * Mathf.Tau),
            1.0f,
            1.0f + 0.1f * Mathf.Sin(t * Mathf.Tau));
        if (_turnIndicator != null && _isActiveTurn)
        {
            float baseY = _cachedBodyHeight * _cachedPixelSize + HpLabelYGap + HpLabelPixelSize * 15.0f + TurnIndicatorYGap;
            _turnIndicator.Position = _turnIndicator.Position with { Y = baseY + 3.0f * Mathf.Sin(t * Mathf.Pi * 3.0f) };
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
