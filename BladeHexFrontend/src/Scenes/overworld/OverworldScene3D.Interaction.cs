// OverworldScene3D.Interaction.cs
// 交互系统 — 修复所有 POI/实体交互问题
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.View.UI.Overworld;
using BladeHex.Map;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 交互系统
    // ========================================

    private InteractionManager? _interactionMgr;
    private InteractionPanel? _interactionPanel;
    private TownPanel? _townPanel;

    /// <summary>当前交互的 town 节点（用于防止重复创建 + 清理）</summary>
    private OverworldTown? _currentTownNode;

    /// <summary>初始化交互系统</summary>
    private void SetupInteractionSystem()
    {
        _interactionMgr = new InteractionManager();
        _interactionMgr.PlayerParty = PlayerParty;
        _interactionMgr.HexGrid = _grid;
        AddChild(_interactionMgr);

        // 交互面板（用于非城镇遭遇：敌人/NPC）
        _interactionPanel = new InteractionPanel();
        AddChild(_interactionPanel);
        _interactionPanel.OptionSelected += OnInteractionOptionSelected;
        _interactionPanel.CloseRequested += OnInteractionClosed;

        // 城镇面板
        _townPanel = new TownPanel();
        AddChild(_townPanel);
        _townPanel.LeaveTown += OnLeaveTown;
        _townPanel.FacilitySelected += OnFacilitySelected;

        // 信号连接 — 所有 InteractionManager 信号
        _interactionMgr.InteractionRequested += OnInteractionRequested;
        _interactionMgr.CombatRequested += OnCombatFromInteraction;
        _interactionMgr.InteractionCompleted += OnInteractionCompleted;
        _interactionMgr.TownEntered += OnTownEntered;
        _interactionMgr.TradeRequested += OnTradeRequested;
        _interactionMgr.RestRequested += OnRestRequested;
        _interactionMgr.TrainRequested += OnTrainRequested;
        _interactionMgr.HealRequested += OnHealRequested;
        _interactionMgr.ArenaRequested += OnArenaRequested;
        _interactionMgr.QuestRequested += OnQuestRequested;
        _interactionMgr.RepairRequested += OnRepairRequested;

        GD.Print("[OverworldScene3D] 交互系统已初始化");
    }

    /// <summary>触发 POI 交互 — 城镇类直接打开 TownPanel，跳过 InteractionPanel</summary>
    private void TriggerPOIInteraction(OverworldPOI poi)
    {
        if (_interactionMgr == null) return;

        IsTimePaused = true;
        _playerMoving = false;

        // 清理上一个 town 节点
        if (_currentTownNode != null && GodotObject.IsInstanceValid(_currentTownNode))
        {
            _currentTownNode.QueueFree();
            _currentTownNode = null;
        }

        // 创建 OverworldTown
        var town = new OverworldTown();
        town.TownName = poi.PoiName;

        switch (poi.PoiTypeEnum)
        {
            case OverworldPOI.POIType.Town:
                town.TownType = "town";
                town.SetupDefaultFacilities();
                break;
            case OverworldPOI.POIType.Village:
                town.TownType = "village";
                town.SetupVillageFacilities();
                break;
            case OverworldPOI.POIType.Castle:
                town.TownType = "castle";
                town.SetupCastleFacilities();
                break;
            case OverworldPOI.POIType.Tavern:
                town.TownType = "tavern";
                town.SetupTavernFacilities();
                break;
            case OverworldPOI.POIType.Outpost:
                town.TownType = "outpost";
                town.SetupOutpostFacilities();
                break;
            case OverworldPOI.POIType.Port:
                town.TownType = "port";
                town.SetupPortFacilities();
                break;
            default:
                town.TownType = "village";
                town.SetupVillageFacilities();
                break;
        }

        // 加入场景树
        town.Visible = false;
        AddChild(town);
        _currentTownNode = town;

        // 城镇/村庄类 POI → 直接打开 TownPanel（不经过 InteractionPanel）
        if (poi.PoiTypeEnum == OverworldPOI.POIType.Town ||
            poi.PoiTypeEnum == OverworldPOI.POIType.Village ||
            poi.PoiTypeEnum == OverworldPOI.POIType.Castle ||
            poi.PoiTypeEnum == OverworldPOI.POIType.Port)
        {
            GD.Print($"[Interaction] 打开 TownPanel: {town.TownName}, 设施数={town.Facilities.Count}");
            _townPanel?.ShowTown(town);
        }
        else
        {
            // 其他类型 → 通过 InteractionManager 走标准流程
            GD.Print($"[Interaction] 走 InteractionManager 流程: {poi.PoiName}");
            _interactionMgr.TriggerInteraction(town);
        }
    }

    // ========================================
    // InteractionPanel 回调（非城镇遭遇）
    // ========================================

    private void OnInteractionRequested(Node2D entity, Godot.Collections.Array<InteractionOption> options)
    {
        var arr = new Godot.Collections.Array();
        foreach (var opt in options) arr.Add(opt);
        _interactionPanel?.ShowForEntity(entity, arr);
    }

    private void OnInteractionOptionSelected(InteractionOption option)
    {
        if (option.CurrentInteractionType == InteractionType.Type.Leave)
            OnInteractionClosed();
        else
            _interactionMgr?.ExecuteOption(option);
    }

    private void OnInteractionClosed()
    {
        _interactionPanel?.HidePanel();
        _interactionMgr?.EndInteraction();
        CleanupInteraction();
    }

    // ========================================
    // TownPanel 回调
    // ========================================

    private void OnTownEntered(OverworldTown town)
    {
        _townPanel?.ShowTown(town);
    }

    private void OnLeaveTown()
    {
        GD.Print("[Interaction] OnLeaveTown 信号收到");
        _townPanel?.HidePanel();
        _interactionMgr?.EndInteraction();
        CleanupInteraction();
    }

    private void OnFacilitySelected(int facilityType)
    {
        GD.Print($"[Interaction] OnFacilitySelected: type={facilityType} ({(TownFacility.FacilityType)facilityType})");

        // 隐藏城镇面板（二级面板会覆盖）
        _townPanel?.HidePanel();

        var fType = (TownFacility.FacilityType)facilityType;
        switch (fType)
        {
            case TownFacility.FacilityType.Market:
                OpenTradePanel();
                break;
            case TownFacility.FacilityType.Tavern:
                OpenRecruitPanel();
                break;
            case TownFacility.FacilityType.Smithy:
                OpenSmithyPanel();
                break;
            case TownFacility.FacilityType.Training:
                OpenTrainingPanel();
                break;
            case TownFacility.FacilityType.Temple:
                OpenTemplePanel();
                break;
            case TownFacility.FacilityType.Arena:
                OpenArenaPanel();
                break;
            case TownFacility.FacilityType.Castle:
                OpenQuestPanel();
                break;
            default:
                // 未实现的设施 → 回到城镇面板
                _townPanel?.ShowPanel();
                break;
        }
    }

    // ========================================
    // 二级面板（懒加载）
    // ========================================

    private TradePanel? _tradePanel2;
    private RestPanel? _restPanel2;
    private RecruitPanel? _recruitPanel2;
    private SmithyPanel? _smithyPanel2;
    private TrainingPanel? _trainingPanel2;
    private TemplePanel? _templePanel2;
    private ArenaPanel? _arenaPanel2;
    private QuestBoardPanel? _questBoardPanel2;

    private void OpenTradePanel()
    {
        if (PlayerParty?.Roster == null || EconomyMgr == null) return;

        // 生成商店库存
        var stock = GenerateTradeStock();

        // 通过 OverworldUI 的部队面板以商店模式打开
        if (_overworldUi != null)
        {
            string shopName = _currentTownNode?.TownName ?? "商店";
            var inventory = PlayerParty.Inventory ?? new BladeHex.Strategic.PartyInventory();
            _overworldUi.OpenPartyShop(shopName, EconomyMgr, stock, _currentTownNode?.Prosperity ?? 50);
        }
    }

    private List<ItemData> GenerateTradeStock()
    {
        var stock = new List<ItemData>();
        int prosperity = _currentTownNode?.Prosperity ?? 50;
        int weaponCount = 3 + prosperity / 20;
        int armorCount = 2 + prosperity / 25;
        int consumCount = 3 + prosperity / 30;
        string difficulty = prosperity >= 70 ? "hard" : prosperity >= 40 ? "normal" : "easy";
        int itemLevel = 1 + prosperity / 10;
        int maxWeaponPrice = 50 + prosperity * 4;

        var weapons = PrototypeData.GetWeapons().Values.ToList();
        var armors = PrototypeData.GetArmors().Values.ToList();
        var consumables = PrototypeData.GetConsumables().Values.ToList();
        var quivers = PrototypeData.GetQuivers().Values.ToList();

        var affordableWeapons = weapons.Where(w => w.Price <= maxWeaponPrice).ToList();
        for (int i = 0; i < System.Math.Min(weaponCount, affordableWeapons.Count); i++)
        {
            var baseW = affordableWeapons[(int)(GD.Randf() * affordableWeapons.Count)];
            if (stock.Any(s => s.ItemId == baseW.ItemId)) continue;
            var rarity = BladeHex.Combat.EquipmentGenerator.RollRarity(difficulty);
            if (rarity > ItemData.Rarity.Rare) rarity = ItemData.Rarity.Rare;
            stock.Add(BladeHex.Combat.EquipmentGenerator.GenerateEquipment(baseW, rarity, itemLevel, difficulty));
        }

        int maxArmorPrice = 30 + prosperity * 5;
        var affordableArmors = armors.Where(a => a.Price <= maxArmorPrice).ToList();
        for (int i = 0; i < System.Math.Min(armorCount, affordableArmors.Count); i++)
        {
            var baseA = affordableArmors[(int)(GD.Randf() * affordableArmors.Count)];
            if (stock.Any(s => s.ItemId == baseA.ItemId)) continue;
            var rarity = BladeHex.Combat.EquipmentGenerator.RollRarity(difficulty);
            if (rarity > ItemData.Rarity.Rare) rarity = ItemData.Rarity.Rare;
            stock.Add(BladeHex.Combat.EquipmentGenerator.GenerateEquipment(baseA, rarity, itemLevel, difficulty));
        }

        for (int i = 0; i < System.Math.Min(consumCount, consumables.Count); i++)
        {
            var c = consumables[(int)(GD.Randf() * consumables.Count)];
            if (!stock.Any(s => s.ItemId == c.ItemId)) stock.Add(c);
        }

        int quiverCount = 1 + (prosperity >= 50 ? 1 : 0);
        for (int i = 0; i < System.Math.Min(quiverCount, quivers.Count); i++)
        {
            var q = quivers[(int)(GD.Randf() * quivers.Count)];
            if (!stock.Any(s => s.ItemId == q.ItemId)) stock.Add(q);
        }

        stock.Sort((a, b) => a.Price.CompareTo(b.Price));
        return stock;
    }

    private void OpenRecruitPanel()
    {
        if (_recruitPanel2 == null)
        {
            _recruitPanel2 = new RecruitPanel();
            AddChild(_recruitPanel2);
            _recruitPanel2.RecruitFinished += (bool _) => OnSecondaryPanelClosed();
        }

        // 使用 RecruitService 提供数据
        if (_recruitService != null && PlayerParty != null && EconomyMgr != null)
        {
            string poiId = _currentTownNode?.TownName ?? "";
            int currentDay = EconomyMgr.DaysPassed;
            _recruitPanel2.ShowRecruitList(_recruitService, poiId, EconomyMgr, PlayerParty, currentDay);
        }
        else
        {
            _recruitPanel2.ShowPanel();
        }
    }

    private void OpenSmithyPanel()
    {
        if (_smithyPanel2 == null)
        {
            _smithyPanel2 = new SmithyPanel();
            AddChild(_smithyPanel2);
            _smithyPanel2.SmithyFinished += OnSecondaryPanelClosed;
        }
        _smithyPanel2.ShowPanel();
    }

    private void OpenTrainingPanel()
    {
        if (_trainingPanel2 == null)
        {
            _trainingPanel2 = new TrainingPanel();
            AddChild(_trainingPanel2);
            _trainingPanel2.TrainingFinished += OnSecondaryPanelClosed;
        }
        _trainingPanel2.ShowPanel();
    }

    private void OpenTemplePanel()
    {
        if (_templePanel2 == null)
        {
            _templePanel2 = new TemplePanel();
            AddChild(_templePanel2);
            _templePanel2.TempleFinished += OnSecondaryPanelClosed;
        }
        _templePanel2.ShowPanel();
    }

    private void OpenArenaPanel()
    {
        if (_arenaPanel2 == null)
        {
            _arenaPanel2 = new ArenaPanel();
            AddChild(_arenaPanel2);
            _arenaPanel2.ArenaFinished += OnSecondaryPanelClosed;
        }
        _arenaPanel2.ShowPanel();
    }

    private void OpenQuestPanel()
    {
        if (_questBoardPanel2 == null)
        {
            _questBoardPanel2 = new QuestBoardPanel();
            AddChild(_questBoardPanel2);
            _questBoardPanel2.BoardClosed += OnSecondaryPanelClosed;
        }
        _questBoardPanel2.ShowPanel();
    }

    /// <summary>二级面板关闭后回到城镇面板</summary>
    private void OnSecondaryPanelClosed()
    {
        // 重新打开城镇面板
        if (_currentTownNode != null && GodotObject.IsInstanceValid(_currentTownNode))
            _townPanel?.ShowTown(_currentTownNode);
        else
            CleanupInteraction();
    }

    // ========================================
    // 设施信号回调（InteractionManager 发出）
    // ========================================

    private void OnTradeRequested(string sourceName)
    {
        GD.Print($"[OverworldScene3D] 交易请求: {sourceName}");
        // TODO: 打开交易面板
        CleanupInteraction();
    }

    private void OnRestRequested(int facilityType)
    {
        GD.Print($"[OverworldScene3D] 休息请求: type={facilityType}");
        // TODO: 打开休息面板
        CleanupInteraction();
    }

    private void OnTrainRequested()
    {
        GD.Print("[OverworldScene3D] 训练请求");
        CleanupInteraction();
    }

    private void OnHealRequested()
    {
        GD.Print("[OverworldScene3D] 治疗请求");
        CleanupInteraction();
    }

    private void OnArenaRequested()
    {
        GD.Print("[OverworldScene3D] 竞技场请求");
        CleanupInteraction();
    }

    private void OnQuestRequested()
    {
        GD.Print("[OverworldScene3D] 委托请求");
        CleanupInteraction();
    }

    private void OnRepairRequested()
    {
        GD.Print("[OverworldScene3D] 修理请求");
        CleanupInteraction();
    }

    // ========================================
    // 战斗
    // ========================================

    private void OnCombatFromInteraction(BattleContext ctx)
    {
        _interactionPanel?.HidePanel();

        // 从 InteractionManager 的当前交互实体中提取 OverworldEntity
        var interactNode = _interactionMgr?.GetCurrentEntity();
        if (interactNode is BladeHex.Strategic.OverworldEnemy oe && oe.EntityRef != null)
            _lastEncounteredEntity = oe.EntityRef;

        _interactionMgr?.EndInteraction();
        CleanupInteraction();
        EnterCombatScene(ctx);
    }

    private void OnInteractionCompleted(string result)
    {
        CleanupInteraction();
    }

    // ========================================
    // 清理
    // ========================================

    /// <summary>统一清理交互状态</summary>
    private void CleanupInteraction()
    {
        IsTimePaused = false;
        _poiEntered = false;
        _encounterActive = false;

        // 清理临时 town 节点
        if (_currentTownNode != null && GodotObject.IsInstanceValid(_currentTownNode))
        {
            _currentTownNode.QueueFree();
            _currentTownNode = null;
        }
    }
}
