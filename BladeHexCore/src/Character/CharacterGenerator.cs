// CharacterGenerator.cs
// 角色生成 — 基于点数分配的属性系统
// 所有生物统一：1级属性25，每级+1
// 对应策划05-角色与职业.md
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 角色生成器 — 纯静态工具类
/// </summary>
public static class CharacterGenerator
{
    // ========================================================================
    // 角色生成
    // ========================================================================

    /// <summary>生成角色</summary>
    public static UnitData GenerateCharacter(RaceData? race = null, int level = 1, long seedVal = -1)
    {
        if (seedVal >= 0)
            GD.Seed((ulong)seedVal);

        race ??= RaceData.GetAllRaces()[GD.Randi() % (uint)RaceData.GetAllRaces().Length];
        var baseAttrs = AllocateAttrs(level, race);
        var traits = RollTraits();
        baseAttrs = ApplyTraitModifiers(baseAttrs, traits);

        var unitData = new UnitData();
        unitData.Level = Mathf.Max(1, level);
        unitData.UnitName = NameGenerator.GenerateFullName(race.raceId, unitData.Level);
        unitData.Xp = RPGRuleEngine.GetXpForLevel(level);
        unitData.Race = race;
        unitData.CharacterTraits = new Godot.Collections.Array<TraitData>(traits);
        unitData.UnspentAttrPoints = 0;

        unitData.Str = Mathf.Max(1, baseAttrs["str"]);
        unitData.Dex = Mathf.Max(1, baseAttrs["dex"]);
        unitData.Con = Mathf.Max(1, baseAttrs["con"]);
        unitData.Intel = Mathf.Max(1, baseAttrs["intel"]);
        unitData.Wis = Mathf.Max(1, baseAttrs["wis"]);
        unitData.Cha = Mathf.Max(1, baseAttrs["cha"]);

        // v0.6: BaseMaxHp 固定为 10，HP 增长来自 CON_HP_Bonus × Level（见 RPGRuleEngine.CalculateMaxHp）
        unitData.BaseMaxHp = 10;
        if (race != null && Array.IndexOf(race.RacialTraits, "dwarven_resilience") >= 0)
            unitData.BaseMaxHp += level; // 矮人种族特性：每级额外 HP

        unitData.BaseAc = 8;
        unitData.BaseMoveRange = 4;
        unitData.BaseInitiative = 0;

        if (race != null && Array.IndexOf(race.RacialTraits, "threat_instinct") >= 0)
            unitData.BaseInitiative += 2;

        // v0.6 10.0 MaxMana = 10 + INT + floor(Level/2)
        unitData.CurrentMana = BladeHex.Combat.CombatStats.GetMaxMana(unitData);
        unitData.CastingAbility = "intel";
        unitData.SkillPoints = 5 + (level - 1);
        unitData.Runtime.Loyalty = 50;

        ApplyFunctionalTraits(unitData, traits);
        EquipStartingGear(unitData);
        return unitData;
    }

    // ========================================================================
    // 初始装备
    // ========================================================================

    /// <summary>
    /// 为角色装备初始装备：布衣 + 鞋子 + 随机1阶主武器，品质随机。
    /// 所有人形角色（玩家、同伴）调用。
    /// </summary>
    public static void EquipStartingGear(UnitData unit)
    {
        // 布衣（轻甲，DR阈值3）
        if (unit.Armor == null)
        {
            var cloth = new ArmorData();
            cloth.ItemId = "starter_cloth_armor";
            cloth.ItemName = "旅行布衣";
            cloth.armorType = ArmorData.ArmorType.Light;
            cloth.AcBonus = 1;
            cloth.MaxDexBonus = 99;
            cloth.DrThreshold = 3;
            cloth.EquipSlotTarget = ItemData.EquipSlot.Costume;
            cloth.ItemRarity = RollStartingRarity();
            cloth.InitializeArmorPoints();
            unit.Armor = cloth;
        }

        // 鞋子
        if (unit.Boots == null)
        {
            var boots = new ArmorData();
            boots.ItemId = "starter_boots";
            boots.ItemName = "皮靴";
            boots.armorType = ArmorData.ArmorType.Light;
            boots.AcBonus = 0;
            boots.MaxDexBonus = 99;
            boots.DrThreshold = 1;
            boots.EquipSlotTarget = ItemData.EquipSlot.Feet;
            boots.ItemRarity = RollStartingRarity();
            boots.InitializeArmorPoints();
            unit.Boots = boots;
        }

        // 随机1阶主武器（从轻型近战武器中随机选一把）
        if (unit.PrimaryMainHand == null)
        {
            var tier1Subtypes = new WeaponData.WeaponSubtype[]
            {
                // Slash Light
                WeaponData.WeaponSubtype.Dagger,
                WeaponData.WeaponSubtype.Seax,
                WeaponData.WeaponSubtype.Kukri,
                // Pierce Light
                WeaponData.WeaponSubtype.Stiletto,
                WeaponData.WeaponSubtype.SpikedDagger,
                WeaponData.WeaponSubtype.Rapier,
                // Crush Light
                WeaponData.WeaponSubtype.Club,
                WeaponData.WeaponSubtype.LightHammer,
                WeaponData.WeaponSubtype.Cestus,
            };

            var subtype = tier1Subtypes[GD.Randi() % (uint)tier1Subtypes.Length];
            var config = WeaponRegistry.GetConfig(subtype);

            var weapon = new WeaponData();
            weapon.ItemId = $"starter_{subtype.ToString().ToLower()}";
            weapon.ItemName = config.Name;
            weapon.Subtype = subtype;
            weapon.Tier = 1;
            weapon.DamageDiceCount = config.DiceCount;
            weapon.DamageDiceSides = config.DiceSides;
            weapon.WeaponDamageType = config.DamageType;
            weapon.ApCost = config.BaseApCost;
            weapon.RangeCells = config.Range;
            weapon.Class = config.Range > 1 ? WeaponData.WeaponClass.Ranged : WeaponData.WeaponClass.Melee;
            weapon.Weight = config.BaseApCost <= 3 ? WeaponData.WeightCategory.Light : WeaponData.WeightCategory.Medium;
            weapon.IsFinesse = subtype == WeaponData.WeaponSubtype.Dagger
                || subtype == WeaponData.WeaponSubtype.Rapier
                || subtype == WeaponData.WeaponSubtype.Kukri;
            weapon.IsDualWieldable = weapon.Weight == WeaponData.WeightCategory.Light;
            weapon.ItemRarity = RollStartingRarity();
            unit.PrimaryMainHand = weapon;
        }
    }

    /// <summary>随机初始装备品质：70%普通, 20%优秀, 8%稀有, 2%史诗</summary>
    private static ItemData.Rarity RollStartingRarity()
    {
        float roll = GD.Randf();
        if (roll < 0.70f) return ItemData.Rarity.Common;
        if (roll < 0.90f) return ItemData.Rarity.Uncommon;
        if (roll < 0.98f) return ItemData.Rarity.Rare;
        return ItemData.Rarity.Epic;
    }

    /// <summary>生成随机敌方单位</summary>
    public static UnitData GenerateRandomEnemy(float cr, UnitData.EnemyType enemyType,
        UnitData.AIStrategy strategy = UnitData.AIStrategy.Instinct)
    {
        var unitData = new UnitData();
        unitData.IsEnemy = true;
        unitData.enemyType = enemyType;
        unitData.ThreatLevel = cr;
        unitData.aiStrategy = strategy;

        // CR -> level: invariant with GetCrFromLevel(level) = floor(level/6).
        // Use the inverse so passing GetCrFromLevel(120)=20 reconstructs level=120.
        unitData.Level = Mathf.Max(1, RPGRuleEngine.GetLevelFromCr(cr));

        // Pick a humanoid race for naming purposes (even if enemyType isn't humanoid,
        // we still need *something* to feed NameGenerator).
        var race = enemyType == UnitData.EnemyType.Humanoid
            ? RaceData.GetAllRaces()[GD.Randi() % (uint)RaceData.GetAllRaces().Length]
            : RaceData.GetAllRaces()[0];
        unitData.Race = race;

        // Generate a sensible name. Append type suffix for non-humanoids so the UI
        // doesn't show "Aelar" for an undead skeleton.
        string baseName = NameGenerator.GenerateFullName(race.raceId, unitData.Level);
        unitData.UnitName = enemyType switch
        {
            UnitData.EnemyType.Humanoid => baseName,
            UnitData.EnemyType.Undead   => $"亡灵·{baseName}",
            UnitData.EnemyType.Beast    => $"野兽·{baseName}",
            UnitData.EnemyType.Demon    => $"恶魔·{baseName}",
            _                           => baseName,
        };

        var attrs = AllocateAttrsForEnemy(unitData.Level, enemyType);
        unitData.Str = attrs["str"]; unitData.Dex = attrs["dex"]; unitData.Con = attrs["con"];
        unitData.Intel = attrs["intel"]; unitData.Wis = attrs["wis"]; unitData.Cha = attrs["cha"];
        unitData.UnspentAttrPoints = 0;

        var resistances = new List<string>();
        var immunities = new List<string>();
        switch (enemyType)
        {
            case UnitData.EnemyType.Undead:
                immunities.Add("poison"); immunities.Add("mind"); break;
            case UnitData.EnemyType.Beast:
                unitData.aiStrategy = UnitData.AIStrategy.Instinct; break;
            case UnitData.EnemyType.Demon:
                resistances.Add("magic"); break;
        }
        unitData.Resistances = resistances.ToArray();
        unitData.Immunities = immunities.ToArray();

        // v0.6: BaseMaxHp 固定 10；HP 来自 CON_HP_Bonus × Level
        unitData.BaseMaxHp = 10;
        unitData.BaseAc = 8;
        unitData.BaseMoveRange = 4;
        unitData.Morale = 0;
        return unitData;
    }

    // ========================================================================
    // 属性分配
    // ========================================================================

    static Dictionary<string, int> AllocateAttrs(int level, RaceData? race = null)
    {
        int totalPoints = RPGRuleEngine.GetTotalAttrPoints(level);
        string[] keys = RPGRuleEngine.AttrKeys;
        var attrs = new Dictionary<string, int>();
        foreach (var key in keys) attrs[key] = RPGRuleEngine.AttrMin;
        int remaining = totalPoints - RPGRuleEngine.AttrMin * 6;

        var weights = GetAllocationWeights(race);
        float totalWeight = 0f;
        foreach (var key in keys) totalWeight += weights.GetValueOrDefault(key, 1.0f);

        foreach (var key in keys)
        {
            float w = weights.GetValueOrDefault(key, 1.0f);
            attrs[key] += Mathf.RoundToInt(w / totalWeight * remaining);
        }

        int diff = totalPoints - SumAttrs(attrs);
        while (diff > 0)
        {
            string k = keys[GD.Randi() % (uint)keys.Length];
            if (attrs[k] < RPGRuleEngine.AttrMax) { attrs[k]++; diff--; }
        }
        while (diff < 0)
        {
            string k = keys[GD.Randi() % (uint)keys.Length];
            if (attrs[k] > RPGRuleEngine.AttrMin) { attrs[k]--; diff++; }
        }

        if (race != null) attrs = ApplyRaceModifiers(attrs, race);
        return attrs;
    }

    static Dictionary<string, float> GetAllocationWeights(RaceData? race)
    {
        // 先随机选一个倾向模板，再叠加种族修正
        var w = _PickRandomTendencyWeights();
        if (race == null) return w;
        if (race.StrMod > 0) w["str"] += race.StrMod * 0.5f;
        if (race.DexMod > 0) w["dex"] += race.DexMod * 0.5f;
        if (race.ConMod > 0) w["con"] += race.ConMod * 0.5f;
        if (race.IntMod > 0) w["intel"] += race.IntMod * 0.5f;
        if (race.WisMod > 0) w["wis"] += race.WisMod * 0.5f;
        if (race.ChaMod > 0) w["cha"] += race.ChaMod * 0.5f;
        return w;
    }

    /// <summary>随机选取一个倾向模板，让角色有明确的属性方向</summary>
    private static Dictionary<string, float> _PickRandomTendencyWeights()
    {
        // 7种倾向模板，随机选一个
        int roll = (int)(GD.Randi() % 7);
        return GetTendencyWeights(roll);
    }

    /// <summary>按 id 取倾向模板（0..6）。供 sim 选定 build 类型生成。</summary>
    public static Dictionary<string, float> GetTendencyWeights(int tendency)
    {
        return tendency switch
        {
            0 => new() { ["str"] = 2.5f, ["dex"] = 1f, ["con"] = 1.8f, ["intel"] = 0.5f, ["wis"] = 0.7f, ["cha"] = 0.8f }, // 战士
            1 => new() { ["str"] = 0.7f, ["dex"] = 2.5f, ["con"] = 0.8f, ["intel"] = 0.8f, ["wis"] = 1.5f, ["cha"] = 0.7f }, // 游侠
            2 => new() { ["str"] = 0.5f, ["dex"] = 0.8f, ["con"] = 0.7f, ["intel"] = 2.5f, ["wis"] = 1.5f, ["cha"] = 0.8f }, // 法师
            3 => new() { ["str"] = 1.2f, ["dex"] = 1.2f, ["con"] = 2.2f, ["intel"] = 0.5f, ["wis"] = 1f, ["cha"] = 0.8f },   // 坦克
            4 => new() { ["str"] = 0.7f, ["dex"] = 1.5f, ["con"] = 0.7f, ["intel"] = 0.8f, ["wis"] = 0.8f, ["cha"] = 2.5f }, // 领袖
            5 => new() { ["str"] = 0.8f, ["dex"] = 1f, ["con"] = 0.8f, ["intel"] = 1.2f, ["wis"] = 2.5f, ["cha"] = 1f },     // 贤者
            _ => new() { ["str"] = 1.8f, ["dex"] = 1.8f, ["con"] = 1f, ["intel"] = 0.7f, ["wis"] = 0.7f, ["cha"] = 1f },     // 斗士
        };
    }

    /// <summary>按倾向模板生成角色（供 sim 测试 build 平衡用）。</summary>
    public static UnitData GenerateCharacterWithTendency(int tendency, int level = 1, long seedVal = -1)
    {
        if (seedVal >= 0) GD.Seed((ulong)seedVal);
        var weights = GetTendencyWeights(tendency);
        return _GenerateCharacterWithWeights(weights, level);
    }

    /// <summary>按显式属性权重生成角色（供 BuildProfiles sim 用）。</summary>
    public static UnitData GenerateCharacterWithWeights(Dictionary<string, float> weights, int level = 1, long seedVal = -1)
    {
        if (seedVal >= 0) GD.Seed((ulong)seedVal);
        return _GenerateCharacterWithWeights(new Dictionary<string, float>(weights), level);
    }

    private static UnitData _GenerateCharacterWithWeights(Dictionary<string, float> weights, int level)
    {
        var race = RaceData.GetAllRaces()[GD.Randi() % (uint)RaceData.GetAllRaces().Length];
        if (race != null)
        {
            if (race.StrMod > 0) weights["str"]   = weights.GetValueOrDefault("str", 1.0f) + race.StrMod * 0.5f;
            if (race.DexMod > 0) weights["dex"]   = weights.GetValueOrDefault("dex", 1.0f) + race.DexMod * 0.5f;
            if (race.ConMod > 0) weights["con"]   = weights.GetValueOrDefault("con", 1.0f) + race.ConMod * 0.5f;
            if (race.IntMod > 0) weights["intel"] = weights.GetValueOrDefault("intel", 1.0f) + race.IntMod * 0.5f;
            if (race.WisMod > 0) weights["wis"]   = weights.GetValueOrDefault("wis", 1.0f) + race.WisMod * 0.5f;
            if (race.ChaMod > 0) weights["cha"]   = weights.GetValueOrDefault("cha", 1.0f) + race.ChaMod * 0.5f;
        }

        int totalPoints = RPGRuleEngine.GetTotalAttrPoints(level);
        string[] keys = RPGRuleEngine.AttrKeys;
        var attrs = new Dictionary<string, int>();
        foreach (var key in keys) attrs[key] = RPGRuleEngine.AttrMin;
        int remaining = totalPoints - RPGRuleEngine.AttrMin * 6;
        float totalWeight = 0f;
        foreach (var key in keys) totalWeight += weights.GetValueOrDefault(key, 1.0f);
        foreach (var key in keys)
        {
            float w = weights.GetValueOrDefault(key, 1.0f);
            attrs[key] += Mathf.RoundToInt(w / totalWeight * remaining);
        }
        int diff = totalPoints - SumAttrs(attrs);
        while (diff > 0)
        {
            string k = keys[GD.Randi() % (uint)keys.Length];
            if (attrs[k] < RPGRuleEngine.AttrMax) { attrs[k]++; diff--; }
        }
        while (diff < 0)
        {
            string k = keys[GD.Randi() % (uint)keys.Length];
            if (attrs[k] > RPGRuleEngine.AttrMin) { attrs[k]--; diff++; }
        }
        if (race != null) attrs = ApplyRaceModifiers(attrs, race);

        var unitData = new UnitData();
        unitData.Level = Mathf.Max(1, level);
        unitData.UnitName = NameGenerator.GenerateFullName(race?.raceId ?? RaceData.Race.Human, unitData.Level);
        unitData.Xp = RPGRuleEngine.GetXpForLevel(level);
        unitData.Race = race;
        unitData.UnspentAttrPoints = 0;
        unitData.Str = Mathf.Max(1, attrs["str"]);
        unitData.Dex = Mathf.Max(1, attrs["dex"]);
        unitData.Con = Mathf.Max(1, attrs["con"]);
        unitData.Intel = Mathf.Max(1, attrs["intel"]);
        unitData.Wis = Mathf.Max(1, attrs["wis"]);
        unitData.Cha = Mathf.Max(1, attrs["cha"]);
        unitData.BaseMaxHp = 10;
        if (race != null && Array.IndexOf(race.RacialTraits, "dwarven_resilience") >= 0)
            unitData.BaseMaxHp += level;
        unitData.BaseAc = 8;
        unitData.BaseMoveRange = 4;
        unitData.BaseInitiative = 0;
        unitData.CurrentMana = BladeHex.Combat.CombatStats.GetMaxMana(unitData);
        unitData.CastingAbility = "intel";
        unitData.SkillPoints = 5 + (level - 1);
        unitData.Runtime.Loyalty = 50;
        EquipStartingGear(unitData);
        return unitData;
    }

    static Dictionary<string, int> AllocateAttrsForEnemy(int level, UnitData.EnemyType enemyType)
    {
        int totalPoints = RPGRuleEngine.GetTotalAttrPoints(level);
        string[] keys = RPGRuleEngine.AttrKeys;
        var attrs = new Dictionary<string, int>();
        foreach (var key in keys) attrs[key] = RPGRuleEngine.AttrMin;
        int remaining = totalPoints - RPGRuleEngine.AttrMin * 6;

        var weights = new Dictionary<string, float>
        { ["str"] = 1f, ["dex"] = 1f, ["con"] = 1f, ["intel"] = 1f, ["wis"] = 1f, ["cha"] = 1f };

        switch (enemyType)
        {
            case UnitData.EnemyType.Beast:
                weights["str"] = 2f; weights["dex"] = 1.5f; weights["con"] = 1.5f;
                weights["intel"] = 0.3f; weights["cha"] = 0.3f; break;
            case UnitData.EnemyType.Undead:
                weights["str"] = 1.5f; weights["con"] = 2f; weights["wis"] = 0.5f; break;
            case UnitData.EnemyType.Demon:
                weights["str"] = 1.5f; weights["con"] = 1.5f; weights["intel"] = 1.5f; weights["cha"] = 1.2f; break;
            case UnitData.EnemyType.Giant:
                weights["str"] = 3f; weights["con"] = 2.5f; weights["dex"] = 0.5f; break;
            case UnitData.EnemyType.Construct:
                weights["str"] = 2f; weights["con"] = 2f; weights["intel"] = 0.2f; weights["cha"] = 0.1f; break;
            case UnitData.EnemyType.Dragon:
                weights["str"] = 2.5f; weights["con"] = 2.5f; weights["intel"] = 2f; weights["cha"] = 1.5f; break;
            case UnitData.EnemyType.Legendary:
                weights["str"] = 2f; weights["con"] = 2f; weights["intel"] = 2f; weights["wis"] = 2f; weights["cha"] = 1.5f; break;
            default:
                weights["str"] = 1.2f; weights["dex"] = 1.2f; weights["con"] = 1.2f;
                weights["intel"] = 1f; weights["wis"] = 1f; weights["cha"] = 0.8f; break;
        }

        float totalWeight = 0f;
        foreach (var key in keys) totalWeight += weights[key];
        foreach (var key in keys)
            attrs[key] += Mathf.RoundToInt(weights[key] / totalWeight * remaining);

        int diff = totalPoints - SumAttrs(attrs);
        while (diff > 0)
        {
            string k = keys[GD.Randi() % (uint)keys.Length];
            if (attrs[k] < RPGRuleEngine.AttrMax) { attrs[k]++; diff--; }
        }
        while (diff < 0)
        {
            string k = keys[GD.Randi() % (uint)keys.Length];
            if (attrs[k] > RPGRuleEngine.AttrMin) { attrs[k]--; diff++; }
        }
        return attrs;
    }

    static int SumAttrs(Dictionary<string, int> attrs)
    {
        int s = 0; foreach (var kv in attrs) s += kv.Value; return s;
    }

    // ========================================================================
    // 修正与特质
    // ========================================================================

    static Dictionary<string, int> ApplyRaceModifiers(Dictionary<string, int> b, RaceData race)
    { b["str"] += race.StrMod; b["dex"] += race.DexMod; b["con"] += race.ConMod;
      b["intel"] += race.IntMod; b["wis"] += race.WisMod; b["cha"] += race.ChaMod; return b; }

    static Dictionary<string, int> ApplyTraitModifiers(Dictionary<string, int> b, TraitData[] traits)
    { foreach (var t in traits) { b["str"] += t.StrMod; b["dex"] += t.DexMod; b["con"] += t.ConMod;
      b["intel"] += t.IntMod; b["wis"] += t.WisMod; b["cha"] += t.ChaMod; } return b; }

    public static void ApplyFunctionalTraits(UnitData u, TraitData[] traits)
    { foreach (var t in traits) { if (t.traitType != TraitData.TraitType.Functional) continue;
      if (t.FunctionalEffect == "alertness") u.BaseInitiative += 3; } }

    public static TraitData[] RollTraits()
    {
        var attrT = TraitData.GetAttributeTraits();
        var funcT = TraitData.GetFunctionalTraits();
        var result = new List<TraitData>();
        int attrCount = GD.RandRange(2, 3);
        for (int i = 0; i < attrCount; i++)
        { var p = WeightedRandom(attrT); if (p != null && !result.Contains(p)) result.Add(p); }
        if (GD.Randf() < 0.5f && funcT.Length > 0)
        { var fp = WeightedRandom(funcT); if (fp != null && !result.Contains(fp)) result.Add(fp); }
        return result.ToArray();
    }

    static TraitData? WeightedRandom(TraitData[] traits)
    {
        float tw = 0f; foreach (var t in traits) tw += t.Weight;
        float roll = GD.Randf() * tw; float cum = 0f;
        foreach (var t in traits) { cum += t.Weight; if (roll <= cum) return t; }
        return traits.Length > 0 ? traits[0] : null;
    }

    // ========================================================================
    // AI升级
    // ========================================================================

    public static void AiAutoLevel(UnitData u, int targetLevel)
    {
        u.Xp = RPGRuleEngine.GetXpForLevel(targetLevel);
        u.Level = targetLevel; u.SkillPoints = 5 + (targetLevel - 1);
        var attrs = AllocateAttrs(targetLevel, u.Race);
        u.Str = Mathf.Max(1, attrs["str"]); u.Dex = Mathf.Max(1, attrs["dex"]);
        u.Con = Mathf.Max(1, attrs["con"]); u.Intel = Mathf.Max(1, attrs["intel"]);
        u.Wis = Mathf.Max(1, attrs["wis"]); u.Cha = Mathf.Max(1, attrs["cha"]);
        u.UnspentAttrPoints = 0;
        u.BaseMaxHp = 10;
        if (u.Race != null && Array.IndexOf(u.Race.RacialTraits, "dwarven_resilience") >= 0)
            u.BaseMaxHp += targetLevel;
        u.CurrentMana = BladeHex.Combat.CombatStats.GetMaxMana(u);
    }

    // ========================================================================
    // 辅助
    // ========================================================================

    public static string DetermineTendency(UnitData u)
    {
        var a = new Dictionary<string, int>
        { ["str"] = u.Str, ["dex"] = u.Dex, ["con"] = u.Con,
          ["intel"] = u.Intel, ["wis"] = u.Wis, ["cha"] = u.Cha };
        string best = "str"; int bv = -1;
        foreach (var kv in a) if (kv.Value > bv) { bv = kv.Value; best = kv.Key; }
        return best switch { "str"=>"力量倾向", "dex"=>"灵巧倾向", "con"=>"体魄倾向",
            "intel"=>"智力倾向", "wis"=>"感知倾向", "cha"=>"魅力倾向", _=>"力量倾向" };
    }

    // ========================================================================
    // 模板生成系统
    // ========================================================================

    public static UnitData GenerateFromTemplate(Godot.Collections.Dictionary tpl, int level = -1)
    {
        var unitData = new UnitData();
        unitData.EnemyTemplateId = tpl.ContainsKey("template_id") ? tpl["template_id"].AsString() : "";
        unitData.UnitName = tpl.ContainsKey("name") ? tpl["name"].AsString() : "未知单位";
        unitData.IsEnemy = true;
        unitData.enemyType = tpl.ContainsKey("enemy_type") ? (UnitData.EnemyType)tpl["enemy_type"].AsInt32() : UnitData.EnemyType.Beast;
        unitData.ThreatLevel = tpl.ContainsKey("cr") ? tpl["cr"].AsSingle() : 1.0f;
        unitData.aiStrategy = tpl.ContainsKey("ai_strategy") ? (UnitData.AIStrategy)tpl["ai_strategy"].AsInt32() : UnitData.AIStrategy.Instinct;

        int targetLevel = level > 0 ? level : Mathf.Max(1, Mathf.RoundToInt(tpl.ContainsKey("cr") ? tpl["cr"].AsSingle() : 1.0f));
        unitData.Level = targetLevel;

        var attrs = AllocateAttrsForEnemy(targetLevel, unitData.enemyType);
        unitData.Str = attrs["str"]; unitData.Dex = attrs["dex"]; unitData.Con = attrs["con"];
        unitData.Intel = attrs["intel"]; unitData.Wis = attrs["wis"]; unitData.Cha = attrs["cha"];
        unitData.UnspentAttrPoints = 0;

        // v0.6: BaseMaxHp 固定 10；模板的 hp_bonus 字段保留（手工设计的特殊单位调整）
        unitData.BaseMaxHp = 10
            + (tpl.ContainsKey("hp_bonus") ? tpl["hp_bonus"].AsInt32() : 0);
        unitData.BaseAc = 8 + (tpl.ContainsKey("ac_bonus") ? tpl["ac_bonus"].AsInt32() : 0);
        unitData.BaseMoveRange = 4;
        unitData.BaseInitiative = tpl.ContainsKey("initiative_bonus") ? tpl["initiative_bonus"].AsInt32() : 0;
        unitData.Morale = 0;

        if (tpl.ContainsKey("resistances"))
        {
            var rArr = (Godot.Collections.Array)tpl["resistances"];
            unitData.Resistances = new string[rArr.Count];
            for (int i = 0; i < rArr.Count; i++) unitData.Resistances[i] = rArr[i].AsString();
        }
        if (tpl.ContainsKey("immunities"))
        {
            var iArr = (Godot.Collections.Array)tpl["immunities"];
            unitData.Immunities = new string[iArr.Count];
            for (int i = 0; i < iArr.Count; i++) unitData.Immunities[i] = iArr[i].AsString();
        }
        if (tpl.ContainsKey("traits"))
        {
            var tArr = (Godot.Collections.Array)tpl["traits"];
            unitData.Traits = new string[tArr.Count];
            for (int i = 0; i < tArr.Count; i++) unitData.Traits[i] = tArr[i].AsString();
        }

        var spellIds = tpl.ContainsKey("spells") ? (Godot.Collections.Array)tpl["spells"] : null;
        if (spellIds != null && spellIds.Count > 0)
        {
            unitData.CurrentMana = BladeHex.Combat.CombatStats.GetMaxMana(unitData);
            unitData.CastingAbility = "intel";
            AssignSpells(unitData, spellIds, targetLevel);
        }

        var skillNames = tpl.ContainsKey("skills") ? (Godot.Collections.Array)tpl["skills"] : null;
        if (skillNames != null) AssignSkills(unitData, skillNames);

        // 技能点：5基础 + 每级1点
        unitData.SkillPoints = 5 + (targetLevel - 1);

        return unitData;
    }

    public static UnitData GenerateRandomLord(int level = 8)
    {
        var templates = UnitTemplateDB.GetLordTemplates();
        var tpl = templates[(int)(GD.Randi() % (uint)templates.Count)];
        return GenerateFromTemplate(tpl, level);
    }

    public static UnitData GenerateRandomAdventurer(int level = -1)
    {
        var templates = UnitTemplateDB.GetAdventurerTemplates();
        var tpl = templates[(int)(GD.Randi() % (uint)templates.Count)];
        float cr = tpl.ContainsKey("cr") ? tpl["cr"].AsSingle() : 1.0f;
        int targetLevel = level > 0 ? level : Mathf.Max(1, Mathf.RoundToInt(cr) + GD.RandRange(-1, 1));
        return GenerateFromTemplate(tpl, targetLevel);
    }

    public static UnitData GenerateRandomMonster(float minCr = 0.25f, float maxCr = 20.0f)
    {
        var templates = UnitTemplateDB.GetTemplatesByCr(minCr, maxCr);
        if (templates.Count == 0) templates = UnitTemplateDB.GetMonsterTemplates();
        var tpl = templates[(int)(GD.Randi() % (uint)templates.Count)];
        return GenerateFromTemplate(tpl);
    }

    public static UnitData GenerateRandomLegendary(int level = 15)
    {
        var templates = UnitTemplateDB.GetLegendaryTemplates();
        var tpl = templates[(int)(GD.Randi() % (uint)templates.Count)];
        return GenerateFromTemplate(tpl, level);
    }

    public static UnitData[] GenerateEncounterParty(float partyCrTotal, int partySize = -1)
    {
        var units = new List<UnitData>();
        int ps = partySize < 0 ? GD.RandRange(2, 6) : partySize;
        float crPerUnit = partyCrTotal / ps;
        for (int i = 0; i < ps; i++)
        {
            var tpls = UnitTemplateDB.GetTemplatesByCr(crPerUnit * 0.5f, crPerUnit * 2.0f);
            if (tpls.Count == 0)
                units.Add(GenerateRandomEnemy(crPerUnit, UnitData.EnemyType.Beast));
            else
            {
                var tpl = tpls[(int)(GD.Randi() % (uint)tpls.Count)];
                units.Add(GenerateFromTemplate(tpl));
            }
        }
        return units.ToArray();
    }

    // ========================================================================
    // 法术分配
    // ========================================================================

    static void AssignSpells(UnitData unitData, Godot.Collections.Array spellIds, int level)
    {
        foreach (var sid in spellIds)
        {
            var spell = CreateSpellById(sid.AsString(), level);
            if (spell != null) unitData.KnownSpells.Add(spell);
        }
    }

    static SpellData? CreateSpellById(string spellId, int level)
    {
        var spell = new SpellData();
        spell.SpellId = spellId;
        switch (spellId)
        {
            case "fireball":
                spell.SpellName = "火球术"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier3; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 2;
                spell.RangeCells = 8; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.DexSave;
                spell.DamageDiceCount = 6 + Mathf.FloorToInt(level / 3.0f); spell.DamageDiceSides = 6; spell.DamageType = "fire";
                break;
            case "magic_missile":
                spell.SpellName = "魔导飞弹"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier1; spell.shape = SpellData.SpellShape.Single; spell.RangeCells = 10;
                spell.resolutionType = SpellData.ResolutionType.AutoHit;
                spell.DamageDiceCount = 4; spell.DamageDiceSides = 4; spell.DamageType = "force";
                break;
            case "ice_storm":
                spell.SpellName = "冰暴术"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier4; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 3;
                spell.RangeCells = 10; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.ConSave;
                spell.DamageDiceCount = 4 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 8; spell.DamageType = "cold";
                break;
            case "frost_breath":
                spell.SpellName = "霜冻吐息"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier5; spell.shape = SpellData.SpellShape.Cone; spell.ShapeSize = 4;
                spell.RangeCells = 4; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.ConSave;
                spell.DamageDiceCount = 8 + Mathf.FloorToInt(level / 3.0f); spell.DamageDiceSides = 8; spell.DamageType = "cold";
                spell.AppliedStatusEffect = "freeze"; spell.StatusDuration = 1;
                break;
            case "blizzard":
                spell.SpellName = "暴风雪"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier6; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 4;
                spell.RangeCells = 12; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.ConSave;
                spell.DamageDiceCount = 10 + Mathf.FloorToInt(level / 2.0f); spell.DamageDiceSides = 8; spell.DamageType = "cold";
                break;
            case "inferno":
                spell.SpellName = "炼狱"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier6; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 3;
                spell.RangeCells = 10; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.DexSave;
                spell.DamageDiceCount = 12 + Mathf.FloorToInt(level / 2.0f); spell.DamageDiceSides = 8; spell.DamageType = "fire";
                break;
            case "meteor_strike":
                spell.SpellName = "陨石坠落"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier7; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 3;
                spell.RangeCells = 12; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.DexSave;
                spell.DamageDiceCount = 15 + Mathf.FloorToInt(level / 2.0f); spell.DamageDiceSides = 8; spell.DamageType = "fire";
                break;
            case "nature_bolt":
                spell.SpellName = "nature_bolt"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier2; spell.shape = SpellData.SpellShape.Single; spell.RangeCells = 8;
                spell.resolutionType = SpellData.ResolutionType.AttackRoll;
                spell.DamageDiceCount = 3 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 8; spell.DamageType = "force";
                break;
            case "life_drain":
                spell.SpellName = "生命汲取"; spell.spellSchool = SpellData.SpellSchool.Necromancy;
                spell.tier = SpellData.SpellTier.Tier2; spell.shape = SpellData.SpellShape.Single; spell.RangeCells = 6;
                spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.ConSave;
                spell.DamageDiceCount = 3 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 6; spell.DamageType = "necrotic";
                spell.HealDiceCount = 3 + Mathf.FloorToInt(level / 4.0f); spell.HealDiceSides = 6;
                break;
            case "raise_dead":
                spell.SpellName = "raise_dead"; spell.spellSchool = SpellData.SpellSchool.Necromancy;
                spell.tier = SpellData.SpellTier.Tier3; spell.shape = SpellData.SpellShape.Self; spell.RangeCells = 3;
                spell.resolutionType = SpellData.ResolutionType.AutoHit;
                spell.SpecialEffect = "summon"; spell.SummonHp = 10 + level * 3; spell.SummonDuration = 5;
                break;
            case "bone_spear":
                spell.SpellName = "骨矛"; spell.spellSchool = SpellData.SpellSchool.Necromancy;
                spell.tier = SpellData.SpellTier.Tier1; spell.shape = SpellData.SpellShape.Ray; spell.RangeCells = 8;
                spell.resolutionType = SpellData.ResolutionType.AttackRoll;
                spell.DamageDiceCount = 2 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 8; spell.DamageType = "necrotic";
                break;
            case "shield":
                spell.SpellName = "魔法护盾"; spell.spellSchool = SpellData.SpellSchool.Abjuration;
                spell.tier = SpellData.SpellTier.Tier1; spell.shape = SpellData.SpellShape.Self; spell.RangeCells = 0;
                spell.resolutionType = SpellData.ResolutionType.AutoHit;
                spell.SpecialEffect = "shield"; spell.DurationTurns = 2;
                break;
            case "entangle":
                spell.SpellName = "藤蔓束缚"; spell.spellSchool = SpellData.SpellSchool.Transmutation;
                spell.tier = SpellData.SpellTier.Tier1; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 2;
                spell.RangeCells = 6; spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.StrSave;
                spell.AppliedStatusEffect = "entangled"; spell.StatusDuration = 2;
                break;
            case "holy_light":
                spell.SpellName = "purifying_flame"; spell.spellSchool = SpellData.SpellSchool.Abjuration;
                spell.tier = SpellData.SpellTier.Tier1; spell.shape = SpellData.SpellShape.Single; spell.RangeCells = 6;
                spell.resolutionType = SpellData.ResolutionType.AutoHit;
                spell.HealDiceCount = 2 + Mathf.FloorToInt(level / 4.0f); spell.HealDiceSides = 8; spell.HealBonus = 2;
                break;
            case "smite":
                spell.SpellName = "smite"; spell.spellSchool = SpellData.SpellSchool.Abjuration;
                spell.tier = SpellData.SpellTier.Tier2; spell.shape = SpellData.SpellShape.Touch; spell.RangeCells = 1;
                spell.resolutionType = SpellData.ResolutionType.AttackRoll;
                spell.DamageDiceCount = 3 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 8; spell.DamageType = "arcane";
                break;
            case "healing_light":
                spell.SpellName = "治愈之光"; spell.spellSchool = SpellData.SpellSchool.Abjuration;
                spell.tier = SpellData.SpellTier.Tier3; spell.shape = SpellData.SpellShape.Sphere; spell.ShapeSize = 2;
                spell.RangeCells = 0; spell.resolutionType = SpellData.ResolutionType.AutoHit;
                spell.HealDiceCount = 4 + Mathf.FloorToInt(level / 3.0f); spell.HealDiceSides = 8; spell.HealBonus = 3;
                break;
            case "moonbeam":
                spell.SpellName = "月光射线"; spell.spellSchool = SpellData.SpellSchool.Evocation;
                spell.tier = SpellData.SpellTier.Tier2; spell.shape = SpellData.SpellShape.Line; spell.RangeCells = 8;
                spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.ConSave;
                spell.DamageDiceCount = 3 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 8; spell.DamageType = "arcane";
                break;
            case "dark_command":
                spell.SpellName = "黑暗统御"; spell.spellSchool = SpellData.SpellSchool.Enchantment;
                spell.tier = SpellData.SpellTier.Tier3; spell.shape = SpellData.SpellShape.Single; spell.RangeCells = 6;
                spell.resolutionType = SpellData.ResolutionType.Save;
                spell.saveType = SpellData.SaveType.WisSave;
                spell.AppliedStatusEffect = "charmed"; spell.StatusDuration = 2;
                break;
            case "shadow_bolt":
                spell.SpellName = "shadow_bolt"; spell.spellSchool = SpellData.SpellSchool.Necromancy;
                spell.tier = SpellData.SpellTier.Tier1; spell.shape = SpellData.SpellShape.Single; spell.RangeCells = 10;
                spell.resolutionType = SpellData.ResolutionType.AttackRoll;
                spell.DamageDiceCount = 2 + Mathf.FloorToInt(level / 4.0f); spell.DamageDiceSides = 8; spell.DamageType = "necrotic";
                break;
            default:
                spell.SpellName = "shadow_bolt"; spell.tier = SpellData.SpellTier.Cantrip;
                break;
        }
        spell.ManaCost = SpellData.GetDefaultManaCost(spell.tier);
        spell.CooldownTurns = SpellData.GetDefaultCooldown(spell.tier);
        return spell;
    }

    // ========================================================================
    // 技能分配
    // ========================================================================

    static void AssignSkills(UnitData unitData, Godot.Collections.Array skillNames)
    {
        foreach (var sn in skillNames)
        {
            var skill = new SkillData();
            string name = sn.AsString();
            skill.SkillName = name;
            skill.Description = GetSkillDescription(name);
            skill.ApCost = GetSkillApCost(name);
            skill.RangeCells = GetSkillRange(name);
            skill.Cooldown = GetSkillCooldown(name);
            unitData.Skills.Add(skill);
        }
    }

    static string GetSkillDescription(string name) => name switch
    {
        "挥砍连击" => "连续挥砍两次，每次造成武器伤害",
        "盾墙" => "举起盾牌，本回合AC+4但无法移动",
        "号令冲锋" => "激励周围友方发起冲锋，范围内友方移动力+2",
        "坚守阵地" => "固守当前位置，获得坚韧状态（伤害减免25%）",
        "暗影步" => "传送至阴影处，下次攻击造成额外暗影伤害",
        "心灵压制" => "用意志力压制目标，使其眩晕1回合",
        "恐惧术" => "散发恐惧气息，使周围敌人士气降低",
        "狂暴冲锋" => "狂暴状态下冲锋，沿途敌人被击退并受到伤害",
        "旋风斩" => "旋转攻击周围所有敌人",
        "战吼" => "发出震天怒吼，提升自身和友方攻击力",
        "嗜血" => "击杀敌人后恢复生命值",
        "连射" => "连续射出多支箭矢",
        "狙击要害" => "瞄准要害射击，暴击率大幅提升",
        "影遁" => "融入阴影，提升闪避率",
        "精准射击" => "精准射击，忽略部分掩护加成",
        "设置陷阱" => "在脚下放置陷阱",
        "闪避" => "增加闪避率直到下回合",
        "法杖打击" => "用法杖进行近战攻击",
        "魔法护盾" => "张开魔法屏障，临时提升AC",
        "奥术斩" => "以奥术之力灌注武器进行近战攻击",
        "治疗之手" => "触碰治疗一名友方单位",
        "奥术护盾" => "展开奥术护盾，为自身和附近友方提供保护",
        "驱邪" => "驱散亡灵或解除诅咒",
        "猛击" => "全力猛击",
        "投石" => "投掷石块进行远程攻击",
        "卑鄙刺击" => "趁敌人不备进行偷袭",
        "毒镖" => "发射淬毒飞镖",
        "狂暴" => "进入狂暴状态，攻击+50%但防御下降",
        "呼唤援兵" => "呼唤更多哥布林加入战斗",
        "撕咬" => "用牙齿撕裂敌人",
        "扑击" => "扑向目标将其扑倒",
        "嗥叫" => "发出嗥叫呼唤同伴",
        "熊抱" => "用双臂抱住敌人进行碾压",
        "撕裂" => "用利爪撕裂目标",
        "践踏" => "践踏周围敌人",
        "骷髅召唤" => "召唤骷髅战士加入战斗",
        "亡灵诅咒" => "诅咒目标使其属性降低",
        "毒雾吐息" => "喷吐毒雾锥形范围攻击",
        "火焰冲锋" => "全身燃烧冲向敌人",
        "恶魔猛击" => "充满黑暗力量的重击",
        "恐惧凝视" => "用恶魔之眼震慑目标",
        "黑暗劈斩" => "以黑暗之力劈斩目标",
        "亡灵哀嚎" => "发出能令活人恐惧的哀嚎",
        "灵魂汲取" => "吸取目标的灵魂能量",
        "恐惧之触" => "触碰目标使其陷入恐惧",
        "冰霜龙息" => "喷吐极寒龙息，冻结大范围区域",
        "尾击" => "用巨尾横扫周围敌人",
        "翼击" => "展开巨翼击退周围敌人",
        "碾压" => "碾压周围的小型敌人",
        "恐惧威慑" => "散发恐怖气场，使敌人士气下降",
        "烈焰风暴" => "召唤烈焰风暴覆盖战场",
        "岩石投掷" => "投掷巨石攻击远处目标",
        "地震" => "猛烈践踏引发局部地震",
        "巨拳猛击" => "以巨拳猛击地面造成冲击波",
        "月华剑舞" => "在月光下施展连续斩击",
        "星辰之力" => "引导星辰之力释放能量",
        "精灵之歌" => "吟唱精灵歌谣，治疗友方",
        "时空裂隙" => "撕裂时空进行短距离传送",
        "酸液喷吐" => "喷吐腐蚀性酸液",
        "吞噬" => "吞噬小型敌人恢复生命",
        "钻地突袭" => "钻入地下后从下方突袭",
        "尾鞭" => "用尾部鞭击敌人",
        "麻痹毒刺" => "蛰刺附带麻痹毒素",
        "吞噬尸体" => "吞食尸体恢复生命",
        _ => "",
    };

    static int GetSkillApCost(string name) => name switch
    {
        "盾墙" or "坚守阵地" or "影遁" or "魔法护盾" or "奥术护盾" => 0,
        "烈焰风暴" or "地震" or "时空裂隙" => 2,
        _ => 1,
    };

    static int GetSkillRange(string name) => name switch
    {
        "连射" or "精准射击" or "投石" or "毒镖" or "岩石投掷" => 8,
        "号令冲锋" or "恐惧威慑" or "亡灵哀嚎" or "战吼" or "嗥叫" => 4,
        _ => 1,
    };

    static int GetSkillCooldown(string name) => name switch
    {
        "号令冲锋" or "呼唤援兵" or "狂暴" or "地震" or "时空裂隙" or "烈焰风暴" => 3,
        "冰霜龙息" or "毒雾吐息" or "恐惧威慑" or "骷髅召唤" or "亡灵诅咒" => 4,
        "旋风斩" or "暗影步" or "恐惧术" or "战吼" => 2,
        _ => 1,
    };
}
