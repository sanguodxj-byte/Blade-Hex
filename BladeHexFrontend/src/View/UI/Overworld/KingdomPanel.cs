using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.Kingdom;
using BladeHex.Scenes.Overworld;
using BladeHex.UI.Common;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 国家外交控制面板
/// </summary>
public partial class KingdomPanel : PanelContainer
{
    private OverworldEntityManager? _entityMgr;
    private ReputationTracker? _reputationTracker;
    private int _currentDay = 1;

    private Label _factionLabel = null!;
    private Label _influenceLabel = null!;
    private VBoxContainer _warListContainer = null!;
    private VBoxContainer _armyListContainer = null!;  // E5: 军团列表容器
    private OptionButton _declareTargetSelector = null!;
    private OptionButton _peaceTargetSelector = null!;
    private Label _statusLabel = null!;

    private Button _declareBtn = null!;
    private Button _peaceBtn = null!;

    private string? _playerFaction;

    // === 新增 Tab 切换容器 ===
    private VBoxContainer _diplomacyTab = null!;
    private VBoxContainer _prisonersTab = null!;
    private VBoxContainer _prisonerListContainer = null!;
    private Label _prisonerStatusLabel = null!;
    private VBoxContainer _kingdomTab = null!;  // M7: 我的王国 Tab
    private BladeHex.View.UI.Encyclopedia.EncyclopediaIndexPanel? _encyclopediaPanel;
    private KingdomDashboardPanel? _kingdomDashboardPanel;
    private FoundKingdomDialog? _foundKingdomDialog;

    public override void _Ready()
    {
        // 1. Sleek glassmorphism flat theme
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f),
            BorderWidthTop = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f), // BorderHighlight
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomLeft = 16,
            CornerRadiusBottomRight = 16,
            ContentMarginLeft = 25,
            ContentMarginRight = 25,
            ContentMarginTop = 20,
            ContentMarginBottom = 20
        };
        AddThemeStyleboxOverride("panel", style);

        CustomMinimumSize = new Vector2(520, 600);
        OverlayPanelLayout.Center(this);

        // 2. Main layout
        var mainVbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(mainVbox);

        // Header
        var titleHbox = new HBoxContainer();
        mainVbox.AddChild(titleHbox);

        var titleLabel = new Label { Text = "国家外交与俘虏事务" };
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f)); // TextAccent
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleHbox.AddChild(titleLabel);

        var closeBtn = new Button { Text = " X " };
        closeBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeBtn.Pressed += () => Visible = false;
        titleHbox.AddChild(closeBtn);

        mainVbox.AddChild(new HSeparator());

        // Tab Selector Bar
        var tabHbox = new HBoxContainer();
        tabHbox.AddThemeConstantOverride("separation", 15);
        mainVbox.AddChild(tabHbox);

        var diplomacyBtn = new Button { Text = "国家外交", CustomMinimumSize = new Vector2(120, 36) };
        diplomacyBtn.AddThemeFontSizeOverride("font_size", 16);
        tabHbox.AddChild(diplomacyBtn);

        var prisonersBtn = new Button { Text = "兵团俘虏", CustomMinimumSize = new Vector2(120, 36) };
        prisonersBtn.AddThemeFontSizeOverride("font_size", 16);
        tabHbox.AddChild(prisonersBtn);

        var encyclopediaBtn = new Button { Text = "世界百科", CustomMinimumSize = new Vector2(120, 36) };
        encyclopediaBtn.AddThemeFontSizeOverride("font_size", 16);
        tabHbox.AddChild(encyclopediaBtn);

        // M7: 我的王国 Tab 按钮
        var kingdomBtn = new Button { Text = "我的王国", CustomMinimumSize = new Vector2(120, 36) };
        kingdomBtn.AddThemeFontSizeOverride("font_size", 16);
        tabHbox.AddChild(kingdomBtn);

        mainVbox.AddChild(new HSeparator());

        // ─── Tab A: 国家外交 ───
        _diplomacyTab = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mainVbox.AddChild(_diplomacyTab);

        // Info Block (Faction and Influence)
        var infoHbox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        _diplomacyTab.AddChild(infoHbox);

        _factionLabel = new Label { Text = "代表国家: 加载中..." };
        _factionLabel.AddThemeFontSizeOverride("font_size", 15);
        infoHbox.AddChild(_factionLabel);

        infoHbox.AddChild(new Control { CustomMinimumSize = new Vector2(30, 0) });

        _influenceLabel = new Label { Text = "影响力余额: 0/200" };
        _influenceLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.9f));
        _influenceLabel.AddThemeFontSizeOverride("font_size", 15);
        infoHbox.AddChild(_influenceLabel);

        _diplomacyTab.AddChild(new HSeparator());

        // Subtitle - Active Wars
        var warHeader = new Label { Text = "【当前进行的战争】" };
        warHeader.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.3f));
        warHeader.AddThemeFontSizeOverride("font_size", 14);
        _diplomacyTab.AddChild(warHeader);

        // War scroll list
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 100), SizeFlagsVertical = SizeFlags.ExpandFill };
        _warListContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_warListContainer);
        _diplomacyTab.AddChild(scroll);

        _diplomacyTab.AddChild(new HSeparator());

        // E5 ─ 军团列表区块
        var armyHeader = new Label { Text = "【本国活跃集结军团】" };
        armyHeader.AddThemeColorOverride("font_color", new Color(0.9f, 0.65f, 0.2f)); // 战金色
        armyHeader.AddThemeFontSizeOverride("font_size", 14);
        _diplomacyTab.AddChild(armyHeader);

        var armyScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 90), SizeFlagsVertical = SizeFlags.ExpandFill };
        _armyListContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        armyScroll.AddChild(_armyListContainer);
        _diplomacyTab.AddChild(armyScroll);

        _diplomacyTab.AddChild(new HSeparator());

        // Decision Section
        var decisionGrid = new GridContainer { Columns = 3 };
        decisionGrid.AddThemeConstantOverride("h_separation", 15);
        decisionGrid.AddThemeConstantOverride("v_separation", 10);
        _diplomacyTab.AddChild(decisionGrid);

        // Row 1: Declare War
        var declareLabel = new Label { Text = "宣战意向国:" };
        decisionGrid.AddChild(declareLabel);

        _declareTargetSelector = new OptionButton { CustomMinimumSize = new Vector2(180, 32) };
        decisionGrid.AddChild(_declareTargetSelector);

        _declareBtn = new Button { Text = "正式宣战 (50 影)" };
        _declareBtn.Pressed += OnDeclarePressed;
        decisionGrid.AddChild(_declareBtn);

        // Row 2: Make Peace
        var peaceLabel = new Label { Text = "停战协议国:" };
        decisionGrid.AddChild(peaceLabel);

        _peaceTargetSelector = new OptionButton { CustomMinimumSize = new Vector2(180, 32) };
        decisionGrid.AddChild(_peaceTargetSelector);

        _peaceBtn = new Button { Text = "递交媾和 (80 影)" };
        _peaceBtn.Pressed += OnPeacePressed;
        decisionGrid.AddChild(_peaceBtn);

        // Status Label (Success/Errors)
        _statusLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _diplomacyTab.AddChild(_statusLabel);


        // ─── Tab B: 兵团俘虏 ───
        _prisonersTab = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
        mainVbox.AddChild(_prisonersTab);

        var prisonerHeader = new Label { Text = "【兵团随队俘虏营】" };
        prisonerHeader.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.3f));
        prisonerHeader.AddThemeFontSizeOverride("font_size", 14);
        _prisonersTab.AddChild(prisonerHeader);

        var prisonerScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 360), SizeFlagsVertical = SizeFlags.ExpandFill };
        _prisonerListContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _prisonerListContainer.AddThemeConstantOverride("separation", 8);
        prisonerScroll.AddChild(_prisonerListContainer);
        _prisonersTab.AddChild(prisonerScroll);

        _prisonerStatusLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        _prisonerStatusLabel.AddThemeFontSizeOverride("font_size", 13);
        _prisonersTab.AddChild(_prisonerStatusLabel);


        // ─── Tab C: 我的王国 (M7) ───
        _kingdomTab = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
        mainVbox.AddChild(_kingdomTab);

        var kingdomHeader = new Label { Text = "【我的王国】" };
        kingdomHeader.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        kingdomHeader.AddThemeFontSizeOverride("font_size", 16);
        _kingdomTab.AddChild(kingdomHeader);

        var kingdomStatusLabel = new Label { Text = "尚未建立王国。占领城堡并达到开国条件后可建立王国。", HorizontalAlignment = HorizontalAlignment.Center };
        kingdomStatusLabel.AddThemeFontSizeOverride("font_size", 14);
        kingdomStatusLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        _kingdomTab.AddChild(kingdomStatusLabel);

        // 建立王国按钮
        var foundKingdomBtn = new Button { Text = "建立王国", CustomMinimumSize = new Vector2(200, 40) };
        foundKingdomBtn.AddThemeFontSizeOverride("font_size", 16);
        foundKingdomBtn.Pressed += () => OnFoundKingdomPressed();
        _kingdomTab.AddChild(foundKingdomBtn);

        // 我的王国按钮（已有王国时显示）
        var myKingdomBtn = new Button { Text = "查看王国详情", CustomMinimumSize = new Vector2(200, 40) };
        myKingdomBtn.AddThemeFontSizeOverride("font_size", 16);
        myKingdomBtn.Pressed += () => OnMyKingdomPressed();
        _kingdomTab.AddChild(myKingdomBtn);


        // Tab Buttons Press Connections
        diplomacyBtn.Pressed += () => {
            _diplomacyTab.Visible = true;
            _prisonersTab.Visible = false;
            Refresh();
        };

        prisonersBtn.Pressed += () => {
            _diplomacyTab.Visible = false;
            _prisonersTab.Visible = true;
            RefreshPrisoners();
        };

        encyclopediaBtn.Pressed += () => {
            if (_encyclopediaPanel == null)
            {
                _encyclopediaPanel = new BladeHex.View.UI.Encyclopedia.EncyclopediaIndexPanel();
                OverlayPanelLayout.AttachCentered(GetParent(), _encyclopediaPanel);
            }
            if (_entityMgr != null)
            {
                _encyclopediaPanel.Initialize(_entityMgr, _entityMgr.Journal);
                OverlayPanelLayout.Center(_encyclopediaPanel);
                _encyclopediaPanel.Visible = true;
            }
        };

        // M7: 我的王国 Tab 按钮处理
        kingdomBtn.Pressed += () => {
            _diplomacyTab.Visible = false;
            _prisonersTab.Visible = false;
            _kingdomTab.Visible = true;
            RefreshKingdomTab();
        };

        Visible = false;
    }

    /// <summary>
    /// 初始化数据引用并刷新面板
    /// </summary>
    public void Initialize(OverworldEntityManager entityMgr, ReputationTracker reputation, int currentDay)
    {
        _entityMgr = entityMgr;
        _reputationTracker = reputation;
        _currentDay = currentDay;
        
        Refresh();
    }

    /// <summary>
    /// 全量刷新外交控制面板数据
    /// </summary>
    public void Refresh()
    {
        if (_entityMgr == null || _reputationTracker == null) return;

        // 1. 获取玩家当前代表的国家归属
        var resolver = new PlayerNationResolver();
        _playerFaction = resolver.GetCurrent(_reputationTracker, _currentDay);

        if (string.IsNullOrEmpty(_playerFaction))
        {
            _factionLabel.Text = "代表国家: 无 (需要在某势力的声望 >= 30)";
            _factionLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            _influenceLabel.Text = "影响力余额: ---";
            
            // 锁定操作
            _declareBtn.Disabled = true;
            _peaceBtn.Disabled = true;
            _declareTargetSelector.Disabled = true;
            _peaceTargetSelector.Disabled = true;
            ClearWarList();
            
            var emptyLabel = new Label { Text = "因您尚未加入国家势力，无法处理本国战事与外交决议。" };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _warListContainer.AddChild(emptyLabel);
            return;
        }

        // 2. 正常加入势力，激活控制
        _declareBtn.Disabled = false;
        _peaceBtn.Disabled = false;
        _declareTargetSelector.Disabled = false;
        _peaceTargetSelector.Disabled = false;

        // 获取国名
        string factionName = _playerFaction == "kingdom" ? "金石同盟王国" : _playerFaction;
        var matchedNation = _entityMgr.Nations.FirstOrDefault(n => n.Id == _playerFaction);
        if (matchedNation != null) factionName = matchedNation.DisplayName;

        _factionLabel.Text = $"代表国家: {factionName}";
        _factionLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.9f, 0.4f));

        // 影响力余额
        int influenceScore = _entityMgr.WorldEngine.Influence.Get(_playerFaction);
        _influenceLabel.Text = $"影响力余额: {influenceScore}/200";

        // 3. 渲染战争明细列表
        PopulateWarList();

        // E5: 渲染军团列表
        PopulateArmyList();

        // 4. 填充可宣战对象与媾和对象下拉框
        PopulateTargetSelectors(influenceScore);
    }

    private void ClearWarList()
    {
        foreach (var child in _warListContainer.GetChildren())
            child.QueueFree();
    }

    // E5: 军团列表渲染
    private void PopulateArmyList()
    {
        foreach (var child in _armyListContainer.GetChildren())
            child.QueueFree();

        if (_entityMgr == null || string.IsNullOrEmpty(_playerFaction)) return;

        var myArmies = _entityMgr.Armies.All()
            .Where(a => a.Faction == _playerFaction && a.State != ArmyState.Disbanding)
            .ToList();

        if (myArmies.Count == 0)
        {
            var emptyLabel = new Label { Text = "本国目前没有活跃集结军团。" };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
            _armyListContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var army in myArmies)
        {
            var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };

            // 状态图标
            string stateIcon = army.State switch
            {
                ArmyState.Forming   => "🗡",
                ArmyState.Marching  => "👣",
                ArmyState.Besieging => "🏰",
                _                   => "ℹ"
            };
            string stateText = army.State switch
            {
                ArmyState.Forming   => "集结中",
                ArmyState.Marching  => "行军中",
                ArmyState.Besieging => "围攻中",
                _                   => "未知"
            };

            var infoLabel = new Label
            {
                Text = $"{stateIcon} 元帅:{army.Marshal?.EntityName ?? "?"}  成员:{army.LivingMemberCount}  目标:{army.TargetPoiName}  [{stateText}]",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.Off,
                ClipText = true
            };

            Color labelColor = army.State switch
            {
                ArmyState.Besieging => new Color(0.95f, 0.5f, 0.2f),
                ArmyState.Marching  => new Color(0.6f, 0.9f, 0.6f),
                _                   => new Color(0.8f, 0.75f, 0.5f)
            };
            infoLabel.AddThemeColorOverride("font_color", labelColor);
            infoLabel.AddThemeFontSizeOverride("font_size", 13);
            hbox.AddChild(infoLabel);

            // E5: 再加一个小型标记：显示总战力
            var powerLabel = new Label { Text = $"[战{army.AggregateCombatPower:F0}]" };
            powerLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 1.0f));
            powerLabel.AddThemeFontSizeOverride("font_size", 12);
            hbox.AddChild(powerLabel);

            _armyListContainer.AddChild(hbox);
        }
    }

    private void PopulateWarList()
    {
        ClearWarList();
        if (_entityMgr == null || string.IsNullOrEmpty(_playerFaction)) return;

        var myWars = _entityMgr.WorldEngine.ActiveWars.Where(w => 
            w.NationA == _playerFaction || w.NationB == _playerFaction).ToList();

        if (myWars.Count == 0)
        {
            var emptyLabel = new Label { Text = "本国目前处于和平状态，无对外战争。" };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
            _warListContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var war in myWars)
        {
            string enemyId = war.NationA == _playerFaction ? war.NationB : war.NationA;
            string enemyName = enemyId;
            var nation = _entityMgr.Nations.FirstOrDefault(n => n.Id == enemyId);
            if (nation != null) enemyName = nation.DisplayName;

            // 净战分计算
            int score = war.NationA == _playerFaction ? war.WarScoreA : -war.WarScoreA;

            var hbox = new HBoxContainer();
            var warLabel = new Label 
            { 
                Text = $"⚔ 与【{enemyName}】交战已持续 {war.DaysSinceStart} 天，净战分: {(score >= 0 ? "+" : "")}{score}" 
            };
            
            if (score > 15) warLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.4f)); // 优势绿
            else if (score < -15) warLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f)); // 劣势红
            else warLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));

            hbox.AddChild(warLabel);
            _warListContainer.AddChild(hbox);
        }
    }

    private void PopulateTargetSelectors(int currentInfluence)
    {
        _declareTargetSelector.Clear();
        _peaceTargetSelector.Clear();

        if (_entityMgr == null || string.IsNullOrEmpty(_playerFaction)) return;

        // 宣战候选：关系 <= -30 的非战争敌国
        _declareTargetSelector.AddItem("-- 选择宣战国家 --");
        // 媾和候选：正在交战的敌国
        _peaceTargetSelector.AddItem("-- 选择停战国家 --");

        foreach (var nation in _entityMgr.Nations)
        {
            if (nation.Id == _playerFaction) continue;

            bool isAtWar = _entityMgr.WorldEngine.AreAtWar(_playerFaction, nation.Id);
            if (isAtWar)
            {
                _peaceTargetSelector.AddItem(nation.DisplayName);
            }
            else
            {
                int relation = _entityMgr.WorldEngine.GetRelation(_playerFaction, nation.Id);
                // 关系必须 <= -30
                if (relation <= -30)
                {
                    _declareTargetSelector.AddItem($"{nation.DisplayName} (关系: {relation})");
                }
            }
        }

        // 按钮余额控制
        _declareBtn.Disabled = currentInfluence < 50 || _declareTargetSelector.ItemCount <= 1;
        _peaceBtn.Disabled = currentInfluence < 80 || _peaceTargetSelector.ItemCount <= 1;
    }

    private void OnDeclarePressed()
    {
        if (_entityMgr == null || string.IsNullOrEmpty(_playerFaction)) return;
        
        int selectedIdx = _declareTargetSelector.Selected;
        if (selectedIdx <= 0)
        {
            ShowStatus("请先从列表中选择一个要宣战的国家势力。", Colors.Red);
            return;
        }

        string selectedText = _declareTargetSelector.GetItemText(selectedIdx);
        // 解析国家名
        var targetNation = _entityMgr.Nations.FirstOrDefault(n => 
            selectedText.StartsWith(n.DisplayName) && n.Id != _playerFaction);

        if (targetNation == null)
        {
            ShowStatus("目标国家解析失败，请重试。", Colors.Red);
            return;
        }

        var result = KingdomDecisionService.TryDeclareWar(_playerFaction, targetNation.Id, _entityMgr.WorldEngine);
        HandleDecisionOutcome(result, $"已成功向 {targetNation.DisplayName} 正式宣战！");
    }

    private void OnPeacePressed()
    {
        if (_entityMgr == null || string.IsNullOrEmpty(_playerFaction)) return;

        int selectedIdx = _peaceTargetSelector.Selected;
        if (selectedIdx <= 0)
        {
            ShowStatus("请先从列表中选择一个希望请求停战的国家势力。", Colors.Red);
            return;
        }

        string targetName = _peaceTargetSelector.GetItemText(selectedIdx);
        var targetNation = _entityMgr.Nations.FirstOrDefault(n => n.DisplayName == targetName);

        if (targetNation == null)
        {
            ShowStatus("停战目标国家解析失败，请重试。", Colors.Red);
            return;
        }

        var result = KingdomDecisionService.TryMakePeace(_playerFaction, targetNation.Id, _entityMgr.WorldEngine);
        HandleDecisionOutcome(result, $"已递交停战书，与 {targetNation.DisplayName} 正式媾和停战！");
    }

    private void HandleDecisionOutcome(DecisionResult outcome, string successMessage)
    {
        switch (outcome)
        {
            case DecisionResult.Success:
                ShowStatus(successMessage, Colors.Green);
                Refresh();
                break;
            case DecisionResult.InsufficientInfluence:
                ShowStatus("外交派遣失败：当前国家影响力余额不足以进行该决议决策。", Colors.Red);
                break;
            case DecisionResult.RelationTooHigh:
                ShowStatus("两国外交关系仍算温和友好，暂不符合发动全面战争的舆论前提条件。", Colors.Red);
                break;
            case DecisionResult.NotAtWar:
                ShowStatus("两国当前并未处于战争交火状态，无需递交媾和条约。", Colors.Red);
                break;
            case DecisionResult.AlreadyAtWar:
                ShowStatus("两国当前早已处于交火战争宣战期中，请勿重复宣战。", Colors.Red);
                break;
            case DecisionResult.InTruce:
                ShowStatus("停战保护期内无法发动战争，请等待停战期结束后再行宣战。", Colors.Red);
                break;
            case DecisionResult.InCooldown:
                ShowStatus("外交操作冷却中，请稍后再试。", Colors.Red);
                break;
            default:
                ShowStatus("发生未知的决议执行失败问题，请重试。", Colors.Red);
                break;
        }
    }

    private void ShowStatus(string msg, Color color)
    {
        _statusLabel.Text = msg;
        _statusLabel.AddThemeColorOverride("font_color", color);
    }

    private void RefreshPrisoners()
    {
        foreach (var child in _prisonerListContainer.GetChildren())
            child.QueueFree();

        if (_entityMgr == null) return;

        // 遍历 "player" 随队俘虏 ID
        var prisoners = _entityMgr.Prisoners.GetPrisonersAt("player");
        if (prisoners == null || prisoners.Count == 0)
        {
            var emptyLabel = new Label { Text = "随队目前没有任何俘虏。" };
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _prisonerListContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var heroId in prisoners)
        {
            var hero = _entityMgr.Heroes.Get(heroId);
            if (hero == null) continue;

            // 获取原所属国名
            string originFactionName = hero.FactionId;
            var originNation = _entityMgr.Nations.FirstOrDefault(n => n.Id == hero.FactionId);
            if (originNation != null) originFactionName = originNation.DisplayName;

            var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            hbox.AddThemeConstantOverride("separation", 10);

            var infoText = $"{hero.DisplayName} - 【{originFactionName}】  (赎金: {hero.RansomGold} 金)";
            var infoLabel = new Label 
            { 
                Text = infoText,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            infoLabel.AddThemeFontSizeOverride("font_size", 14);
            hbox.AddChild(infoLabel);

            // 操作按钮
            // 1. 无条件释放
            var releaseBtn = new Button { Text = "释放" };
            releaseBtn.AddThemeFontSizeOverride("font_size", 12);
            releaseBtn.Pressed += () => {
                BladeHex.Strategic.Hero.PrisonerActions.Release(hero, _currentDay, _entityMgr.Heroes, _entityMgr.Prisoners, _entityMgr.Relations, _entityMgr.WorldEngine);
                _ShowPrisonerStatus($"已无条件释放 {hero.DisplayName}，与其家族关系改善！", Colors.Green);
                RefreshPrisoners();
            };
            hbox.AddChild(releaseBtn);

            // 2. 索要赎金
            var ransomBtn = new Button { Text = "赎金" };
            ransomBtn.AddThemeFontSizeOverride("font_size", 12);
            ransomBtn.Pressed += () => {
                int goldAmount = hero.RansomGold;
                BladeHex.Strategic.Hero.PrisonerActions.CollectRansom(hero, _currentDay, _entityMgr.Prisoners, _entityMgr.Relations, _entityMgr.WorldEngine);
                var ctx = GetContext();
                ctx?.AddGold(goldAmount);
                _ShowPrisonerStatus($"成功收取了 {hero.DisplayName} 的赎金 {goldAmount} 金币并释放了他！", Colors.Green);
                RefreshPrisoners();
            };
            hbox.AddChild(ransomBtn);

            // 3. 招降
            var recruitBtn = new Button { Text = "招降" };
            recruitBtn.AddThemeFontSizeOverride("font_size", 12);
            
            // 判断招降条件是否满足
            int relation = _entityMgr.Relations.Get("player", hero.HeroId);
            int playerInfluence = _entityMgr.WorldEngine.Influence.Get("player");
            bool canRecruit = relation >= 50 && playerInfluence >= 50;
            recruitBtn.Disabled = !canRecruit;

            recruitBtn.Pressed += () => {
                bool ok = BladeHex.Strategic.Hero.PrisonerActions.Recruit(hero, _currentDay, _entityMgr.Heroes, _entityMgr.Prisoners, _entityMgr.Relations, _entityMgr.WorldEngine.Influence, _entityMgr.WorldEngine);
                if (ok)
                {
                    _ShowPrisonerStatus($"{hero.DisplayName} 已臣服，并正式加入了您的兵团！", Colors.Green);
                }
                else
                {
                    _ShowPrisonerStatus("招降失败，请检查好感度与影响力条件。", Colors.Red);
                }
                RefreshPrisoners();
            };
            
            // 如果不满足条件，置灰并显示说明
            if (!canRecruit)
            {
                recruitBtn.TooltipText = $"招降需要: 与玩家好感度 >= 50 (当前: {relation})，玩家势力影响力 >= 50 (当前: {playerInfluence})";
            }
            hbox.AddChild(recruitBtn);

            _prisonerListContainer.AddChild(hbox);
        }
    }

    private void _ShowPrisonerStatus(string msg, Color color)
    {
        _prisonerStatusLabel.Text = msg;
        _prisonerStatusLabel.AddThemeColorOverride("font_color", color);
    }

    private IOverworldContext? GetContext()
    {
        Node? n = GetParent();
        while (n != null)
        {
            if (n is IOverworldContext ctx) return ctx;
            n = n.GetParent();
        }
        return null;
    }

    // ========================================
    // M7: 我的王国 Tab 相关方法
    // ========================================

    private void RefreshKingdomTab()
    {
        if (_entityMgr == null) return;

        // 更新王国状态显示
        var kingdom = _entityMgr.PlayerKingdom;
        if (kingdom != null)
        {
            // 已建立王国
            var statusLabel = _kingdomTab.GetChild<Label>(1); // 第二个子节点是状态标签
            if (statusLabel != null)
            {
                statusLabel.Text = $"王国: {kingdom.DisplayName} | 家族: {kingdom.FamilyName} | 都城: {kingdom.CapitalPoiName} | 领土: {kingdom.PoiCount} 个";
                statusLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
            }
        }
        else
        {
            // 尚未建立王国
            var statusLabel = _kingdomTab.GetChild<Label>(1);
            if (statusLabel != null)
            {
                int pendingCount = _entityMgr.PendingConquests.Count;
                statusLabel.Text = $"尚未建立王国。已占领 {pendingCount} 个据点。占领城堡并达到开国条件后可建立王国。";
                statusLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            }
        }
    }

    private void OnFoundKingdomPressed()
    {
        if (_entityMgr == null) return;

        // 检查开国条件
        var (ok, reason) = PlayerKingdomService.CanFoundKingdom(
            _entityMgr.Pois,
            _entityMgr.WorldEngine.Influence,
            20, // TODO: 获取实际玩家等级
            _entityMgr.PendingConquests);

        if (!ok)
        {
            ShowStatus($"无法建立王国: {reason}", Colors.Red);
            return;
        }

        // 显示创建王国对话框
        if (_foundKingdomDialog == null)
        {
            _foundKingdomDialog = new FoundKingdomDialog();
            GetParent().AddChild(_foundKingdomDialog);
            _foundKingdomDialog.KingdomFounded += OnKingdomFounded;
        }

        _foundKingdomDialog.ShowDialog(_entityMgr.PendingConquests, _entityMgr.Pois);
    }

    private void OnKingdomFounded(string kingdomName, string familyName, Color bannerColor, string capitalPoiName)
    {
        if (_entityMgr == null) return;

        // 调用 PlayerKingdomService.Found
        var capitalPoi = _entityMgr.Pois.FirstOrDefault(p => p.PoiName == capitalPoiName) ?? _entityMgr.Pois[0];
        var playerKingdom = PlayerKingdomService.Found(
            kingdomName,
            familyName,
            capitalPoi,
            bannerColor,
            _currentDay,
            _entityMgr.Heroes,
            _entityMgr.Families,
            _entityMgr.Nations,
            _entityMgr.WorldEngine,
            _entityMgr.WorldEngine.Influence,
            _entityMgr.PendingConquests);

        _entityMgr.PlayerKingdom = playerKingdom;

        ShowStatus($"王国 {kingdomName} 已建立！", Colors.Green);
        RefreshKingdomTab();
    }

    private void OnMyKingdomPressed()
    {
        if (_entityMgr == null || _entityMgr.PlayerKingdom == null)
        {
            ShowStatus("尚未建立王国。", Colors.Red);
            return;
        }

        // 显示王国详情面板
        if (_kingdomDashboardPanel == null)
        {
            _kingdomDashboardPanel = new KingdomDashboardPanel();
            GetParent().AddChild(_kingdomDashboardPanel);
        }

        _kingdomDashboardPanel.ShowPanel(_entityMgr.PlayerKingdom, _entityMgr.Heroes);
    }
}
