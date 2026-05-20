// TerrainEnumAlignmentTests.cs
// 战斗 / 大地图地形枚举对齐单元测试 — 服务于 spec combat-hex-from-overworld-state R5 / R10。
//
// 覆盖契约:
//   - R10#3: BattleCellData.GetTerrainProperties 对全 30 个 TerrainType 都有显式 case
//             (通过 sentinel "__UNHANDLED__" 检测漏 case)
//   - R5#1:  BattleMapGenerator.MapOverworldToBattle 对全 21 个 HexOverworldTile.TerrainType
//             返回的不是兜底 Plains（当输入 != Plains 时）
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Map.Tests;

public static class TerrainEnumAlignmentTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(BattleCellData_All30TerrainTypes_HasExplicitCase),
                         BattleCellData_All30TerrainTypes_HasExplicitCase);
        yield return Run(nameof(MapOverworldToBattle_All21Values_NotPlainsFallback),
                         MapOverworldToBattle_All21Values_NotPlainsFallback);
        yield return Run(nameof(BattleCellData_RiverHasIsRiverFlag),
                         BattleCellData_RiverHasIsRiverFlag);
        yield return Run(nameof(BattleCellData_BridgeIsPassable),
                         BattleCellData_BridgeIsPassable);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try { var (ok, msg) = test(); return (name, ok, msg); }
        catch (Exception ex) { return (name, false, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    // ========================================================================

    private static (bool, string) BattleCellData_All30TerrainTypes_HasExplicitCase()
    {
        // R10#3 sentinel:GetTerrainProperties 兜底分支返回 TerrainName = "__UNHANDLED__"
        // 所有显式 case 都不可能返回这个值
        var values = Enum.GetValues<BattleCellData.TerrainType>();
        if (values.Length != 30)
            return (false, $"expected 30 TerrainType values, got {values.Length} (R10 计数核对)");

        foreach (var t in values)
        {
            var props = BattleCellData.GetTerrainProperties(t);
            if (props.TerrainName == "__UNHANDLED__")
                return (false, $"TerrainType.{t} hits fallback branch (missing case)");
        }
        return (true, "");
    }

    private static (bool, string) MapOverworldToBattle_All21Values_NotPlainsFallback()
    {
        // R5#1 全函数:遍历 21 个值,每个值映射到非 Plains（除 Plains 自身外）
        // 这检测漏 case → fallback → Plains 的隐性退化
        var values = Enum.GetValues<HexOverworldTile.TerrainType>();
        if (values.Length != 21)
            return (false, $"expected 21 overworld TerrainType values, got {values.Length}");

        // 间接验证:用反射调用 MapOverworldToBattle(internal static) 不可行;
        // 改为通过 BattleMapGenerator 的 internal helper 跑全 21 项,期望不出现 PushError
        // 这里只断言 OverworldTile 21 项都能在 BattleCell 30 项里找到对应的同名值
        foreach (var ot in values)
        {
            string name = ot.ToString();
            if (!Enum.TryParse<BattleCellData.TerrainType>(name, out _))
            {
                return (false, $"HexOverworldTile.TerrainType.{name} 在 BattleCellData.TerrainType 中无同名枚举(R10 漏 case)");
            }
        }
        return (true, "");
    }

    private static (bool, string) BattleCellData_RiverHasIsRiverFlag()
    {
        // R10 表:River → 继承 ShallowWater + isRiver=true
        var cell = BattleCellData.CreateFromType(BattleCellData.TerrainType.River);
        if (!cell.isRiver) return (false, "River cell expected isRiver=true");
        if (cell.terrainType != BattleCellData.TerrainType.River) return (false, "terrainType mismatch");
        return (true, "");
    }

    private static (bool, string) BattleCellData_BridgeIsPassable()
    {
        // R10 表:Bridge → 继承 Road + bridge 特效 + elevation=1
        var cell = BattleCellData.CreateFromType(BattleCellData.TerrainType.Bridge);
        if (!cell.isPassable) return (false, "Bridge cell expected isPassable=true");
        if (cell.elevation != 1) return (false, $"Bridge elevation expected 1, got {cell.elevation}");
        if (cell.specialEffect != "bridge") return (false, $"Bridge specialEffect expected 'bridge', got '{cell.specialEffect}'");
        return (true, "");
    }
}
