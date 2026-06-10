



using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.AssetSystem;
using BladeHex.View.Data;
using BladeHex.View.Unit;
using BladeHex.View.Unit.Skeleton;
using BladeHex.View.Unit.Skeleton.Editor;
using BladeHex.View.Unit.Slots;
using static BladeHex.View.Unit.Slots.SlotConfigTable;
using CreatureTex = BladeHex.View.Unit.CreatureTextureConfig;

namespace BladeHex.Combat;

[GlobalClass]
public partial class CharacterRenderNode : Node3D
{




    [Signal] public delegate void DiedEventHandler();
    [Signal] public delegate void EquipmentSlotChangedEventHandler(int slot);





    private const float DeathFadeDuration = 1.0f;
    private const float HitFlashDuration = 0.5f;





    public Unit? UnitRef { get; private set; }





    private UpperBodySkeleton? _skeleton;
    private Node3D? _bodyRoot;
    private Sprite3D? _basePedestal;
    private Sprite3D? _blobShadow;
    private static Texture2D? _basePedestalTexture;
    private static Texture2D? _blobShadowTexture;
    private AnimationPlayer? _animPlayer;


    private WeaponAnimCategory _weaponCategory = WeaponAnimCategory.Slash;
    private string _currentAnimName = "idle";


    private UnitHudController? _hud;


    private float _cachedBodyHeight = 120.0f;
    private float _cachedPixelSize = 1.5f;
    private bool _isDead;


    private bool _isCreatureMode;
    private Sprite3D? _creatureSprite;





    public override void _Ready()
    {
        Visible = false;
    }




    public void Setup(Unit unit)
    {
        UnitRef = unit;
        if (unit.Data == null)
        {
            GD.PushWarning("CharacterRenderNode.Setup: Unit.Data is null.");
            return;
        }

        _isDead = unit.CurrentHp <= 0;


        if (TrySetupCreatureSprite(unit))
        {
            BuildHud();
            Visible = true;
            return;
        }


        var weapon = unit.Data.PrimaryMainHand as WeaponData;
        if (weapon != null)
            _weaponCategory = WeaponAnimCategoryUtil.FromSubtype(weapon.Subtype);

        BuildBodyRoot();
        BuildSkeleton();
        LoadEquipment();
        ApplyEquipmentOffsets();
        BuildHud();

        Visible = true;


        PlayAnimation("idle");
    }




    private bool TrySetupCreatureSprite(Unit unit)
    {
        if (unit.Data == null) return false;


        if (!CreatureTex.IsCreature(unit.Data))
            return false;

        var texture = CreatureTex.TryLoadSprite(unit.Data);
        bool isPlaceholder = false;
        if (texture == null)
        {
            texture = CreatureTex.GeneratePlaceholder(unit.Data);
            isPlaceholder = true;
            GD.Print($"[CharacterRenderNode] Creature placeholder mode: {unit.Data.UnitName} ({CreatureTex.GetTypeName(unit.Data.enemyType)})");
        }
        else
        {
            GD.Print($"[CharacterRenderNode] Creature sprite mode: {unit.Data.UnitName}");
        }


        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);


        var pedestalTex = LoadBasePedestalTexture();
        if (pedestalTex != null)
        {
            _basePedestal = new Sprite3D
            {
                Name = "BasePedestal",
                Texture = pedestalTex,
                PixelSize = 0.5f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                RotationDegrees = new Vector3(-90, 0, 0),
                Position = new Vector3(0, 2.0f, 0),
                AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
                SortingOffset = 1.0f,
            };
            _bodyRoot.AddChild(_basePedestal);
        }




        var (fw, fh) = UnitFootprint.GetSize(unit.Data);
        float scaleFactor = Mathf.Max(fw, fh);

        float targetWorldHeight = 120.0f * scaleFactor;
        float spritePixelSize = targetWorldHeight / texture.GetHeight();
        float spriteWorldHeight = texture.GetHeight() * spritePixelSize;

        _creatureSprite = new Sprite3D
        {
            Name = "CreatureSprite",
            Texture = texture,
            PixelSize = spritePixelSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            Position = new Vector3(0, spriteWorldHeight * 0.5f + 2.0f, 0),
            AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
            Shaded = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };


        if (isPlaceholder)
        {
            _creatureSprite.Modulate = Colors.White;
        }

        _bodyRoot.AddChild(_creatureSprite);

        _cachedPixelSize = spritePixelSize;
        _cachedBodyHeight = spriteWorldHeight * 0.5f + 2.0f;
        _isCreatureMode = true;

        return true;
    }

    private void BuildBodyRoot()
    {
        _bodyRoot = new Node3D { Name = "BodyRoot" };
        AddChild(_bodyRoot);




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



    private void EnsureBlobShadow(float scaleFactor)
    {
        if (_blobShadow != null) return;

        var tex = LoadBlobShadowTexture();
        if (tex == null) return;


        float targetWorldDiameter = 80.0f * scaleFactor;
        float pixelSize = targetWorldDiameter / tex.GetWidth();

        _blobShadow = new Sprite3D
        {
            Name = "BlobShadow",
            Texture = tex,
            PixelSize = pixelSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            RotationDegrees = new Vector3(-90, 0, 0),
            Position = new Vector3(0, 1.0f, 0),
            Modulate = new Color(0, 0, 0, 0.45f),
            Shaded = false,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            SortingOffset = -1.0f,
        };
        _bodyRoot!.AddChild(_blobShadow);
    }


    private static Texture2D? LoadBlobShadowTexture()
    {
        if (_blobShadowTexture != null) return _blobShadowTexture;

        const int size = 128;
        float center = size / 2.0f;
        float maxR = center;
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) - center;
                float dy = (y + 0.5f) - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                float alpha = dist >= 1.0f ? 0.0f : 1.0f - Mathf.SmoothStep(0.0f, 1.0f, dist);

                img.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        _blobShadowTexture = ImageTexture.CreateFromImage(img);
        return _blobShadowTexture;
    }

    private static Texture2D? LoadBasePedestalTexture()
    {
        if (_basePedestalTexture != null) return _basePedestalTexture;
        _basePedestalTexture = TextureAssetResolver.LoadIcon(
            "unit_base_steel",
            "res://assets/ui_icons/unit_base_steel.png");
        return _basePedestalTexture;
    }

    private void BuildSkeleton()
    {
        var config = BoneConfig.Standard;
        _skeleton = new UpperBodySkeleton();
        _skeleton.Build(_bodyRoot!, config);
        _cachedPixelSize = config.PixelSize;
        _cachedBodyHeight = config.TorsoHeight + config.HeadOffsetY;


        _animPlayer = new AnimationPlayer { Name = "SkeletonAnimPlayer" };
        _skeleton.BoneRoot.AddChild(_animPlayer);


        var animLib = new AnimationLibrary();
        _animPlayer.AddAnimationLibrary("", animLib);


        _animPlayer.AnimationFinished += (StringName animName) =>
        {
            if (animName != "idle" && animName != "die" && !_isDead)
                PlayAnimation("idle");
        };
    }





    public void SetSlotTexture(ItemData.EquipSlot slot, Texture2D texture)
    {

        if (slot == ItemData.EquipSlot.Hands)
        {
            if (_skeleton != null)
            {
                _skeleton.SpriteHands.Visible = false;
                if (_skeleton.SpriteHandsL != null)
                    _skeleton.SpriteHandsL.Visible = false;
            }
            return;
        }

        var sprite = GetSpriteForSlot(slot);
        if (sprite == null) return;

        if (texture != null)
        {
            sprite.Texture = texture;
            sprite.Visible = true;
            sprite.Scale = Vector2.One;


            if (slot == ItemData.EquipSlot.Hands && _skeleton != null && _skeleton.SpriteHandsL != null)
            {
                _skeleton.SpriteHandsL.Texture = texture;
                _skeleton.SpriteHandsL.Visible = true;
                _skeleton.SpriteHandsL.Scale = Vector2.One;
            }
        }
        else
        {
            sprite.Visible = false;
            if (slot == ItemData.EquipSlot.Hands && _skeleton != null && _skeleton.SpriteHandsL != null)
            {
                _skeleton.SpriteHandsL.Visible = false;
            }
        }
        EmitSignal(SignalName.EquipmentSlotChanged, (int)slot);
    }

    public void SetSlotFrames(ItemData.EquipSlot slot, SpriteFrames frames)
    {

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
        if (_skeleton.SpriteHandsL != null) _skeleton.SpriteHandsL.Visible = false;
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
                continue;

            var sprite = _skeleton!.GetSlotSprite(slot);
            if (sprite == null || !sprite.Visible) continue;

            sprite.Offset = new Vector2(config.OffsetX, config.OffsetY);


            if (EquipmentOffsetConfig.SupportsRotation(slot))
            {
                sprite.RotationDegrees = config.Rotation;
            }


            if (!Mathf.IsEqualApprox(config.Scale, 1.0f))
            {
                sprite.Scale = new Vector2(config.Scale, config.Scale);
            }


            if (config.FlipH)
            {
                sprite.Scale = new Vector2(-sprite.Scale.X, sprite.Scale.Y);
            }


            if (slot == ItemData.EquipSlot.Hands && _skeleton != null && _skeleton.SpriteHandsL != null)
            {
                _skeleton.SpriteHandsL.Offset = sprite.Offset;
                _skeleton.SpriteHandsL.Scale = sprite.Scale;
            }
        }


        if (_skeleton!.SpriteBody != null)
        {
            var bodyCfg = EquipmentOffsetConfig.Get(ItemData.EquipSlot.Body);
            _skeleton.SpriteBody.Scale = new Vector2(bodyCfg.Scale, bodyCfg.Scale);
            _skeleton.SpriteBody.Offset = new Vector2(bodyCfg.OffsetX, bodyCfg.OffsetY);
        }
        if (_skeleton.SpriteHead != null)
        {
            var headCfg = EquipmentOffsetConfig.Get(ItemData.EquipSlot.Head);
            _skeleton.SpriteHead.Scale = new Vector2(headCfg.Scale, headCfg.Scale);
            _skeleton.SpriteHead.Offset = new Vector2(headCfg.OffsetX, headCfg.OffsetY);
        }
    }





    public void PlayAnimation(string animName)
    {
        if (_isDead && animName != "die") return;
        if (_isCreatureMode) return;


        string resolved = animName switch
        {
            "default" => "idle",
            "attack" => _weaponCategory is WeaponAnimCategory.Bow or WeaponAnimCategory.Crossbow or WeaponAnimCategory.Throw
                ? "attack_ranged" : "attack_melee",
            _ => animName,
        };

        _currentAnimName = resolved;


        var clip = AnimClipSerializer.Load(resolved, _weaponCategory);
        if (clip != null && _animPlayer != null)
        {
            var animLib = _animPlayer.GetAnimationLibrary("");
            if (animLib != null && !animLib.HasAnimation(resolved))
            {
                var godotAnim = SkeletonAnimationLibrary.ConvertClipToAnimation(clip, _skeleton?.Config);
                if (godotAnim != null)
                    animLib.AddAnimation(resolved, godotAnim);
            }

            _animPlayer.Play(resolved);
            return;
        }


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
        _hud?.HideAll();
        EmitSignal(SignalName.Died);
    }




    public void UpdateHp(int current, int maximum) => _hud?.UpdateHp(current, maximum);

    public void UpdateStatusEffects(Godot.Collections.Array<Godot.Collections.Dictionary> effects)
    {
        var arr = new Godot.Collections.Array();
        foreach (var e in effects) arr.Add(e);
        _hud?.UpdateStatusEffects(arr);
    }

    public void UpdateStatusEffects(Godot.Collections.Array effects) => _hud?.UpdateStatusEffects(effects);





    public void SetSelected(bool on) => _hud?.SetSelected(on);

    public void SetActiveTurn(bool on) => _hud?.SetActiveTurn(on);





    private int _facing = 0;

    public void SetFacing(int facing)
    {
        _facing = ((facing % 6) + 6) % 6;
        bool facingLeft = _facing >= 2 && _facing <= 4;
        if (_isCreatureMode)
        {

            if (_creatureSprite != null)
                _creatureSprite.FlipH = facingLeft;
            return;
        }
        _skeleton?.SetFacing(facingLeft);



        if (_skeleton?.Billboard != null)
        {
            var s = _skeleton.Billboard.Scale;
            float targetX = facingLeft ? 1f : -1f;
            if (Mathf.Sign(s.X) != Mathf.Sign(targetX))
                _skeleton.Billboard.Scale = new Vector3(targetX, s.Y, s.Z);
        }
    }





    private void BuildHud()
    {
        _hud = new UnitHudController { Name = "HudController" };
        AddChild(_hud);
        _hud.Build(_cachedBodyHeight, _cachedPixelSize);

        _hud.HpChanged += (current, max) =>
        {
            if (UnitRef != null && CharacterRenderBus.Instance != null)
                CharacterRenderBus.Instance.EmitSignal(CharacterRenderBus.SignalName.UnitHpChanged, UnitRef, current, max);
        };
    }





    private void FlashAll(Color color)
    {
        if (_isCreatureMode && _creatureSprite != null)
        {
            var orig = _creatureSprite.Modulate;
            _creatureSprite.Modulate = color;
            Schedule(0.1f, () => { if (GodotObject.IsInstanceValid(_creatureSprite)) _creatureSprite.Modulate = orig; });
            return;
        }
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
        if (_isCreatureMode && _creatureSprite != null)
        {
            var tw = CreateTween();
            tw.TweenProperty(_creatureSprite, "modulate:a", 0.0, duration);
            tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(_creatureSprite)) _creatureSprite.Visible = false; }));
            return;
        }
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





    private void Schedule(float delay, System.Action callback)
    {
        var timer = GetTree().CreateTimer(delay);
        timer.Timeout += callback;
    }
}
