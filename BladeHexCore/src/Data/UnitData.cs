// UnitData.cs
// 单位基础数据资源 (RPG 核心版本)
// 对应策划案 05/06 — 完整装备槽位、敌方模板、词缀加成
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;

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

    // ========================================
    // 基础信息
    // ========================================

    [Export] public string UnitName { get; set; } = "未命名单位";
    [Export] public int Level { get; set; } = 1;
    [Export] public bool IsWounded { get; set; }

    // ========================================
    // 六维属性
    // ========================================

    [Export] public int Str { get; set; } = 10;
    [Export] public int Dex { get; set; } = 10;
    [Export] public int Con { get; set; } = 10;
    [Export] public int Intel { get; set; } = 10;
    [Export] public int Wis { get; set; } = 10;
    [Export] public int Cha { get; set; } = 10;

    // ========================================
    // 技能盘数据
    // ========================================

    [Export] public Godot.Collections.Dictionary SkillTreeData = new();
    [Export] public int CharacterId { get; set; } = -1;

    // ========================================
    // 已装备技能（v0.7 战斗中可用的 10 个主动技能槽位）
    // 每个元素是技能盘节点的 SkillEffect ID（"double_attack" / "blood_vortex" 等）；
    // 空字符串/null 表示空槽。SkillTreeUI 是手动修改的唯一入口，战斗 HUD 只读。
    // ========================================

    public const int MaxEquippedSkills = 10;

    [Export] public Godot.Collections.Array<string> EquippedSkills = new();

    /// <summary>已装备技能数（非空槽）。</summary>
    public int GetEquippedSkillCount()
    {
        int n = 0;
        for (int i = 0; i < EquippedSkills.Count; i++)
            if (!string.IsNullOrEmpty(EquippedSkills[i])) n++;
        return n;
    }

    /// <summary>检查某个 SkillEffect 是否已装备。</summary>
    public bool IsSkillEquipped(string skillEffect)
    {
        if (string.IsNullOrEmpty(skillEffect)) return false;
        for (int i = 0; i < EquippedSkills.Count; i++)
            if (EquippedSkills[i] == skillEffect) return true;
        return false;
    }

    /// <summary>装备技能到指定槽位（0..9）。空字符串 = 卸下；不允许重复装备。</summary>
    public bool SetEquippedSkill(int slot, string skillEffect)
    {
        if (slot < 0 || slot >= MaxEquippedSkills) return false;
        // 防重复：若已在其他槽，先清空原槽
        if (!string.IsNullOrEmpty(skillEffect))
        {
            for (int i = 0; i < EquippedSkills.Count; i++)
                if (i != slot && EquippedSkills[i] == skillEffect)
                    EquippedSkills[i] = "";
        }
        // 扩容到 MaxEquippedSkills
        while (EquippedSkills.Count < MaxEquippedSkills) EquippedSkills.Add("");
        EquippedSkills[slot] = skillEffect ?? "";
        return true;
    }

    /// <summary>读取指定槽位（越界或空 → ""）。</summary>
    public string GetEquippedSkill(int slot)
    {
        if (slot < 0 || slot >= EquippedSkills.Count) return "";
        return EquippedSkills[slot] ?? "";
    }

    /// <summary>找到第一个空槽（-1 = 全满）。</summary>
    public int FindFirstEmptyEquippedSlot()
    {
        while (EquippedSkills.Count < MaxEquippedSkills) EquippedSkills.Add("");
        for (int i = 0; i < EquippedSkills.Count; i++)
            if (string.IsNullOrEmpty(EquippedSkills[i])) return i;
        return -1;
    }

    // ========================================
    // 基础战斗属性
    // ========================================

    [Export] public int BaseMaxHp { get; set; } = 10;
    [Export] public int BaseAc { get; set; } = 8;
    [Export] public int BaseAp { get; set; } = 12;
    [Export] public int BaseMoveRange { get; set; } = 4;
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

    [Export] public RaceData? Race { get; set; }
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
    [Export] public string CastingAbility { get; set; } = "intel";

    // ========================================
    // 坐骑系统
    // ========================================

    [Export] public MountData? Mount { get; set; }
    public int MountCurrentHp;
    public bool IsMounted;

    // ========================================
    // 消耗品背包
    // ========================================

    [Export] public Godot.Collections.Array<ConsumableData> Consumables = new();

    // ========================================
    // 装备槽位
    // ========================================

    [Export] public ArmorData? Armor { get; set; }
    [Export] public ArmorData? Shield { get; set; }
    [Export] public ArmorData? Helmet { get; set; }
    [Export] public ArmorData? Gauntlets { get; set; }       // 护手
    [Export] public ArmorData? Boots { get; set; }           // 鞋子
    [Export] public AccessoryData? Accessory1 { get; set; }
    [Export] public AccessoryData? Accessory2 { get; set; }

    // 武器组 A
    [Export] public WeaponData? PrimaryMainHand { get; set; }
    [Export] public ItemData? PrimaryOffHand;

    // 武器组 B
    [Export] public WeaponData? SecondaryMainHand { get; set; }
    [Export] public ItemData? SecondaryOffHand;

    // 技能额外武器栏（由特定技能/天赋解锁，如双持大师、武器收藏家等）
    // 每个条目包含一个武器槽，运行时由技能系统动态添加/移除
    [Export] public Godot.Collections.Array<WeaponData> ExtraWeaponSlots = new();

    // ========================================
    // 技能列表
    // ========================================

    [Export] public Godot.Collections.Array<SkillData> Skills = new();

    /// <summary>纸娃娃捏脸头像数据（优先于此处的旧式分散字段）</summary>
    [Export] public AvatarData? Avatar { get; set; }

    // ────── 旧式分散字段（保留兼容，新代码应使用 Avatar） ──────
    [Export] public string GenderCustom { get; set; } = "";
    [Export] public int FaceIndex { get; set; } = 0;
    [Export] public int HairIndex { get; set; } = 0;

    [Export] public string PortraitId { get; set; } = "";
    [Export] public string BattleSpriteId { get; set; } = "";
    [Export] public string OverworldSpriteId { get; set; } = "";
    [Export] public string SpriteFramesId { get; set; } = "";

    // ========================================
    // 敌方专属字段
    // ========================================

    [Export] public string EnemyTemplateId { get; set; } = "";
    [Export] public bool IsEnemy;
    [Export] public EnemyType enemyType = EnemyType.Humanoid;
    [Export] public CreatureSize creatureSize = CreatureSize.Medium;
    /// <summary>
    /// 多格占用宽度（沿 q 轴方向）。0 或负数表示使用 CreatureSize 默认值。
    /// 模板可通过 "footprint_w" / "footprint_h" 字段覆盖。
    /// 示例：1×1=普通, 1×2=蛇形, 2×2=大型, 2×3=巨型, 3×4=超巨型
    /// </summary>
    [Export] public int FootprintW = 0;
    /// <summary>多格占用高度（沿 r 轴方向）。0 或负数表示使用 CreatureSize 默认值。</summary>
    [Export] public int FootprintH = 0;
    [Export] public float ThreatLevel;
    [Export] public AIStrategy aiStrategy = AIStrategy.Instinct;
    [Export] public string[] Immunities = [];
    [Export] public string[] Resistances = [];
    [Export] public string[] Weaknesses = [];
    /// <summary>
    /// 模板风味描述特质（"嗅觉追踪", "龙族恐惧" 等中文短语）。
    /// 当前仅供 UI 显示用（敌人详情面板等）；战斗逻辑不读。
    /// 与 UnitData.CharacterTraits（玩家角色生成的 TraitData[] 实例）无关。
    /// 如需"龙族恐惧"等机械效果，应转为 SpecialAbilities + EquipmentAbilityRegistry 走规则化分发。
    /// </summary>
    [Export] public string[] Traits = [];

    // 传奇专属
    [Export] public int LegendaryResistanceUses;
    [Export] public int LegendaryActionPoints;
    /// <summary>
    /// 传奇生物每回合可执行的额外动作（如龙的"翼击"/"尾扫"）。
    /// 模板里已有数据，但当前 AI / TurnManager 未消费。
    /// 计划：实装 LegendaryActionScheduler，每个非传奇单位回合结束让传奇生物
    /// 选 1 个 action 触发，消耗 LegendaryActionPoints。
    /// </summary>
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> LegendaryActions = new();
    /// <summary>
    /// 巢穴动作（巢穴战斗专属，每回合 init 20 触发的环境/陷阱效果）。
    /// 当前未实装，模板数据保留以便未来对接 LairBattleManager。
    /// </summary>
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> LairActions = new();
    /// <summary>
    /// 多阶段战斗（如龙在 50% HP 切换到飞行阶段）。
    /// 当前未实装，模板数据保留。
    /// </summary>
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> Phases = new();
    [Export] public string UniqueDropId { get; set; } = "";

    // ========================================
    // 战斗运行时状态 — 已完全迁移至 UnitRuntimeState
    // 访问方式：unit.Data.Runtime.Facing / .IsDefending / .ActiveStatusEffects 等
    // ========================================

    // （无残留字段 — 所有运行时状态均通过 Data.Runtime 访问）

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

    /// <summary>
    /// 角色自身能力（与装备能力分离）—
    /// 技能树解锁、种族特性、职业天赋等永久能力放这里。
    /// 战斗能力聚合时由 UnitAbilities.GetAll 自动包含。
    /// </summary>
    public System.Collections.Generic.List<BladeHex.Combat.Abilities.EquipmentAbility> IntrinsicAbilities { get; }
        = new();

    // ========================================
    // 枚举显示名方法
    // ========================================

   /// <summary>获取敌方类型的中文显示名</summary>
   public string GetEnemyTypeName()
   {
       return enemyType switch
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
   }

   /// <summary>获取 AI 策略的中文显示名</summary>
   public string GetAiStrategyName()
   {
       return aiStrategy switch
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
   }

   /// <summary>获取 CR（Challenge Rating）显示文本</summary>
   public string GetCrText()
   {
       return $"CR {ThreatLevel}";
   }

   /// <summary>从所有特质中聚合个性分数（用于 AI 策略选择）</summary>
   public System.Collections.Generic.Dictionary<string, float> GetAllPersonalityScores()
   {
       var scores = new System.Collections.Generic.Dictionary<string, float>
       {
           { "calculating", 0f },
           { "valor", 0f },
           { "mercy", 0f },
           { "honor", 0f },
       };

       if (CharacterTraits == null) return scores;

       foreach (var trait in CharacterTraits)
       {
           if (trait == null || trait.AiDirectionBonus == null) continue;
           foreach (var key in trait.AiDirectionBonus.Keys)
           {
               string keyStr = key.AsString();
               if (scores.ContainsKey(keyStr))
                   scores[keyStr] += (float)trait.AiDirectionBonus[key];
           }
       }

       return scores;
   }

    // ========================================
    // 装备逻辑
    // ========================================

    /// <summary>装备一个物品到对应槽位（纯数据操作，不涉及经济管理）</summary>
    public void EquipItem(ItemData item)
    {
        if (!CanEquipItemBySkillTree(item)) return;

        if (item is ArmorData armorItem)
        {
            switch (armorItem.armorType)
            {
                case ArmorData.ArmorType.Shield:
                    if (Shield != null || PrimaryOffHand is ArmorData { armorType: ArmorData.ArmorType.Shield })
                        UnequipItem("shield");
                    Shield = armorItem;
                    PrimaryOffHand = armorItem;
                    break;
                default:
                    // 根据 EquipSlotTarget 区分头盔/护手/身体
                    if (item.EquipSlotTarget == ItemData.EquipSlot.Helmet || item.EquipSlotTarget == ItemData.EquipSlot.Head)
                    {
                        if (Helmet != null) UnequipItem("helmet");
                        Helmet = armorItem;
                    }
                    else if (item.EquipSlotTarget == ItemData.EquipSlot.Hands)
                    {
                        if (Gauntlets != null) UnequipItem("gauntlets");
                        Gauntlets = armorItem;
                    }
                    else if (item.EquipSlotTarget == ItemData.EquipSlot.Feet)
                    {
                        if (Boots != null) UnequipItem("boots");
                        Boots = armorItem;
                    }
                    else
                    {
                        if (Armor != null) UnequipItem("armor");
                        Armor = armorItem;
                    }
                    break;
            }
        }
        else if (item is WeaponData)
        {
            if (PrimaryMainHand != null) UnequipItem("primary_main");
            PrimaryMainHand = (WeaponData)item;
        }
        else if (item is AccessoryData)
        {
            if (Accessory1 == null)
                Accessory1 = (AccessoryData)item;
            else if (Accessory2 == null)
                Accessory2 = (AccessoryData)item;
            else
            {
                UnequipItem("accessory_1");
                Accessory1 = (AccessoryData)item;
            }
        }
        RefreshAccessoryBonuses();
    }

    /// <summary>装备到指定槽位（显式指定槽位名）</summary>
    public void EquipToSlot(ItemData item, string slotName)
    {
        if (!CanEquipItemBySkillTree(item, slotName)) return;

        UnequipItem(slotName);
        switch (slotName)
        {
            case "helmet": Helmet = item as ArmorData; break;
            case "armor": case "body": Armor = item as ArmorData; break;
            case "gauntlets": Gauntlets = item as ArmorData; break;
            case "boots": Boots = item as ArmorData; break;
            case "shield": Shield = item as ArmorData; PrimaryOffHand = item; break;
            case "primary_main": case "main_hand": PrimaryMainHand = item as WeaponData; break;
            case "primary_off": case "off_hand":
                    PrimaryOffHand = item;
                    if (item is ArmorData armor2 && armor2.armorType == ArmorData.ArmorType.Shield)
                        Shield = armor2;
                    break;
            case "secondary_main": SecondaryMainHand = item as WeaponData; break;
            case "secondary_off": SecondaryOffHand = item; break;
            case "accessory_1": Accessory1 = item as AccessoryData; break;
            case "accessory_2": Accessory2 = item as AccessoryData; break;
        }
        RefreshAccessoryBonuses();
    }

    public bool CanEquipItemBySkillTree(ItemData item, string slotName = "")
    {
        if (item is ArmorData armor)
        {
            if (armor.armorType == ArmorData.ArmorType.Shield && !SkillTreeKeystoneResolver.CanEquipShield(this))
                return false;
            if ((armor.armorType == ArmorData.ArmorType.Medium || armor.armorType == ArmorData.ArmorType.Heavy)
                && !SkillTreeKeystoneResolver.CanEquipMediumOrHeavyArmor(this))
                return false;
        }

        if (item is WeaponData weapon)
        {
            bool goesMainHand = string.IsNullOrEmpty(slotName)
                || slotName is "primary_main" or "main_hand" or "secondary_main";
            if (goesMainHand && weapon.IsTwoHanded && !SkillTreeKeystoneResolver.CanEquipTwoHandedWeapon(this))
                return false;
        }

        return true;
    }

    public void SanitizeEquipmentBySkillTree()
    {
        if (Shield != null && !SkillTreeKeystoneResolver.CanEquipShield(this))
            UnequipItem("shield");

        if (PrimaryOffHand is ArmorData offArmor
            && offArmor.armorType == ArmorData.ArmorType.Shield
            && !SkillTreeKeystoneResolver.CanEquipShield(this))
            UnequipItem("primary_off");

        if (Armor != null
            && (Armor.armorType == ArmorData.ArmorType.Medium || Armor.armorType == ArmorData.ArmorType.Heavy)
            && !SkillTreeKeystoneResolver.CanEquipMediumOrHeavyArmor(this))
            UnequipItem("armor");

        if (PrimaryMainHand != null
            && PrimaryMainHand.IsTwoHanded
            && !SkillTreeKeystoneResolver.CanEquipTwoHandedWeapon(this))
            UnequipItem("primary_main");

        if (SecondaryMainHand != null
            && SecondaryMainHand.IsTwoHanded
            && !SkillTreeKeystoneResolver.CanEquipTwoHandedWeapon(this))
            UnequipItem("secondary_main");
    }

    /// <summary>卸下指定槽位的装备，返回被卸下的物品（纯数据操作）</summary>
    public ItemData? UnequipItem(string slot)
    {
        ItemData? removed = null;
        switch (slot)
        {
            case "armor":
                if (Armor != null) { removed = Armor; Armor = null; }
                break;
            case "shield":
                if (Shield != null) { removed = Shield; Shield = null; }
                if (PrimaryOffHand == removed) PrimaryOffHand = null;
                break;
            case "helmet":
                if (Helmet != null) { removed = Helmet; Helmet = null; }
                break;
            case "gauntlets":
                if (Gauntlets != null) { removed = Gauntlets; Gauntlets = null; }
                break;
            case "boots":
                if (Boots != null) { removed = Boots; Boots = null; }
                break;
            case "accessory_1":
                if (Accessory1 != null) { removed = Accessory1; Accessory1 = null; }
                break;
            case "accessory_2":
                if (Accessory2 != null) { removed = Accessory2; Accessory2 = null; }
                break;
            case "primary_main":
                if (PrimaryMainHand != null) { removed = PrimaryMainHand; PrimaryMainHand = null; }
                break;
            case "primary_off":
                if (PrimaryOffHand != null) { removed = PrimaryOffHand; PrimaryOffHand = null; }
                if (Shield == removed) Shield = null;
                break;
            case "secondary_main":
                if (SecondaryMainHand != null) { removed = SecondaryMainHand; SecondaryMainHand = null; }
                break;
            case "secondary_off":
                if (SecondaryOffHand != null) { removed = SecondaryOffHand; SecondaryOffHand = null; }
                break;
        }
        RefreshAccessoryBonuses();
        return removed;
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
    // 注意: 护甲对防御的贡献完全通过DR系统（max_dr / dr_threshold），不通过AC
    // AC由 Unit.GetAc() 计算：10 + DEX修正 + 盾牌AC + sqrt(DR)
    // ========================================

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
        if (Helmet != null) items.Add(Helmet);
        if (PrimaryMainHand != null) items.Add(PrimaryMainHand);
        if (PrimaryOffHand != null && PrimaryOffHand != Shield) items.Add(PrimaryOffHand);
        if (Shield != null) items.Add(Shield);
        if (SecondaryMainHand != null) items.Add(SecondaryMainHand);
        if (SecondaryOffHand != null) items.Add(SecondaryOffHand);
        if (Accessory1 != null) items.Add(Accessory1);
        if (Accessory2 != null) items.Add(Accessory2);
        return items.ToArray();
    }

    // ========================================
    // 运行时状态委托（新架构）
    // ========================================

    private Combat.UnitRuntimeState? _runtimeState;

    /// <summary>获取运行时状态实例（懒初始化）</summary>
    public Combat.UnitRuntimeState Runtime => _runtimeState ??= new Combat.UnitRuntimeState();

    /// <summary>是否已创建运行时状态</summary>
    public bool HasRuntimeState => _runtimeState != null;

    /// <summary>清除运行时状态（战斗结束时调用）</summary>
    public void ClearRuntimeState() => _runtimeState = null;

    // ========================================
    // 头像数据统一访问
    // ========================================

    /// <summary>
    /// 获取头像数据。优先返回 Avatar 属性的结构化数据，
    /// 如果 Avatar 为 null 则从旧式分散字段构造兼容数据。
    /// 保证返回值不为 null。
    /// </summary>
    public AvatarData GetAvatar()
    {
        if (Avatar != null)
            return Avatar;

        // 从旧式字段构造兼容数据
        int seed = System.Math.Abs((UnitName ?? "").GetHashCode() + CharacterId * 31);
        string gender = (!string.IsNullOrEmpty(GenderCustom)
            ? GenderCustom
            : ((seed % 2 == 0) ? "male" : "female"));

        return AvatarData.FromLegacy(
            Race?.raceId ?? RaceData.Race.Human,
            gender,
            FaceIndex > 0 ? FaceIndex : (seed % 9) + 1,
            HairIndex);
    }
}
