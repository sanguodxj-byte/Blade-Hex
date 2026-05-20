// POIFootprintApplier.cs
// 比例尺统一 — 给 POI 应用 footprint 的工具方法（世界生成 + 存档恢复共享）

using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

public static class POIFootprintApplier
{
    /// <summary>
    /// 根据 POI 类型查 preset，用 TryFit 给 POI 应用 footprint；失败时回退到 solo。
    /// 调用前必须设好 PoiTypeEnum / SettlementRaceValue / LairTypeValue / Position（pixel）。
    /// 调用后会写入 CenterHex / FootprintTemplateName / FootprintRotation / OccupiedHexes。
    /// </summary>
    /// <param name="usedHexes">已被占用的 hex 集合（其他 POI 的 footprint）。会更新此集合加入新 POI 占用</param>
    public static void Apply(
        OverworldPOI poi,
        System.Func<Vector2I, HexOverworldTile?> getTile,
        HashSet<Vector2I> usedHexes)
    {
        var centerHex = HexOverworldTile.PixelToAxial(poi.Position.X, poi.Position.Y);
        poi.CenterHex = centerHex;

        var preset = POIBattlePresetRegistry.Resolve(poi);
        var tpl = FootprintTemplateRegistry.Get(preset.FootprintTemplate);

        bool IsHexFree(Vector2I hex) => !usedHexes.Contains(hex) || hex == centerHex;

        var fit = tpl.TryFit(centerHex, getTile, IsHexFree);
        if (fit == null)
        {
            // 回退到 solo
            poi.FootprintTemplateName = "solo";
            poi.FootprintRotation = 0;
            poi.OccupiedHexes = [centerHex];
            usedHexes.Add(centerHex);
            // 写入 tile 标记
            var centerTile = getTile(centerHex);
            if (centerTile != null) { centerTile.PoiId = poi.PoiName; centerTile.IsPoiCenter = true; }
            return;
        }

        poi.FootprintTemplateName = preset.FootprintTemplate;
        poi.FootprintRotation = fit.Value.Rotation;
        poi.OccupiedHexes = fit.Value.OccupiedHexes;

        foreach (var h in fit.Value.OccupiedHexes)
        {
            usedHexes.Add(h);
            var t = getTile(h);
            if (t != null)
            {
                t.PoiId = poi.PoiName;
                t.IsPoiCenter = (h == centerHex);
            }
        }
    }
}
