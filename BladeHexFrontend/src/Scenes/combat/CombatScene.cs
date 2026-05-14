// CombatScene.cs
// 正式战斗场景 — 继承 CombatSceneBase，仅持有初始化状态和结束处理
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Strategic;

namespace BladeHex.Scenes;

/// <summary>
/// 战术战斗主场景 — 从大地图进入战斗时实例化。
/// 仅持有初始化数据（Roster/Encounter/BattleContext）和结束处理（战利品/任务汇报）。
/// 所有战斗交互逻辑由 CombatSceneBase 统一提供。
/// </summary>
[GlobalClass]
public partial class CombatScene : CombatSceneBase
{
    // ========== 大地图传入数据 ==========

    /// <summary>战斗上下文 — 由 OverworldScene 传入</summary>
    public BattleContext? BattleContextRef;

    /// <summary>玩家队伍名册 — 由 OverworldScene 传入</summary>
    public PartyRoster? PlayerRoster;

    /// <summary>遭遇敌方单位列表 — 由 OverworldScene 传入</summary>
    public List<UnitData>? EncounterEnemies;

    /// <summary>战斗结果（战斗结束后填充，供大地图回调读取）</summary>
    public BattleOutcome? LastBattleOutcome { get; private set; }

    // ============================================================
    // 战前设置（天气）
    // ============================================================

    protected override void OnPreBattleSetup()
    {
        SetupCombatWeather();
    }

    // ============================================================
    // 战斗音乐
    // ============================================================

    protected override void PlayCombatMusic()
    {
        float totalThreat = 0.0f;
        foreach (var enemy in _combatManager.EnemyUnits)
        {
            if (IsInstanceValid(enemy) && enemy.Data != null)
                totalThreat += enemy.Data.ThreatLevel;
        }

        if (_audioManager == null || !IsInstanceValid(_audioManager)) return;

        if (totalThreat >= 3.0f)
            _audioManager.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Combat, "boss", 1.0f);
        else
            _audioManager.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Combat, "normal", 1.0f);
    }

    // ============================================================
    // 地图生成
    // ============================================================

    protected override void GenerateBattlefield()
    {
        var generator = new BattleMapGenerator();
        if (BattleContextRef != null)
        {
            _mapData = generator.Generate(BattleContextRef);
        }
        else
        {
            var templateNames = generator.GetTemplateNames();
            if (templateNames.Length == 0) { GD.PrintErr("[CombatScene] 无可用模板"); return; }
            string randomTemplate = templateNames[GD.RandRange(0, templateNames.Length - 1)];
            _mapData = generator.GenerateFromTemplate(randomTemplate, BattleMapGenerator.BattleSize.Mercenary);
        }

        _mapWidth = _mapData.Width;
        _mapHeight = _mapData.Height;
        _hexGrid.LoadFromMapData(_mapData);

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
    // 单位生成
    // ============================================================

    protected override void SpawnUnits()
    {
        var pDeploy = _mapData!.PlayerDeployment.Select(v => v.AsVector2I()).OrderBy(_ => GD.Randf()).ToList();
        var eDeploy = _mapData.EnemyDeployment.Select(v => v.AsVector2I()).OrderBy(_ => GD.Randf()).ToList();

        if (PlayerRoster != null && PlayerRoster.GetDeployableMembers().Count > 0)
            SpawnFromRoster(pDeploy);
        else
            SpawnHardcodedPlayer(pDeploy);

        if (EncounterEnemies != null && EncounterEnemies.Count > 0)
            SpawnFromEncounter(eDeploy);
        else
            SpawnHardcodedEnemies(eDeploy);

        UpdateFov();
    }

    private void SpawnFromRoster(List<Vector2I> positions)
    {
        var deployable = PlayerRoster!.GetDeployableMembers();
        int deployed = 0;
        foreach (var memberData in deployable)
        {
            if (deployed >= positions.Count) break;
            var unit = new Unit { Data = memberData, Name = $"Player_{memberData.UnitName}" };
            PlaceUnitAt(unit, positions[deployed].X, positions[deployed].Y);
            _combatManager.RegisterUnit(unit, true);
            _combatUi.RegisterAlly(unit);
            unit.InitDr();
            if (deployed == 0) _activePlayerUnit = unit;
            deployed++;
        }
    }

    private void SpawnFromEncounter(List<Vector2I> positions)
    {
        int deployed = 0;
        foreach (var enemyData in EncounterEnemies!)
        {
            if (deployed >= positions.Count) break;
            var unit = new Unit { Data = enemyData, Name = $"Enemy_{enemyData.UnitName}" };
            PlaceUnitAt(unit, positions[deployed].X, positions[deployed].Y);
            _combatManager.RegisterUnit(unit, false);
            _combatUi.RegisterEnemy(unit);
            unit.InitDr();
            deployed++;
        }
    }

    private void SpawnHardcodedPlayer(List<Vector2I> pDeploy)
    {
        var playerData = new UnitData
        {
            UnitName = "战士", Str = 16, Dex = 14, Con = 15, BaseMaxHp = 10, BaseAc = 10,
            Armor = new ArmorData { ItemName = "链甲", armorType = ArmorData.ArmorType.Medium, AcBonus = 4, MaxDexBonus = 2 },
            PrimaryMainHand = new WeaponData { ItemName = "长剑", DamageDiceCount = 1, DamageDiceSides = 8, WeaponDamageType = WeaponData.DamageType.Slash },
            SecondaryMainHand = new WeaponData { ItemName = "长弓", IsRanged = true, RangeCells = 6, DamageDiceCount = 1, DamageDiceSides = 8, WeaponDamageType = WeaponData.DamageType.Pierce },
        };
        var unit = new Unit { Data = playerData, Name = "PlayerWarrior" };
        var pos = pDeploy.Count > 0 ? pDeploy[^1] : new Vector2I(2, 2);
        if (pDeploy.Count > 0) pDeploy.RemoveAt(pDeploy.Count - 1);
        PlaceUnitAt(unit, pos.X, pos.Y);
        _combatManager.RegisterUnit(unit, true);
        unit.InitDr();
        _activePlayerUnit = unit;
    }

    private void SpawnHardcodedEnemies(List<Vector2I> eDeploy)
    {
        var enemies = new List<UnitData>
        {
            new() { UnitName = "哥布林射手_1", IsEnemy = true, enemyType = UnitData.EnemyType.Humanoid, ThreatLevel = 0.25f, aiStrategy = UnitData.AIStrategy.Cautious, Str = 8, Dex = 16, Con = 10, BaseMaxHp = 7, BaseAc = 13, PrimaryMainHand = new WeaponData { ItemName = "短弓", IsRanged = true, RangeCells = 6, DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true }, Traits = new[] { "敏捷撤退" } },
            new() { UnitName = "哥布林射手_2", IsEnemy = true, enemyType = UnitData.EnemyType.Humanoid, ThreatLevel = 0.25f, aiStrategy = UnitData.AIStrategy.Cautious, Str = 8, Dex = 16, Con = 10, BaseMaxHp = 7, BaseAc = 13, PrimaryMainHand = new WeaponData { ItemName = "短弓", IsRanged = true, RangeCells = 6, DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true }, Traits = new[] { "敏捷撤退" } },
            new() { UnitName = "骷髅战士", IsEnemy = true, enemyType = UnitData.EnemyType.Undead, ThreatLevel = 0.5f, aiStrategy = UnitData.AIStrategy.Instinct, Str = 10, Dex = 14, Con = 10, BaseMaxHp = 13, BaseAc = 13, PrimaryMainHand = new WeaponData { ItemName = "锈蚀短剑", DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true }, Immunities = new[] { "毒素" }, Resistances = new[] { "穿刺" } },
            new() { UnitName = "兽人狂战", IsEnemy = true, enemyType = UnitData.EnemyType.Humanoid, ThreatLevel = 1.0f, aiStrategy = UnitData.AIStrategy.Reckless, Morale = 10, Str = 16, Dex = 12, Con = 14, BaseMaxHp = 15, BaseAc = 13, PrimaryMainHand = new WeaponData { ItemName = "巨斧", DamageDiceCount = 1, DamageDiceSides = 12 }, Traits = new[] { "鲁莽攻击" } },
        };

        int deployed = 0;
        foreach (var data in enemies)
        {
            var unit = new Unit { Data = data, Name = $"Enemy_{data.UnitName}" };
            if (eDeploy.Count > 0) { var pos = eDeploy[^1]; eDeploy.RemoveAt(eDeploy.Count - 1); PlaceUnitAt(unit, pos.X, pos.Y); }
            else PlaceUnitAt(unit, 8, deployed * 2);
            _combatManager.RegisterUnit(unit, false); _combatUi.RegisterEnemy(unit); unit.InitDr();
            deployed++;
        }
    }

    // ============================================================
    // 战斗结束 → 战利品 + 任务汇报
    // ============================================================

    protected override void HandleCombatEnd(bool victory)
    {
        if (victory) ReportCombatResultsToQuests();
        LastBattleOutcome = BuildBattleOutcome(victory);
    }

    private BattleOutcome BuildBattleOutcome(bool victory)
    {
        var outcome = new BattleOutcome { AttackerWon = victory };

        foreach (var unit in _combatManager.PlayerUnits)
        {
            if (unit?.Data == null) continue;
            if (unit.CurrentHp <= 0) outcome.DeadUnitNames.Add(unit.Data.UnitName);
            else outcome.SurvivorHp[unit.Data.UnitName] = unit.CurrentHp;
        }

        if (victory)
        {
            int enemyCount = _combatManager.EnemyUnits.Count;
            int avgLevel = enemyCount > 0
                ? _combatManager.EnemyUnits.Sum(e => e.Data?.Level ?? 1) / enemyCount : 1;
            outcome.GoldGranted = avgLevel * 10 + enemyCount * 8;
            outcome.XpGranted = avgLevel * 25 + enemyCount * 15;
            GenerateLoot(outcome);
        }
        return outcome;
    }

    private void GenerateLoot(BattleOutcome outcome)
    {
        float totalCr = 0;
        foreach (var enemy in _combatManager.EnemyUnits)
        {
            if (enemy?.Data == null) continue;
            var data = enemy.Data;
            totalCr += data.ThreatLevel;

            if (data.Armor != null)
                outcome.LootEntries.Add(new LootEntry(data.Armor.GetFullName(), LootEntry.LootType.Armor, 1, data.Armor.GetSellPrice(), data.Armor.GetArmorDescription()));
            if (data.Shield != null)
                outcome.LootEntries.Add(new LootEntry(data.Shield.GetFullName(), LootEntry.LootType.Shield, 1, data.Shield.GetSellPrice(), data.Shield.GetArmorDescription()));
            if (data.Helmet != null)
                outcome.LootEntries.Add(new LootEntry(data.Helmet.GetFullName(), LootEntry.LootType.Helmet, 1, data.Helmet.GetSellPrice(), data.Helmet.GetArmorDescription()));
            if (data.PrimaryMainHand != null && GD.Randf() < 0.5f)
                outcome.LootEntries.Add(new LootEntry(data.PrimaryMainHand.GetFullName(), LootEntry.LootType.Weapon, 1, data.PrimaryMainHand.GetSellPrice(), data.PrimaryMainHand.GetWeaponDescription()));
        }

        string difficulty = EquipmentGenerator.GetDifficultyFromCr(totalCr);
        int itemLevel = EquipmentGenerator.GetItemLevelFromCr(totalCr);
        int bonusDrops = Math.Clamp((int)(totalCr / 3.0f), 0, 4);

        for (int i = 0; i < bonusDrops; i++)
        {
            if (GD.Randf() < 0.4f)
            {
                var generated = GD.Randf() < 0.5f
                    ? (ItemData)EquipmentGenerator.GenerateRandomWeapon(null, (ItemData.Rarity)(-1), itemLevel, difficulty)
                    : (ItemData)EquipmentGenerator.GenerateRandomArmor(null, (ItemData.Rarity)(-1), itemLevel, difficulty);
                var lootType = generated is WeaponData ? LootEntry.LootType.Weapon : LootEntry.LootType.Armor;
                outcome.LootEntries.Add(new LootEntry(generated.GetFullName(), lootType, 1, generated.GetSellPrice(), $"{generated.GetRarityName()} | {generated.GetAffixDescriptions()}"));
            }
        }

        var consumableList = new List<ConsumableData>(PrototypeData.GetConsumables().Values);
        int consumDrops = _combatManager.EnemyUnits.Count / 3;
        for (int i = 0; i < consumDrops; i++)
        {
            if (GD.Randf() < 0.35f && consumableList.Count > 0)
            {
                var c = consumableList[(int)(GD.Randf() * consumableList.Count)];
                outcome.LootEntries.Add(new LootEntry(c.ItemName, LootEntry.LootType.Consumable, 1, c.Price, c.Description));
            }
        }
    }

    private void ReportCombatResultsToQuests()
    {
        var qm = GetParent()?.GetNodeOrNull("QuestManager");
        if (qm == null || !IsInstanceValid(qm)) return;
        var activeQuests = qm.Call("get", "active_quests").AsGodotArray();
        if (activeQuests == null) return;

        foreach (var questVar in activeQuests)
        {
            var quest = questVar.AsGodotDictionary();
            if (quest == null) continue;
            if (quest.GetValueOrDefault("quest_type", 0).AsInt32() == (int)QuestData.QuestType.Extermination)
                qm.Call("update_quest_progress", quest.GetValueOrDefault("quest_id", "").AsString(), 3);
        }
    }
}
