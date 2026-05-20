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
            var weatherMgr = BladeHex.Data.Globals.WeatherOrNull;
            bool isRaining = weatherMgr != null
                && weatherMgr.GetActiveWeatherType() == BladeHex.View.Environment.WeatherType.Rain;
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
        int seed = (int)GD.Randi();

        // 攻城/守城战强制使用 Stronghold 规模
        bool isSiege = templateName == "castle_siege" || templateName == "castle_defense";
        if (isSiege)
            battleSize = BattleContext.BattleSize.Stronghold;

        GD.Print($"[QuickCombat] template='{templateName}', size={battleSize}, seed={seed}, isSiege={isSiege}");

        if (!string.IsNullOrEmpty(templateName))
        {
            if (isSiege)
            {
                GD.Print($"[QuickCombat] 攻城/守城路径: 创建 context...");
                var ctx = Map.Generation.BattleOverworldFactory.CreateContext(
                    templateName, battleSize, seed);
                ctx.AttackingSide = templateName == "castle_defense"
                    ? BattleContext.BattleSide.Enemy
                    : BattleContext.BattleSide.Player;
                GD.Print($"[QuickCombat] context 创建完成, grid tiles={ctx.OverworldGrid?.TileCount() ?? 0}, coord={ctx.EncounterCoord}");
                _mapData = generator.Generate(ctx);
                GD.Print($"[QuickCombat] 地图生成完成, cells={_mapData?.Cells.Count ?? 0}");
            }
            else
            {
                _mapData = generator.GenerateFromTemplate(templateName,
                    (BattleMapGenerator.BattleSize)(int)battleSize, seed);
            }
        }
        else
        {
            var presetNames = BattleMapGenerator.GetAvailablePresetNames();
            var filtered = new System.Collections.Generic.List<string>();
            foreach (var name in presetNames)
            {
                var preset = Map.Generation.BattleOverworldFactory.GetPreset(name);
                if (!preset.IsStronghold) filtered.Add(name);
            }
            string randomPreset = filtered.Count > 0
                ? filtered[GD.RandRange(0, filtered.Count - 1)]
                : presetNames[GD.RandRange(0, presetNames.Length - 1)];
            _mapData = generator.GenerateFromTemplate(randomPreset,
                (BattleMapGenerator.BattleSize)(int)battleSize, seed);
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

        bool deployMode = UsePlayerDeployment();

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

            if (!deployMode)
            {
                var pos = FindPlayerDeployPos(i, playerCount);
                PlaceUnitAt(playerUnit, pos.X, pos.Y);
            }
            _combatManager.RegisterUnit(playerUnit, true);
            _combatUi.RegisterAlly(playerUnit);
            playerUnit.InitDr();

            // 分配技能盘并自动加点
            AssignSkillTree(playerUnit, playerLevel);

            playerUnits.Add(playerUnit);
        }

        if (playerUnits.Count > 0)
            _activePlayerUnit = playerUnits[0];

        // === 敌方（始终自动放置）===
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

            if (thisEnemyType == UnitData.EnemyType.Humanoid)
                AssignSkillTree(enemyUnit, Mathf.Max(1, enemyData.Level));
        }

        UpdateFov();
    }

    /// <summary>为单位分配随机装备（完整套装）</summary>
    private static void EquipUnit(UnitData unitData, int itemLevel, string difficulty)
    {
        EquipmentGenerator.EquipFullSet(unitData, itemLevel, difficulty);
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

    private static void AssignSkillTree(Unit unit, int level)
    {
        var stm = BladeHex.Data.Globals.SkillTreesOrNull;
        if (stm?.TreeData == null) return;
        if (unit.Data == null) return;

        var tree = BladeHex.Strategic.SkillTreeAllocator.AllocateForUnit(unit.Data, stm.TreeData);
        if (tree != null)
        {
            unit.SkillTree = tree;
            unit.Data.Runtime.SkillTree = tree;
        }
    }

    // ============================================================
    // 辅助：部署位置查找
    // ============================================================

    private Vector2I FindPlayerDeployPos(int index, int total)
    {
        if (_mapData?.PlayerDeployment != null && _mapData.PlayerDeployment.Count > 0)
        {
            for (int attempt = 0; attempt < _mapData.PlayerDeployment.Count; attempt++)
            {
                int idx = (index + attempt) % _mapData.PlayerDeployment.Count;
                var coord = _mapData.PlayerDeployment[idx].AsVector2I();
                var cell = _hexGrid.GetCell(coord.X, coord.Y);
                if (cell != null && cell.Occupant == null && (cell.Data == null || cell.Data.isPassable))
                    return coord;
            }
        }

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

        return Vector2I.Zero;
    }

    private Vector2I FindEnemyDeployPos(int index, int total)
    {
        if (_mapData?.EnemyDeployment != null && _mapData.EnemyDeployment.Count > 0)
        {
            for (int attempt = 0; attempt < _mapData.EnemyDeployment.Count; attempt++)
            {
                int idx = (index + attempt) % _mapData.EnemyDeployment.Count;
                var coord = _mapData.EnemyDeployment[idx].AsVector2I();
                var cell = _hexGrid.GetCell(coord.X, coord.Y);
                if (cell != null && cell.Occupant == null && (cell.Data == null || cell.Data.isPassable))
                    return coord;
            }
        }

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

        return Vector2I.Zero;
    }
}
