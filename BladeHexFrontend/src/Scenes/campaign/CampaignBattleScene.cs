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
using BladeHex.UI.Combat;

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
			ActivePlayerUnit = playerUnits[0];

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
					if (tree != null) { enemyUnit.SkillTree = tree; enemyUnit.Model.SkillTree = tree; }
				}
			}
		}

		/* FOV 机制已移除，见 notes.md */
	}

	// ============================================================
	// 战斗结束
	// ============================================================

	protected override void HandleCombatEnd(bool victory)
	{
		// 计算奖励
		var econCtx = CreateCampaignEconomyContext();
		int xpGranted = victory ? CampaignPricingService.GetBattleXpReward(econCtx) : 0;
		int goldGranted = victory ? CampaignPricingService.GetBattleGoldReward(econCtx) : 0;

		// 构建队员状态列表
		var unitStatuses = new List<UnitStatusEntry>();
		var deadUnits = new List<UnitData>();

		foreach (var unit in _combatManager.PlayerUnits)
		{
			if (unit?.Data == null) continue;

			if (unit.CurrentHp > 0)
			{
				// 存活：加经验、回血
				if (victory)
				{
					unit.Data.Xp += xpGranted;
					int maxHp = unit.Data.BaseMaxHp;
					int healed = unit.CurrentHp + (int)(maxHp * 0.3f);
					PartyRoster.SetCurrentHp(unit.Data, Mathf.Min(healed, maxHp));
				}

				// 检查升级前后的等级变化
				int oldLevel = unit.Data.Level;
				if (victory)
					CampSystem.CheckAndApplyLevelUps(_campaignCtx.Roster);

				unitStatuses.Add(new UnitStatusEntry(unit.Data, UnitStatus.Alive, unit.Data.Level > oldLevel));
			}
			else
			{
				// 阵亡/重伤
				if (_campaignCtx.Roster.IsLeader(unit.Data))
				{
					deadUnits.Add(unit.Data);
					unitStatuses.Add(new UnitStatusEntry(unit.Data, UnitStatus.Dead));
				}
				else
				{
					unit.Data.IsWounded = true;
					PartyRoster.SetCurrentHp(unit.Data, 0);
					unitStatuses.Add(new UnitStatusEntry(unit.Data, UnitStatus.Wounded));
				}
			}
		}

		// 移除阵亡队员
		foreach (var d in deadUnits)
			_campaignCtx.Roster.Remove(d);

		// 生成战利品（调用基类方法）
		var lootEntries = new List<LootEntry>();
		var lootItems = new List<BladeHex.Data.ItemData>();
		if (victory)
		{
			GenerateLoot(lootEntries, lootItems);
		}

		// 胜利时：金币奖励接入经济系统（通过 EventBus 通知 UI）
		if (victory)
		{
			int oldGold = _campaignCtx.Gold;
			_campaignCtx.Gold += goldGranted;
			// 发布金币变化事件，接入经济系统
			BladeHex.Events.EventBus.Instance?.PublishGoldChanged(oldGold, _campaignCtx.Gold, goldGranted);
			// 将掉落物品加入队伍背包
			if (lootEntries.Count > 0)
			{
				int added = _campaignCtx.Inventory.AddAll(lootEntries);
				GD.Print($"[Campaign] 战利品已加入背包: {added} 件物品");
				// 发布物品获取事件
				foreach (var entry in lootEntries)
				{
					BladeHex.Events.EventBus.Instance?.Publish(BladeHex.Events.EventBus.Signals.ItemAcquired,
						new Godot.Collections.Dictionary { { "item_name", entry.ItemName } });
				}
			}
			_campaignCtx.CurrentLevel++;
		}

		// 显示结算面板（含战利品）
		var resultPanel = new BattleResultPanel();
		AddChild(resultPanel);
		resultPanel.ShowResult(
			victory,
			xpGranted,
			goldGranted,
			lootEntries.Count > 0 ? lootEntries : null,
			unitStatuses,
			victory ? "战斗胜利！你的队伍获得了经验和金币。" : "队伍被击败，被迫撤退..."
		);
		resultPanel.ContinueClicked += () =>
		{
			if (victory)
			{
				BladeHex.View.SceneTransition.ChangeSceneTo(
					GetTree(), "res://BladeHexFrontend/src/scenes/campaign/campaign_scene.tscn");
			}
			else
			{
				_campaignCtx.IsActive = false;
				BladeHex.View.SceneTransition.ChangeSceneTo(
					GetTree(), "res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
			}
		};
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
			unit.Model.SkillTree = existing;
		}
		else
		{
			var tree = SkillTreeAllocator.AllocateForUnit(unit.Data, stm.TreeData);
			if (tree != null)
			{
				unit.SkillTree = tree;
				unit.Model.SkillTree = tree;
				stm.SetSkillTree(charId, tree);
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
