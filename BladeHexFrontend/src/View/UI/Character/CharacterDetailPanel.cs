// CharacterDetailPanel.cs
// CRPG风格队伍与物资面板 — 参考骑马与砍杀/战场兄弟
// 左侧1/3：按人体部位排列装备栏 + 角色切换 + 战斗属性（数值制）
// 右侧2/3：背包物品格
// 尺寸 950×600
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.UI;

[GlobalClass]
public partial class CharacterDetailPanel : PanelContainer
{
    [Signal] public delegate void CloseRequestedEventHandler();
    [Signal] public delegate void EquipmentSlotClickedEventHandler(string slotName);
    [Signal] public delegate void InventoryItemClickedEventHandler(int itemIndex);
    [Signal] public delegate void SkillTreeRequestedEventHandler();

    private UIFactory _factory = null!;
    private new UITheme Theme => UITheme.Instance!;

    // 角色数据
    private UnitData? _currentUnit;
    private List<UnitData> _partyList = new();
    private int _currentIndex = 0;

    // UI 引用 - 左侧
    private OptionButton _characterDropdown = null!;
    private Button _prevCharBtn = null!;
    private Button _nextCharBtn = null!;
    private Label _charNameLabel = null!;
    private readonly Dictionary<string, Label> _statLabels = new();
    private readonly Dictionary<string, Control> _equipSlots = new();

    // UI 引用 - 右侧
    private GridContainer _inventoryGrid = null!;

    // ============================================================================
    // 尺寸常量 (950×600 CRPG风格)
    // ============================================================================
    private const int SlotSize = 48;         // 装备槽尺寸
    private const int InvSlotSize = 56;      // 背包格尺寸
    private const int PanelWidth = 950;      // 面板总宽
    private const int PanelHeight = 600;     // 面板总高
    private const int FontTiny = 11;
    private const int FontSmall = 12;
    private const int FontMed = 14;
    private const int FontLarge = 16;
    private const int Spacing2 = 4;
    private const int Spacing3 = 6;
    private const int Spacing4 = 8;
    private const int Spacing6 = 12;

    public override void _Ready()
    {
        _factory = new UIFactory();
        Setup();
        Visible = false;
    }

    private void Setup()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
        Size = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = Theme.BorderHighlight;
        panelStyle.SetCornerRadiusAll(4);
        panelStyle.ShadowColor = new Color(0, 0, 0, 0.7f);
        panelStyle.ShadowSize = 8;
        AddThemeStyleboxOverride("panel", panelStyle);

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", Spacing6);
        rootMargin.AddThemeConstantOverride("margin_right", Spacing6);
        rootMargin.AddThemeConstantOverride("margin_top", Spacing4);
        rootMargin.AddThemeConstantOverride("margin_bottom", Spacing4);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", Spacing3);
        rootMargin.AddChild(mainVbox);

        // === 顶部标题栏（极简） ===
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", Spacing4);
        mainVbox.AddChild(header);

        var title = new Label();
        title.Text = "队伍与物资";
        title.AddThemeFontSizeOverride("font_size", FontLarge);
        title.AddThemeColorOverride("font_color", Theme.TextAccent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var skillBtn = CreateCompactButton("技能盘", 50);
        skillBtn.Pressed += () => EmitSignal(SignalName.SkillTreeRequested);
        header.AddChild(skillBtn);

        var closeBtn = CreateCompactButton("✕", 22);
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
        header.AddChild(closeBtn);

        // 分割线
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", MakeThinSep());
        mainVbox.AddChild(sep);

        // === 主体：左1/3 + 右2/3 ===
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", Spacing6);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(body);

        // ==========================================
        // 左栏 (1/3宽度): 装备 + 角色切换 + 属性
        // ==========================================
        var leftCol = new VBoxContainer();
        leftCol.CustomMinimumSize = new Vector2(280, 0);
        leftCol.AddThemeConstantOverride("separation", Spacing3);
        body.AddChild(leftCol);

        // --- 装备栏：按人体部位从上到下排列 ---
        BuildEquipmentColumn(leftCol);

        // --- 分割线 ---
        var sep2 = new HSeparator();
        sep2.AddThemeStyleboxOverride("separator", MakeThinSep());
        leftCol.AddChild(sep2);

        // --- 角色名 + 左右箭头 + 下拉 ---
        BuildCharacterSwitcher(leftCol);

        // --- 分割线 ---
        var sep3 = new HSeparator();
        sep3.AddThemeStyleboxOverride("separator", MakeThinSep());
        leftCol.AddChild(sep3);

        // --- 战斗属性（数值制，非D20） ---
        BuildCombatStats(leftCol);

        // ==========================================
        // 垂直分割线
        // ==========================================
        var vsep = new VSeparator();
        vsep.AddThemeStyleboxOverride("separator", MakeThinSep());
        body.AddChild(vsep);

        // ==========================================
        // 右栏 (2/3宽度): 背包格
        // ==========================================
        BuildInventoryPanel(body);
    }

    // ============================================================================
    // 装备栏构建 — 按人体部位从头到脚
    // ============================================================================
    private void BuildEquipmentColumn(VBoxContainer parent)
    {
        // 头盔
        CreateCompactEquipSlot(parent, "helmet", "头盔");
        // 身体/铠甲
        CreateCompactEquipSlot(parent, "armor", "铠甲");
        // 主武器 + 副手 (同一行)
        var weaponRow = new HBoxContainer();
        weaponRow.AddThemeConstantOverride("separation", Spacing3);
        parent.AddChild(weaponRow);
        CreateCompactEquipSlotInline(weaponRow, "primary_main", "主手");
        CreateCompactEquipSlotInline(weaponRow, "primary_off", "副手");

        // 护手
        CreateCompactEquipSlot(parent, "gauntlets", "护手");
        // 鞋子
        CreateCompactEquipSlot(parent, "boots", "鞋子");
        // 饰品行
        var accRow = new HBoxContainer();
        accRow.AddThemeConstantOverride("separation", Spacing3);
        parent.AddChild(accRow);
        CreateCompactEquipSlotInline(accRow, "accessory_1", "饰品1");
        CreateCompactEquipSlotInline(accRow, "accessory_2", "饰品2");
    }

    private void CreateCompactEquipSlot(VBoxContainer parent, string slotId, string slotName)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", Spacing3);
        parent.AddChild(row);

        var slot = CreateSlotBox(slotId, slotName);
        row.AddChild(slot);

        var lbl = new Label();
        lbl.Text = slotName;
        lbl.AddThemeFontSizeOverride("font_size", FontTiny);
        lbl.AddThemeColorOverride("font_color", Theme.TextMuted);
        lbl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(lbl);
    }

    private void CreateCompactEquipSlotInline(HBoxContainer parent, string slotId, string slotName)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 1);
        parent.AddChild(vbox);

        var slot = CreateSlotBox(slotId, slotName);
        vbox.AddChild(slot);

        var lbl = new Label();
        lbl.Text = slotName;
        lbl.AddThemeFontSizeOverride("font_size", FontTiny);
        lbl.AddThemeColorOverride("font_color", Theme.TextMuted);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(lbl);
    }

    private PanelContainer CreateSlotBox(string slotId, string tooltip)
    {
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
        slot.TooltipText = tooltip;
        var slotStyle = new StyleBoxFlat();
        slotStyle.BgColor = new Color(0.12f, 0.11f, 0.15f, 0.9f);
        slotStyle.SetBorderWidthAll(1);
        slotStyle.BorderColor = Theme.BorderDefault;
        slotStyle.SetCornerRadiusAll(3);
        slotStyle.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", slotStyle);

        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        slot.AddChild(iconRect);

        slot.SetMeta("icon_rect", iconRect);
        slot.SetMeta("slot_id", slotId);

        slot.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                EmitSignal(SignalName.EquipmentSlotClicked, slotId);
        };

        _equipSlots[slotId] = slot;
        return slot;
    }

    // ============================================================================
    // 角色切换区
    // ============================================================================
    private void BuildCharacterSwitcher(VBoxContainer parent)
    {
        // 第一行：< 角色名(可点击下拉) >
        var switchRow = new HBoxContainer();
        switchRow.AddThemeConstantOverride("separation", Spacing2);
        switchRow.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        parent.AddChild(switchRow);

        _prevCharBtn = CreateCompactButton("<", 16);
        _prevCharBtn.Pressed += OnPrevCharacter;
        switchRow.AddChild(_prevCharBtn);

        // 下拉按钮（显示角色名，点击展开列表）
        _characterDropdown = new OptionButton();
        _characterDropdown.CustomMinimumSize = new Vector2(90, 18);
        _characterDropdown.AddThemeFontSizeOverride("font_size", FontSmall);
        _characterDropdown.ItemSelected += OnCharacterSelected;
        var dropStyle = new StyleBoxFlat();
        dropStyle.BgColor = new Color(0.10f, 0.10f, 0.13f, 0.9f);
        dropStyle.SetBorderWidthAll(1);
        dropStyle.BorderColor = Theme.BorderDefault;
        dropStyle.SetCornerRadiusAll(3);
        dropStyle.SetContentMarginAll(2);
        _characterDropdown.AddThemeStyleboxOverride("normal", dropStyle);
        _characterDropdown.AddThemeColorOverride("font_color", Theme.TextPrimary);
        switchRow.AddChild(_characterDropdown);

        _nextCharBtn = CreateCompactButton(">", 16);
        _nextCharBtn.Pressed += OnNextCharacter;
        switchRow.AddChild(_nextCharBtn);
    }

    // ============================================================================
    // 战斗属性区 — 紧凑数值显示
    // ============================================================================
    private void BuildCombatStats(VBoxContainer parent)
    {
        // 主属性行 (STR/DEX/CON/INT/WIS/CHA 两列三行)
        var attrGrid = new GridContainer();
        attrGrid.Columns = 4; // name val | name val
        attrGrid.AddThemeConstantOverride("h_separation", Spacing3);
        attrGrid.AddThemeConstantOverride("v_separation", 1);
        parent.AddChild(attrGrid);

        CreateStatCell(attrGrid, "str", "力");
        CreateStatCell(attrGrid, "dex", "敏");
        CreateStatCell(attrGrid, "con", "体");
        CreateStatCell(attrGrid, "intel", "智");
        CreateStatCell(attrGrid, "wis", "感");
        CreateStatCell(attrGrid, "cha", "魅");

        // 分割
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", MakeThinSep());
        parent.AddChild(sep);

        // 战斗副属性（数值制）
        var combatGrid = new GridContainer();
        combatGrid.Columns = 4;
        combatGrid.AddThemeConstantOverride("h_separation", Spacing3);
        combatGrid.AddThemeConstantOverride("v_separation", 1);
        parent.AddChild(combatGrid);

        CreateStatCell(combatGrid, "atk", "攻击");
        CreateStatCell(combatGrid, "hit", "命中");
        CreateStatCell(combatGrid, "crit", "暴击");
        CreateStatCell(combatGrid, "ac", "闪避");
        CreateStatCell(combatGrid, "dodge", "闪避");
        CreateStatCell(combatGrid, "init", "先攻");
        CreateStatCell(combatGrid, "move", "移动");
        CreateStatCell(combatGrid, "hp", "生命");
    }

    private void CreateStatCell(GridContainer parent, string id, string displayName)
    {
        var nameL = new Label();
        nameL.Text = displayName;
        nameL.AddThemeFontSizeOverride("font_size", FontTiny);
        nameL.AddThemeColorOverride("font_color", Theme.TextMuted);
        nameL.CustomMinimumSize = new Vector2(22, 0);
        parent.AddChild(nameL);

        var valL = new Label();
        valL.Text = "—";
        valL.AddThemeFontSizeOverride("font_size", FontSmall);
        valL.AddThemeColorOverride("font_color", Theme.TextPrimary);
        valL.CustomMinimumSize = new Vector2(20, 0);
        parent.AddChild(valL);
        _statLabels[id] = valL;
    }

    // ============================================================================
    // 背包区
    // ============================================================================
    private void BuildInventoryPanel(HBoxContainer body)
    {
        var rightCol = new VBoxContainer();
        rightCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightCol.AddThemeConstantOverride("separation", Spacing3);
        body.AddChild(rightCol);

        var invHeader = new Label();
        invHeader.Text = "背包";
        invHeader.AddThemeFontSizeOverride("font_size", FontMed);
        invHeader.AddThemeColorOverride("font_color", Theme.TextAccent);
        rightCol.AddChild(invHeader);

        var invPanel = new PanelContainer();
        var invStyle = new StyleBoxFlat();
        invStyle.BgColor = new Color(0.08f, 0.08f, 0.10f, 0.9f);
        invStyle.SetBorderWidthAll(1);
        invStyle.BorderColor = Theme.BorderDefault;
        invStyle.SetCornerRadiusAll(3);
        invStyle.SetContentMarginAll(Spacing4);
        invPanel.AddThemeStyleboxOverride("panel", invStyle);
        invPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        rightCol.AddChild(invPanel);

        var invScroll = new ScrollContainer();
        invScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        invScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        invScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        invPanel.AddChild(invScroll);

        _inventoryGrid = new GridContainer();
        _inventoryGrid.Columns = 8;
        _inventoryGrid.AddThemeConstantOverride("h_separation", Spacing2);
        _inventoryGrid.AddThemeConstantOverride("v_separation", Spacing2);
        invScroll.AddChild(_inventoryGrid);
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    private Button CreateCompactButton(string text, int width)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(width, 18);
        btn.AddThemeFontSizeOverride("font_size", FontSmall);

        var normal = new StyleBoxFlat();
        normal.BgColor = new Color(0.14f, 0.13f, 0.17f);
        normal.SetBorderWidthAll(1);
        normal.BorderColor = Theme.BorderDefault;
        normal.SetCornerRadiusAll(3);
        normal.SetContentMarginAll(2);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat();
        hover.BgColor = new Color(0.22f, 0.20f, 0.28f);
        hover.SetBorderWidthAll(1);
        hover.BorderColor = Theme.BorderHighlight;
        hover.SetCornerRadiusAll(3);
        hover.SetContentMarginAll(2);
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = new StyleBoxFlat();
        pressed.BgColor = new Color(0.10f, 0.09f, 0.12f);
        pressed.SetBorderWidthAll(1);
        pressed.BorderColor = Theme.BorderHighlight;
        pressed.SetCornerRadiusAll(3);
        pressed.SetContentMarginAll(2);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        btn.AddThemeColorOverride("font_color", Theme.TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", Theme.TextAccent);
        return btn;
    }

    private static StyleBoxFlat MakeThinSep()
    {
        var s = new StyleBoxFlat();
        s.BgColor = new Color(0.3f, 0.28f, 0.25f, 0.4f);
        s.SetContentMarginAll(0);
        return s;
    }

    // ============================================================================
    // 角色切换逻辑
    // ============================================================================

    private void OnPrevCharacter()
    {
        if (_partyList.Count <= 1) return;
        _currentIndex = (_currentIndex - 1 + _partyList.Count) % _partyList.Count;
        _characterDropdown.Select(_currentIndex);
        SetCurrentUnit(_partyList[_currentIndex]);
    }

    private void OnNextCharacter()
    {
        if (_partyList.Count <= 1) return;
        _currentIndex = (_currentIndex + 1) % _partyList.Count;
        _characterDropdown.Select(_currentIndex);
        SetCurrentUnit(_partyList[_currentIndex]);
    }

    private void OnCharacterSelected(long index)
    {
        _currentIndex = (int)index;
        if (_currentIndex >= 0 && _currentIndex < _partyList.Count)
            SetCurrentUnit(_partyList[_currentIndex]);
    }

    private void SetCurrentUnit(UnitData unit)
    {
        _currentUnit = unit;
        UpdateDisplay();
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void ShowDetail(UnitData unit, List<UnitData>? partyList = null)
    {
        _partyList = partyList ?? new List<UnitData>();
        _characterDropdown.Clear();
        _currentIndex = 0;

        if (_partyList.Count > 0)
        {
            for (int i = 0; i < _partyList.Count; i++)
            {
                var u = _partyList[i];
                _characterDropdown.AddItem(u?.UnitName ?? "未知", i);
            }

            if (unit != null)
            {
                _currentIndex = _partyList.IndexOf(unit);
                if (_currentIndex < 0) _currentIndex = 0;
            }

            _characterDropdown.Select(_currentIndex);
            SetCurrentUnit(_partyList[_currentIndex]);
        }
        else if (unit != null)
        {
            _characterDropdown.AddItem(unit.UnitName, 0);
            _characterDropdown.Select(0);
            SetCurrentUnit(unit);
        }

        RefreshInventory();
        Visible = true;
    }

    public void HideDetail()
    {
        Visible = false;
        _currentUnit = null;
    }

    public void UpdateDisplay()
    {
        if (_currentUnit == null || !IsInstanceValid(_currentUnit))
            return;

        var u = _currentUnit;

        // 主属性（基础值 + 装备加值）
        SetStat("str", u.Str + u.AccessoryStrBonus);
        SetStat("dex", u.Dex + u.AccessoryDexBonus);
        SetStat("con", u.Con + u.AccessoryConBonus);
        SetStat("intel", u.Intel + u.AccessoryIntBonus);
        SetStat("wis", u.Wis + u.AccessoryWisBonus);
        SetStat("cha", u.Cha + u.AccessoryChaBonus);

        // 战斗属性（数值制）
        int totalStr = u.Str + u.AccessoryStrBonus;
        int totalDex = u.Dex + u.AccessoryDexBonus;
        int totalCon = u.Con + u.AccessoryConBonus;
        int totalInt = u.Intel + u.AccessoryIntBonus;

        // 攻击力 = 武器平均伤害(骰子期望) + STR/DEX修正 + 词缀加成
        int atkBonus = 0;
        if (u.PrimaryMainHand != null)
        {
            var w = u.PrimaryMainHand;
            // 骰子期望值: count * (sides+1)/2 + bonus
            atkBonus = w.GetTotalDamageDiceCount() * (w.GetTotalDamageDiceSides() + 1) / 2
                       + w.GetTotalDamageBonus();
            if (w.IsFinesse)
                atkBonus += System.Math.Max(totalStr, totalDex) / 3;
            else if (w.IsRanged)
                atkBonus += totalDex / 3;
            else
                atkBonus += totalStr / 3;
        }
        else
        {
            atkBonus = 1 + totalStr / 5;
        }
        SetStat("atk", atkBonus);

        // 命中 = 基础50 + DEX*2 + 武器命中修正
        int hitVal = 50 + totalDex * 2;
        if (u.PrimaryMainHand != null)
            hitVal += u.PrimaryMainHand.GetTotalAttackBonus();
        SetStat("hit", hitVal);

        // 暴击 = 5 + DEX/4 + 武器暴击范围扩展
        int critVal = 5 + totalDex / 4;
        if (u.PrimaryMainHand != null)
            critVal += u.PrimaryMainHand.BonusCritRange;
        SetStat("crit", critVal);

        // 护甲(AC)
        int acVal = u.BaseAc + u.AccessoryAcBonus;
        SetStat("ac", acVal);

        // 闪避 = DEX*2
        int dodgeVal = totalDex * 2;
        SetStat("dodge", dodgeVal);

        // 先攻
        int initVal = u.BaseInitiative + u.AccessoryInitiativeBonus;
        SetStat("init", initVal);

        // 移动力
        int moveVal = u.BaseMoveRange + u.GetEquipmentMoveBonus();
        SetStat("move", moveVal);

        // 生命
        int hpVal = u.BaseMaxHp + u.GetEquipmentHpBonus();
        SetStat("hp", hpVal);

        // 更新装备图标
        UpdateEquipIcon("helmet", u.Helmet);
        UpdateEquipIcon("armor", u.Armor);
        UpdateEquipIcon("gauntlets", u.Gauntlets);
        UpdateEquipIcon("boots", u.Boots);
        UpdateEquipIcon("primary_main", u.PrimaryMainHand);
        UpdateEquipIcon("primary_off", u.PrimaryOffHand);
        UpdateEquipIcon("accessory_1", u.Accessory1);
        UpdateEquipIcon("accessory_2", u.Accessory2);
    }

    private void SetStat(string id, int value)
    {
        if (_statLabels.TryGetValue(id, out var lbl))
            lbl.Text = value.ToString();
    }

    private void UpdateEquipIcon(string slotId, Resource? item)
    {
        if (!_equipSlots.TryGetValue(slotId, out var slot)) return;
        var rect = slot.GetMeta("icon_rect").As<TextureRect>();

        if (item is ItemData idata)
        {
            // 加载装备图标纹理（通过 ResourceRegistry 解析 EquipTextureId）
            var iconTex = BladeHex.View.Data.ResourceRegistry.GetIcon(idata.EquipTextureId);
            if (iconTex != null)
                rect.Texture = iconTex;
            else
                rect.Texture = null; // 无图标时留空（边框颜色仍能标识稀有度）

            slot.TooltipText = idata.GetFullName();
            // 根据稀有度设置边框颜色
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.12f, 0.11f, 0.15f, 0.9f);
            slotStyle.SetBorderWidthAll(1);
            slotStyle.BorderColor = idata.GetRarityColor();
            slotStyle.SetCornerRadiusAll(3);
            slotStyle.SetContentMarginAll(2);
            slot.AddThemeStyleboxOverride("panel", slotStyle);
        }
        else
        {
            rect.Texture = null;
            slot.TooltipText = slotId;
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.12f, 0.11f, 0.15f, 0.9f);
            slotStyle.SetBorderWidthAll(1);
            slotStyle.BorderColor = Theme.BorderDefault;
            slotStyle.SetCornerRadiusAll(3);
            slotStyle.SetContentMarginAll(2);
            slot.AddThemeStyleboxOverride("panel", slotStyle);
        }
    }

    private void RefreshInventory()
    {
        foreach (var child in _inventoryGrid.GetChildren())
            child.QueueFree();

        // 8列 x 6行 = 48格背包
        for (int i = 0; i < 48; i++)
        {
            int index = i;
            var slot = new PanelContainer();
            slot.CustomMinimumSize = new Vector2(InvSlotSize, InvSlotSize);
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.10f, 0.10f, 0.12f, 0.8f);
            slotStyle.SetBorderWidthAll(1);
            slotStyle.BorderColor = new Color(0.25f, 0.25f, 0.28f, 0.5f);
            slotStyle.SetCornerRadiusAll(2);
            slotStyle.SetContentMarginAll(1);
            slot.AddThemeStyleboxOverride("panel", slotStyle);

            var iconRect = new TextureRect();
            iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            slot.AddChild(iconRect);

            slot.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    EmitSignal(SignalName.InventoryItemClicked, index);
            };

            _inventoryGrid.AddChild(slot);
        }
    }
}
