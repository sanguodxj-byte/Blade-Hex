// CampaignScene.cs
// 战役模式场景 — 关卡间准备阶段
// 复用大地图的 PartyPanel（完整装备/属性/技能盘/背包）+ 商店 + 雇佣
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Data.Contexts;
using BladeHex.Combat;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.View.AssetSystem;
using BladeHex.View.UI.Overworld;
using BladeHex.UI;
using BladeHex.UI.Loading;

namespace BladeHex.Scenes.Campaign;

/// <summary>
/// 战役模式主场景 — 每关开始前展示准备界面：
/// - 顶部：关卡信息 + 金币
/// - 中部：复用 PartyPanel（完整的部队管理/装备/商店）
/// - 底部：开始战斗 / 雇佣 / 返回
/// </summary>
[GlobalClass]
public partial class CampaignScene : CanvasLayer
{
    private CampaignContext _ctx = null!;
    private List<CampaignLevelDef> _levels = null!;
    private CampaignLevelDef _currentLevelDef = null!;

    // 经济管理器（战役专用实例）
    private EconomyManager _economy = null!;

    // UI
    private PartyPanel? _partyPanel;
    private SkillTreeUI? _skillTreeUi;
    private Label _goldLabel = null!;
    private Label _levelTitle = null!;
    private RichTextLabel _levelDesc = null!;
    private VBoxContainer _sidePanel = null!;
    private VBoxContainer _hireList = null!;
    private VBoxContainer _medicPanel = null!;
    private VBoxContainer _medicList = null!;

    // 雇佣数据
    private List<UnitData> _hireableUnits = new();

    public override void _Ready()
    {
        var gs = BladeHex.Data.Globals.State;
        _ctx = gs.Campaign;
        _levels = CampaignLevels.GetDefaultCampaign();

        if (_ctx.CurrentLevel >= _levels.Count)
        {
            ShowVictoryScreen();
            return;
        }

        _currentLevelDef = _levels[_ctx.CurrentLevel];

        // 首关且队伍为空时：先显示角色命名界面
        if (_ctx.CurrentLevel == 0 && _ctx.Roster.IsEmpty)
        {
            ShowNameEntryScreen();
            return;
        }

        InitPrepPhase();
    }

    public override void _ExitTree()
    {
        // 退出时同步金币回 context
        if (_economy != null)
            _ctx.Gold = _economy.Gold;
    }

    // ========================================
    // 初始队伍生成
    // ========================================

    /// <summary>初始化准备阶段（命名完成后或非首关时调用）</summary>
    private void InitPrepPhase()
    {
        // 检查点自动保存
        _ctx.SaveCheckpoint();

        // 创建战役专用经济管理器（同步金币）
        _economy = new EconomyManager();
        _economy.Gold = _ctx.Gold;
        _economy.DailyWage = 0; // 战役不扣日薪
        AddChild(_economy);
        _economy.ResourcesChanged += SyncGold;

        GenerateHireableUnits();
        BuildUI();
    }

    // ========================================
    // 角色命名界面
    // ========================================

    private void ShowNameEntryScreen()
    {

        var bg = new ColorRect();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0.03f, 0.03f, 0.05f);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        var panelBg = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f) };
        panelBg.SetBorderWidthAll(2);
        panelBg.BorderColor = new Color(0.5f, 0.45f, 0.3f);
        panelBg.SetCornerRadiusAll(8);
        panelBg.SetContentMarginAll(40);
        panel.AddThemeStyleboxOverride("panel", panelBg);
        panel.CustomMinimumSize = new Vector2(500, 0);
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        panel.AddChild(vbox);

        var title = new Label { Text = "组建你的战队" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 队长名字
        vbox.AddChild(MakeFieldLabel("队长名字"));
        var leaderInput = new LineEdit { PlaceholderText = "输入队长名字...", CustomMinimumSize = new Vector2(0, 42) };
        leaderInput.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(leaderInput);

        // 副官名字
        vbox.AddChild(MakeFieldLabel("副官名字"));
        var companionInput = new LineEdit { PlaceholderText = "输入副官名字...", CustomMinimumSize = new Vector2(0, 42) };
        companionInput.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(companionInput);

        // 确认按钮
        var confirmBtn = MakeButton("开始战役", new Color(0.9f, 0.75f, 0.4f));
        confirmBtn.CustomMinimumSize = new Vector2(200, 50);
        confirmBtn.AddThemeFontSizeOverride("font_size", 20);
        confirmBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        confirmBtn.Pressed += () =>
        {
            string leaderName = string.IsNullOrWhiteSpace(leaderInput.Text) ? "队长" : leaderInput.Text.Trim();
            string companionName = string.IsNullOrWhiteSpace(companionInput.Text) ? "副官" : companionInput.Text.Trim();
            GenerateStartingRoster(leaderName, companionName);

            // 清除命名界面，进入准备阶段
            foreach (var child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }
            InitPrepPhase();
        };
        vbox.AddChild(confirmBtn);
    }

    private static Label MakeFieldLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.6f));
        return label;
    }

    private void GenerateStartingRoster(string leaderName, string companionName)
    {
        // 生成 2 个初始角色（等级 1）
        var leader = CharacterGenerator.GenerateRandomAdventurer(1);
        leader.UnitName = leaderName;
        CharacterGenerator.EquipStartingGear(leader);
        PartyRoster.SetCurrentHp(leader, leader.BaseMaxHp);
        _ctx.Roster.SetLeader(leader);

        var companion = CharacterGenerator.GenerateRandomAdventurer(1);
        companion.UnitName = companionName;
        CharacterGenerator.EquipStartingGear(companion);
        PartyRoster.SetCurrentHp(companion, companion.BaseMaxHp);
        _ctx.Roster.Add(companion);
    }

    // ========================================
    // 可雇佣角色生成
    // ========================================

    private void GenerateHireableUnits()
    {
        _hireableUnits.Clear();
        // 雇佣角色等级 = 当前关卡敌方等级 - 1（至少 1）
        int level = Mathf.Max(1, _currentLevelDef.EnemyLevel - 1);
        int itemLevel = EquipmentGenerator.GetItemLevelFromCr(RPGRuleEngine.GetCrFromLevel(level));
        string difficulty = _currentLevelDef.Difficulty switch { 0 => "easy", 2 => "hard", _ => "normal" };

        int count = (int)GD.RandRange(2, 4);
        for (int i = 0; i < count; i++)
        {
            var unit = CharacterGenerator.GenerateRandomAdventurer(level);
            // 完整装备套装（武器+副手+护甲+头盔+饰品等）
            EquipmentGenerator.EquipFullSet(unit, itemLevel, difficulty);
            PartyRoster.SetCurrentHp(unit, unit.BaseMaxHp);
            _hireableUnits.Add(unit);
        }
        GD.Print($"[Campaign] 生成 {_hireableUnits.Count} 个可雇佣角色 (等级 {level}, 装备等级 {itemLevel})");
    }

    // ========================================
    // 商店物品生成
    // ========================================

    private List<ItemData> GenerateShopStock()
    {
        int prosperity = CampaignPricingService.GetShopProsperity(CreateCampaignEconomyContext());
        return BladeHex.Strategic.Facilities.MarketStockService.GenerateStock(prosperity);
    }

    // ========================================
    // UI 构建
    // ========================================

    private void BuildUI()
    {
        // 背景
        var bg = new ColorRect();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0.03f, 0.03f, 0.05f);
        AddChild(bg);

        // 关卡插图（填充顶栏和底栏之间的区域）
        var illustPath = $"res://assets/generated_campaign_illust/campaign_{(_ctx.CurrentLevel + 1):D2}_{GetLevelImageSuffix()}.png";
        GD.Print($"[Campaign] 加载插图: {illustPath}");
        var illustTex = TextureAssetResolver.LoadCampaignIllustration(illustPath);
        GD.Print($"[Campaign] 插图加载结果: {(illustTex != null ? $"OK {illustTex.GetWidth()}x{illustTex.GetHeight()}" : "NULL")}");
        if (illustTex != null)
        {
            var illustRect = new TextureRect();
            illustRect.Texture = illustTex;
            illustRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            illustRect.OffsetTop = 80;
            illustRect.OffsetBottom = -70;
            illustRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            illustRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            illustRect.Modulate = new Color(0.6f, 0.6f, 0.6f, 0.8f); // 压暗以便文字可读
            AddChild(illustRect);
        }

        // 顶部信息栏
        var topBar = new PanelContainer();
        topBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        topBar.OffsetBottom = 80;
        var topBg = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f) };
        topBg.SetBorderWidthAll(0);
        topBg.BorderColor = new Color(0.4f, 0.35f, 0.25f);
        topBg.SetContentMarginAll(16);
        topBar.AddThemeStyleboxOverride("panel", topBg);
        AddChild(topBar);

        var topHbox = new HBoxContainer();
        topHbox.AddThemeConstantOverride("separation", 20);
        topBar.AddChild(topHbox);

        // 关卡标题
        _levelTitle = new Label();
        _levelTitle.Text = $"关卡 {_ctx.CurrentLevel + 1}/{_levels.Count} — {_currentLevelDef.Name}";
        _levelTitle.AddThemeFontSizeOverride("font_size", 24);
        _levelTitle.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
        _levelTitle.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        topHbox.AddChild(_levelTitle);

        // 关卡描述
        _levelDesc = new RichTextLabel();
        _levelDesc.BbcodeEnabled = true;
        _levelDesc.ScrollActive = false;
        _levelDesc.FitContent = true;
        _levelDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _levelDesc.Text = $"[i]{_currentLevelDef.Description}[/i]";
        _levelDesc.AddThemeFontSizeOverride("normal_font_size", 14);
        _levelDesc.AddThemeColorOverride("default_color", new Color(0.65f, 0.65f, 0.7f));
        _levelDesc.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _levelDesc.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _levelDesc.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        topHbox.AddChild(_levelDesc);

        // 金币
        _goldLabel = new Label();
        _goldLabel.Text = $"💰 {_ctx.Gold}";
        _goldLabel.AddThemeFontSizeOverride("font_size", 20);
        _goldLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        _goldLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        topHbox.AddChild(_goldLabel);

        // 底部按钮栏
        var bottomBar = new PanelContainer();
        bottomBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottomBar.OffsetTop = -70;
        var botBg = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f) };
        botBg.SetContentMarginAll(12);
        bottomBar.AddThemeStyleboxOverride("panel", botBg);
        AddChild(bottomBar);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        bottomBar.AddChild(btnRow);

        var backBtn = MakeButton("返回主菜单", new Color(0.5f, 0.45f, 0.35f));
        backBtn.Pressed += OnBackToMenu;
        btnRow.AddChild(backBtn);

        var partyBtn = MakeButton("管理部队", new Color(0.6f, 0.7f, 0.9f));
        partyBtn.Pressed += OnOpenPartyPanel;
        btnRow.AddChild(partyBtn);

        var shopBtn = MakeButton("商店", new Color(0.5f, 0.8f, 0.5f));
        shopBtn.Pressed += OnOpenShop;
        btnRow.AddChild(shopBtn);

        var hireBtn = MakeButton("雇佣", new Color(0.7f, 0.6f, 0.9f));
        hireBtn.Pressed += OnOpenHirePanel;
        btnRow.AddChild(hireBtn);

        var skillBtn = MakeButton("技能盘", new Color(0.7f, 0.6f, 1.0f));
        skillBtn.Pressed += OnOpenSkillTree;
        btnRow.AddChild(skillBtn);

        var medicBtn = MakeButton("医官治疗", new Color(0.9f, 0.4f, 0.4f));
        medicBtn.Pressed += OnOpenMedicPanel;
        btnRow.AddChild(medicBtn);

        var startBtn = MakeButton("⚔ 开始战斗", new Color(0.95f, 0.8f, 0.4f));
        startBtn.CustomMinimumSize = new Vector2(180, 50);
        startBtn.AddThemeFontSizeOverride("font_size", 20);
        startBtn.Pressed += OnStartBattle;
        btnRow.AddChild(startBtn);

        // PartyPanel（初始隐藏，点击"管理部队"时显示）
        _partyPanel = new PartyPanel();
        _partyPanel.Visible = false;
        _partyPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _partyPanel.OffsetTop = 84;
        _partyPanel.OffsetBottom = -74;
        _partyPanel.OffsetLeft = 16;
        _partyPanel.OffsetRight = -16;
        _partyPanel.PanelClosed += () => { _partyPanel.Visible = false; };
        AddChild(_partyPanel);

        // 雇佣侧面板（初始隐藏）
        BuildHirePanel();

        // 医官救治面板（初始隐藏）
        BuildMedicPanel();
    }

    private void BuildHirePanel()
    {
        _sidePanel = new VBoxContainer();
        _sidePanel.Visible = false;
        _sidePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _sidePanel.OffsetTop = 84;
        _sidePanel.OffsetBottom = -74;
        _sidePanel.OffsetLeft = 16;
        _sidePanel.OffsetRight = -16;
        AddChild(_sidePanel);

        // 半透明背景
        var panelBg = new PanelContainer();
        panelBg.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        panelBg.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var bgStyle = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.06f, 0.95f) };
        bgStyle.SetBorderWidthAll(2);
        bgStyle.BorderColor = new Color(0.4f, 0.35f, 0.5f);
        bgStyle.SetCornerRadiusAll(6);
        bgStyle.SetContentMarginAll(20);
        panelBg.AddThemeStyleboxOverride("panel", bgStyle);
        _sidePanel.AddChild(panelBg);

        var innerVbox = new VBoxContainer();
        innerVbox.AddThemeConstantOverride("separation", 12);
        innerVbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        innerVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panelBg.AddChild(innerVbox);

        // 标题行
        var titleRow = new HBoxContainer();
        innerVbox.AddChild(titleRow);

        var title = new Label { Text = "雇佣佣兵" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.95f));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);

        var closeBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(40, 40) };
        closeBtn.AddThemeFontSizeOverride("font_size", 20);
        closeBtn.Pressed += () => { _sidePanel.Visible = false; };
        titleRow.AddChild(closeBtn);

        // 雇佣列表
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        innerVbox.AddChild(scroll);

        _hireList = new VBoxContainer();
        _hireList.AddThemeConstantOverride("separation", 8);
        _hireList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _hireList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_hireList);

        RefreshHireList();
    }

    // ========================================
    // 交互
    // ========================================

    private void OnOpenPartyPanel()
    {
        _sidePanel.Visible = false;
        _medicPanel.Visible = false;
        _partyPanel!.Open(_ctx.Roster, _ctx.Inventory);
    }

    private void OnOpenShop()
    {
        _sidePanel.Visible = false;
        _medicPanel.Visible = false;
        var stock = GenerateShopStock();
        int prosperity = CampaignPricingService.GetShopProsperity(CreateCampaignEconomyContext());
        _partyPanel!.OpenShop(_ctx.Roster, _ctx.Inventory, "战役商店", _economy, stock, prosperity);
    }

    private void OnOpenHirePanel()
    {
        _partyPanel!.Visible = false;
        if (_skillTreeUi != null) _skillTreeUi.Visible = false;
        _medicPanel.Visible = false;
        _sidePanel.Visible = true;
        RefreshHireList();
    }

    private void OnOpenMedicPanel()
    {
        _partyPanel!.Visible = false;
        if (_skillTreeUi != null) _skillTreeUi.Visible = false;
        _sidePanel.Visible = false;
        _medicPanel.Visible = true;
        RefreshMedicList();
    }

    private void OnOpenSkillTree()
    {
        _partyPanel!.Visible = false;
        _sidePanel.Visible = false;
        _medicPanel.Visible = false;

        var stm = BladeHex.Data.Globals.SkillTreesOrNull;
        if (stm == null || stm.TreeData == null)
        {
            GD.PrintErr("[CampaignScene] SkillTreeManager 未初始化");
            return;
        }

        if (_skillTreeUi == null)
        {
            _skillTreeUi = new SkillTreeUI();
            _skillTreeUi.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _skillTreeUi.OffsetTop = 84;
            _skillTreeUi.OffsetBottom = -74;
            AddChild(_skillTreeUi);
            _skillTreeUi.CloseRequested += () => { _skillTreeUi.Visible = false; };
        }

        // 打开队伍第一个角色的技能盘（玩家可在 PartyPanel 切换角色后再打开）
        var leader = _ctx.Roster.Leader;
        if (leader == null) return;

        long charId = (long)leader.GetInstanceId();
        var charTree = stm.GetSkillTree(charId);
        if (charTree == null)
        {
            charTree = stm.CreateSkillTree(charId, leader.Level);
            stm.InitCharacterLevel(charId, leader.Level);
        }

        // v0.7: 传入角色 + 队伍列表，启用底部角色切换
        _skillTreeUi.OpenSkillTree(charTree, stm.TreeData, leader, _ctx.Roster.Members);
    }

    private void OnStartBattle()
    {
        // 同步金币
        _ctx.Gold = _economy.Gold;

        // CampaignBattleScene 直接从 CampaignContext + CampaignLevels 读取配置
        LoadingScreen.LoadScene(
            "res://BladeHexFrontend/src/scenes/campaign/campaign_battle.tscn",
            LoadingScreen.PhaseType.Combat);
    }

    private void OnBackToMenu()
    {
        _ctx.IsActive = false;
        BladeHex.View.SceneTransition.ChangeSceneTo(
            GetTree(), "res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
    }

    // ========================================
    // 雇佣列表
    // ========================================

    private void RefreshHireList()
    {
        if (_hireList == null) return;

        foreach (var child in _hireList.GetChildren())
        {
            _hireList.RemoveChild(child);
            child.QueueFree();
        }

        if (_hireableUnits.Count == 0)
        {
            var empty = new Label { Text = "无可雇佣角色" };
            empty.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _hireList.AddChild(empty);
            return;
        }

        foreach (var unit in _hireableUnits)
        {
            var row = CreateHireRow(unit);
            _hireList.AddChild(row);
        }
    }

    private Control CreateHireRow(UnitData unit)
    {
        var panel = new PanelContainer();
        var bg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.12f, 0.8f) };
        bg.SetBorderWidthAll(1);
        bg.BorderColor = new Color(0.3f, 0.28f, 0.4f);
        bg.SetCornerRadiusAll(4);
        bg.SetContentMarginAll(12);
        panel.AddThemeStyleboxOverride("panel", bg);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(hbox);

        // 信息
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(infoVbox);

        var nameLabel = new Label { Text = unit.UnitName ?? "佣兵" };
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.8f));
        infoVbox.AddChild(nameLabel);

        string raceName = unit.Race?.RaceName ?? "未知";
        var statsLabel = new Label
        {
            Text = $"Lv.{unit.Level} {raceName} | 力{unit.Str} 敏{unit.Dex} 体{unit.Con} 智{unit.Intel} 感{unit.Wis} 魅{unit.Cha}"
        };
        statsLabel.AddThemeFontSizeOverride("font_size", 13);
        statsLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        infoVbox.AddChild(statsLabel);

        var weaponName = unit.PrimaryMainHand?.ItemName ?? "无武器";
        var armorName = unit.Armor?.ItemName ?? "无甲";
        var equipLabel = new Label { Text = $"装备: {weaponName} / {armorName}" };
        equipLabel.AddThemeFontSizeOverride("font_size", 12);
        equipLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.5f));
        infoVbox.AddChild(equipLabel);

        // 费用 + 按钮
        int hireCost = CampaignPricingService.GetHireCost(unit, CreateCampaignEconomyContext());
        var costLabel = new Label { Text = $"{hireCost} 金" };
        costLabel.AddThemeFontSizeOverride("font_size", 16);
        costLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        costLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        hbox.AddChild(costLabel);

        var hireBtn = MakeButton("雇佣", new Color(0.5f, 0.5f, 0.85f));
        hireBtn.Disabled = _economy.Gold < hireCost || _ctx.Roster.IsFull;
        hireBtn.Pressed += () => DoHire(unit, hireCost);
        hireBtn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        hbox.AddChild(hireBtn);

        return panel;
    }

    private void DoHire(UnitData unit, int cost)
    {
        if (!_economy.SpendGold(cost)) return;
        if (_ctx.Roster.IsFull) return;

        _hireableUnits.Remove(unit);
        _ctx.Roster.Add(unit);

        RefreshHireList();
        UpdateGoldDisplay();
        BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_click");
    }

    // ========================================
    // 胜利画面
    // ========================================

    private void ShowVictoryScreen()
    {
        var bg = new ColorRect();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0.02f, 0.02f, 0.04f);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 30);
        center.AddChild(vbox);

        var title = new Label { Text = "战 役 通 关" };
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.4f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var desc = new Label { Text = "恭喜！你已击败暗影领主，完成了全部战役关卡。" };
        desc.AddThemeFontSizeOverride("font_size", 20);
        desc.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
        desc.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(desc);

        var backBtn = MakeButton("返回主菜单", new Color(0.9f, 0.75f, 0.4f));
        backBtn.CustomMinimumSize = new Vector2(200, 50);
        backBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        backBtn.Pressed += OnBackToMenu;
        vbox.AddChild(backBtn);
    }

    // ========================================
    // 辅助
    // ========================================

    private void SyncGold()
    {
        _ctx.Gold = _economy.Gold;
        UpdateGoldDisplay();
    }

    private void UpdateGoldDisplay()
    {
        if (_goldLabel != null)
            _goldLabel.Text = $"💰 {_economy.Gold}";
    }

    private string GetLevelImageSuffix()
    {
        // Map level names to image file suffixes
        return (_ctx.CurrentLevel) switch
        {
            0 => "forest_ambush",
            1 => "undead_crypt",
            2 => "wild_hunt",
            3 => "bandit_stronghold",
            4 => "shadow_swamp",
            5 => "abandoned_mine",
            6 => "orc_camp",
            7 => "dragon_approach",
            8 => "dark_altar",
            9 => "shadow_lord",
            _ => "forest_ambush",
        };
    }

    private static Button MakeButton(string text, Color accent)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(110, 42);
        btn.AddThemeFontSizeOverride("font_size", 16);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(accent.R * 0.25f, accent.G * 0.25f, accent.B * 0.25f, 0.85f);
        style.SetBorderWidthAll(1);
        style.BorderColor = accent;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("normal", style);

        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(accent.R * 0.4f, accent.G * 0.4f, accent.B * 0.4f, 0.9f);
        btn.AddThemeStyleboxOverride("hover", hover);

        btn.AddThemeColorOverride("font_color", accent);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);

        return btn;
    }

    // ========================================
    // 战壕医官救治系统 UI与逻辑
    // ========================================

    private void BuildMedicPanel()
    {
        _medicPanel = new VBoxContainer();
        _medicPanel.Visible = false;
        _medicPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _medicPanel.OffsetTop = 84;
        _medicPanel.OffsetBottom = -74;
        _medicPanel.OffsetLeft = 16;
        _medicPanel.OffsetRight = -16;
        AddChild(_medicPanel);

        // 半透明背景
        var panelBg = new PanelContainer();
        panelBg.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        panelBg.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var bgStyle = new StyleBoxFlat { BgColor = new Color(0.06f, 0.03f, 0.03f, 0.95f) };
        bgStyle.SetBorderWidthAll(2);
        bgStyle.BorderColor = new Color(0.7f, 0.35f, 0.35f);
        bgStyle.SetCornerRadiusAll(6);
        bgStyle.SetContentMarginAll(20);
        panelBg.AddThemeStyleboxOverride("panel", bgStyle);
        _medicPanel.AddChild(panelBg);

        var innerVbox = new VBoxContainer();
        innerVbox.AddThemeConstantOverride("separation", 12);
        innerVbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        innerVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panelBg.AddChild(innerVbox);

        // 标题行
        var titleRow = new HBoxContainer();
        innerVbox.AddChild(titleRow);

        var title = new Label { Text = "战壕医官救治" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.6f));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);

        var closeBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(40, 40) };
        closeBtn.AddThemeFontSizeOverride("font_size", 20);
        closeBtn.Pressed += () => { _medicPanel.Visible = false; };
        titleRow.AddChild(closeBtn);

        // 救治列表
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        innerVbox.AddChild(scroll);

        _medicList = new VBoxContainer();
        _medicList.AddThemeConstantOverride("separation", 8);
        _medicList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _medicList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_medicList);

        RefreshMedicList();
    }

    private void RefreshMedicList()
    {
        if (_medicList == null) return;

        foreach (var child in _medicList.GetChildren())
        {
            _medicList.RemoveChild(child);
            child.QueueFree();
        }

        var woundedUnits = new List<UnitData>();
        foreach (var unit in _ctx.Roster.Members)
        {
            if (unit.IsWounded)
                woundedUnits.Add(unit);
        }

        if (woundedUnits.Count == 0)
        {
            var empty = new Label { Text = "当前无重伤未愈的佣兵" };
            empty.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _medicList.AddChild(empty);
            return;
        }

        foreach (var unit in woundedUnits)
        {
            var row = CreateMedicRow(unit);
            _medicList.AddChild(row);
        }
    }

    private Control CreateMedicRow(UnitData unit)
    {
        var panel = new PanelContainer();
        var bg = new StyleBoxFlat { BgColor = new Color(0.12f, 0.08f, 0.08f, 0.8f) };
        bg.SetBorderWidthAll(1);
        bg.BorderColor = new Color(0.5f, 0.28f, 0.28f);
        bg.SetCornerRadiusAll(4);
        bg.SetContentMarginAll(12);
        panel.AddThemeStyleboxOverride("panel", bg);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(hbox);

        // 信息
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(infoVbox);

        var nameLabel = new Label { Text = unit.UnitName ?? "佣兵" };
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.8f));
        infoVbox.AddChild(nameLabel);

        string raceName = unit.Race?.RaceName ?? "未知";
        var statsLabel = new Label
        {
            Text = $"Lv.{unit.Level} {raceName} | 状态: 重伤未愈"
        };
        statsLabel.AddThemeFontSizeOverride("font_size", 13);
        statsLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.5f, 0.5f));
        infoVbox.AddChild(statsLabel);

        // 费用 + 按钮
        int cost = CampaignPricingService.GetMedicTreatmentCost(unit, CreateCampaignEconomyContext());
        var costLabel = new Label { Text = $"{cost} 金" };
        costLabel.AddThemeFontSizeOverride("font_size", 16);
        costLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.3f));
        costLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        hbox.AddChild(costLabel);

        var treatBtn = MakeButton("救治", new Color(0.9f, 0.5f, 0.5f));
        treatBtn.Disabled = _economy.Gold < cost;
        treatBtn.Pressed += () => DoTreat(unit, cost);
        treatBtn.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        hbox.AddChild(treatBtn);

        return panel;
    }

    private void DoTreat(UnitData unit, int cost)
    {
        if (!_economy.SpendGold(cost)) return;

        unit.IsWounded = false;
        PartyRoster.SetCurrentHp(unit, unit.BaseMaxHp);

        RefreshMedicList();
        UpdateGoldDisplay();
        BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_click");
    }

    private CampaignEconomyContext CreateCampaignEconomyContext()
    {
        return new CampaignEconomyContext(
            LevelIndex: _ctx.CurrentLevel,
            EnemyLevel: _currentLevelDef.EnemyLevel,
            EnemyCount: _currentLevelDef.EnemyCount,
            Difficulty: _currentLevelDef.Difficulty,
            BattleSize: _currentLevelDef.BattleSize,
            IsBoss: _currentLevelDef.IsBoss);
    }
}
