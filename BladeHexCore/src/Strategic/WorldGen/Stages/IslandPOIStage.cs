// IslandPOIStage.cs
// 世界生成阶段 8：在每个海岛上放置港口或特殊探索 POI。
//
// 抽取自 WorldCreator.PlaceIslandPOIs + BuildIslandPort + BuildIslandSpecialPOI。
// 输入依赖：ctx.IslandCenters（来自 IslandStage）。
// RNG：seed ^ 0x49504F49 ("IPOI")。
using System;
using BladeHex.Map;
using Godot;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 8：在 IslandStage 生成的每个海岛上放置一个 POI。
/// 第一个海岛强制港口，之后每 3 个一个港口，其余为特殊探索点。
/// </summary>
public sealed class IslandPOIStage : IWorldStage
{
    public string Name => "放置海岛 POI";
    public float ProgressWeight => 3f;

    public void Execute(WorldBuildContext ctx)
    {
        if (ctx.IslandCenters.Count == 0)
        {
            GD.Print("[IslandPOIStage] 0 个海岛 POI（无海岛）");
            return;
        }

        var rng = ctx.NewRng(0x49504F49); // "IPOI"
        int placed = 0;
        bool hasPort = false;

        for (int i = 0; i < ctx.IslandCenters.Count; i++)
        {
            var center = ctx.IslandCenters[i];

            var chunkCoord = ChunkData.WorldToChunk(center.X, center.Y);
            if (!ctx.Chunks.TryGetValue(chunkCoord, out var chunk)) continue;
            var tile = chunk.GetTile(center.X, center.Y);
            if (tile == null || !tile.IsPassable) continue;

            var poi = new OverworldPOI();
            poi.Position = HexOverworldTile.AxialToPixel(center.X, center.Y);
            poi.OwningFaction = "";

            if (!hasPort || (i > 0 && i % 3 == 0))
            {
                BuildIslandPort(poi, rng);
                hasPort = true;
            }
            else
            {
                BuildIslandSpecialPOI(poi, rng);
            }

            ctx.Pois.Add(poi);
            placed++;
        }

        GD.Print($"[IslandPOIStage] {placed} 个海岛 POI");
    }

    private static void BuildIslandPort(OverworldPOI poi, Random rng)
    {
        string[] portNames = ["走私者港湾", "自由港", "海风码头", "潮汐锚地", "漂流者避风港"];
        poi.PoiName = portNames[rng.Next(portNames.Length)];
        poi.PoiTypeEnum = OverworldPOI.POIType.Port;
        poi.HasTavern = true;
        poi.HasShop = true;
        poi.FerryCost = 30 + rng.Next(40);
        poi.GarrisonMax = 25;
        poi.GarrisonCurrent = 20 + rng.Next(5);
        poi.Prosperity = 25 + rng.Next(35);
    }

    private static void BuildIslandSpecialPOI(OverworldPOI poi, Random rng)
    {
        // 加权随机：海盗(25) / 沉船(20) / 隐士(15) / 海兽(15) / 灯塔(10) / 流放者(10) / 宝藏(5)
        int roll = rng.Next(100);

        if (roll < 25)
        {
            string[] names = ["黑帆海盗窝", "血骷髅洞穴", "暴风海寇巢", "深渊劫掠者"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.PirateCove;
            poi.LairLevel = 2 + rng.Next(3);
            poi.ThreatLevel = 0.6f + (float)rng.NextDouble() * 0.3f;
        }
        else if (roll < 45)
        {
            string[] names = ["沉没的商船", "幽灵帆船残骸", "远古战舰遗骨", "深海宝藏船"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.Ruins;
            poi.LairLevel = 1 + rng.Next(3);
            poi.ThreatLevel = 0.4f + (float)rng.NextDouble() * 0.3f;
        }
        else if (roll < 60)
        {
            string[] names = ["海神祭坛", "隐士灯塔", "潮汐圣所", "珊瑚神殿"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Shrine;
            poi.Prosperity = 20;
            poi.GarrisonMax = 0;
            poi.GarrisonCurrent = 0;
        }
        else if (roll < 75)
        {
            string[] names = ["海蛇巢穴", "巨蟹洞窟", "深渊利维坦巢", "海妖礁石"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.DragonLair;
            poi.LairLevel = 3 + rng.Next(3);
            poi.ThreatLevel = 0.8f + (float)rng.NextDouble() * 0.2f;
        }
        else if (roll < 85)
        {
            string[] names = ["远古灯塔", "星辰观测台", "风暴信标塔", "迷雾引路灯"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.Ruins;
            poi.LairLevel = 2 + rng.Next(2);
            poi.ThreatLevel = 0.3f + (float)rng.NextDouble() * 0.3f;
        }
        else if (roll < 95)
        {
            string[] names = ["流放者营地", "海难幸存者", "逃亡者避难所", "无法之地"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Tavern;
            poi.HasTavern = true;
            poi.GarrisonMax = 12;
            poi.GarrisonCurrent = 8 + rng.Next(4);
            poi.Prosperity = 15 + rng.Next(20);
        }
        else
        {
            string[] names = ["藏宝洞穴", "海盗埋骨地", "黄金沙滩", "失落的宝库"];
            poi.PoiName = names[rng.Next(names.Length)];
            poi.PoiTypeEnum = OverworldPOI.POIType.Lair;
            poi.LairTypeValue = OverworldPOI.LairType.AncientTomb;
            poi.LairLevel = 1 + rng.Next(2);
            poi.ThreatLevel = 0.2f + (float)rng.NextDouble() * 0.2f;
        }
    }
}
