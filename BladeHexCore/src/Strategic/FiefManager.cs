// FiefManager.cs
// 封地管理器 — 管理玩家所有封地的建造、收入、防御
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 封地管理器 — 管理玩家拥有的所有封地
/// </summary>
public class FiefManager
{
    private readonly List<FiefData> _fiefs = new();
    private readonly ReputationTracker _reputation;

    public IReadOnlyList<FiefData> Fiefs => _fiefs;
    public int FiefCount => _fiefs.Count;

    public FiefManager(ReputationTracker reputation)
    {
        _reputation = reputation;
    }

    // ============================================================================
    // 封地获取
    // ============================================================================

    /// <summary>请求封地（需声望≥60）</summary>
    public FiefData? RequestFief(string factionId, string fiefName, Vector2I centerHex, Vector2 worldPos)
    {
        if (!_reputation.CanRequestFief(factionId))
        {
            GD.Print($"[Fief] 声望不足，无法获得 {factionId} 的封地");
            return null;
        }

        // 检查是否已有该势力的封地
        foreach (var f in _fiefs)
            if (f.OwningFaction == factionId)
            {
                GD.Print($"[Fief] 已拥有 {factionId} 的封地: {f.FiefName}");
                return null;
            }

        var fief = new FiefData
        {
            FiefId = $"fief_{factionId}_{centerHex.X}_{centerHex.Y}",
            FiefName = fiefName,
            OwningFaction = factionId,
            CenterHex = centerHex,
            WorldPosition = worldPos,
            Prosperity = 30,
            Population = 50,
            GarrisonCount = 0,
            GarrisonMax = 8,
            WallLevel = 0,
        };

        // 自动放置领主宅邸在中心格
        var manor = new FiefBuilding
        {
            Type = FiefBuilding.BuildingType.LordManor,
            HexIndex = 0,
            Level = 1,
            CurrentHp = 100,
        };
        fief.Buildings.Add(manor);

        _fiefs.Add(fief);
        GD.Print($"[Fief] 获得封地: {fiefName} (势力: {factionId})");
        return fief;
    }

    // ============================================================================
    // 建造
    // ============================================================================

    /// <summary>在封地建造建筑，返回花费金额（0=失败）</summary>
    public int Build(FiefData fief, FiefBuilding.BuildingType type, int hexIndex, int edgeDir, int availableGold)
    {
        // 类型检查
        if (!fief.CanBuild(type))
        {
            GD.Print("[Fief] 无法建造：格位已满");
            return 0;
        }

        // 位置冲突检查
        if (FiefBuilding.IsEdgeType(type))
        {
            if (fief.IsEdgeOccupied(hexIndex, edgeDir))
            {
                GD.Print("[Fief] 无法建造：该边缘已有建筑");
                return 0;
            }
        }
        else
        {
            if (fief.IsHexOccupied(hexIndex))
            {
                GD.Print("[Fief] 无法建造：该格位已被占用");
                return 0;
            }
        }

        var building = new FiefBuilding
        {
            Type = type,
            HexIndex = hexIndex,
            EdgeDirection = FiefBuilding.IsEdgeType(type) ? edgeDir : -1,
            Level = 1,
        };

        if (availableGold < building.BuildCost)
        {
            GD.Print($"[Fief] 金币不足：需要 {building.BuildCost}，拥有 {availableGold}");
            return 0;
        }

        building.StartConstruction();
        fief.AddBuilding(building);
        GD.Print($"[Fief] 开始建造 {building.GetDisplayName()}，花费 {building.BuildCost} 金，需要 {building.BuildDays} 天");
        return building.BuildCost;
    }

    // ============================================================================
    // 每日结算
    // ============================================================================

    /// <summary>每日结算所有封地（收入、建造进度、食物）</summary>
    public FiefDailyReport ProcessAllFiefs()
    {
        var totalReport = new FiefDailyReport();

        foreach (var fief in _fiefs)
        {
            // 推进建造
            foreach (var b in fief.Buildings)
            {
                if (b.AdvanceConstruction())
                    GD.Print($"[Fief] {fief.FiefName}: {b.GetDisplayName()} 建造完成！");
            }

            // 每日收入
            var report = fief.ProcessDay();
            totalReport.GoldEarned += report.GoldEarned;
            totalReport.FoodProduced += report.FoodProduced;
            totalReport.FoodConsumed += report.FoodConsumed;

            // 声望缓慢增长
            _reputation.AddReputation(fief.OwningFaction, 0); // 不增加，仅触发日志（未来可改为+0.1/天）
        }

        return totalReport;
    }

    // ============================================================================
    // 城防战
    // ============================================================================

    /// <summary>获取封地的城防战配置（建筑位置、驻军、防御塔数据）</summary>
    public FiefDefenseConfig GetDefenseConfig(FiefData fief)
    {
        var config = new FiefDefenseConfig
        {
            FiefName = fief.FiefName,
            GarrisonCount = fief.GarrisonCount,
            WallLevel = fief.WallLevel,
            DefenseRating = fief.DefenseRating,
        };

        foreach (var b in fief.Buildings)
        {
            if (b.IsUnderConstruction) continue;
            config.Buildings.Add(new FiefDefenseConfig.BuildingPlacement
            {
                Type = b.Type,
                HexIndex = b.HexIndex,
                EdgeDirection = b.EdgeDirection,
                Level = b.Level,
                Hp = b.CurrentHp > 0 ? b.CurrentHp : b.MaxHp,
                AutoDamage = b.AutoAttackDamage,
                AttackRange = b.AttackRange,
            });
        }

        return config;
    }

    // ============================================================================
    // 查询
    // ============================================================================

    public FiefData? GetFief(string fiefId)
    {
        foreach (var f in _fiefs)
            if (f.FiefId == fiefId) return f;
        return null;
    }

    public FiefData? GetFiefByFaction(string factionId)
    {
        foreach (var f in _fiefs)
            if (f.OwningFaction == factionId) return f;
        return null;
    }
}

/// <summary>城防战配置 — 传递给战斗系统</summary>
public class FiefDefenseConfig
{
    public string FiefName { get; set; } = "";
    public int GarrisonCount { get; set; }
    public int WallLevel { get; set; }
    public int DefenseRating { get; set; }
    public List<BuildingPlacement> Buildings { get; set; } = new();

    public class BuildingPlacement
    {
        public FiefBuilding.BuildingType Type { get; set; }
        public int HexIndex { get; set; }
        public int EdgeDirection { get; set; }
        public int Level { get; set; }
        public int Hp { get; set; }
        public int AutoDamage { get; set; }
        public int AttackRange { get; set; }
    }
}
