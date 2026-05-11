// UnitData.cs
// 单位基础数据资源 (RPG 核心版本)
// 对应策划案 05/06 — 完整装备槽位、敌方模板、词缀加成
// 迁移自 GDScript UnitData.gd
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class UnitData : Resource
{
    // ========================================
    // 敌方专属枚举
    // ========================================

    public enum EnemyType
    {
        Humanoid,  // 类人
        Beast,     // 野兽
        Undead,    // 亡灵
        Demon,     // 魔物
        Giant,     // 巨型
        Construct, // 构造体
        Dragon,    // 龙族
        Legendary, // 传奇
    }

    public enum CreatureSize
    {
        Tiny,       // 微型
        Small,      // 小型
        Medium,     // 中型
        Large,      // 大型
        Huge,       // 巨型
        Gargantuan, // 超巨型
    }

    public enum AIStrategy
    {
        Reckless,    // 鲁莽
        Cautious,    // 谨慎
        Tactical,    // 战术
        Instinct,    // 本能
        Territorial, // 领地
        Cunning,     // 狡诈
        Intimidate,  // 恐吓
        Berserk,     // 狂暴
    }

    public enum MoraleLevel
    {
        High,    // 高昂 (+20~+40)
        Normal,  // 正常 (-19~+19)
        Low,     // 低落 (-39~-20)
        Broken,  // 崩溃 (-59~-40)
        Routing, // 溃逃 (-60)
    }

    // ========================================
    // 基础信息
    // ========================================

    [Export] public string UnitName = "未命名单位";
    [Export] public int Level = 1;

    // ========================================
    // 六维属性
    // ========================================

    [Export] public int Str = 10;
    [Export] public int Dex = 10;
    [Export] public int Con = 10;
    [Export] public int Intel = 10;
    [Export] public int Wis = 10;
    [Export] public int Cha = 10;

    // ========================================
    // 技能盘数据
    // ========================================

    [Export] public Godot.Collections.Dictionary SkillTreeData = new();
    [Export] public int CharacterId = -1;

    // ========================================
    // 基础战斗属性
    // ========================================

    [Export] public int BaseMaxHp = 10;
    [Export] public int BaseAc = 10;
    [Export] public int BaseAp = 12;
    [Export] public int BaseMoveRange = 4;
    [Export] public int BaseInitiative;

    // ========================================
    // 装甲系统 (Damage Reduction)
    // ========================================

    [Export] public int CurrentDr;
    public int MaxDr;
    [Export] public int NaturalDr;
    [Export] public int NaturalDrThreshold;

    // ========================================
    // 种族与特质
    // ========================================

    [Export] public RaceData? Race;
    [Export] public Godot.Collections.Array<TraitData> CharacterTraits = new();

    // ========================================
    // 经验与等级
    // ========================================

    [Export] public int Xp;
    [Export] public int SkillPoints;
    public int UnspentAttrPoints;
    [Export] public int JumpsUsed;

    // ========================================
    // 武器精通（运行时，不序列化）
    // 按伤害类型×轻重共 9 条轨道，造成伤害即获得 XP
    // ========================================

    private WeaponMastery? _weaponMastery;
    public WeaponMastery WeaponMastery => _weaponMastery ??= new WeaponMastery();

    // ========================================
    // 法术系统
    // ========================================

    [Export] public Godot.Collections.Array<SpellData> KnownSpells = new();
    public int CurrentMana;
    public Godot.Collections.Dictionary SpellCooldowns = new();
    [Export] public string CastingAbility = "intel";

    // ========================================
    // 坐骑系统
    // ========================================

    [Export] public MountData? Mount;
    public int MountCurrentHp;
    public bool IsMounted;

    // ========================================
    // 消耗品背包
    // ========================================

    [Export] public Godot.Collections.Array<ConsumableData> Consumables = new();

    // ========================================
    // 装备槽位
    // ========================================

    [Export] public ArmorData? Armor;
    [Export] public ArmorData? Shield;
    [Export] public ArmorData? Helmet;
    [Export] public AccessoryData? Accessory1;
    [Export] public AccessoryData? Accessory2;

    // 武器组 A
    [Export] public WeaponData? PrimaryMainHand;
    [Export] public ItemData? PrimaryOffHand;

    // 武器组 B
    [Export] public WeaponData? SecondaryMainHand;
    [Export] public ItemData? SecondaryOffHand;

    // ========================================
    // 技能列表
    // ========================================

    [Export] public Godot.Collections.Array<SkillData> Skills = new();

    [Export] public Texture2D? Portrait;
    [Export] public Texture2D? BattleSprite;
    [Export] public Texture2D? OverworldSprite;
    [Export] public SpriteFrames? SpriteFramesValue;

    // ========================================
    // 敌方专属字段
    // ========================================

    [Export] public string EnemyTemplateId = "";
    [Export] public bool IsEnemy;
    [Export] public EnemyType enemyType = EnemyType.Humanoid;
    [Export] public CreatureSize creatureSize = CreatureSize.Medium;
    [Export] public float ThreatLevel;
    [Export] public AIStrategy aiStrategy = AIStrategy.Instinct;
    [Export] public int Morale;
    [Export] public string[] Immunities = [];
    [Export] public string[] Resistances = [];
    [Export] public string[] Weaknesses = [];
    [Export] public string[] Traits = [];

    // 传奇专属
    [Export] public int LegendaryResistanceUses;
    [Export] public int LegendaryActionPoints;
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> LegendaryActions = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> LairActions = new();
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Phases = new();
    [Export] public string UniqueDropId = "";

    // ========================================
    // 战斗运行时状态
    // ========================================

    public int Facing;
    public bool IsDefending;
    public bool IsRangedWeaponLoaded = true;
    public int Loyalty = 50;
    public int DeathSaveSuccesses;
    public int DeathSaveFailures;
    public Godot.Collections.Array<Godot.Collections.Dictionary> ActiveStatusEffects = new();
    public bool AooUsedThisTurn;
    public bool CounterUsedThisTurn;

    // ========================================
    // 词缀加成缓存
    // ========================================

    public int AccessoryStrBonus;
    public int AccessoryDexBonus;
    public int AccessoryConBonus;
    public int AccessoryIntBonus;
    public int AccessoryWisBonus;
    public int AccessoryChaBonus;
    public int AccessoryHpBonus;
    public int AccessoryAcBonus;
    public int AccessoryMoveBonus;
    public int AccessoryInitiativeBonus;

    // ========================================
    // 枚举显示名方法
    // ========================================

    public MoraleLevel GetMoraleLevel() => Morale switch
    {
        >= 20 => MoraleLevel.High,
        >= -19 => MoraleLevel.Normal,
        >= -39 => MoraleLevel.Low,
        >= -59 => MoraleLevel.Broken,
        _ => MoraleLevel.Routing,
    };

    public string GetEnemyTypeName() => enemyType switch
    {
        EnemyType.Humanoid => "类人",
        EnemyType.Beast => "野兽",
        EnemyType.Undead => "亡灵",
        EnemyType.Demon => "魔物",
        EnemyType.Giant => "巨型",
        EnemyType.Construct => "构造体",
        EnemyType.Dragon => "龙族",
        EnemyType.Legendary => "传奇",
        _ => "未知",
    };

    public string GetAiStrategyName() => aiStrategy switch
    {
        AIStrategy.Reckless => "鲁莽",
        AIStrategy.Cautious => "谨慎",
        AIStrategy.Tactical => "战术",
        AIStrategy.Instinct => "本能",
        AIStrategy.Territorial => "领地",
        AIStrategy.Cunning => "狡诈",
        AIStrategy.Intimidate => "恐吓",
        AIStrategy.Berserk => "狂暴",
        _ => "未知",
    };

    public string GetSizeName() => creatureSize switch
    {
        CreatureSize.Tiny => "微型",
        CreatureSize.Small => "小型",
        CreatureSize.Medium => "中型",
        CreatureSize.Large => "大型",
        CreatureSize.Huge => "巨型",
        CreatureSize.Gargantuan => "超巨型",
        _ => "未知",
    };

    public string GetCrText()
    {
        if (ThreatLevel == 0) return "CR 0";
        if (ThreatLevel < 1) return $"CR 1/{Mathf.RoundToInt(1.0f / ThreatLevel)}";
        return $"CR {Mathf.RoundToInt(ThreatLevel)}";
    }

    // ========================================
    // 装备逻辑
    // ========================================

    /// <summary>装备一个物品到对应槽位</summary>
    public void EquipItem(ItemData item, Node? economyManager)
    {
        if (item is ArmorData armorItem)
        {
            if (armorItem.armorType == ArmorData.ArmorType.Shield)
            {
                if (Shield != null) UnequipItem("shield", economyManager);
                Shield = armorItem;
            }
            else
            {
                if (Armor != null) UnequipItem("armor", economyManager);
                Armor = armorItem;
            }
            economyManager?.Call("remove_item", item);
        }
        else if (item is WeaponData)
        {
            if (PrimaryMainHand != null) UnequipItem("primary_main", economyManager);
            PrimaryMainHand = (WeaponData)item;
            economyManager?.Call("remove_item", item);
        }
        else if (item is AccessoryData)
        {
            if (Accessory1 == null)
                Accessory1 = (AccessoryData)item;
            else if (Accessory2 == null)
                Accessory2 = (AccessoryData)item;
            else
            {
                UnequipItem("accessory_1", economyManager);
                Accessory1 = (AccessoryData)item;
            }
            economyManager?.Call("remove_item", item);
        }
        RefreshAccessoryBonuses();
    }

    /// <summary>卸下指定槽位的装备</summary>
    public void UnequipItem(string slot, Node? economyManager)
    {
        switch (slot)
        {
            case "armor":
                if (Armor != null) { economyManager?.Call("add_item", Armor); Armor = null; }
                break;
            case "shield":
                if (Shield != null) { economyManager?.Call("add_item", Shield); Shield = null; }
                break;
            case "helmet":
                if (Helmet != null) { economyManager?.Call("add_item", Helmet); Helmet = null; }
                break;
            case "accessory_1":
                if (Accessory1 != null) { economyManager?.Call("add_item", Accessory1); Accessory1 = null; }
                break;
            case "accessory_2":
                if (Accessory2 != null) { economyManager?.Call("add_item", Accessory2); Accessory2 = null; }
                break;
            case "primary_main":
                if (PrimaryMainHand != null) { economyManager?.Call("add_item", PrimaryMainHand); PrimaryMainHand = null; }
                break;
            case "primary_off":
                if (PrimaryOffHand != null) { economyManager?.Call("add_item", PrimaryOffHand); PrimaryOffHand = null; }
                break;
            case "secondary_main":
                if (SecondaryMainHand != null) { economyManager?.Call("add_item", SecondaryMainHand); SecondaryMainHand = null; }
                break;
            case "secondary_off":
                if (SecondaryOffHand != null) { economyManager?.Call("add_item", SecondaryOffHand); SecondaryOffHand = null; }
                break;
        }
        RefreshAccessoryBonuses();
    }

    /// <summary>刷新饰品加成缓存</summary>
    public void RefreshAccessoryBonuses()
    {
        AccessoryStrBonus = 0;
        AccessoryDexBonus = 0;
        AccessoryConBonus = 0;
        AccessoryIntBonus = 0;
        AccessoryWisBonus = 0;
        AccessoryChaBonus = 0;
        AccessoryHpBonus = 0;
        AccessoryAcBonus = 0;
        AccessoryMoveBonus = 0;
        AccessoryInitiativeBonus = 0;

        foreach (var acc in new AccessoryData?[] { Accessory1, Accessory2 })
        {
            if (acc == null) continue;
            AccessoryStrBonus += acc.StrBonus;
            AccessoryDexBonus += acc.DexBonus;
            AccessoryConBonus += acc.ConBonus;
            AccessoryIntBonus += acc.IntBonus;
            AccessoryWisBonus += acc.WisBonus;
            AccessoryChaBonus += acc.ChaBonus;
            AccessoryHpBonus += acc.HpBonus;
            AccessoryAcBonus += acc.AcBonus;
            AccessoryMoveBonus += acc.MoveBonus;
            AccessoryInitiativeBonus += acc.InitiativeBonus;
        }
    }

    // ========================================
    // 装备加成计算接口
    // ========================================

    /// <summary>
    /// 已弃用：护甲AcBonus不再参与AC计算。
    /// AC由 Unit.get_ac() 计算：10 + sqrt(DEX/2) + 盾牌 + 技能盘。
    /// 护甲对防御的贡献完全通过DR系统（max_dr / dr_threshold）。
    /// </summary>
    [System.Obsolete("Use Unit.get_ac() instead. Armor AcBonus does not affect AC.")]
    public int GetEquipmentAcBonus()
    {
        int bonus = 0;
        if (Shield != null) bonus += Shield.AcBonus;
        if (Helmet != null) bonus += Helmet.AcBonus;
        bonus += AccessoryAcBonus;
        // 注意：Armor.AcBonus 已移除，护甲不贡献AC
        return bonus;
    }

    public int GetEquipmentHpBonus()
    {
        int bonus = 0;
        if (Armor != null) bonus += Armor.BonusHp;
        if (Helmet != null) bonus += Helmet.BonusHp;
        bonus += AccessoryHpBonus;
        return bonus;
    }

    public int GetEquipmentMoveBonus()
    {
        int bonus = 0;
        if (Armor != null) bonus += Armor.BonusMove;
        bonus += AccessoryMoveBonus;
        if (Armor != null && Armor.MovementPenalty > 0)
            bonus -= Armor.MovementPenalty;
        return bonus;
    }

    public int GetEquipmentInitiativeBonus() => AccessoryInitiativeBonus;

    public string[] GetEquipmentResistances()
    {
        var res = new System.Collections.Generic.List<string>();
        if (Armor != null && Armor.BonusResistance != "" && !res.Contains(Armor.BonusResistance))
            res.Add(Armor.BonusResistance);
        if (Helmet != null && Helmet.BonusResistance != "" && !res.Contains(Helmet.BonusResistance))
            res.Add(Helmet.BonusResistance);
        foreach (var acc in new AccessoryData?[] { Accessory1, Accessory2 })
            if (acc != null && acc.Resistance != "" && !res.Contains(acc.Resistance))
                res.Add(acc.Resistance);
        return res.ToArray();
    }

    public string[] GetEquipmentImmunities()
    {
        var imm = new System.Collections.Generic.List<string>();
        if (Armor != null && Armor.BonusImmunity != "" && !imm.Contains(Armor.BonusImmunity))
            imm.Add(Armor.BonusImmunity);
        if (Helmet != null && Helmet.BonusImmunity != "" && !imm.Contains(Helmet.BonusImmunity))
            imm.Add(Helmet.BonusImmunity);
        foreach (var acc in new AccessoryData?[] { Accessory1, Accessory2 })
            if (acc != null && acc.Immunity != "" && !imm.Contains(acc.Immunity))
                imm.Add(acc.Immunity);
        return imm.ToArray();
    }

    public ItemData[] GetAllEquippedItems()
    {
        var items = new System.Collections.Generic.List<ItemData>();
        if (Armor != null) items.Add(Armor);
        if (Shield != null) items.Add(Shield);
        if (Helmet != null) items.Add(Helmet);
        if (PrimaryMainHand != null) items.Add(PrimaryMainHand);
        if (PrimaryOffHand != null) items.Add(PrimaryOffHand);
        if (SecondaryMainHand != null) items.Add(SecondaryMainHand);
        if (SecondaryOffHand != null) items.Add(SecondaryOffHand);
        if (Accessory1 != null) items.Add(Accessory1);
        if (Accessory2 != null) items.Add(Accessory2);
        return items.ToArray();
    }
}
