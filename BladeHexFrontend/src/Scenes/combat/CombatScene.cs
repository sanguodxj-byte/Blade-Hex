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
using BladeHex.Strategic.Economy;

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

    /// <summary>战斗上下文 — 由 OverworldScene3D 传入</summary>
    public BattleContext? BattleContextRef;

    /// <summary>玩家队伍名册 — 由 OverworldScene3D 传入</summary>
    public PartyRoster? PlayerRoster;

    /// <summary>遭遇敌方单位列表 — 由 OverworldScene3D 传入</summary>
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

        // 根据威胁等级和天气选择战斗BGM变体
        string variant;
        if (totalThreat >= 3.0f)
            variant = "boss"; // 领主/传奇生物
        else
        {
            // 检查是否雨中战斗（从 Autoload 实时读取）
            var weatherMgr = BladeHex.Data.Globals.WeatherOrNull;
            bool isRaining = weatherMgr != null
                && weatherMgr.GetActiveWeatherType() == BladeHex.View.Environment.WeatherType.Rain;
            variant = isRaining ? "rain" : "normal";
        }

        _audioManager.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Combat, variant, 1.0f);
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
            // 兜底：随机选一个 preset，通过统一 pipeline 生成
            var presetNames = BattleMapGenerator.GetAvailablePresetNames();
            if (presetNames.Length == 0) { GD.PrintErr("[CombatScene] 无可用模板"); return; }
            string randomPreset = presetNames[GD.RandRange(0, presetNames.Length - 1)];
            _mapData = generator.GenerateFromTemplate(randomPreset, BattleMapGenerator.BattleSize.Mercenary);
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

        if (UsePlayerDeployment())
        {
            // 部署模式：玩家单位只注册不放置，由部署阶段交互放置
            SpawnPlayerUnitsForDeployment();
        }
        else
        {
            if (PlayerRoster != null && PlayerRoster.GetDeployableMembers().Count > 0)
                SpawnFromRoster(pDeploy);
            else
                SpawnHardcodedPlayer(pDeploy);
        }

        // 敌方始终自动放置。
        // 优先使用显式 EncounterEnemies（委托目标等旧入口），否则从 BattleContext 的部署描述生成。
        if (EncounterEnemies == null || EncounterEnemies.Count == 0)
            EncounterEnemies = BuildEncounterEnemiesFromBattleContext();

        if (EncounterEnemies != null && EncounterEnemies.Count > 0)
            SpawnFromEncounter(eDeploy);
        else
            SpawnHardcodedEnemies(eDeploy);

    }

    private List<UnitData>? BuildEncounterEnemiesFromBattleContext()
    {
        if (BattleContextRef?.DefenderDeployment == null || BattleContextRef.DefenderDeployment.Length == 0)
            return null;

        int seed = BattleContextRef.Seed != 0
            ? BattleContextRef.Seed
            : BattleContextRef.EncounterCoord.X * 997 + BattleContextRef.EncounterCoord.Y * 1009;

        return BattleDeploymentFactory.BuildUnits(BattleContextRef.DefenderDeployment, seed);
    }

    /// <summary>部署模式：创建玩家单位并注册，但不放置到地图上</summary>
    private void SpawnPlayerUnitsForDeployment()
    {
        if (PlayerRoster != null && PlayerRoster.GetDeployableMembers().Count > 0)
        {
            var deployable = PlayerRoster.GetDeployableMembers();
            int idx = 0;
            foreach (var memberData in deployable)
            {
                var unit = new Unit { Data = memberData, Name = $"Player_{memberData.UnitName}" };
                // 不调用 PlaceUnitAt — 单位不放到地图上
                RegisterAndInitUnit(unit, true);
                if (idx == 0) ActivePlayerUnit = unit;
                idx++;
            }
        }
        else
        {
            // Hardcoded fallback — 创建但不放置
            var playerData = new UnitData
            {
                UnitName = "战士", Str = 16, Dex = 14, Con = 15, BaseMaxHp = 10, BaseAc = 8,
                Armor = new ArmorData { ItemName = "链甲", armorType = ArmorData.ArmorType.Medium, AcBonus = 4, MaxDexBonus = 2 },
                PrimaryMainHand = new WeaponData { ItemName = "长剑", DamageDiceCount = 1, DamageDiceSides = 8, WeaponDamageType = WeaponData.DamageType.Slash },
                SecondaryMainHand = new WeaponData { ItemName = "长弓", IsRanged = true, RangeCells = 6, DamageDiceCount = 1, DamageDiceSides = 8, WeaponDamageType = WeaponData.DamageType.Pierce },
            };
            var unit = new Unit { Data = playerData, Name = "PlayerWarrior" };
            RegisterAndInitUnit(unit, true);
            ActivePlayerUnit = unit;
        }
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
            RegisterAndInitUnit(unit, true);
            if (deployed == 0) ActivePlayerUnit = unit;
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
            RegisterAndInitUnit(unit, false);
            deployed++;
        }
    }

    private void SpawnHardcodedPlayer(List<Vector2I> pDeploy)
    {
        // 用 CharacterGenerator 生成随机角色替代硬编码数据
        var playerData = CharacterGenerator.GenerateCharacter(level: 1, seedVal: -1);
        if (string.IsNullOrEmpty(playerData.UnitName))
            playerData.UnitName = "战士";
        var unit = new Unit { Data = playerData, Name = "PlayerWarrior" };
        // 优先使用部署区,fallback 用 (0,0) — PlaceUnitAt 会自动找最近可部署 cell
        var pos = pDeploy.Count > 0 ? pDeploy[^1] : Vector2I.Zero;
        if (pDeploy.Count > 0) pDeploy.RemoveAt(pDeploy.Count - 1);
        PlaceUnitAt(unit, pos.X, pos.Y);
        RegisterAndInitUnit(unit, true);
        ActivePlayerUnit = unit;
    }

    private void SpawnHardcodedEnemies(List<Vector2I> eDeploy)
    {
        var enemies = new List<UnitData>
        {
            new() { UnitName = "哥布林射手_1", IsEnemy = true, enemyType = UnitData.EnemyType.Humanoid, ThreatLevel = 0.25f, aiStrategy = UnitData.AIStrategy.Cautious, Str = 8, Dex = 16, Con = 10, BaseMaxHp = 7, BaseAc = 8, Armor = new ArmorData { ItemName = "皮甲", armorType = ArmorData.ArmorType.Light, AcBonus = 2, MaxDexBonus = 99 }, PrimaryMainHand = new WeaponData { ItemName = "短弓", IsRanged = true, RangeCells = 6, DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true }, Traits = new[] { "敏捷撤退" } },
            new() { UnitName = "哥布林射手_2", IsEnemy = true, enemyType = UnitData.EnemyType.Humanoid, ThreatLevel = 0.25f, aiStrategy = UnitData.AIStrategy.Cautious, Str = 8, Dex = 16, Con = 10, BaseMaxHp = 7, BaseAc = 8, Armor = new ArmorData { ItemName = "皮甲", armorType = ArmorData.ArmorType.Light, AcBonus = 2, MaxDexBonus = 99 }, PrimaryMainHand = new WeaponData { ItemName = "短弓", IsRanged = true, RangeCells = 6, DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true }, Traits = new[] { "敏捷撤退" } },
            new() { UnitName = "骷髅战士", IsEnemy = true, enemyType = UnitData.EnemyType.Undead, ThreatLevel = 0.5f, aiStrategy = UnitData.AIStrategy.Instinct, Str = 10, Dex = 14, Con = 10, BaseMaxHp = 13, BaseAc = 8, Armor = new ArmorData { ItemName = "锈蚀锁甲", armorType = ArmorData.ArmorType.Medium, AcBonus = 3, MaxDexBonus = 2 }, PrimaryMainHand = new WeaponData { ItemName = "锈蚀短剑", DamageDiceCount = 1, DamageDiceSides = 6, IsFinesse = true }, Immunities = new[] { "毒素" }, Resistances = new[] { "穿刺" } },
            new() { UnitName = "兽人狂战", IsEnemy = true, enemyType = UnitData.EnemyType.Humanoid, ThreatLevel = 1.0f, aiStrategy = UnitData.AIStrategy.Reckless, Str = 16, Dex = 12, Con = 14, BaseMaxHp = 15, BaseAc = 8, Armor = new ArmorData { ItemName = "兽皮甲", armorType = ArmorData.ArmorType.Medium, AcBonus = 4, MaxDexBonus = 2 }, PrimaryMainHand = new WeaponData { ItemName = "巨斧", DamageDiceCount = 1, DamageDiceSides = 12 }, Traits = new[] { "鲁莽攻击" } },
        };

        int deployed = 0;
        foreach (var data in enemies)
        {
            var unit = new Unit { Data = data, Name = $"Enemy_{data.UnitName}" };
            // 优先使用部署区,fallback 用 (0,0) — PlaceUnitAt 会自动找最近可部署 cell
            if (eDeploy.Count > 0)
            {
                var pos = eDeploy[^1]; eDeploy.RemoveAt(eDeploy.Count - 1);
                PlaceUnitAt(unit, pos.X, pos.Y);
            }
            else
            {
                PlaceUnitAt(unit, 0, 0);
            }
            RegisterAndInitUnit(unit, false);
            deployed++;
        }
    }

    // ============================================================
    // 战斗结束 → 战利品 + 任务汇报
    // ============================================================

    protected override BattleOutcome? BuildOutcomeForPresentation(bool victory)
    {
        var outcome = BuildBattleOutcome(victory);
        LastBattleOutcome = outcome;
        return outcome;
    }

    protected override void HandleCombatEnd(bool victory)
    {
        // LastBattleOutcome 已在 BuildOutcomeForPresentation 中设置
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
            outcome.GoldGranted = RewardPricingService.GetEncounterGold(avgLevel, enemyCount);
            outcome.XpGranted = RewardPricingService.GetEncounterXp(avgLevel, enemyCount);
            GenerateLoot(outcome.LootEntries, outcome.LootItems);
        }
        return outcome;
    }

}
