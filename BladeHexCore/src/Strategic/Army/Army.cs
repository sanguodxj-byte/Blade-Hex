using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.Army;

public enum ArmyState
{
    Forming,    // 集结中
    Marching,   // 行军中
    Besieging,  // 围攻中
    Disbanding  // 解散中
}

public class Army
{
    public string ArmyId { get; set; } = "";
    public string Faction { get; set; } = "";
    public OverworldEntity? Marshal { get; set; }
    public List<OverworldEntity> Members { get; set; } = new();
    public string TargetPoiName { get; set; } = "";
    public ArmyState State { get; set; } = ArmyState.Forming;
    public int FormedDay { get; set; }
    public Vector2 RallyPoint { get; set; } = Vector2.Zero;

    public float AggregateCombatPower
        => Members.Where(m => m.IsAlive).Sum(m => m.CombatPower);

    public int AggregateGarrisonSize
        => Members.Where(m => m.IsAlive).Sum(m => m.GarrisonSize);

    public int LivingMemberCount
        => Members.Count(m => m.IsAlive);
}
