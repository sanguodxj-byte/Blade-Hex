// EquipmentSlotView.cs
// 装备纸娃娃容器视图 — 多个固定槽位，每个槽位接受特定类型物品
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.Data;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 装备槽容器视图。
/// 内部维护多个 PanelContainer 槽位，每个槽位实现 IItemContainer 单元命中。
/// </summary>
[GlobalClass]
public partial class EquipmentSlotView : Control, IItemContainer
{
    public const int SlotSize = 54;

    private static readonly Color BgEquipSlot = new(0.09f, 0.08f, 0.11f, 0.9f);

    private UnitData? _unit;
    private DragController? _dragController;
    private ItemPopup? _popup;
    private readonly Dictionary<string, PanelContainer> _slots = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
    }

    public void Initialize(UnitData? unit, DragController dragCtrl, ItemPopup popup)
    {
        _unit = unit;
        _dragController = dragCtrl;
        _popup = popup;
        _dragController?.RegisterContainer(this);
        Rebuild();
    }

    public void SetUnit(UnitData? unit)
    {
        _unit = unit;
        Rebuild();
    }

    public UnitData? Unit => _unit;

    public void Rebuild()
    {
        foreach (var c in GetChildren()) c.QueueFree();
        _slots.Clear();

        if (_unit == null) return;

        var equipBox = new VBoxContainer();
        equipBox.AddThemeConstantOverride("separation", 3);
        AddChild(equipBox);

        // 行1: 头盔
        AddRow(equipBox, ("helmet", "头盔", _unit.Helmet));

        // 行2: 主手 | 铠甲 | 副手
        AddRow(equipBox,
            ("primary_main", "主手", _unit.PrimaryMainHand),
            ("armor", "铠甲", _unit.Armor),
            ("primary_off", "副手", _unit.PrimaryOffHand));

        // 行3: 护手 | 空 | 饰品1
        AddRow(equipBox,
            ("gauntlets", "护手", _unit.Gauntlets),
            ("spacer", "", null),
            ("accessory_1", "饰品", _unit.Accessory1));

        // 行4: 鞋子
        AddRow(equipBox, ("boots", "鞋子", _unit.Boots));

        // 行5: 副武器 | 饰品2 | 副武器副
        AddRow(equipBox,
            ("secondary_main", "副武器", _unit.SecondaryMainHand),
            ("accessory_2", "饰品", _unit.Accessory2),
            ("secondary_off", "副副手", _unit.SecondaryOffHand));
    }

    public void Refresh() => Rebuild();

    private void AddRow(VBoxContainer parent, params (string slotId, string label, ItemData? item)[] cells)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        parent.AddChild(row);

        foreach (var (slotId, label, item) in cells)
        {
            if (slotId == "spacer")
                row.AddChild(new Control { CustomMinimumSize = new Vector2(SlotSize, 0) });
            else
                row.AddChild(MakeSlot(slotId, label, item));
        }
    }

    private PanelContainer MakeSlot(string slotId, string label, ItemData? equipped)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(SlotSize, SlotSize),
            TooltipText = equipped != null ? $"{label}: {equipped.GetFullName()}" : $"{label}: 空",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            MouseFilter = MouseFilterEnum.Stop,
        };

        var bgColor = equipped != null ? new Color(0.1f, 0.09f, 0.13f, 0.95f) : BgEquipSlot;
        var borderColor = equipped != null
            ? new Color(equipped.GetRarityColor().R * 0.7f, equipped.GetRarityColor().G * 0.7f, equipped.GetRarityColor().B * 0.7f, 0.85f)
            : new Color(0.45f, 0.42f, 0.38f, 0.85f); // 空槽位用更亮的边框

        var style = new StyleBoxFlat { BgColor = bgColor };
        style.SetBorderWidthAll(equipped != null ? 2 : 1);
        style.BorderColor = borderColor;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", style);

        // 图标 / 占位符
        Texture2D? tex = (equipped != null && !string.IsNullOrEmpty(equipped.IconId))
            ? ResourceRegistry.GetIcon(equipped.IconId)
            : null;

        if (tex != null)
        {
            // 真实纹理
            var iconRect = new TextureRect();
            iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            iconRect.OffsetLeft = 4; iconRect.OffsetTop = 4; iconRect.OffsetRight = -4; iconRect.OffsetBottom = -4;
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.MouseFilter = MouseFilterEnum.Ignore;
            iconRect.Texture = tex;
            slot.AddChild(iconRect);
        }
        else
        {
            // 程序化占位符：装备时按物品类型，未装备时按槽位类型
            var placeholder = new ItemPlaceholderRenderer
            {
                Shape = equipped != null
                    ? ItemPlaceholderRenderer.GetShapeForItem(equipped)
                    : ItemPlaceholderRenderer.GetShapeForSlot(slotId),
                MainColor = equipped != null
                    ? equipped.GetRarityColor()
                    : new Color(0.4f, 0.38f, 0.42f, 0.7f),
                BgColor = new Color(0.08f, 0.07f, 0.10f, 0.4f),
            };
            placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            placeholder.OffsetLeft = 4; placeholder.OffsetTop = 4;
            placeholder.OffsetRight = -4; placeholder.OffsetBottom = -4;
            slot.AddChild(placeholder);
        }

        // 事件：左键拖拽，右键查看详情
        slot.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && equipped != null)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    var src = new DragSource { Container = this, Item = equipped, Origin = slotId };
                    _dragController?.BeginDrag(src, mb.GlobalPosition, slot.GlobalPosition, slot.Size);
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _popup?.ShowFor(equipped, mb.GlobalPosition);
                    GetViewport().SetInputAsHandled();
                }
            }
        };

        _slots[slotId] = slot;
        return slot;
    }

    // ============================================================================
    // IItemContainer
    // ============================================================================

    public new Rect2 GetGlobalRect() => base.GetGlobalRect();

    public ContainerHitInfo? HitTest(Vector2 globalMousePos)
    {
        foreach (var kvp in _slots)
        {
            if (!IsInstanceValid(kvp.Value)) continue;
            if (kvp.Value.GetGlobalRect().HasPoint(globalMousePos))
                return new ContainerHitInfo { Container = this, Target = kvp.Key };
        }
        return null;
    }

    public bool CanAccept(DragSource source, ContainerHitInfo hit)
    {
        if (_unit == null || hit.Target is not string slotId) return false;
        return CanEquipToSlot(source.Item, slotId);
    }

    public bool Accept(DragSource source, ContainerHitInfo hit)
    {
        if (_unit == null || hit.Target is not string slotId) return false;
        if (!CanEquipToSlot(source.Item, slotId)) return false;

        // 同槽位内交换：先卸下源槽位
        if (source.Origin is string fromSlotId && fromSlotId != slotId)
            _unit.UnequipItem(fromSlotId);

        // 装备到目标槽位
        _unit.EquipToSlot(source.Item, slotId);
        return true;
    }

    public void RemoveFromSource(DragSource source)
    {
        if (_unit == null) return;
        if (source.Container == this && source.Origin is string slotId)
            _unit.UnequipItem(slotId);
    }

    public void HighlightDropTarget(DragSource source, ContainerHitInfo? hit)
    {
        ClearHighlights();
        if (hit?.Container != this || hit.Target is not string slotId) return;
        if (!_slots.TryGetValue(slotId, out var slot)) return;

        bool ok = CanEquipToSlot(source.Item, slotId);
        var border = ok ? new Color(0.3f, 0.8f, 0.3f, 0.9f) : new Color(0.8f, 0.3f, 0.3f, 0.9f);

        var style = (StyleBoxFlat)slot.GetThemeStylebox("panel");
        var newStyle = new StyleBoxFlat { BgColor = style.BgColor };
        newStyle.SetBorderWidthAll(2);
        newStyle.BorderColor = border;
        newStyle.SetCornerRadiusAll(4);
        newStyle.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", newStyle);

        _highlightedSlotId = slotId;
    }

    private string? _highlightedSlotId;

    public void ClearHighlights()
    {
        if (_highlightedSlotId == null) return;
        if (!_slots.TryGetValue(_highlightedSlotId, out var slot))
        {
            _highlightedSlotId = null;
            return;
        }

        // 恢复槽位默认外观
        var unitData = _unit;
        ItemData? equippedItem = null;
        if (unitData != null)
        {
            equippedItem = _highlightedSlotId switch
            {
                "helmet" => unitData.Helmet,
                "armor" => unitData.Armor,
                "gauntlets" => unitData.Gauntlets,
                "boots" => unitData.Boots,
                "primary_main" => unitData.PrimaryMainHand,
                "primary_off" => unitData.PrimaryOffHand,
                "secondary_main" => unitData.SecondaryMainHand,
                "secondary_off" => unitData.SecondaryOffHand,
                "accessory_1" => unitData.Accessory1,
                "accessory_2" => unitData.Accessory2,
                _ => null,
            };
        }

        var bgColor = equippedItem != null ? new Color(0.1f, 0.09f, 0.13f, 0.95f) : BgEquipSlot;
        var borderColor = equippedItem != null
            ? new Color(equippedItem.GetRarityColor().R * 0.7f, equippedItem.GetRarityColor().G * 0.7f, equippedItem.GetRarityColor().B * 0.7f, 0.85f)
            : new Color(0.45f, 0.42f, 0.38f, 0.85f);

        var style = new StyleBoxFlat { BgColor = bgColor };
        style.SetBorderWidthAll(equippedItem != null ? 2 : 1);
        style.BorderColor = borderColor;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", style);

        _highlightedSlotId = null;
    }

    /// <summary>判断物品类型是否能装备到指定槽位（严格按 EquipSlotTarget 匹配）</summary>
    private static bool CanEquipToSlot(ItemData item, string slotId)
    {
        // 武器
        if (item is WeaponData weapon)
        {
            return slotId switch
            {
                "primary_main" or "secondary_main" => true,
                "primary_off" or "secondary_off" => weapon.IsThrowing,
                _ => false,
            };
        }

        // 护甲
        if (item is ArmorData armor)
        {
            // 盾牌：只能放在副手槽
            if (armor.armorType == ArmorData.ArmorType.Shield)
                return slotId is "primary_off" or "secondary_off";

            // 非盾护甲：严格按 EquipSlotTarget 匹配槽位
            // 显式的 EquipSlotTarget → slot 映射
            string requiredSlotForArmor = armor.EquipSlotTarget switch
            {
                ItemData.EquipSlot.Body or ItemData.EquipSlot.Costume => "armor",
                ItemData.EquipSlot.Helmet or ItemData.EquipSlot.Head => "helmet",
                ItemData.EquipSlot.Hands => "gauntlets",
                ItemData.EquipSlot.Feet => "boots",
                _ => "", // 未知 slot：拒绝放任何位置
            };
            return slotId == requiredSlotForArmor;
        }

        // 饰品
        if (item is AccessoryData)
            return slotId is "accessory_1" or "accessory_2";

        // 箭筒
        if (item.IsQuiver)
            return slotId is "primary_off" or "secondary_off";

        return false;
    }
}
