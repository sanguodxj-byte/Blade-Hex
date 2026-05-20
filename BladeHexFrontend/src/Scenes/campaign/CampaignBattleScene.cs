// CampaignBattleScene.cs
// 战役战斗场景 — 继承 CombatSceneBase
// 玩家方：从 CampaignContext.Roster 加载（不生成）
// 敌方：用 CharacterGenerator 按关卡配置生成
// 战斗结束：经验/金币/升级/阵亡处理，返回准备阶段或主菜单
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Data.Contexts;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.Scenes.Campaign;

[GlobalClass]
public partial class CampaignBattleScene : CombatSceneBase
{
	private CampaignContext _campaignCtx = null!;
	private CampaignLevelDef _levelDef = null!;

	public override void _Ready()
	{
		var gs = BladeHex.Data.Globals.State;
		_campaignCtx = gs.Campaign;

		var levels = CampaignLevels.GetDefaultCampaign();
		int idx = Mathf.Clamp(_campaignCtx.CurrentLevel, 0, levels.Count - 1);
		_levelDef = levels[idx];

		base._Ready();
	}

	// ============================================================
	// 战前设置
	// ============================================================

	protected override void OnPreBattleSetup() => SetupCombatWeather();

	protected override void PlayCombatMusic()
	{
		if (_audioManager == null || !IsInstanceValid(_audioManager)) return;
		string variant = _levelDef.IsBoss ? "boss" : "normal";
		_audioManager.PlayScenarioBgm(BladeHex.Audio.AudioManager.Scenario.Combat, variant, 1.0f);
	}

	// ============================================================
	// 地图生成（复用 QuickCombat 的模板 pipeline）
	// ============================================================

	protected override void GenerateBattlefield()
	{
		var battleSize = _levelDef.BattleSize switch
		{
			1 => BattleContext.BattleSize.Knight,
			2 => BattleContext.BattleSize.Lord,
			3 => BattleContext.BattleSize.Stronghold,
			_ => BattleContext.BattleSize.Mercenary,
		};

		var generator = new BattleMapGenerator();
		int seed = (int)GD.Randi();
		string templateName = _levelDef.MapTemplate;

		bool isSiege = templateName == "castle_siege" || templateName == "castle_defense";
		if (isSiege)
			battleSize = BattleContext.BattleSize.Stronghold;

		if (!string.IsNullOrEmpty(templateName))
		{
			if (isSiege)
			{
				var ctx = Map.Generation.BattleOverworldFactory.CreateContext(templateName, battleSize, seed);
				ctx.AttackingSide = templateName == "castle_defense"
					? BattleContext.BattleSide.Enemy : BattleContext.BattleSide.Player;
				_mapData = generator.Generate(ctx);
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
			var filtered = new List<string>();
			foreach (var name in presetNames)
			{
				var preset = Map.Generation.BattleOverworldFactory.GetPreset(name);
				if (!preset.IsStronghold) filtered.Add(name);
			}
			string pick = filtered.Count > 0
				? filtered[GD.RandRange(0, filtered.Count - 1)]
				: presetNames[GD.RandRange(0, presetNames.Length - 1)];
			_mapData = generator.GenerateFromTemplate(pick,
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
	// 单位生成
	// ============================================================

	protected override void SpawnUnits()
	{
		// === 玩家方 ===
		var members = _campaignCtx.Roster.GetDeployableMembers();
		var playerUnits = new List<Unit>();

		if (UsePlayerDeployment())
		{
			// 部署模式：创建单位但不放置
			for (int i = 0; i < members.Count; i++)
			{
				var unitData = members[i];
				unitData.IsEnemy = false;
				var playerUnit = new Unit { Data = unitData, Name = $"Player_{i}" };
				_combatManager.RegisterUnit(playerUnit, true);
				_combatUi.RegisterAlly(playerUnit);
				playerUnit.InitDr();
				AssignSkillTreeForCampaignUnit(playerUnit);
				playerUnits.Add(playerUnit);
			}
		}
		else
		{
			// 自动放置模式
			for (int i = 0; i < members.Count; i++)
			{
				var unitData = members[i];
				unitData.IsEnemy = false;
				var playerUnit = new Unit { Data = unitData, Name = $"Player_{i}" };
				var pos = FindDeployPos(_mapData?.PlayerDeployment, i, members.Count, true);
				PlaceUnitAt(playerUnit, pos.X, pos.Y);
				_combatManager.RegisterUnit(playerUnit, true);
				_combatUi.RegisterAlly(playerUnit);
				playerUnit.InitDr();
				AssignSkillTreeForCampaignUnit(playerUnit);
				playerUnits.Add(playerUnit);
			}
		}

		if (playerUnits.Count > 0)
			_activePlayerUnit = playerUnits[0];

		// === 敌方：用生成器按关卡配置生成 ===
		int enemyCount = _levelDef.EnemyCount;
		int enemyLevel = _levelDef.EnemyLevel;
		int difficulty = _levelDef.Difficulty;
		int enemyTypeIdx = _levelDef.EnemyType;

		float difficultyMult = difficulty switch { 0 => 0.7f, 2 => 1.5f, _ => 1.0f };
		string difficultyStr = difficulty switch { 0 => "easy", 2 => "hard", _ => "normal" };
		int itemLevel = EquipmentGenerator.GetItemLevelFromCr(RPGRuleEngine.GetCrFromLevel(enemyLevel));

		var enemyType = enemyTypeIdx switch
		{
			0 => UnitData.EnemyType.Humanoid,
			1 => UnitData.EnemyType.Undead,
			2 => UnitData.EnemyType.Beast,
			_ => UnitData.EnemyType.Humanoid,
		};

		for (int i = 0; i < enemyCount; i++)
		{
			var thisType = enemyTypeIdx == 3
				? (UnitData.EnemyType)((int)(GD.Randf() * 3))
				: enemyType;

			float cr = RPGRuleEngine.GetCrFromLevel(enemyLevel) * difficultyMult;
			var enemyData = CharacterGenerator.GenerateRandomEnemy(cr, thisType);
			enemyData.BaseAc = 8;
			EquipmentGenerator.EquipFullSet(enemyData, itemLevel, difficultyStr);

			var enemyUnit = new Unit { Data = enemyData, Name = $"Enemy_{i}" };

			var pos = FindDeployPos(_mapData?.EnemyDeployment, i, enemyCount, false);
			PlaceUnitAt(enemyUnit, pos.X, pos.Y);
			_combatManager.RegisterUnit(enemyUnit, false);
			_combatUi.RegisterEnemy(enemyUnit);
			enemyUnit.InitDr();

			if (thisType == UnitData.EnemyType.Humanoid)
			{
				var stm = BladeHex.Data.Globals.SkillTreesOrNull;
				if (stm?.TreeData != null)
				{
					var tree = SkillTreeAllocator.AllocateForUnit(enemyData, stm.TreeData);
					if (tree != null) { enemyUnit.SkillTree = tree; enemyData.Runtime.SkillTree = tree; }
				}
			}
		}

		UpdateFov();
	}

	// ============================================================
	// 战斗结束
	// ============================================================

	protected override void HandleCombatEnd(bool victory)
	{
		if (victory)
		{
			// 经验奖励
			int xpGranted = CampaignPricingService.GetBattleXpReward(CreateCampaignEconomyContext());

			foreach (var unit in _combatManager.PlayerUnits)
			{
				if (unit?.Data == null) continue;
				if (unit.CurrentHp > 0)
				{
					unit.Data.Xp += xpGranted;
					int maxHp = unit.Data.BaseMaxHp;
					int healed = unit.CurrentHp + (int)(maxHp * 0.3f);
					PartyRoster.SetCurrentHp(unit.Data, Mathf.Min(healed, maxHp));
				}
			}

			// 移除阵亡与重伤处理
			var dead = new List<UnitData>();
			foreach (var unit in _combatManager.PlayerUnits)
			{
				if (unit?.Data != null && unit.CurrentHp <= 0)
				{
					if (_campaignCtx.Roster.IsLeader(unit.Data))
					{
						dead.Add(unit.Data);
					}
					else
					{
						unit.Data.IsWounded = true;
						PartyRoster.SetCurrentHp(unit.Data, 0);
					}
				}
			}
			foreach (var d in dead)
				_campaignCtx.Roster.Remove(d);

			// 金币奖励 + 升级检查
			_campaignCtx.Gold += CampaignPricingService.GetBattleGoldReward(CreateCampaignEconomyContext());
			CampSystem.CheckAndApplyLevelUps(_campaignCtx.Roster);

			// 推进关卡
			_campaignCtx.CurrentLevel++;

			BladeHex.View.SceneTransition.ChangeSceneTo(
				GetTree(), "res://src/scenes/campaign/campaign_scene.tscn");
		}
		else
		{
			_campaignCtx.IsActive = false;
			var timer = GetTree().CreateTimer(2.0f);
			timer.Timeout += () =>
			{
				BladeHex.View.SceneTransition.ChangeSceneTo(
					GetTree(), "res://src/ui/main_menu/main_menu.tscn");
			};
		}
	}

	// ============================================================
	// 辅助
	// ============================================================

	private static void AssignSkillTreeForCampaignUnit(Unit unit)
	{
		var stm = BladeHex.Data.Globals.SkillTreesOrNull;
		if (stm?.TreeData == null || unit.Data == null) return;

		long charId = (long)unit.Data.GetInstanceId();
		var existing = stm.GetSkillTree(charId);

		if (existing != null)
		{
			unit.SkillTree = existing;
			unit.Data.Runtime.SkillTree = existing;
		}
		else
		{
			var tree = SkillTreeAllocator.AllocateForUnit(unit.Data, stm.TreeData);
			if (tree != null)
			{
				unit.SkillTree = tree;
				unit.Data.Runtime.SkillTree = tree;
				stm.CharacterTrees[charId] = tree;
			}
		}
	}

	private Vector2I FindDeployPos(Godot.Collections.Array? deployList, int index, int total, bool isPlayer)
	{
		if (deployList != null && deployList.Count > 0)
		{
			for (int attempt = 0; attempt < deployList.Count; attempt++)
			{
				int idx = (index + attempt) % deployList.Count;
				var coord = deployList[idx].AsVector2I();
				var cell = _hexGrid.GetCell(coord.X, coord.Y);
				if (cell != null && cell.Occupant == null && (cell.Data == null || cell.Data.isPassable))
					return coord;
			}
		}

		// 回退随机
		int qMin = isPlayer ? 1 : Math.Max(1, _mapWidth - 4);
		int qMax = isPlayer ? 3 : _mapWidth - 1;

		for (int attempt = 0; attempt < 20; attempt++)
		{
			int q = (int)GD.RandRange(qMin, qMax);
			int r = (int)GD.RandRange(0, _mapHeight - 1);
			var cell = _hexGrid.GetCell(q, r);
			if (cell != null && cell.Occupant == null && (cell.Data == null || cell.Data.isPassable))
				return new Vector2I(q, r);
		}

		return Vector2I.Zero;
	}

	private CampaignEconomyContext CreateCampaignEconomyContext()
	{
		return new CampaignEconomyContext(
			LevelIndex: _campaignCtx.CurrentLevel,
			EnemyLevel: _levelDef.EnemyLevel,
			EnemyCount: _levelDef.EnemyCount,
			Difficulty: _levelDef.Difficulty,
			BattleSize: _levelDef.BattleSize,
			IsBoss: _levelDef.IsBoss);
	}
}
