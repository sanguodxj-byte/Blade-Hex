// QuickCombatScene.cs
// 快速战斗场景 — 继承 CombatSceneBase，只保留随机单位生成和返回主菜单
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Strategic;
using BladeHex.View.Environment;

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
        // 快速战斗使用普通战斗音乐
        _audioManager.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Combat, "normal", 1.0f);
    }

    // ============================================================
    // 子类实现：地图生成
    // ============================================================

    protected override void GenerateBattlefield()
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");

        string templateName = gs.QuickCombatTemplate ?? "";
        int sizeIdx = gs.QuickCombatSize;
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

        // 重新定位摄像机到地图中心
        float centerQ = _mapWidth / 2.0f;
        float centerR = _mapHeight / 2.0f;
        _camera.Position = new Vector3(centerQ * 100.0f, 800, centerR * 100.0f + 500);

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
        var gs = GetNode<GlobalState>("/root/GlobalState");
        int playerCount = Mathf.Clamp(gs.QuickCombatPlayerCount, 1, 6);
        int enemyCount = Mathf.Clamp(gs.QuickCombatEnemyCount, 1, 10);
        int difficulty = gs.QuickCombatDifficulty;

        // === 玩家方 ===
        var playerUnits = new List<Unit>();

        for (int i = 0; i < playerCount; i++)
        {
            var unitData = new UnitData
            {
                UnitName = $"冒险者_{i + 1}",
                Str = 14,
                Dex = 12,
                Con = 14,
                BaseMaxHp = 10,
                BaseAc = 10,
            };

            AssignRandomEquipment(unitData, isPlayer: true);

            var playerUnit = new Unit
            {
                Data = unitData,
                Name = $"Player_{i}"
            };

            var pos = FindPlayerDeployPos(i, playerCount);
            PlaceUnitAt(playerUnit, pos.X, pos.Y);
            _combatManager.RegisterUnit(playerUnit, true);
            playerUnit.InitDr();
            playerUnits.Add(playerUnit);
        }

        if (playerUnits.Count > 0)
            _activePlayerUnit = playerUnits[0];

        // === 敌方 ===
        int statBonus = difficulty switch { 0 => -2, 2 => 3, _ => 0 };

        for (int i = 0; i < enemyCount; i++)
        {
            var enemyData = new UnitData
            {
                UnitName = $"敌人_{i + 1}",
                IsEnemy = true,
                enemyType = UnitData.EnemyType.Humanoid,
                ThreatLevel = 0.5f + difficulty * 0.25f,
                aiStrategy = UnitData.AIStrategy.Instinct,
                Str = 12 + statBonus,
                Dex = 12 + statBonus,
                Con = 12 + statBonus,
                BaseMaxHp = 10 + difficulty * 3,
                BaseAc = 11 + difficulty,
            };

            AssignRandomEquipment(enemyData, isPlayer: false);

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
        }

        UpdateFov();
    }

    // ============================================================
    // 子类实现：战斗结束 → 返回主菜单
    // ============================================================

    protected override void HandleCombatEnd(bool victory)
    {
        BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/main_menu.tscn");
    }

    // ============================================================
    // 辅助：随机装备分配
    // ============================================================

    private static void AssignRandomEquipment(UnitData unitData, bool isPlayer)
    {
        float weaponRoll = GD.Randf();
        if (weaponRoll < 0.4f)
        {
            unitData.PrimaryMainHand = new WeaponData
            {
                ItemName = "长剑",
                DamageDiceCount = 1,
                DamageDiceSides = 8,
                WeaponDamageType = WeaponData.DamageType.Slash,
            };
        }
        else if (weaponRoll < 0.7f)
        {
            unitData.PrimaryMainHand = new WeaponData
            {
                ItemName = "巨斧",
                DamageDiceCount = 1,
                DamageDiceSides = 12,
                WeaponDamageType = WeaponData.DamageType.Slash,
            };
        }
        else
        {
            unitData.PrimaryMainHand = new WeaponData
            {
                ItemName = "长弓",
                IsRanged = true,
                RangeCells = 6,
                DamageDiceCount = 1,
                DamageDiceSides = 8,
                WeaponDamageType = WeaponData.DamageType.Pierce,
            };
        }

        if (GD.Randf() < 0.5f)
        {
            WeaponData backup;
            if (unitData.PrimaryMainHand != null && unitData.PrimaryMainHand.IsRanged)
            {
                backup = new WeaponData
                {
                    ItemName = "短剑",
                    DamageDiceCount = 1,
                    DamageDiceSides = 6,
                    WeaponDamageType = WeaponData.DamageType.Pierce,
                    IsFinesse = true,
                };
            }
            else
            {
                backup = new WeaponData
                {
                    ItemName = "短弓",
                    IsRanged = true,
                    RangeCells = 5,
                    DamageDiceCount = 1,
                    DamageDiceSides = 6,
                    WeaponDamageType = WeaponData.DamageType.Pierce,
                };
            }
            unitData.SecondaryMainHand = backup;
        }

        if (isPlayer || GD.Randf() < 0.6f)
        {
            unitData.Armor = new ArmorData
            {
                ItemName = isPlayer ? "链甲" : "皮甲",
                armorType = isPlayer ? ArmorData.ArmorType.Medium : ArmorData.ArmorType.Light,
                AcBonus = isPlayer ? 4 : 2,
                MaxDexBonus = isPlayer ? 2 : 3,
            };
        }
    }

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

        return new Vector2I(2, index * 2);
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

        return new Vector2I(maxQ - 1, index * 2);
    }
}
