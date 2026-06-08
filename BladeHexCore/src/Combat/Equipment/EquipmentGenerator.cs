// EquipmentGenerator.cs
// Procedural equipment generator. Uses the ambient CombatRandom so headless
// simulation runs and unit tests get deterministic outputs given a seed.
//
// Lives in Core (no Node/Sprite/Texture dependencies).
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Combat;

/// <summary>
/// Random equipment generation: rarity rolls, item-level scaling, affixes,
/// and "full loadout" composition for an entire unit (weapon + armor + shield
/// + helmet + accessory + quiver/ammo).
/// </summary>
public static class EquipmentGenerator
{
    private static readonly Dictionary<ItemData.Rarity, float> DefaultRarityWeights = new()
    {
        { ItemData.Rarity.Common,    60.0f },
        { ItemData.Rarity.Uncommon,  25.0f },
        { ItemData.Rarity.Rare,      10.0f },
        { ItemData.Rarity.Epic,       4.0f },
        { ItemData.Rarity.Legendary,  1.0f },
    };

    private static readonly Dictionary<string, Dictionary<ItemData.Rarity, float>> RarityWeightsByDifficulty = new()
    {
        { "easy",      new() { { ItemData.Rarity.Common, 70 }, { ItemData.Rarity.Uncommon, 20 }, { ItemData.Rarity.Rare,  8 }, { ItemData.Rarity.Epic,  2 }, { ItemData.Rarity.Legendary, 0 } } },
        { "normal",    new() { { ItemData.Rarity.Common, 55 }, { ItemData.Rarity.Uncommon, 28 }, { ItemData.Rarity.Rare, 12 }, { ItemData.Rarity.Epic,  4 }, { ItemData.Rarity.Legendary, 1 } } },
        { "hard",      new() { { ItemData.Rarity.Common, 40 }, { ItemData.Rarity.Uncommon, 30 }, { ItemData.Rarity.Rare, 18 }, { ItemData.Rarity.Epic,  9 }, { ItemData.Rarity.Legendary, 3 } } },
        { "nightmare", new() { { ItemData.Rarity.Common, 25 }, { ItemData.Rarity.Uncommon, 30 }, { ItemData.Rarity.Rare, 25 }, { ItemData.Rarity.Epic, 14 }, { ItemData.Rarity.Legendary, 6 } } },
    };

    // ========================================================================
    // Single-item generators
    // ========================================================================

    public static ItemData GenerateEquipment(ItemData baseItem, ItemData.Rarity targetRarity = (ItemData.Rarity)(-1), int itemLevel = 1, string difficulty = "normal")
    {
        var item = DeepCopyItem(baseItem);
        item.ItemLevel = itemLevel;

        if ((int)targetRarity == -1) targetRarity = RollRarity(difficulty);
        item.ItemRarity = targetRarity;

        ApplyRandomAffixes(item, itemLevel);
        return item;
    }

    public static WeaponData GenerateRandomWeapon(string[]? weaponPool = null, ItemData.Rarity targetRarity = (ItemData.Rarity)(-1), int itemLevel = 1, string difficulty = "normal")
    {
        var allWeapons = PrototypeData.GetWeapons();
        var candidates = new List<WeaponData>();

        if (weaponPool == null || weaponPool.Length == 0)
        {
            candidates.AddRange(allWeapons.Values);
        }
        else
        {
            foreach (var key in weaponPool)
                if (allWeapons.TryGetValue(key, out var w)) candidates.Add(w);
        }

        if (candidates.Count == 0)
        {
            candidates.Add(allWeapons.ContainsKey("arming_sword")
                ? allWeapons["arming_sword"]
                : new WeaponData { ItemName = "练习剑" });
        }

        int idx = CombatRandom.RandRange(0, candidates.Count - 1);
        var baseItem = candidates[idx];
        return (GenerateEquipment(baseItem, targetRarity, itemLevel, difficulty) as WeaponData)!;
    }

    public static ArmorData GenerateRandomArmor(string[]? armorPool = null, ItemData.Rarity targetRarity = (ItemData.Rarity)(-1), int itemLevel = 1, string difficulty = "normal")
    {
        var allArmors = PrototypeData.GetArmors();
        var candidates = new List<ArmorData>();

        if (armorPool == null || armorPool.Length == 0)
        {
            candidates.AddRange(allArmors.Values.Where(a => a.armorType != ArmorData.ArmorType.Shield));
        }
        else
        {
            foreach (var key in armorPool)
                if (allArmors.TryGetValue(key, out var a)) candidates.Add(a);
        }

        if (candidates.Count == 0)
        {
            candidates.Add(allArmors.ContainsKey("leather")
                ? allArmors["leather"]
                : new ArmorData { ItemName = "布衣" });
        }

        int idx = CombatRandom.RandRange(0, candidates.Count - 1);
        var baseItem = candidates[idx];
        return (GenerateEquipment(baseItem, targetRarity, itemLevel, difficulty) as ArmorData)!;
    }

    public static ItemData.Rarity RollRarity(string difficulty = "normal")
    {
        var weights = RarityWeightsByDifficulty.GetValueOrDefault(difficulty, DefaultRarityWeights);
        float total = weights.Values.Sum();
        float roll = (float)CombatRandom.RandRange(0, 1_000_000) / 1_000_000f * total;
        float cumulative = 0;
        foreach (var pair in weights)
        {
            cumulative += pair.Value;
            if (roll <= cumulative) return pair.Key;
        }
        return ItemData.Rarity.Common;
    }

    public static int GetItemLevelFromCr(float cr)
    {
        if (cr <= 0.25f) return CombatRandom.RandRange(1, 2);
        if (cr <= 0.5f)  return CombatRandom.RandRange(1, 3);
        if (cr <= 1.0f)  return CombatRandom.RandRange(2, 4);
        if (cr <= 2.0f)  return CombatRandom.RandRange(3, 6);
        if (cr <= 5.0f)  return CombatRandom.RandRange(5, 10);
        if (cr <= 10.0f) return CombatRandom.RandRange(8, 15);
        return CombatRandom.RandRange(12, 20);
    }

    public static string GetDifficultyFromCr(float cr)
    {
        if (cr <= 0.5f) return "easy";
        if (cr <= 2.0f) return "normal";
        if (cr <= 5.0f) return "hard";
        return "nightmare";
    }

    // ========================================================================
    // Full loadout (single source of truth shared by QuickCombat, sim harness,
    // future encounter factories).
    //
    // Fills every relevant slot on the unit. Preserves any pre-existing items
    // (idempotent: callers can pre-equip a few specific pieces and let this
    // method fill the rest).
    // ========================================================================

    /// <summary>Rules governing which gear is generated for a given character level.</summary>
    public readonly struct LoadoutPolicy
    {
        /// <summary>Probability 0..1 that an optional slot (off-hand / shield / helmet / boots / accessory) is filled.</summary>
        public float OptionalSlotChance { get; init; }

        /// <summary>Forced weapon tier (1, 2, or 3).</summary>
        public int WeaponTier { get; init; }

        /// <summary>
        /// Pool of armor IDs considered for body armor. Higher-level units pull
        /// from heavier-tier pools. Empty array = "no restriction" (all armor).
        /// </summary>
        public string[] BodyArmorPool { get; init; }

        /// <summary>Pool of helmet IDs (subset of armors with slot=Helmet).</summary>
        public string[] HelmetPool { get; init; }

        /// <summary>Pool of shield IDs (heavier shields at higher tiers).</summary>
        public string[] ShieldPool { get; init; }

        /// <summary>Pool of gauntlets/gloves IDs (subset of armors with slot=Hands).</summary>
        public string[] GauntletsPool { get; init; }
    }

    /// <summary>
    /// Pick the loadout policy that matches a character level.
    ///   1-29  : optional slots probabilistic (~70%), tier-1 weapons, light/cloth armor
    ///   30-49 : all slots filled, tier-1 weapons, light/medium armor pool
    ///   50-89 : tier-2 weapons, chain_mail / studded_leather body armor
    ///   90+   : tier-3 weapons, half_plate / full_plate body armor
    /// </summary>
    public static LoadoutPolicy GetLoadoutPolicy(int level)
    {
        // 1..29 : probabilistic slots, tier 1, light gear
        if (level < 30)
        {
            return new LoadoutPolicy
            {
                OptionalSlotChance = 0.70f,
                WeaponTier = 1,
                BodyArmorPool = new[] { "cloth", "mage_robe", "leather", "studded_leather" },
                HelmetPool    = new[] { "leather_cap", "iron_helm" },
                ShieldPool    = new[] { "light_wooden_shield", "infantry_round_shield" },
                GauntletsPool = new[] { "leather_gloves" },
            };
        }
        // 30..49 : 100% optional fill, still tier 1, light/medium pool
        if (level < 50)
        {
            return new LoadoutPolicy
            {
                OptionalSlotChance = 1.0f,
                WeaponTier = 1,
                BodyArmorPool = new[] { "leather", "studded_leather", "chain_mail" },
                HelmetPool    = new[] { "iron_helm", "great_helm" },
                ShieldPool    = new[] { "infantry_round_shield", "infantry_heavy_shield" },
                GauntletsPool = new[] { "leather_gloves", "chain_gauntlets" },
            };
        }
        // 50..89 : tier 2, chain mail / studded leather
        if (level < 90)
        {
            return new LoadoutPolicy
            {
                OptionalSlotChance = 1.0f,
                WeaponTier = 2,
                BodyArmorPool = new[] { "studded_leather", "chain_mail" },
                HelmetPool    = new[] { "great_helm", "iron_helm" },
                ShieldPool    = new[] { "infantry_heavy_shield", "knight_shield" },
                GauntletsPool = new[] { "chain_gauntlets", "plate_gauntlets" },
            };
        }
        // 90+ : tier 3, plate
        return new LoadoutPolicy
        {
            OptionalSlotChance = 1.0f,
            WeaponTier = 3,
            BodyArmorPool = new[] { "half_plate", "full_plate" },
            HelmetPool    = new[] { "great_helm", "knight_helm" },
            ShieldPool    = new[] { "knight_shield", "legion_tower_shield" },
            GauntletsPool = new[] { "plate_gauntlets" },
        };
    }

    /// <summary>
    /// Equip a unit with a complete loadout: main weapon, off-hand weapon
    /// (probabilistic at low levels, guaranteed past lvl 30), armor, shield,
    /// helmet, boots, and accessory. Uses the level-based <see cref="LoadoutPolicy"/>
    /// so high-level units always look the part.
    /// </summary>
    public static void EquipFullSet(UnitData unit, int itemLevel, string difficulty)
    {
        EquipFullSet(unit, itemLevel, difficulty, BuildPreference.None);
    }

    /// <summary>
    /// 主属性偏好（决定武器系列和护甲选择）。供 sim / 强敌生成器使用。
    /// </summary>
    public enum BuildPreference
    {
        /// <summary>无偏好 — 随机肉搏 / 远程</summary>
        None,
        /// <summary>STR 主：重型近战 + 重甲 + 盾</summary>
        Str,
        /// <summary>DEX 主：远程（弓/弩） + 轻甲</summary>
        Dex,
        /// <summary>CON 主：中型近战 + 重甲 + 大盾</summary>
        Con,
        /// <summary>INT 主：Catalyst + 布甲，禁盾（v0.6 10.0 法术装备限制）</summary>
        Int,
        /// <summary>WIS 主：Catalyst（治疗法师）+ 布甲</summary>
        Wis,
        /// <summary>CHA 主：中型近战 + 中甲 + 小盾</summary>
        Cha,
    }

    /// <summary>
    /// Equip 一个角色的完整装备，按主属性偏好选择武器系列和护甲。
    /// </summary>
    public static void EquipFullSet(UnitData unit, int itemLevel, string difficulty, BuildPreference pref)
    {
        if (unit == null) return;

        // v0.8: 非人形生物中，野兽(Beast)、构造体(Construct)、龙族(Dragon)等不装备物理武器和防具，
        // 它们完全依赖天然 AC/DR 以及天生技能。
        if (unit.IsEnemy && (unit.enemyType == UnitData.EnemyType.Beast 
                             || unit.enemyType == UnitData.EnemyType.Construct 
                             || unit.enemyType == UnitData.EnemyType.Dragon))
        {
            return;
        }

        var policy = GetLoadoutPolicy(unit.Level);
        bool fullKit = policy.OptionalSlotChance >= 1.0f;

        // ----- Main weapon (always present) -----
        if (unit.PrimaryMainHand == null)
        {
            // STR 主：默认重型近战，但 25% 概率纯远程投掷主武器（反风筝、机动）
            string[] pool;
            if (pref == BuildPreference.Str && CombatRandom.RandRange(0, 99) < 25)
                pool = TierThrowingPool(policy.WeaponTier);
            else
                pool = pref switch
                {
                    BuildPreference.Str => TierMeleeHeavyPool(policy.WeaponTier),
                    BuildPreference.Dex => TierBowCrossbowPool(policy.WeaponTier),
                    BuildPreference.Con => TierMeleeMediumOneHandPool(policy.WeaponTier),
                    BuildPreference.Int => TierCatalystPool(policy.WeaponTier),
                    BuildPreference.Wis => TierLightMeleePool(policy.WeaponTier), // 刺客：轻型近战暴击
                    BuildPreference.Cha => TierMeleeMediumOneHandPool(policy.WeaponTier),
                    _ => CombatRandom.RandRange(0, 99) < 30
                        ? PickRangedPoolForTier(policy.WeaponTier)
                        : PickMeleePoolForTier(policy.WeaponTier),
                };
            // Fallback if the build-preference pool is empty (e.g. tier missing):
            if (pool == null || pool.Length == 0)
                pool = PickMeleePoolForTier(policy.WeaponTier);
            unit.PrimaryMainHand = GenerateRandomWeapon(pool, (ItemData.Rarity)(-1), itemLevel, difficulty);
        }

        // ----- Off-hand weapon -----
        if (unit.SecondaryMainHand == null && Roll(policy.OptionalSlotChance))
        {
            bool mainIsRanged   = unit.PrimaryMainHand is WeaponData pw && pw.IsRanged;
            bool mainIsThrowing = unit.PrimaryMainHand is WeaponData pt && pt.IsThrowing;
            bool mainIsCatalyst = unit.PrimaryMainHand is WeaponData pc && pc.IsCatalyst;

            string[] offPool;
            if (mainIsCatalyst)
            {
                // 法师不需要副手武器（off-hand 经常是法术媒介或留空）
                offPool = TierCatalystPool(policy.WeaponTier);
            }
            else if (mainIsThrowing)
            {
                offPool = TierThrowingPool(policy.WeaponTier);
            }
            else if (mainIsRanged)
            {
                offPool = TierLightMeleePool(policy.WeaponTier);
            }
            else
            {
                // 近战主手 → 副手按主属性偏好选择反制武器
                //   STR：高概率投掷或远程（重型武器无盾，需要远程反制风筝）
                //   CON：低概率投掷（CON 主流是单手中型 + 塔盾，副手少用）
                //   DEX/CHA/WIS：默认混合（轻型远程 / 投掷各半）
                int throwingProb = pref switch
                {
                    BuildPreference.Str => 70,  // STR 偏向带投掷反风筝
                    BuildPreference.Con => 25,  // CON 倾向纯盾，副手投掷罕见
                    BuildPreference.Wis => 50,  // 刺客副手投掷给点远程能力
                    BuildPreference.Cha => 35,
                    _                   => 40,
                };
                bool throwingOff = CombatRandom.RandRange(0, 99) < throwingProb;
                offPool = throwingOff
                    ? TierThrowingPool(policy.WeaponTier)
                    : TierLightRangedPool(policy.WeaponTier);
            }
            unit.SecondaryMainHand = GenerateRandomWeapon(offPool, (ItemData.Rarity)(-1), itemLevel, difficulty);
        }

        // ----- Body armor (always present, but pool depends on policy + preference) -----
        if (unit.Armor == null)
        {
            string[] armorPool = pref switch
            {
                // 法师 必须穿布甲 (v0.6 10.0 法术装备限制)
                BuildPreference.Int => new[] { "cloth", "mage_robe" },
                // 刺客（WIS）：轻甲 + 中甲（皮甲/镶钉/链甲），不能板甲
                BuildPreference.Wis => new[] { "leather", "studded_leather", "chain_mail" },
                // STR / CON 优先重甲
                BuildPreference.Str or BuildPreference.Con => FilterToHeavierArmors(policy.BodyArmorPool),
                // DEX 主（游侠/弓手）：轻甲 → 中甲，不能板甲（板甲 -4/-5 AP 完全杀死风筝）
                BuildPreference.Dex => new[] { "leather", "studded_leather" },
                // CHA 主（诗人/外交官）：中甲（保护 + 不影响 social check 想象）
                BuildPreference.Cha => new[] { "studded_leather", "chain_mail" },
                _ => policy.BodyArmorPool,
            };
            if (armorPool == null || armorPool.Length == 0) armorPool = policy.BodyArmorPool;
            unit.Armor = GenerateRandomArmor(armorPool, (ItemData.Rarity)(-1), itemLevel, difficulty);
            unit.Armor?.InitializeArmorPoints();
        }

        // ----- Shield (only for one-handed melee mains; INT/WIS 法师禁盾) -----
        bool eligibleForShield =
            unit.PrimaryMainHand is WeaponData mainWpn
            && !mainWpn.IsRanged
            && !mainWpn.IsTwoHanded
            && !mainWpn.IsCatalyst;  // catalyst 也不能持盾（v0.6 10.0）
        bool prefBlocksShield = pref == BuildPreference.Int;  // 仅法师禁盾，刺客可持
        if (unit.Shield == null && eligibleForShield && !prefBlocksShield && Roll(fullKit ? 0.85f : 0.60f))
        {
            var shieldBase = PickFromPool(policy.ShieldPool, isShield: true);
            if (shieldBase != null)
            {
                unit.Shield = (ArmorData)GenerateEquipment(shieldBase, (ItemData.Rarity)(-1), itemLevel, difficulty);
                unit.Shield.InitializeArmorPoints();
            }
        }

        // ----- Quiver + ammo (auto for ranged mains) -----
        bool hasQuiver = false;
        if (unit.PrimaryMainHand is WeaponData rangedWpn
            && rangedWpn.IsRanged && !rangedWpn.IsThrowing && !rangedWpn.IsCatalyst)
        {
            var quivers = ItemDataLoader.GetQuivers();
            if (quivers.Count > 0)
            {
                var quiverList = new List<ItemData>(quivers.Values);
                int idx = CombatRandom.RandRange(0, quiverList.Count - 1);
                unit.PrimaryOffHand = quiverList[idx];
                hasQuiver = true;
            }
        }
        if (unit.PrimaryMainHand is WeaponData primaryWpn && primaryWpn.NeedsAmmo)
            primaryWpn.InitializeAmmo(hasQuiver);
        if (unit.SecondaryMainHand is WeaponData secondaryWpn && secondaryWpn.NeedsAmmo)
            secondaryWpn.InitializeAmmo(false);

        // ----- Helmet -----
        if (unit.Helmet == null && Roll(policy.OptionalSlotChance))
        {
            var helmBase = PickFromPool(policy.HelmetPool, slot: ItemData.EquipSlot.Helmet);
            if (helmBase != null)
            {
                unit.Helmet = (ArmorData)GenerateEquipment(helmBase, (ItemData.Rarity)(-1), itemLevel, difficulty);
                unit.Helmet.InitializeArmorPoints();
            }
        }

        // ----- Gauntlets -----
        if (unit.Gauntlets == null && Roll(policy.OptionalSlotChance))
        {
            var gauntletsBase = PickFromPool(policy.GauntletsPool, slot: ItemData.EquipSlot.Hands);
            if (gauntletsBase != null)
            {
                unit.Gauntlets = (ArmorData)GenerateEquipment(gauntletsBase, (ItemData.Rarity)(-1), itemLevel, difficulty);
                unit.Gauntlets.InitializeArmorPoints();
            }
        }

        // ----- Boots -----
        if (unit.Boots == null && Roll(policy.OptionalSlotChance))
        {
            var bootsBase = PickAnyBoots();
            if (bootsBase != null)
            {
                unit.Boots = (ArmorData)GenerateEquipment(bootsBase, (ItemData.Rarity)(-1), itemLevel, difficulty);
                unit.Boots.InitializeArmorPoints();
            }
        }

        // ----- Accessory -----
        if (unit.Accessory1 == null && Roll(policy.OptionalSlotChance))
        {
            var accessories = AccessoryData.GetAllAccessories();
            if (accessories.Length > 0)
            {
                int idx = CombatRandom.RandRange(0, accessories.Length - 1);
                unit.Accessory1 = accessories[idx];
            }
        }

        // ----- Weapon mastery preset (v0.7) -----
        // 按角色等级预填武器精通 XP，避免 lvl 120 的角色仍是新手命中。
        // 主武器与副武器各自的精通轨道独立预填。
        PresetWeaponMasteryForLevel(unit);
    }

    /// <summary>
    /// v0.7: 按 unit.Level 给武器精通 XP 预填值。
    /// 主武器精通 = clamp(ceil(level/9) + 1, 1, MaxMasteryLevel - 1)
    ///   Lv.1 → 2, Lv.9 → 3, Lv.30 → 5, Lv.60 → 8, Lv.120 → 14。
    /// 副武器精通 = max(1, primaryLevel - 3)（主副武器若同轨道则取主等级）。
    /// 已存在更高 XP 的不覆盖（保护战斗中累积的精通进度）。
    /// </summary>
    public static void PresetWeaponMasteryForLevel(UnitData unit)
    {
        if (unit == null || unit.Level <= 0) return;

        int primaryLv = System.Math.Clamp(
            (unit.Level + 8) / 9 + 1,
            1,
            WeaponMastery.MaxMasteryLevel - 1);
        int secondaryLv = System.Math.Max(1, primaryLv - 3);
        int primaryXp   = WeaponMastery.XpForLevel(primaryLv);
        int secondaryXp = WeaponMastery.XpForLevel(secondaryLv);

        ApplyPresetXp(unit, unit.PrimaryMainHand as WeaponData, primaryXp);
        ApplyPresetXp(unit, unit.SecondaryMainHand as WeaponData, primaryXp);  // 武器组 B 主等同主轨道
        ApplyPresetXp(unit, unit.PrimaryOffHand as WeaponData,   secondaryXp);
        ApplyPresetXp(unit, unit.SecondaryOffHand as WeaponData, secondaryXp);
    }

    private static void ApplyPresetXp(UnitData unit, WeaponData? weapon, int xp)
    {
        if (weapon == null || xp <= 0) return;
        int existing = unit.WeaponMastery.GetXpBySubtype(weapon.Subtype);
        if (existing >= xp) return; // 不覆盖已积累更高的轨道
        unit.WeaponMastery.SetXpBySubtype(weapon.Subtype, xp);
    }

    // ========================================================================
    // Tier / pool helpers
    // ========================================================================

    /// <summary>Roll a probability with the ambient deterministic random source.</summary>
    private static bool Roll(float chance)
    {
        if (chance <= 0f) return false;
        if (chance >= 1f) return true;
        return CombatRandom.RandRange(0, 999) < (int)(chance * 1000);
    }

    /// <summary>
    /// For melee subtypes, produce a pool of all weapon IDs at the requested tier.
    /// Falls back to all melee if no entry matches.
    /// </summary>
    private static string[] PickMeleePoolForTier(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (w.IsRanged) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        return pool.Count > 0 ? pool.ToArray() : System.Array.Empty<string>();
    }

    private static string[] PickRangedPoolForTier(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (!w.IsRanged) continue;
            if (w.IsThrowing) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        // Fall back: if no ranged at this tier, accept any tier <= requested
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
            {
                if (!w.IsRanged) continue;
                if (w.IsThrowing) continue;
                if (w.Tier <= tier) pool.Add(id);
            }
        }
        return pool.ToArray();
    }

    private static string[] TierThrowingPool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (!w.IsThrowing) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            // fallback: any throwing
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (w.IsThrowing) pool.Add(id);
        }
        return pool.ToArray();
    }

    private static string[] TierLightMeleePool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (w.IsRanged || w.IsTwoHanded) continue;
            if (w.Weight != WeaponData.WeightCategory.Light) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (!w.IsRanged && !w.IsTwoHanded && w.Tier == tier) pool.Add(id);
        }
        return pool.ToArray();
    }

    private static string[] TierLightRangedPool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (!w.IsRanged || w.IsThrowing || w.IsTwoHanded) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (w.IsRanged && !w.IsThrowing && w.Tier <= tier) pool.Add(id);
        }
        return pool.ToArray();
    }

    /// <summary>近战 Heavy 重型武器池（巨剑/巨斧/大锤等双手）。STR 主用。</summary>
    private static string[] TierMeleeHeavyPool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (w.IsRanged || w.IsCatalyst) continue;
            if (w.Weight != WeaponData.WeightCategory.Heavy) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (!w.IsRanged && !w.IsCatalyst && w.Weight == WeaponData.WeightCategory.Heavy && w.Tier <= tier) pool.Add(id);
        }
        return pool.ToArray();
    }

    /// <summary>近战 Medium 单手武器池（武装剑/战斧/狼牙棒等）。CON / CHA 主用。</summary>
    private static string[] TierMeleeMediumOneHandPool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (w.IsRanged || w.IsCatalyst || w.IsTwoHanded) continue;
            if (w.Weight != WeaponData.WeightCategory.Medium) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (!w.IsRanged && !w.IsCatalyst && !w.IsTwoHanded && w.Weight == WeaponData.WeightCategory.Medium && w.Tier <= tier) pool.Add(id);
        }
        return pool.ToArray();
    }

    /// <summary>弓 + 弩池（不含投掷）。DEX 主用。</summary>
    private static string[] TierBowCrossbowPool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (!w.IsRanged || w.IsThrowing || w.IsCatalyst) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (w.IsRanged && !w.IsThrowing && !w.IsCatalyst && w.Tier <= tier) pool.Add(id);
        }
        return pool.ToArray();
    }

    /// <summary>Catalyst 法术媒介池（魔杖/法球/法杖）。INT / WIS 主用。</summary>
    private static string[] TierCatalystPool(int tier)
    {
        var pool = new List<string>();
        foreach (var (id, w) in PrototypeData.GetWeapons())
        {
            if (!w.IsCatalyst) continue;
            if (w.Tier == tier) pool.Add(id);
        }
        if (pool.Count == 0)
        {
            foreach (var (id, w) in PrototypeData.GetWeapons())
                if (w.IsCatalyst && w.Tier <= tier) pool.Add(id);
        }
        return pool.ToArray();
    }

    /// <summary>从 BodyArmor 池里挑出"最重的几件"（板甲优先链甲优先皮甲）。STR / CON 主用。</summary>
    private static string[] FilterToHeavierArmors(string[] basePool)
    {
        if (basePool == null || basePool.Length == 0) return basePool ?? System.Array.Empty<string>();
        // 重排：含 plate 的最优先，然后 chain，然后 leather，最后其他
        var armors = PrototypeData.GetArmors();
        int Score(string id)
        {
            if (id.Contains("full_plate")) return 5;
            if (id.Contains("half_plate")) return 4;
            if (id.Contains("chain")) return 3;
            if (id.Contains("studded")) return 2;
            if (id.Contains("leather")) return 1;
            return 0;
        }
        var ranked = new List<string>(basePool);
        ranked.Sort((a, b) => Score(b).CompareTo(Score(a)));
        // 取前一半（至少 1 件）作为重甲优先池
        int take = System.Math.Max(1, ranked.Count / 2);
        return ranked.GetRange(0, take).ToArray();
    }

    /// <summary>Look up a base ArmorData by id, restricted to a slot/type.</summary>
    private static ArmorData? PickFromPool(string[] ids, bool isShield = false, ItemData.EquipSlot? slot = null)
    {
        if (ids == null || ids.Length == 0) return null;
        var armors = PrototypeData.GetArmors();
        var matches = new List<ArmorData>();
        foreach (var id in ids)
        {
            if (!armors.TryGetValue(id, out var a)) continue;
            if (isShield && a.armorType != ArmorData.ArmorType.Shield) continue;
            if (slot.HasValue && a.EquipSlotTarget != slot.Value) continue;
            matches.Add(a);
        }
        if (matches.Count == 0) return null;
        return matches[CombatRandom.RandRange(0, matches.Count - 1)];
    }

    private static ArmorData? PickAnyBoots()
    {
        var boots = new List<ArmorData>();
        foreach (var a in PrototypeData.GetArmors().Values)
            if (a.EquipSlotTarget == ItemData.EquipSlot.Feet) boots.Add(a);
        if (boots.Count == 0) return null;
        return boots[CombatRandom.RandRange(0, boots.Count - 1)];
    }

    // ========================================================================
    // Affix application
    // ========================================================================

    private static void ApplyRandomAffixes(ItemData item, int itemLevel)
    {
        int maxAffixes = item.GetMaxAffixCount();
        if (maxAffixes == 0) return;

        EquipmentAffix.AffixTarget target = EquipmentAffix.AffixTarget.Any;
        if      (item is WeaponData) target = EquipmentAffix.AffixTarget.Weapon;
        else if (item is ArmorData armor)
        {
            target = armor.armorType == ArmorData.ArmorType.Shield
                ? EquipmentAffix.AffixTarget.Shield
                : EquipmentAffix.AffixTarget.Armor;
        }
        else if (item is AccessoryData) target = EquipmentAffix.AffixTarget.Accessory;

        var available = EquipmentAffix.GetAffixesForTarget(target, itemLevel, (int)item.ItemRarity);
        if (available.Length == 0) return;

        var prefixes = available.Where(a => a.IsPrefix).ToList();
        var suffixes = available.Where(a => !a.IsPrefix).ToList();

        if (prefixes.Count > 0 && maxAffixes > 0)
        {
            var chosen = WeightedRandomAffix(prefixes);
            if (chosen != null)
            {
                item.AddAffix(chosen);
                maxAffixes--;
            }
        }

        while (maxAffixes > 0 && suffixes.Count > 0)
        {
            var chosen = WeightedRandomAffix(suffixes);
            if (chosen != null)
            {
                item.AddAffix(chosen);
                suffixes.Remove(chosen);
                maxAffixes--;
            }
            else break;
        }
    }

    private static EquipmentAffix? WeightedRandomAffix(List<EquipmentAffix> pool)
    {
        if (pool.Count == 0) return null;
        float total = pool.Sum(a => a.Weight);
        float roll = (float)CombatRandom.RandRange(0, 1_000_000) / 1_000_000f * total;
        float cumulative = 0;
        foreach (var a in pool)
        {
            cumulative += a.Weight;
            if (roll <= cumulative) return a;
        }
        return pool.Last();
    }

    private static ItemData DeepCopyItem(ItemData src) => (ItemData)src.Duplicate();
}
