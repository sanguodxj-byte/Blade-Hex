// TradePanel.cs
// 真实市场面板 — 根据城镇繁荣度随机生成武器/护甲/消耗品
// 支持购买和出售，价格受繁荣度影响
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TradePanel : POIPanelBase
{
    [Signal] public delegate void TradeFinishedEventHandler();

    // ============================================================================
    // 面板规格
    // ============================================================================
    protected override int PanelWidth => 750;
    protected override int PanelHeight => 520;
    protected override int PanelMargin => 16;
    protected override bool CloseOnOverlayClick => false;

    // ============================================================================
    // 字段
    // ============================================================================
    private Label _titleLabel = null!;
    private Label _goldLabel = null!;
    private GridContainer _shopGrid = null!;
    private GridContainer _inventoryGrid = null!;
    private RichTextLabel _detailText = null!;
    private Button _buyBtn = null!;
    private Button _sellBtn = null!;

    private EconomyManager? _economyManager;
    private int _prosperity = 50;
    private readonly List<ItemData> _shopStock = new();
    private ItemData? _selectedShopItem;
    private ItemData? _selectedInvItem;
    private int _selectedInvIndex = -1;

    // ============================================================================
    // 公开接口
    // ============================================================================
    public void ShowTrade(string sourceName, EconomyManager economy, int prosperity = 50)
    {
        _economyManager = economy;
        _prosperity = prosperity;
        _titleLabel.Text = $"市场 — {sourceName}";
        _selectedShopItem = null;
        _selectedInvItem = null;
        _selectedInvIndex = -1;
        UpdateGold();
        GenerateShopStock();
        PopulateShop();
        PopulateInventory();
        ClearDetail();
        Root.Visible = true;
    }

    public void ShowTrade(string sourceName, EconomyManager economy)
    {
        ShowTrade(sourceName, economy, 50);
    }

    public override void HidePanel() { base.HidePanel(); }

    // ============================================================================
    // 关闭处理
    // ============================================================================
    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.TradeFinished);
        HidePanel();
    }

    // ============================================================================
    // 商品生成 — 根据繁荣度 + 稀有度词缀
    // ============================================================================
    private void GenerateShopStock()
    {
        _shopStock.Clear();
        var rand = new Random();

        // 繁荣度决定商品数量和品质
        int weaponCount = 3 + _prosperity / 20;   // 5-8件武器
        int armorCount = 2 + _prosperity / 25;    // 4-6件护甲
        int consumCount = 3 + _prosperity / 30;   // 4-6件消耗品

        var weapons = PrototypeData.GetWeapons().Values.ToList();
        var armors = PrototypeData.GetArmors().Values.ToList();
        var consumables = PrototypeData.GetConsumables().Values.ToList();

        // 确定商店稀有度分布（繁荣度越高，高稀有度越多，但最高蓝装）
        // 紫装和橙装仅限战利品掉落
        string difficulty = _prosperity >= 70 ? "hard" : _prosperity >= 40 ? "normal" : "easy";
        int itemLevel = 1 + _prosperity / 10; // 繁荣50→6, 繁荣100→11

        // 武器：繁荣度高的城镇有更贵的武器，部分带词缀
        int maxWeaponPrice = 50 + _prosperity * 4;
        var affordableWeapons = weapons.Where(w => w.Price <= maxWeaponPrice).ToList();
        for (int i = 0; i < System.Math.Min(weaponCount, affordableWeapons.Count); i++)
        {
            var baseW = affordableWeapons[rand.Next(affordableWeapons.Count)];
            if (_shopStock.Any(s => s.ItemId == baseW.ItemId)) continue;

            // 使用 EquipmentGenerator 生成带稀有度和词缀的版本
            // 商店最高蓝装（Rare），紫橙仅限战利品
            var rarity = BladeHex.Combat.EquipmentGenerator.RollRarity(difficulty);
            if (rarity > ItemData.Rarity.Rare) rarity = ItemData.Rarity.Rare;
            var generated = BladeHex.Combat.EquipmentGenerator.GenerateEquipment(
                baseW, rarity, itemLevel, difficulty);
            _shopStock.Add(generated);
        }

        // 护甲：同理
        int maxArmorPrice = 30 + _prosperity * 5;
        var affordableArmors = armors.Where(a => a.Price <= maxArmorPrice).ToList();
        for (int i = 0; i < System.Math.Min(armorCount, affordableArmors.Count); i++)
        {
            var baseA = affordableArmors[rand.Next(affordableArmors.Count)];
            if (_shopStock.Any(s => s.ItemId == baseA.ItemId)) continue;

            var rarityA = BladeHex.Combat.EquipmentGenerator.RollRarity(difficulty);
            if (rarityA > ItemData.Rarity.Rare) rarityA = ItemData.Rarity.Rare;
            var generated = BladeHex.Combat.EquipmentGenerator.GenerateEquipment(
                baseA, rarityA, itemLevel, difficulty);
            _shopStock.Add(generated);
        }

        // 消耗品：不带词缀，直接添加
        for (int i = 0; i < System.Math.Min(consumCount, consumables.Count); i++)
        {
            var c = consumables[rand.Next(consumables.Count)];
            if (!_shopStock.Any(s => s.ItemId == c.ItemId))
                _shopStock.Add(c);
        }

        // 箭筒：每个商店固定上架1-2个
        var quivers = PrototypeData.GetQuivers().Values.ToList();
        int quiverCount = 1 + (_prosperity >= 50 ? 1 : 0);
        for (int i = 0; i < System.Math.Min(quiverCount, quivers.Count); i++)
        {
            var q = quivers[rand.Next(quivers.Count)];
            if (!_shopStock.Any(s => s.ItemId == q.ItemId))
                _shopStock.Add(q);
        }

        // 按价格排序
        _shopStock.Sort((a, b) => a.Price.CompareTo(b.Price));
    }

    // ============================================================================
    // UI填充
    // ============================================================================
    private void PopulateShop()
    {
        foreach (Node child in _shopGrid.GetChildren())
            child.QueueFree();

        foreach (var item in _shopStock)
        {
            var slot = CreateItemSlot(item, true);
            _shopGrid.AddChild(slot);
        }
    }

    private void PopulateInventory()
    {
        foreach (Node child in _inventoryGrid.GetChildren())
            child.QueueFree();

        if (_economyManager == null || _economyManager.PlayerInventory.Count == 0)
        {
            var empty = new Label();
            empty.Text = "背包为空";
            empty.AddThemeFontSizeOverride("font_size", FontSizeSm);
            empty.AddThemeColorOverride("font_color", ThemeTextMuted);
            _inventoryGrid.AddChild(empty);
            return;
        }

        for (int i = 0; i < _economyManager.PlayerInventory.Count; i++)
        {
            var item = _economyManager.PlayerInventory[i];
            if (item == null) continue;
            int idx = i;
            var slot = CreateItemSlot(item, false);
            slot.SetMeta("inv_index", idx);
            _inventoryGrid.AddChild(slot);
        }
    }

    private PanelContainer CreateItemSlot(ItemData item, bool isShop)
    {
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(90, 60);
        slot.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var style = new StyleBoxFlat { BgColor = ThemeBgCard };
        style.SetBorderWidthAll(1);
        style.BorderColor = item.GetRarityColor();
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(4);
        slot.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        slot.AddChild(vbox);

        var nameLbl = new Label();
        nameLbl.Text = item.ItemName;
        nameLbl.AddThemeFontSizeOverride("font_size", FontSizeXs);
        nameLbl.AddThemeColorOverride("font_color", ThemeTextPrimary);
        nameLbl.ClipText = true;
        nameLbl.CustomMinimumSize = new Vector2(82, 0);
        vbox.AddChild(nameLbl);

        var priceLbl = new Label();
        int displayPrice = isShop ? GetBuyPrice(item) : GetSellPrice(item);
        priceLbl.Text = $"{displayPrice}金";
        priceLbl.AddThemeFontSizeOverride("font_size", FontSizeXs);
        priceLbl.AddThemeColorOverride("font_color", isShop ? ThemeTextAccent : ThemeTextPositive);
        vbox.AddChild(priceLbl);

        // 类型标签
        var typeLbl = new Label();
        typeLbl.Text = GetItemTypeShort(item);
        typeLbl.AddThemeFontSizeOverride("font_size", 9);
        typeLbl.AddThemeColorOverride("font_color", ThemeTextMuted);
        vbox.AddChild(typeLbl);

        var capturedItem = item;
        slot.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                if (isShop)
                    OnShopItemClicked(capturedItem);
                else
                    OnInvItemClicked(capturedItem, slot.GetMeta("inv_index").AsInt32());
            }
        };

        return slot;
    }

    // ============================================================================
    // 选择与详情
    // ============================================================================
    private void OnShopItemClicked(ItemData item)
    {
        _selectedShopItem = item;
        _selectedInvItem = null;
        _selectedInvIndex = -1;
        ShowItemDetail(item, true);
        _buyBtn.Disabled = _economyManager == null || _economyManager.Gold < GetBuyPrice(item);
        _buyBtn.Visible = true;
        _sellBtn.Visible = false;
    }

    private void OnInvItemClicked(ItemData item, int index)
    {
        _selectedInvItem = item;
        _selectedInvIndex = index;
        _selectedShopItem = null;
        ShowItemDetail(item, false);
        _buyBtn.Visible = false;
        _sellBtn.Visible = true;
        _sellBtn.Disabled = false;
    }

    private void ShowItemDetail(ItemData item, bool isBuying)
    {
        string desc = $"[color=#f0e8d0]{item.GetFullName()}[/color]\n";
        desc += $"[color=#888]{item.GetRarityName()} | {GetItemTypeFull(item)}[/color]\n\n";

        if (item is WeaponData w)
        {
            desc += $"[color=#ccc]伤害: {w.DamageDiceCount}d{w.DamageDiceSides}[/color]\n";
            desc += $"[color=#ccc]AP消耗: {w.ApCost}[/color]\n";
            if (w.RangeCells > 1) desc += $"[color=#ccc]射程: {w.RangeCells}[/color]\n";
            desc += $"[color=#ccc]{w.GetWeaponDescription()}[/color]\n";
        }
        else if (item is ArmorData a)
        {
            desc += $"[color=#ccc]{a.GetArmorDescription()}[/color]\n";
            if (a.DrThreshold > 0) desc += $"[color=#ccc]装甲: {a.DrThreshold}[/color]\n";
        }
        else if (item is ConsumableData)
        {
            desc += $"[color=#ccc]{item.Description}[/color]\n";
        }

        desc += "\n";
        if (isBuying)
            desc += $"[color=#e6cc80]购买价格: {GetBuyPrice(item)}金[/color]";
        else
            desc += $"[color=#4ddb4d]出售价格: {GetSellPrice(item)}金[/color]";

        _detailText.Text = desc;
    }

    private void ClearDetail()
    {
        _detailText.Text = "[color=#888]点击商品或背包物品查看详情[/color]";
        _buyBtn.Visible = false;
        _sellBtn.Visible = false;
    }

    // ============================================================================
    // 买卖逻辑
    // ============================================================================
    private void OnBuyPressed()
    {
        if (_selectedShopItem == null || _economyManager == null) return;
        int price = GetBuyPrice(_selectedShopItem);
        if (_economyManager.Gold < price) return;

        _economyManager.Gold -= price;
        // 复制物品加入背包
        var copy = (ItemData)_selectedShopItem.Duplicate();
        _economyManager.PlayerInventory.Add(copy);

        // 从商店移除
        _shopStock.Remove(_selectedShopItem);
        _selectedShopItem = null;

        UpdateGold();
        PopulateShop();
        PopulateInventory();
        ClearDetail();
    }

    private void OnSellPressed()
    {
        if (_selectedInvItem == null || _economyManager == null || _selectedInvIndex < 0) return;
        int price = GetSellPrice(_selectedInvItem);

        _economyManager.Gold += price;
        _economyManager.PlayerInventory.RemoveAt(_selectedInvIndex);
        _selectedInvItem = null;
        _selectedInvIndex = -1;

        UpdateGold();
        PopulateInventory();
        ClearDetail();
    }

    // ============================================================================
    // 价格计算
    // ============================================================================
    private int GetBuyPrice(ItemData item)
    {
        // 繁荣度高的城镇价格略低（竞争多）
        float mult = 1.2f - _prosperity * 0.002f; // 繁荣50→1.1x, 繁荣100→1.0x
        mult = Mathf.Clamp(mult, 0.9f, 1.3f);
        return Mathf.Max(1, (int)(item.Price * mult));
    }

    private int GetSellPrice(ItemData item)
    {
        // 出售价格为购买价的40-60%（繁荣度高的城镇收购价更好）
        float mult = 0.35f + _prosperity * 0.002f; // 繁荣50→0.45x, 繁荣100→0.55x
        mult = Mathf.Clamp(mult, 0.3f, 0.6f);
        return Mathf.Max(1, (int)(item.Price * mult));
    }

    private void UpdateGold()
    {
        _goldLabel.Text = _economyManager != null ? $"金币: {_economyManager.Gold}" : "金币: —";
    }

    // ============================================================================
    // 辅助
    // ============================================================================
    private static string GetItemTypeShort(ItemData item) => item switch
    {
        WeaponData w => w.IsRanged ? "远程" : "近战",
        ArmorData a => a.GetArmorTypeName(),
        ConsumableData => "消耗",
        _ => "物品"
    };

    private static string GetItemTypeFull(ItemData item) => item switch
    {
        WeaponData w => $"武器 ({(w.IsRanged ? "远程" : "近战")})",
        ArmorData a => $"防具 ({a.GetArmorTypeName()})",
        ConsumableData => "消耗品",
        _ => "物品"
    };

    // ============================================================================
    // 内容构建
    // ============================================================================
    protected override void BuildContent(VBoxContainer container)
    {
        container.AddThemeConstantOverride("separation", 8);

        // 标题栏
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        container.AddChild(header);

        _titleLabel = new Label();
        _titleLabel.Text = "市场";
        _titleLabel.AddThemeFontSizeOverride("font_size", FontSizeLg);
        _titleLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        _titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(_titleLabel);

        _goldLabel = new Label();
        _goldLabel.Text = "金币: —";
        _goldLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        _goldLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        header.AddChild(_goldLabel);

        var closeBtn = CreateButton("离开商店", new Vector2(90, 28));
        closeBtn.Pressed += () => { EmitSignal(SignalName.TradeFinished); HidePanel(); };
        header.AddChild(closeBtn);

        container.AddChild(CreateSeparatorH());

        // 主体：左(商品) + 右(详情+背包)
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        container.AddChild(body);

        // 左栏：商品列表
        var leftCol = new VBoxContainer();
        leftCol.CustomMinimumSize = new Vector2(420, 0);
        leftCol.AddThemeConstantOverride("separation", 4);
        body.AddChild(leftCol);

        var shopLabel = new Label();
        shopLabel.Text = "商品";
        shopLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        shopLabel.AddThemeColorOverride("font_color", ThemeTextAccent);
        leftCol.AddChild(shopLabel);

        var shopScroll = new ScrollContainer();
        shopScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        shopScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        leftCol.AddChild(shopScroll);

        _shopGrid = new GridContainer();
        _shopGrid.Columns = 4;
        _shopGrid.AddThemeConstantOverride("h_separation", 4);
        _shopGrid.AddThemeConstantOverride("v_separation", 4);
        shopScroll.AddChild(_shopGrid);

        // 分割
        leftCol.AddChild(CreateSeparatorH());

        var invLabel = new Label();
        invLabel.Text = "背包 (点击出售)";
        invLabel.AddThemeFontSizeOverride("font_size", FontSizeSm);
        invLabel.AddThemeColorOverride("font_color", ThemeTextSecondary);
        leftCol.AddChild(invLabel);

        var invScroll = new ScrollContainer();
        invScroll.CustomMinimumSize = new Vector2(0, 120);
        invScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        leftCol.AddChild(invScroll);

        _inventoryGrid = new GridContainer();
        _inventoryGrid.Columns = 4;
        _inventoryGrid.AddThemeConstantOverride("h_separation", 4);
        _inventoryGrid.AddThemeConstantOverride("v_separation", 4);
        invScroll.AddChild(_inventoryGrid);

        // 右栏：详情+操作
        var rightCol = new VBoxContainer();
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightCol.AddThemeConstantOverride("separation", 6);
        body.AddChild(rightCol);

        var detailTitle = new Label();
        detailTitle.Text = "物品详情";
        detailTitle.AddThemeFontSizeOverride("font_size", FontSizeMd);
        detailTitle.AddThemeColorOverride("font_color", ThemeTextAccent);
        rightCol.AddChild(detailTitle);

        var detailPanel = new PanelContainer();
        var dStyle = new StyleBoxFlat { BgColor = ThemeBgSecondary };
        dStyle.SetBorderWidthAll(1);
        dStyle.BorderColor = ThemeBorderDefault;
        dStyle.SetCornerRadiusAll(4);
        dStyle.SetContentMarginAll(8);
        detailPanel.AddThemeStyleboxOverride("panel", dStyle);
        detailPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightCol.AddChild(detailPanel);

        _detailText = new RichTextLabel();
        _detailText.BbcodeEnabled = true;
        _detailText.ScrollActive = true;
        _detailText.FitContent = false;
        _detailText.Text = "[color=#888]点击商品或背包物品查看详情[/color]";
        detailPanel.AddChild(_detailText);

        // 操作按钮
        _buyBtn = CreateButton("购买", new Vector2(0, 32));
        _buyBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _buyBtn.Visible = false;
        _buyBtn.Pressed += OnBuyPressed;
        rightCol.AddChild(_buyBtn);

        _sellBtn = CreateButton("出售", new Vector2(0, 32));
        _sellBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _sellBtn.AddThemeColorOverride("font_color", ThemeTextPositive);
        _sellBtn.Visible = false;
        _sellBtn.Pressed += OnSellPressed;
        rightCol.AddChild(_sellBtn);
    }
}
