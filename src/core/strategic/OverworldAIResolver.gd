# OverworldAIResolver.gd
# AI间战斗自动结算引擎 —— 非玩家势力之间的战斗由系统自动结算
# 结算公式：战力比 × 随机因素(0.8-1.2) → 判定胜方
# 败方损失40-70%兵力，胜方损失10-30%
class_name OverworldAIResolver
extends RefCounted

## 结算一场AI间的战斗
## 返回: { "attacker_won": bool, "attacker_losses": float, "defender_losses": float, "description": String }
static func resolve_battle(attacker: OverworldEntity, defender: OverworldEntity) -> Dictionary:
	var atk_power = attacker.combat_power
	var def_power = defender.combat_power
	
	# 围攻加成：围攻方攻击力减少（因为要攻城）
	if attacker.ai_state == OverworldEntity.AIState.BESIEGING:
		def_power *= 1.5  # 防御方有城墙优势
	if attacker.siege_target != null:
		def_power += attacker.siege_target.get_defense_power() * 0.3
	
	# 随机因素
	var atk_roll = atk_power * randf_range(0.8, 1.2)
	var def_roll = def_power * randf_range(0.8, 1.2)
	
	var attacker_won = atk_roll > def_roll
	var power_ratio = atk_roll / max(def_roll, 0.1)
	
	# 损失计算
	var attacker_loss_pct: float
	var defender_loss_pct: float
	
	if attacker_won:
		# 攻方获胜：攻方少损，守方重损
		# 大优势(>2x) → 攻方10%损，守方70%损
		# 小优势(1-1.5x) → 攻方30%损，守方40%损
		if power_ratio > 2.0:
			attacker_loss_pct = 0.10
			defender_loss_pct = 0.70
		elif power_ratio > 1.5:
			attacker_loss_pct = 0.15
			defender_loss_pct = 0.55
		else:
			attacker_loss_pct = 0.25
			defender_loss_pct = 0.45
	else:
		# 守方获胜
		var def_ratio = def_roll / max(atk_roll, 0.1)
		if def_ratio > 2.0:
			attacker_loss_pct = 0.70
			defender_loss_pct = 0.10
		elif def_ratio > 1.5:
			attacker_loss_pct = 0.55
			defender_loss_pct = 0.15
		else:
			attacker_loss_pct = 0.45
			defender_loss_pct = 0.25
	
	# 应用损失
	var atk_original = attacker.combat_power
	var def_original = defender.combat_power
	attacker.combat_power = max(0.0, attacker.combat_power * (1.0 - attacker_loss_pct))
	defender.combat_power = max(0.0, defender.combat_power * (1.0 - defender_loss_pct))
	
	# 兵力损失（领主/掠夺队有 party_size）
	attacker.party_size = maxi(0, attacker.party_size - int(attacker.party_size * attacker_loss_pct))
	defender.party_size = maxi(0, defender.party_size - int(defender.party_size * defender_loss_pct))
	
	# 战力归零 = 被消灭
	var atk_destroyed = attacker.combat_power < 1.0 or attacker.party_size <= 0
	var def_destroyed = defender.combat_power < 1.0 or defender.party_size <= 0
	
	var desc = "%s vs %s → %s胜 (攻方战力%.0f→%.0f, 守方战力%.0f→%.0f)" % [
		attacker.entity_name, defender.entity_name,
		"攻" if attacker_won else "守",
		atk_original, attacker.combat_power,
		def_original, defender.combat_power
	]
	
	return {
		"attacker_won": attacker_won,
		"attacker_destroyed": atk_destroyed,
		"defender_destroyed": def_destroyed,
		"attacker_losses": attacker_loss_pct,
		"defender_losses": defender_loss_pct,
		"description": desc,
	}

## 结算围攻战斗（攻击方 vs POI守军）
## 返回: { "attacker_won": bool, "description": String }
static func resolve_siege(attacker: OverworldEntity, target: OverworldPOI) -> Dictionary:
	var atk_power = attacker.combat_power
	var def_power = target.get_defense_power()
	
	var atk_roll = atk_power * randf_range(0.8, 1.2)
	var def_roll = def_power * randf_range(0.8, 1.2)
	
	var attacker_won = atk_roll > def_roll
	
	if attacker_won:
		# 攻方获胜：守军重创
		var garrison_loss = mini(target.garrison_current, int(target.garrison_current * 0.6))
		target.garrison_current = maxi(0, target.garrison_current - garrison_loss)
		attacker.combat_power *= 0.7  # 攻方也损失30%
		attacker.party_size = maxi(0, attacker.party_size - int(attacker.party_size * 0.3))
		# 繁荣度下降
		target.prosperity = maxi(0, target.prosperity - 20)
	else:
		# 守方获胜：攻方重创
		attacker.combat_power *= 0.4
		attacker.party_size = maxi(0, attacker.party_size - int(attacker.party_size * 0.6))
		var garrison_loss = mini(target.garrison_current, int(target.garrison_current * 0.2))
		target.garrison_current = maxi(0, target.garrison_current - garrison_loss)
	
	var desc = "围攻 %s: %s → %s (攻方战力%.0f, 守方防御%.0f)" % [
		target.poi_name,
		attacker.entity_name,
		"攻方胜" if attacker_won else "守方胜",
		atk_power, def_power
	]
	
	return {
		"attacker_won": attacker_won,
		"attacker_destroyed": attacker.combat_power < 1.0 or attacker.party_size <= 0,
		"description": desc,
	}

## 结算掠夺队袭击村庄
static func resolve_raid(attacker: OverworldEntity, village: OverworldPOI) -> Dictionary:
	var village_defense = village.get_defense_power()
	var atk_roll = attacker.combat_power * randf_range(0.8, 1.2)
	var def_roll = village_defense * randf_range(0.8, 1.2)
	
	var raider_won = atk_roll > def_roll
	
	if raider_won:
		var damage = 10 + randi() % 20
		village.prosperity = maxi(0, village.prosperity - damage)
		attacker.loot_carried += 15 + randi() % 30
		attacker.combat_power *= 0.9  # 小损失
	else:
		attacker.combat_power *= 0.6  # 村民抵抗，掠夺队损失大
	
	return {
		"raider_won": raider_won,
		"raider_destroyed": attacker.combat_power < 1.0,
		"prosperity_damage": maxi(0, village.prosperity) if raider_won else 0,
		"description": "%s 袭击 %s → %s" % [
			attacker.entity_name, village.poi_name,
			"掠夺成功" if raider_won else "被击退"
		],
	}
