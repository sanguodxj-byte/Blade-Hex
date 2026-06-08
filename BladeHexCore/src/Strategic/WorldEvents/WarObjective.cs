using System;
using System.Collections.Generic;

namespace BladeHex.Strategic.WorldEvents;

/// <summary>
/// 战争攻势目标
/// </summary>
public class WarObjective
{
    public string AttackerNationId { get; set; } = "";
    public string DefenderNationId { get; set; } = "";
    public string TargetPoiId { get; set; } = ""; // 目标 POI 名称
    public int Priority { get; set; } = 1;        // 2 = High, 1 = Normal
    public List<string> AssignedLordIds { get; } = new(); // 已经分配前往攻击该目标的领主 EntityName 列表
}
