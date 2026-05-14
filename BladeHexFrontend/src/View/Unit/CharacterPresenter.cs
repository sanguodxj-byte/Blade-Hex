// CharacterPresenter.cs
// 角色分部位渲染的"资源解析"中心。
// 输入：UnitData（含装备槽位 + sprite/portrait id）
// 输出：每个槽位最终用哪个 Texture2D 或 SpriteFrames
//
// 这是 3D（CharacterRenderNode）/ 2D（CharacterView2D）/ UI（CharacterAvatarControl）三套渲染的共同上游，
// 是"所有地方角色用同一套渲染"的根基。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.Data;

namespace BladeHex.View.Unit;

/// <summary>解析好的单个槽位渲染数据</summary>
public sealed class CharacterSlotResolution
{
    public ItemData.EquipSlot Slot { get; init; }
    public SpriteFrames? Frames { get; init; }
    public Texture2D? Texture { get; init; }
    public Color Modulate { get; init; } = Colors.White;

    public bool HasContent => Frames != null || Texture != null;
}

/// <summary>整个角色解析后的所有槽位 + 默认动画 + body 高度等元信息</summary>
public sealed class CharacterResolution
{
    public Dictionary<ItemData.EquipSlot, CharacterSlotResolution> Slots { get; } = new();
    public float BodyTextureHeight { get; set; } = 120.0f;
    public bool BodyIsPlaceholder { get; set; }
    public Color PlaceholderModulate { get; set; } = Colors.White;
    public string DefaultAnimation { get; set; } = "default";
}

/// <summary>角色渲染资源解析器（无副作用）</summary>
public static class CharacterPresenter
{
    /// <summary>玩家方占位色（蓝）— 与 CharacterRenderBus 保持一致</summary>
    public static readonly Color PlayerPlaceholder = new(0.4f, 0.7f, 1.0f);

    /// <summary>敌方占位色（红）</summary>
    public static readonly Color EnemyPlaceholder = new(1.0f, 0.4f, 0.4f);

    /// <summary>
    /// 从 UnitData 解析角色完整渲染数据。装备槽未解析或为空时该 slot 返回空内容。
    /// </summary>
    public static CharacterResolution Resolve(UnitData data, bool useSecondaryWeapon = false)
    {
        var result = new CharacterResolution();

        // ─── 1. Body 层 ───
        var bodySlot = ResolveBody(data, out float bodyHeight, out bool isPlaceholder, out Color placeholderColor);
        result.Slots[ItemData.EquipSlot.Body] = bodySlot;
        result.BodyTextureHeight = bodyHeight;
        result.BodyIsPlaceholder = isPlaceholder;
        result.PlaceholderModulate = placeholderColor;

        // ─── 2. 装备槽 ───
        TryAddEquip(result, data.Helmet);
        TryAddEquip(result, data.Armor);
        TryAddEquip(result, data.Shield);

        var mainHand = useSecondaryWeapon ? data.SecondaryMainHand : data.PrimaryMainHand;
        TryAddEquip(result, mainHand);

        return result;
    }

    /// <summary>仅解析 portrait（用于 UI 头像 / 出身界面 / 对话）</summary>
    public static Texture2D? ResolvePortrait(UnitData data)
    {
        var tex = ResourceRegistry.GetIcon(data.PortraitId);
        if (tex != null) return tex;
        // fallback 到 BattleSprite（许多角色没有专属头像）
        return ResourceRegistry.GetIcon(data.BattleSpriteId);
    }

    // ===========================================================
    // 内部
    // ===========================================================

    private static CharacterSlotResolution ResolveBody(
        UnitData data, out float height, out bool isPlaceholder, out Color placeholderColor)
    {
        height = 120.0f;
        isPlaceholder = false;
        placeholderColor = data.IsEnemy ? EnemyPlaceholder : PlayerPlaceholder;

        // 优先级 1：SpriteFrames（多帧动画）
        var frames = ResourceRegistry.GetSpriteFrames(data.SpriteFramesId);
        if (frames != null)
        {
            if (frames.GetFrameCount("default") > 0)
            {
                var firstFrame = frames.GetFrameTexture("default", 0);
                if (firstFrame != null) height = firstFrame.GetHeight();
            }
            return new CharacterSlotResolution
            {
                Slot = ItemData.EquipSlot.Body,
                Frames = frames,
            };
        }

        // 优先级 2：单图 BattleSprite
        var battleSprite = ResourceRegistry.GetIcon(data.BattleSpriteId);
        if (battleSprite != null)
        {
            height = battleSprite.GetHeight();
            return new CharacterSlotResolution
            {
                Slot = ItemData.EquipSlot.Body,
                Texture = battleSprite,
            };
        }

        // 优先级 3：占位符
        isPlaceholder = true;
        var placeholder = new PlaceholderTexture2D { Size = new Vector2(80, 120) };
        height = 120.0f;
        return new CharacterSlotResolution
        {
            Slot = ItemData.EquipSlot.Body,
            Texture = placeholder,
            Modulate = placeholderColor,
        };
    }

    private static void TryAddEquip(CharacterResolution result, ItemData? item)
    {
        if (item == null) return;
        var slot = item.EquipSlotTarget;

        // 优先级 1：装备序列帧
        var frames = ResourceRegistry.GetSpriteFrames(item.EquipSpriteFramesId);
        if (frames != null)
        {
            result.Slots[slot] = new CharacterSlotResolution { Slot = slot, Frames = frames };
            return;
        }

        // 优先级 2：装备单图
        var tex = ResourceRegistry.GetIcon(item.EquipTextureId);
        if (tex != null)
        {
            result.Slots[slot] = new CharacterSlotResolution { Slot = slot, Texture = tex };
            return;
        }
        // 没有外观资源 → 不渲染该层
    }
}
