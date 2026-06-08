using System;
using Godot;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Diplomacy;

/// <summary>
/// 战果关系处理器 — 负责在战斗结束应用结果时调节势力外交关系
/// </summary>
public static class CombatResultProcessor
{
    /// <summary>
    /// 处理战败与交火造成的势力关系下调
    /// </summary>
    public static void ProcessCombatRelations(
        string attackerFaction, 
        string defenderFaction, 
        bool attackerWon, 
        WorldEventEngine engine)
    {
        if (string.IsNullOrEmpty(attackerFaction) || string.IsNullOrEmpty(defenderFaction) || attackerFaction == defenderFaction)
            return;

        if (attackerFaction == "hostile" || defenderFaction == "hostile")
            return;
        if (attackerFaction == "neutral" || defenderFaction == "neutral")
            return;

        int penalty = -10;
        engine.FactionRelations.AdjustRelation(attackerFaction, defenderFaction, penalty);
        
        GD.Print($"[CombatResultProcessor] {attackerFaction} 与 {defenderFaction} 发生战斗，关系下调 {penalty}，当前关系为: {engine.FactionRelations.GetRelation(attackerFaction, defenderFaction)}");
    }
}
