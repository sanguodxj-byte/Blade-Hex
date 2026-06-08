// FerryRouteStage.cs
// 世界生成阶段 9：连接所有港口的渡船航线。
//
// 抽取自 WorldCreator.ConnectFerryRoutes。
// 算法：每个港口连接最近的 2 个其他港口（双向）。
using System.Linq;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 9：在所有 Port 类型 POI 之间建立渡船航线（每港口连接最近 2 个，双向）。
/// </summary>
public sealed class FerryRouteStage : IWorldStage
{
    public string Name => "建立港口航线";
    public float ProgressWeight => 1f;

    public void Execute(WorldBuildContext ctx)
    {
        var ports = ctx.Pois.Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Town && p.IsPortCity).ToList();
        if (ports.Count < 2)
        {
            GD.Print($"[FerryRouteStage] 0 条航线（港口数量 {ports.Count}）");
            return;
        }

        foreach (var port in ports)
        {
            var others = ports
                .Where(p => p != port)
                .OrderBy(p => port.Position.DistanceTo(p.Position))
                .Take(2)
                .ToList();

            foreach (var other in others)
            {
                if (!port.FerryDestinations.Contains(other.PoiName))
                    port.FerryDestinations.Add(other.PoiName);

                if (!other.FerryDestinations.Contains(port.PoiName))
                    other.FerryDestinations.Add(port.PoiName);
            }
        }

        GD.Print($"[FerryRouteStage] {ports.Count} 个港口已互联");
    }
}
