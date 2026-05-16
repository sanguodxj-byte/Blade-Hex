// QuickCombatScene.cs
// 快速战斗场景 — 继承 CombatSceneBase，只保留随机单位生成和返回主菜单
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Strategic;

namespace BladeHex.Scenes;

/// <summary>
/// 快速战斗场景 — 随机战场 + 随机单位，结束后返回主菜单。
/// </summary>
[GlobalClass]
public partial class QuickCombatScene : CombatSceneBase
{
    // ============================================================
    // 子类实现：战前设置（天气）
    // ============================================================

    protected override void OnPreBattleSetup()
    {
        SetupCombatWeather();
    }

    protected override void PlayCombatMusic()
    {
        if (_audioManager == null || !IsInstanceValid(_audioManager)) return;

        float totalThreat = 0.0f;
        foreach (var enemy in _combatManager.EnemyUnits)
        {
            if (IsInstanceValid(enemy) && enemy.Data != null)
                totalThreat += enemy.Data.ThreatLevel;
        }

        // 根据威胁等级和天气选择战斗BGM变体（与正式战斗一致）
        string variant;
        if (totalThreat >= 3.0f)
            variant = "boss";
        else
        {
            var gs = BladeHex.Data.Globals.StateOrNull;
            bool isRaining = gs != null && gs.Weather.Type == 0;
            variant = isRaining ? "rain" : "normal";
        }

        _audioManager.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Combat, variant, 1.0f);
    }

    // ============================================================
    // 子类实现：地图生成
    // ============================================================

    protected override void GenerateBattlefield()
    {
        var gs = BladeHex.Data.Globals.State;

        string templateName = gs.QuickCombat.Template ?? "";
        int sizeIdx = gs.QuickCombat.Size;
        var battleSize = sizeIdx switch
        {
            1 => BattleContext.BattleSize.Knight,
            2 => BattleContext.BattleSize.Lord,
            3 => BattleContext.BattleSize.Stronghold,
            _ => BattleContext.BattleSize.Mercenary,
        };

        var generator = new BattleMapGenerator();

        if (!string.IsNullOrEmpty(templateName))
        {
            _mapData = generator.GenerateFromTemplate(templateName,
                (BattleMapGenerator.BattleSize)(int)battleSize, (int)GD.Randi());
        }
        else
        {
            var owGenerator = new HexOverworldGenerator();
            var owGrid = owGenerator.Generate(10, 8, (int)GD.Randi());
            var center = owGrid.GetCenterPixel();
            var centerTile = owGrid.FindPassableNearPixel(center.X, center.Y, 5);

            Vector2I encounterCoord = centerTile != null ? centerTile.Coord : new Vector2I(5, 4);
            int terrainType = centerTile != null
                ? owGrid.SampleTerrainAtPixel(centerTile.PixelPos.X, centerTile.PixelPos.Y)
                : owGrid.SampleTerrainAtPixel(0.0f, 0.0f);

            var ctx = new BattleContext
            {
                Terrain = (HexOverworldTile.TerrainType)terrainType,
                Size = battleSize,
                Engagement = BattleContext.EngagementType.Normal,
                Seed = (int)GD.Randi(),
                OverworldGrid = owGrid,
                EncounterCoord = encounterCoord,
                PoiType = -1,
            };
            _mapData = generator.Generate(ctx);
        }

        _mapWidth = _mapData.Width;
        _mapHeight = _mapData.Height;

        _hexGrid.LoadFromMapData(_mapData);

        // 连接单元格事件
        foreach (var kvp in _hexGrid.Cells)
        {
            var cell = kvp.Value;
            cell.CellSingleClicked += OnCellClicked;
            cell.CellRightClicked += OnCellRightClicked;
            cell.CellMouseEntered += OnCellHover;
            cell.CellMouseExited += OnCellHoverExit;
        }
    }

    // ============================================================
    // 子类实现：随机单位生成
    // ============================================================

    protected override void SpawnUnits()
    {
        var gs = BladeHex.Data.Globals.State;
        int playerCount = Mathf.Clamp(gs.QuickCombat.PlayerCount, 1, 6);
        int enemyCount = Mathf.Clamp(gs.QuickCombat.EnemyCount, 1, 10);
        int difficulty = gs.QuickCombat.Difficulty;
        int playerLevel = Mathf.Clamp(gs.QuickCombat.PlayerLevel, 1, 120);
        int enemyTypeIdx = Mathf.Clamp(gs.QuickCombat.EnemyType, 0, 3);

        string difficultyStr = difficulty switch { 0 => "easy", 2 => "hard", _ => "normal" };
        int itemLevel = EquipmentGenerator.GetItemLevelFromCr(RPGRuleEngine.GetCrFromLevel(playerLevel));

        // === 玩家方 ===
        var playerUnits = new List<Unit>();

        for (int i = 0; i < playerCount; i++)
        {
            var unitData = CharacterGenerator.GenerateRandomAdventurer(playerLevel);
            unitData.UnitName = $"冒险者_{i + 1}";
            unitData.IsEnemy = false;
            unitData.BaseAc = 8;

            // 分配装备（武器 + 护甲）
            EquipUnit(unitData, itemLevel, difficultyStr);

            var playerUnit = new Unit
            {
                Data = unitData,
                Name = $"Player_{i}"
            };

            var pos = FindPlayerDeployPos(i, playerCount);
            PlaceUnitAt(playerUnit, pos.X, pos.Y);
            _combatManager.RegisterUnit(playerUnit, true);
            _combatUi.RegisterAlly(playerUnit);
            playerUnit.InitDr();

            // 分配技能盘并自动加点
            AssignSkillTree(playerUnit, playerLevel);

            playerUnits.Add(playerUnit);
        }

        if (playerUnits.Count > 0)
            _activePlayerUnit = playerUnits[0];

        // === 敌方 ===
        float difficultyMult = difficulty switch { 0 => 0.7f, 2 => 1.5f, _ => 1.0f };
        var enemyType = enemyTypeIdx switch
        {
            0 => UnitData.EnemyType.Humanoid,
            1 => UnitData.EnemyType.Undead,
            2 => UnitData.EnemyType.Beast,
            _ => UnitData.EnemyType.Humanoid,
        };

        for (int i = 0; i < enemyCount; i++)
        {
            var thisEnemyType = enemyTypeIdx == 3
                ? (UnitData.EnemyType)((int)(GD.Randf() * 3))
                : enemyType;

            float cr = RPGRuleEngine.GetCrFromLevel(playerLevel) * difficultyMult;
            var enemyData = CharacterGenerator.GenerateRandomEnemy(cr, thisEnemyType);
            enemyData.BaseAc = 8;

            // 分配装备
            EquipUnit(enemyData, itemLevel, difficultyStr);

            var enemyUnit = new Unit
            {
                Data = enemyData,
                Name = $"Enemy_{i}"
            };

            var pos = FindEnemyDeployPos(i, enemyCount);
            PlaceUnitAt(enemyUnit, pos.X, pos.Y);
            _combatManager.RegisterUnit(enemyUnit, false);
            _combatUi.RegisterEnemy(enemyUnit);
            enemyUnit.InitDr();

            // 人形敌人也有技能盘
            if (thisEnemyType == UnitData.EnemyType.Humanoid)
                AssignSkillTree(enemyUnit, Mathf.Max(1, enemyData.Level));
        }

        UpdateFov();
    }

    /// <summary>为单位分配随机装备（完整套装：武器+副手+护甲+盾牌+头盔+饰品+箭筒+弹药）</summary>
    private static void EquipUnit(UnitData unitData, int itemLevel, string difficulty)
    {
        // 主手武器
        if (unitData.PrimaryMainHand == null)
        {
            unitData.PrimaryMainHand = GD.Randf() < 0.7f
                ? EquipmentGenerator.GenerateRandomWeapon(null, (ItemData.Rarity)(-1), itemLevel, difficulty)
                : EquipmentGenerator.GenerateRandomWeapon(new[] { "shortbow", "hunting_bow", "light_crossbow", "standard_crossbow" }, (ItemData.Rarity)(-1), itemLevel, difficulty);
        }

        // 副手武器（50% 概率）
        if (unitData.SecondaryMainHand == null && GD.Randf() < 0.5f)
        {
            bool mainIsRanged = unitData.PrimaryMainHand is WeaponData pw && pw.IsRanged;
            bool mainIsThrowing = unitData.PrimaryMainHand is WeaponData pt && pt.IsThrowing;

            if (mainIsThrowing)
            {
                // 投掷武器可同时作为主副武器
                unitData.SecondaryMainHand = EquipmentGenerator.GenerateRandomWeapon(
                    new[] { "javelin", "throwing_knife", "francisca", "dart" }, (ItemData.Rarity)(-1), itemLevel, difficulty);
            }
            else if (mainIsRanged)
            {
                // 弓/弩主手 → 副手近战
                unitData.SecondaryMainHand = EquipmentGenerator.GenerateRandomWeapon(
                    new[] { "arming_sword", "dagger", "infantry_spear", "seax" }, (ItemData.Rarity)(-1), itemLevel, difficulty);
            }
            else
            {
                // 近战主手 → 副手远程（投掷优先，也可弓弩）
                unitData.SecondaryMainHand = GD.Randf() < 0.4f
                    ? EquipmentGenerator.GenerateRandomWeapon(new[] { "javelin", "throwing_knife", "francisca" }, (ItemData.Rarity)(-1), itemLevel, difficulty)
                    : EquipmentGenerator.GenerateRandomWeapon(new[] { "shortbow", "light_crossbow" }, (ItemData.Rarity)(-1), itemLevel, difficulty);
            }
        }

        // 护甲
        if (unitData.Armor == null)
        {
            unitData.Armor = EquipmentGenerator.GenerateRandomArmor(null, (ItemData.Rarity)(-1), itemLevel, difficulty);
            unitData.Armor?.InitializeArmorPoints();
        }

        // 盾牌（近战主手 + 非双手武器时 60% 概率）
        if (unitData.Shield == null && unitData.PrimaryMainHand is WeaponData mainWpn && !mainWpn.IsRanged && !mainWpn.IsTwoHanded)
        {
            if (GD.Randf() < 0.6f)
            {
                var allArmors = PrototypeData.GetArmors();
                var shields = new List<ArmorData>();
                foreach (var a in allArmors.Values)
                    if (a.armorType == ArmorData.ArmorType.Shield) shields.Add(a);
                if (shields.Count > 0)
                {
                    var baseShield = shields[(int)(GD.Randf() * shields.Count)];
                    unitData.Shield = (ArmorData)EquipmentGenerator.GenerateEquipment(baseShield, (ItemData.Rarity)(-1), itemLevel, difficulty);
                    unitData.Shield.InitializeArmorPoints();
                }
            }
        }

        // 箭筒（弓/弩主手时作为副手装备，将弹药量提升至满值）
        bool hasQuiver = false;
        if (unitData.PrimaryMainHand is WeaponData rangedWpn && rangedWpn.IsRanged && !rangedWpn.IsThrowing && !rangedWpn.IsCatalyst)
        {
            var quivers = ItemDataLoader.GetQuivers();
            if (quivers.Count > 0)
            {
                var quiverList = new List<ItemData>(quivers.Values);
                var quiver = quiverList[(int)(GD.Randf() * quiverList.Count)];
                unitData.PrimaryOffHand = quiver;
                hasQuiver = true;
            }
        }

        // 初始化弹药（有箭筒：弓30发/弩24发，无箭筒：弓20发/弩18发）
        if (unitData.PrimaryMainHand is WeaponData primaryWpn && primaryWpn.NeedsAmmo)
            primaryWpn.InitializeAmmo(hasQuiver);
        if (unitData.SecondaryMainHand is WeaponData secondaryWpn && secondaryWpn.NeedsAmmo)
            secondaryWpn.InitializeAmmo(false); // 副手无箭筒加成

        // 头盔（70% 概率）
        if (unitData.Helmet == null && GD.Randf() < 0.7f)
        {
            var allArmors = PrototypeData.GetArmors();
            var helmets = new List<ArmorData>();
            foreach (var a in allArmors.Values)
                if (a.EquipSlotTarget == ItemData.EquipSlot.Helmet) helmets.Add(a);
            if (helmets.Count > 0)
            {
                var baseHelmet = helmets[(int)(GD.Randf() * helmets.Count)];
                unitData.Helmet = (ArmorData)EquipmentGenerator.GenerateEquipment(baseHelmet, (ItemData.Rarity)(-1), itemLevel, difficulty);
                unitData.Helmet.InitializeArmorPoints();
            }
        }

        // 饰品（80% 概率获得至少一件）
        if (unitData.Accessory1 == null && GD.Randf() < 0.8f)
        {
            var accessories = AccessoryData.GetAllAccessories();
            if (accessories.Length > 0)
                unitData.Accessory1 = accessories[(int)(GD.Randf() * accessories.Length)];
        }
    }

    // ============================================================
    // 子类实现：战斗结束 → 返回主菜单
    // ============================================================

    protected override void HandleCombatEnd(bool victory)
    {
        BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/main_menu.tscn");
    }

    // ============================================================
    // 辅助：技能盘分配
    // ============================================================

    /// <summary>为单位创建技能盘并自动分配技能点</summary>
    private static void AssignSkillTree(Unit unit, int level)
    {
        var stm = BladeHex.Data.Globals.SkillTreesOrNull;
        if (stm?.TreeData == null) return;

        var tree = new CharacterSkillTree(stm.TreeData, level);
        // 初始技能点 = 5 + (level - 1)
        int points = 5 + (level - 1);
        tree.AddSkillPoint(points);
        // 跳跃次数
        int jumps = 1 + (level - 1) / 6;
        if (jumps > 6) jumps = 6;
        for (int i = 0; i < jumps; i++)
            tree.RegisterJump();

        // 根据主属性自动加点
        string primaryAttr = GetPrimaryAttr(unit.Data);
        string secondaryAttr = GetSecondaryAttr(unit.Data);
        tree.AiAllocatePoints(0.8f, primaryAttr, secondaryAttr);

        unit.SkillTree = tree;
    }

    private static string GetPrimaryAttr(UnitData? data)
    {
        if (data == null) return "str";
        int max = data.Str;
        string attr = "str";
        if (data.Dex > max) { max = data.Dex; attr = "dex"; }
        if (data.Intel > max) { max = data.Intel; attr = "int"; }
        if (data.Wis > max) { max = data.Wis; attr = "wis"; }
        return attr;
    }

    private static string GetSecondaryAttr(UnitData? data)
    {
        if (data == null) return "dex";
        string primary = GetPrimaryAttr(data);
        var attrs = new (string name, int val)[] {
            ("str", data.Str), ("dex", data.Dex), ("con", data.Con),
            ("int", data.Intel), ("wis", data.Wis), ("cha", data.Cha)
        };
        System.Array.Sort(attrs, (a, b) => b.val.CompareTo(a.val));
        return attrs[0].name == primary ? attrs[1].name : attrs[0].name;
    }

    // ============================================================
    // ============================================================
    // 辅助：部署位置查找
    // ============================================================

    private Vector2I FindPlayerDeployPos(int index, int total)
    {
        int q = (int)GD.RandRange(1, 3);
        int rStep = Math.Max(1, _mapHeight / (total + 1));
        int r = rStep * (index + 1) - rStep / 2;
        r = Math.Clamp(r, 0, _mapHeight - 1);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var cell = _hexGrid.GetCell(q, r);
            if (cell != null && cell.Occupant == null && (cell.Data == null || cell.Data.isPassable))
                return new Vector2I(q, r);
            q = (int)GD.RandRange(1, 3);
            r = (int)GD.RandRange(0, _mapHeight - 1);
        }

        // Fallback: clamp 到地图范围内
        return new Vector2I(Math.Clamp(2, 0, _mapWidth - 1), Math.Clamp(index * 2, 0, _mapHeight - 1));
    }

    private Vector2I FindEnemyDeployPos(int index, int total)
    {
        int maxQ = _mapWidth - 1;
        int qLow = Math.Max(1, maxQ - 3);
        int q = (int)GD.RandRange(qLow, maxQ);
        int rStep = Math.Max(1, _mapHeight / (total + 1));
        int r = rStep * (index + 1) - rStep / 2;
        r = Math.Clamp(r, 0, _mapHeight - 1);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var cell = _hexGrid.GetCell(q, r);
            if (cell != null && cell.Occupant == null && (cell.Data == null || cell.Data.isPassable))
                return new Vector2I(q, r);
            q = (int)GD.RandRange(qLow, maxQ);
            r = (int)GD.RandRange(0, _mapHeight - 1);
        }

        // Fallback: clamp 到地图范围内
        return new Vector2I(Math.Clamp(maxQ - 1, 0, _mapWidth - 1), Math.Clamp(index * 2, 0, _mapHeight - 1));
    }
}
