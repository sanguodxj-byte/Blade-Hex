// DamageResolutionParityTest.cs — T-105 / T-303
// 伤害解算 Parity 测试夹具
// 职责：
//   1. 以固定种子生成 100 组 (attacker, defender, weapon, damage) 输入
//   2. 运行 BattleUnitModel.ApplyDamage 捕获 HP 序列 → 写入 fixture JSON (Baseline)
//   3. 通过 AssertApplyDamageMatches 验证当前 ApplyDamage 实现与 baseline 一致
//
// 使用方式：
//   - Wave 1 / Wave 3: 调用 RunAll() 自动走 baseline → assert 流程
//   - 单独 BuildBaseline(): 手动刷新 fixture
//
// 4 条伤害路径统一说明 (T-303)：
//   ┌─ CombatResolver        → defender.Model.ApplyDamage() — 武器穿透路径
//   ├─ SkillEffectExecutor   → Unit.TakeDamage(dmg) → Model.ApplyDamage(source:Skill)
//   ├─ ConsumableManager     → Unit.TakeDamage(dmg) → Model.ApplyDamage(source:Item)
//   └─ EnvironmentEventSystem → Unit.TakeDamage(dmg) → Model.ApplyDamage(source:Environment)
// 所有路径最终汇集于 BattleUnitModel.ApplyDamage（Core 层单一真相源）。
// 本测试直接调用 ApplyDamage（消除 call chain 差异），验证结果与 baseline 一致。
//
// 不依赖任何测试框架 —— 可从 Godot 场景或控制台 harness 直接调用
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BladeHex.Data;

namespace BladeHex.Combat.Tests;

public static class DamageResolutionParityTest
{
    private const int SeedValue = 42;
    private const int SampleCount = 100;
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tests", "fixtures", "damage_parity.json");

    /// <summary>
    /// 单组样本输入 —— 可复现地构造 attacker/defender/weapon
    /// </summary>
    public readonly record struct ParitySample(
        int Seed,
        int AttackerStr, int AttackerDex,
        int DefenderCon, int DefenderBaseHp,
        int DamageAmount,
        WeaponData.DamageType DamageType,
        int ArmorDrThreshold, int ArmorCurrentPoints,
        int NaturalRoll,
        WeaponData.WeaponSubtype WeaponSubtype);

    /// <summary>
    /// 单组样本的 ApplyDamage 结果 —— 作为 fixture 的一行
    /// </summary>
    public sealed class ParityRecord
    {
        public int Seed { get; set; }
        public int HpBefore { get; set; }
        public int HpAfter { get; set; }
        public int HpDamage { get; set; }
        public int DrDamage { get; set; }
        public bool IsPenetrated { get; set; }
        public bool ArmorBroken { get; set; }
        public bool Killed { get; set; }
    }

    // ========================================================================
    // 构造样本 —— 固定种子，100 组
    // ========================================================================

    public static List<ParitySample> GenerateSamples()
    {
        var rng = new Random(SeedValue);
        var samples = new List<ParitySample>(SampleCount);
        var damageTypes = new[] {
            WeaponData.DamageType.Slash,
            WeaponData.DamageType.Pierce,
            WeaponData.DamageType.Crush,
        };
        var subtypes = new[] {
            WeaponData.WeaponSubtype.Unarmed,
            WeaponData.WeaponSubtype.ArmingSword,
            WeaponData.WeaponSubtype.InfantrySpear,
            WeaponData.WeaponSubtype.BattleAxe,
        };
        for (int i = 0; i < SampleCount; i++)
        {
            samples.Add(new ParitySample(
                Seed: i,
                AttackerStr: 10 + rng.Next(0, 10),
                AttackerDex: 10 + rng.Next(0, 10),
                DefenderCon: 10 + rng.Next(0, 10),
                DefenderBaseHp: 20 + rng.Next(0, 30),
                DamageAmount: 1 + rng.Next(0, 40),
                DamageType: damageTypes[rng.Next(damageTypes.Length)],
                ArmorDrThreshold: rng.Next(0, 20),        // 0 = 裸甲分支
                ArmorCurrentPoints: rng.Next(10, 200),
                NaturalRoll: 1 + rng.Next(0, 20),
                WeaponSubtype: subtypes[rng.Next(subtypes.Length)]
            ));
        }
        return samples;
    }

    // ========================================================================
    // 核心执行 —— 构造 BattleUnitModel 并跑 ApplyDamage
    // ========================================================================

    private static ParityRecord RunSample(ParitySample s)
    {
        // 构造 defender UnitData（最小化）
        var defenderData = new UnitData
        {
            UnitName = $"Defender_{s.Seed}",
            Str = 10,
            Dex = 10,
            Con = s.DefenderCon,
            BaseMaxHp = s.DefenderBaseHp,
        };

        // 可选装备 Armor
        if (s.ArmorDrThreshold > 0)
        {
            defenderData.Armor = new ArmorData
            {
                DrThreshold = s.ArmorDrThreshold,
                CurrentArmorPoints = s.ArmorCurrentPoints,
                MaxArmorPoints = s.ArmorCurrentPoints,
            };
        }

        var model = new BattleUnitModel(defenderData);
        model.CurrentHp = s.DefenderBaseHp;

        // 执行伤害（不走 mastery，保留可重现）
        int hpBefore = model.CurrentHp;
        var result = model.ApplyDamage(
            source: DamageSource.WeaponAttack,
            amount: s.DamageAmount,
            damageType: s.DamageType,
            naturalRoll: s.NaturalRoll,
            attackerMastery: null,
            weaponSubtype: s.WeaponSubtype
        );

        return new ParityRecord
        {
            Seed = s.Seed,
            HpBefore = hpBefore,
            HpAfter = result.RemainingHp,
            HpDamage = result.HpDamage,
            DrDamage = result.DrDamage,
            IsPenetrated = result.IsPenetrated,
            ArmorBroken = result.ArmorBroken,
            Killed = result.KilledUnit,
        };
    }

    // ========================================================================
    // Baseline 生成 —— Wave 1 阶段调用（当前实现 = 期望实现）
    // ========================================================================

    public static string BuildBaseline(string? outPath = null)
    {
        var path = outPath ?? FixturePath;
        var records = new List<ParityRecord>(SampleCount);
        foreach (var sample in GenerateSamples())
            records.Add(RunSample(sample));

        var json = JsonSerializer.Serialize(records, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    // ========================================================================
    // 运行所有 —— Wave 3 T-303 入口
    // ========================================================================

    /// <summary>
    /// 完整流程：baseline 不存在则生成 → 对比 → 返回结果
    /// </summary>
    public static (bool pass, List<string> diffs, int samplesTested) RunAll(string? fixturePath = null)
    {
        var path = fixturePath ?? FixturePath;

        // 如果 fixture 不存在，先构建 baseline
        if (!File.Exists(path))
        {
            var written = BuildBaseline(path);
            Console.WriteLine($"[DamageResolutionParityTest] Baseline written: {written}");
        }

        var (pass, diffs) = AssertApplyDamageMatches(path);
        return (pass, diffs, SampleCount);
    }

    // ========================================================================
    // 等价性断言 —— T-303 激活
    // ========================================================================

    /// <summary>
    /// 加载 baseline fixture 并对当前 ApplyDamage 实现做逐样本比对
    /// </summary>
    /// <returns>(pass, diffDetails)</returns>
    public static (bool pass, List<string> diffs) AssertApplyDamageMatches(string? fixturePath = null)
    {
        var path = fixturePath ?? FixturePath;
        var diffs = new List<string>();

        if (!File.Exists(path))
        {
            diffs.Add($"Fixture not found: {path} — run BuildBaseline first");
            return (false, diffs);
        }

        var expectedJson = File.ReadAllText(path);
        var expected = JsonSerializer.Deserialize<List<ParityRecord>>(expectedJson);
        if (expected == null || expected.Count != SampleCount)
        {
            diffs.Add($"Fixture malformed or sample count mismatch: expected {SampleCount} got {expected?.Count ?? 0}");
            return (false, diffs);
        }

        var samples = GenerateSamples();
        for (int i = 0; i < SampleCount; i++)
        {
            var actual = RunSample(samples[i]);
            var want = expected[i];
            if (actual.HpAfter != want.HpAfter ||
                actual.HpDamage != want.HpDamage ||
                actual.DrDamage != want.DrDamage ||
                actual.IsPenetrated != want.IsPenetrated ||
                actual.ArmorBroken != want.ArmorBroken ||
                actual.Killed != want.Killed)
            {
                diffs.Add($"Sample[{i}] seed={want.Seed}: " +
                          $"expected hp={want.HpAfter} hpDmg={want.HpDamage} drDmg={want.DrDamage} pen={want.IsPenetrated} broken={want.ArmorBroken} killed={want.Killed}; " +
                          $"actual hp={actual.HpAfter} hpDmg={actual.HpDamage} drDmg={actual.DrDamage} pen={actual.IsPenetrated} broken={actual.ArmorBroken} killed={actual.Killed}");
            }
        }
        return (diffs.Count == 0, diffs);
    }
}
