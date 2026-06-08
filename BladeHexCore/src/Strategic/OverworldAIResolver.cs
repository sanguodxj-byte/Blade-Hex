using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// AI间战斗自动结算引擎 —— 非玩家势力之间的战斗由系统自动结算
/// </summary>
public static class OverworldAIResolver
{
    private static readonly Random _random = new();

    /// <summary>结算一场AI间的战斗</summary>
    public static Godot.Collections.Dictionary ResolveBattle(OverworldEntity attacker, OverworldEntity defender, float? attackerArmyPower = null, float? defenderArmyPower = null)
    {
        float atkPower = attackerArmyPower ?? attacker.CombatPower;
        float defPower = defenderArmyPower ?? defender.CombatPower;

        // 围攻加成：防守方有城墙优势
        if (attacker.CurrentAIState == OverworldEntity.AIState.Besieging)
        {
            defPower *= 1.5f;
            if (attacker.SiegeTarget != null)
                defPower += attacker.SiegeTarget.GetDefensePower() * 0.3f;
        }

        // 随机因素 (0.8 - 1.2)
        float atkRoll = atkPower * (0.8f + (float)_random.NextDouble() * 0.4f);
        float defRoll = defPower * (0.8f + (float)_random.NextDouble() * 0.4f);

        bool attackerWon = atkRoll > defRoll;
        float powerRatio = atkRoll / Math.Max(defRoll, 0.1f);

        float attackerLossPct;
        float defenderLossPct;

        if (attackerWon)
        {
            if (powerRatio > 2.0f) { attackerLossPct = 0.10f; defenderLossPct = 0.70f; }
            else if (powerRatio > 1.5f) { attackerLossPct = 0.15f; defenderLossPct = 0.55f; }
            else { attackerLossPct = 0.25f; defenderLossPct = 0.45f; }
        }
        else
        {
            float defRatio = defRoll / Math.Max(atkRoll, 0.1f);
            if (defRatio > 2.0f) { attackerLossPct = 0.70f; defenderLossPct = 0.10f; }
            else if (defRatio > 1.5f) { attackerLossPct = 0.55f; defenderLossPct = 0.15f; }
            else { attackerLossPct = 0.45f; defenderLossPct = 0.25f; }
        }

        // 应用损失
        float atkOriginal = attacker.CombatPower;
        float defOriginal = defender.CombatPower;

        attacker.CombatPower = Math.Max(0.0f, attacker.CombatPower * (1.0f - attackerLossPct));
        defender.CombatPower = Math.Max(0.0f, defender.CombatPower * (1.0f - defenderLossPct));

        attacker.PartySize = Math.Max(0, attacker.PartySize - (int)(attacker.PartySize * attackerLossPct));
        defender.PartySize = Math.Max(0, defender.PartySize - (int)(defender.PartySize * defenderLossPct));

        bool atkDestroyed = attacker.CombatPower < 1.0f || attacker.PartySize <= 0;
        bool defDestroyed = defender.CombatPower < 1.0f || defender.PartySize <= 0;

        string desc = $"{attacker.EntityName} vs {defender.EntityName} → {(attackerWon ? "攻" : "守")}胜 (攻方战力{atkOriginal:F0}→{attacker.CombatPower:F0}, 守方战力{defOriginal:F0}→{defender.CombatPower:F0})";

        return new Godot.Collections.Dictionary
        {
            { "attacker_won", attackerWon },
            { "attacker_destroyed", atkDestroyed },
            { "defender_destroyed", defDestroyed },
            { "attacker_losses", attackerLossPct },
            { "defender_losses", defenderLossPct },
            { "description", desc }
        };
    }

    /// <summary>结算围攻战斗（攻击方 vs POI守军）</summary>
    public static Godot.Collections.Dictionary ResolveSiege(OverworldEntity attacker, OverworldPOI target, float? armyTotalPower = null)
    {
        float atkPower = armyTotalPower ?? attacker.CombatPower;
        float defPower = target.GetDefensePower();

        float atkRoll = atkPower * (0.8f + (float)_random.NextDouble() * 0.4f);
        float defRoll = defPower * (0.8f + (float)_random.NextDouble() * 0.4f);

        bool attackerWon = atkRoll > defRoll;

        if (attackerWon)
        {
            int garrisonLoss = Math.Min(target.GarrisonCurrent, (int)(target.GarrisonCurrent * 0.6f));
            target.GarrisonCurrent = Math.Max(0, target.GarrisonCurrent - garrisonLoss);
            attacker.CombatPower *= 0.7f;
            attacker.PartySize = Math.Max(0, attacker.PartySize - (int)(attacker.PartySize * 0.3f));
            target.Prosperity = Math.Max(0, target.Prosperity - 20);
        }
        else
        {
            attacker.CombatPower *= 0.4f;
            attacker.PartySize = Math.Max(0, attacker.PartySize - (int)(attacker.PartySize * 0.6f));
            int garrisonLoss = Math.Min(target.GarrisonCurrent, (int)(target.GarrisonCurrent * 0.2f));
            target.GarrisonCurrent = Math.Max(0, target.GarrisonCurrent - garrisonLoss);
        }

        string desc = $"围攻 {target.PoiName}: {attacker.EntityName} → {(attackerWon ? "攻方胜" : "守方胜")} (攻方战力{atkPower:F0}, 守方防御{defPower:F0})";

        return new Godot.Collections.Dictionary
        {
            { "attacker_won", attackerWon },
            { "attacker_destroyed", attacker.CombatPower < 1.0f || attacker.PartySize <= 0 },
            { "description", desc }
        };
    }

    /// <summary>结算掠夺队袭击村庄</summary>
    public static Godot.Collections.Dictionary ResolveRaid(OverworldEntity attacker, OverworldPOI village)
    {
        float villageDefense = village.GetDefensePower();
        float atkRoll = attacker.CombatPower * (0.8f + (float)_random.NextDouble() * 0.4f);
        float defRoll = villageDefense * (0.8f + (float)_random.NextDouble() * 0.4f);

        bool raiderWon = atkRoll > defRoll;

        if (raiderWon)
        {
            int damage = 10 + _random.Next(20);
            village.Prosperity = Math.Max(0, village.Prosperity - damage);
            attacker.LootCarried += 15 + _random.Next(30);
            attacker.CombatPower *= 0.9f;
        }
        else
        {
            attacker.CombatPower *= 0.6f;
        }

        return new Godot.Collections.Dictionary
        {
            { "raider_won", raiderWon },
            { "raider_destroyed", attacker.CombatPower < 1.0f },
            { "prosperity_damage", raiderWon ? Math.Max(0, village.Prosperity) : 0 },
            { "description", $"{attacker.EntityName} 袭击 {village.PoiName} → {(raiderWon ? "掠夺成功" : "被击退")}" }
        };
    }
}
