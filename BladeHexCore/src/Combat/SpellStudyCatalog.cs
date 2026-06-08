using Godot;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.Combat;

public sealed class SpellStudyOption
{
    public string SchoolKey { get; init; } = "";
    public string SchoolName { get; init; } = "";
    public string SpellName { get; init; } = "";
    public string Description { get; init; } = "";
    public SpellData Spell { get; init; } = new();
}

public static class SpellStudyCatalog
{
    public const string EquippedSpellPrefix = "spell:";

    public static bool IsEquippedSpellEntry(string entry)
        => !string.IsNullOrEmpty(entry) && entry.StartsWith(EquippedSpellPrefix, System.StringComparison.Ordinal);

    public static string GetSpellIdFromEntry(string entry)
        => IsEquippedSpellEntry(entry) ? entry[EquippedSpellPrefix.Length..] : "";

    public static string MakeEquippedSpellEntry(string spellId)
        => string.IsNullOrEmpty(spellId) ? "" : $"{EquippedSpellPrefix}{spellId}";

    public static bool IsSpellSlotEffect(string effect)
        => !string.IsNullOrEmpty(effect) && effect.StartsWith("spell_slot_", System.StringComparison.Ordinal);

    public static int GetTierFromSpellSlotEffect(string effect)
    {
        const string prefix = "spell_slot_";
        if (!effect.StartsWith(prefix, System.StringComparison.Ordinal)) return 0;
        return int.TryParse(effect[prefix.Length..], out int tier) ? Mathf.Clamp(tier, 1, 5) : 0;
    }

    public static SpellStudyOption[] GetOptions(int tier)
    {
        return tier switch
        {
            1 =>
            [
                Make("destruction", "毁灭", Spark()),
                Make("illusion", "幻术", Blur()),
                Make("enchantment", "附魔", Bless()),
                Make("abjuration", "防护", MageArmor()),
                Make("life", "生命", CureLight()),
            ],
            2 =>
            [
                Make("destruction", "毁灭", IceCone()),
                Make("illusion", "幻术", MirrorImage()),
                Make("enchantment", "附魔", Haste()),
                Make("abjuration", "防护", Counterspell()),
                Make("life", "生命", CureSerious()),
            ],
            3 =>
            [
                Make("destruction", "毁灭", Fireball()),
                Make("illusion", "幻术", Invisibility()),
                Make("enchantment", "附魔", MassBless()),
                Make("abjuration", "防护", MagicResistance()),
                Make("life", "生命", MassHeal()),
            ],
            4 =>
            [
                Make("destruction", "毁灭", ChainLightning()),
                Make("illusion", "幻术", TimeStop()),
                Make("enchantment", "附魔", HeroicImbue()),
                Make("abjuration", "防护", ForceBarrier()),
                Make("life", "生命", Regeneration()),
            ],
            5 =>
            [
                Make("destruction", "毁灭", Thunderstorm()),
                Make("illusion", "幻术", ShadowDouble()),
                Make("enchantment", "附魔", BattleCommand()),
                Make("abjuration", "防护", ArcaneBastion()),
                Make("life", "生命", SongOfLife()),
            ],
            _ => [],
        };
    }

    public static SpellData? CreateRandomSpellForTier(int tier)
    {
        var options = GetOptions(tier);
        if (options.Length == 0) return null;
        return options[System.Random.Shared.Next(options.Length)].Spell;
    }

    public static SpellData? CreateRandomSpellForTier(UnitData data, int tier)
    {
        var options = GetOptions(tier)
            .Where(o => SkillTreeKeystoneResolver.CanStudySpell(data, o.Spell))
            .ToArray();
        if (options.Length == 0) return null;
        return options[System.Random.Shared.Next(options.Length)].Spell;
    }

    public static SpellData? FindById(string spellId)
    {
        for (int tier = 1; tier <= 5; tier++)
            foreach (var option in GetOptions(tier))
                if (option.Spell.SpellId == spellId) return option.Spell;
        return null;
    }

    public static bool HasSpell(UnitData data, string spellId)
    {
        foreach (var spell in data.KnownSpells)
            if (spell != null && spell.SpellId == spellId) return true;
        return false;
    }

    public static SpellData? GetKnownSpell(UnitData data, string spellId)
    {
        foreach (var spell in data.KnownSpells)
            if (spell != null && spell.SpellId == spellId) return spell;
        return null;
    }

    public static string GetKnownSpellNameForTier(UnitData data, int tier)
    {
        foreach (var spell in data.KnownSpells)
            if (spell != null && (int)spell.tier == tier) return spell.SpellName;
        return "";
    }

    private static SpellStudyOption Make(string schoolKey, string schoolName, SpellData spell)
        => new() { SchoolKey = schoolKey, SchoolName = schoolName, SpellName = spell.SpellName, Description = spell.Description, Spell = spell };

    private static SpellData Base(string id, string name, int tier, SpellData.SpellSchool school, int ap, int mana, int range, SpellData.SpellShape shape, int shapeSize, SpellData.ResolutionType resolution)
        => new()
        {
            SpellId = id,
            SpellName = name,
            tier = (SpellData.SpellTier)tier,
            spellSchool = school,
            ManaCost = mana,
            CooldownTurns = SpellData.GetDefaultCooldown((SpellData.SpellTier)tier),
            castingTime = ap <= 0 ? SpellData.CastingTime.Reaction : SpellData.CastingTime.MainAction,
            RangeCells = range,
            shape = shape,
            ShapeSize = shapeSize,
            resolutionType = resolution,
        };

    private static SpellData Spark()
    {
        var s = Base("spark", "火花", 1, SpellData.SpellSchool.Evocation, 3, 4, 6, SpellData.SpellShape.Single, 1, SpellData.ResolutionType.AutoHit);
        s.Description = "单体 6 格，造成 1d8 火焰真伤。";
        s.DamageDiceCount = 1; s.DamageDiceSides = 8; s.DamageType = "fire";
        return s;
    }

    private static SpellData IceCone()
    {
        var s = Base("ice_cone", "冰锥术", 2, SpellData.SpellSchool.Evocation, 4, 8, 4, SpellData.SpellShape.Cone, 4, SpellData.ResolutionType.Save);
        s.Description = "锥形 4 格，造成 3d6 冰霜真伤，DEX 豁免半伤。";
        s.DamageDiceCount = 3; s.DamageDiceSides = 6; s.DamageType = "frost"; s.saveType = SpellData.SaveType.DexSave; s.AppliedStatusEffect = "slow"; s.StatusDuration = 1;
        return s;
    }

    private static SpellData Fireball()
    {
        var s = Base("fireball", "火球术", 3, SpellData.SpellSchool.Evocation, 6, 12, 6, SpellData.SpellShape.Sphere, 1, SpellData.ResolutionType.Save);
        s.Description = "半径 1，造成 4d6 火焰真伤，DEX 豁免半伤。";
        s.DamageDiceCount = 4; s.DamageDiceSides = 6; s.DamageType = "fire"; s.saveType = SpellData.SaveType.DexSave; s.AppliedStatusEffect = "burning"; s.StatusDuration = 2;
        return s;
    }

    private static SpellData ChainLightning()
    {
        var s = Base("chain_lightning_spell", "连锁闪电", 4, SpellData.SpellSchool.Evocation, 7, 16, 8, SpellData.SpellShape.Sphere, 2, SpellData.ResolutionType.AutoHit);
        s.Description = "范围内敌人受到 5d6 雷电真伤。";
        s.DamageDiceCount = 5; s.DamageDiceSides = 6; s.DamageType = "lightning";
        return s;
    }

    private static SpellData Thunderstorm()
    {
        var s = Base("thunderstorm", "雷暴", 5, SpellData.SpellSchool.Evocation, 8, 24, 8, SpellData.SpellShape.Sphere, 2, SpellData.ResolutionType.Save);
        s.Description = "半径 2，造成 6d8 雷电真伤，DEX 豁免半伤。";
        s.DamageDiceCount = 6; s.DamageDiceSides = 8; s.DamageType = "lightning"; s.saveType = SpellData.SaveType.DexSave; s.AppliedStatusEffect = "stun"; s.StatusDuration = 1;
        return s;
    }

    private static SpellData Blur() => BuffSelf("blur", "模糊术", 1, SpellData.SpellSchool.Illusion, 3, 4, "mirror_image", "自身获得 3 回合幻影防护。");
    private static SpellData MirrorImage() => BuffSelf("mirror_image_spell", "镜像", 2, SpellData.SpellSchool.Illusion, 4, 8, "mirror_image", "自身获得 3 回合镜影分身。");
    private static SpellData Invisibility() => BuffSingle("invisibility", "隐形术", 3, SpellData.SpellSchool.Illusion, 5, 12, "stealth", "单体友军获得隐形。");
    private static SpellData TimeStop() => BuffSelf("time_stop", "时间停滞", 4, SpellData.SpellSchool.Illusion, 6, 16, "haste", "自身获得短暂额外行动节奏。");
    private static SpellData ShadowDouble() => BuffSelf("shadow_double", "影武者分身", 5, SpellData.SpellSchool.Illusion, 8, 24, "mirror_image", "自身获得强力镜影防护。");

    private static SpellData Bless() => BuffSingle("bless_spell", "祈福", 1, SpellData.SpellSchool.Enchantment, 3, 4, "bless", "单体友军攻击与防御提升。");
    private static SpellData Haste() => BuffSingle("haste_spell", "迅捷术", 2, SpellData.SpellSchool.Enchantment, 4, 8, "haste", "单体友军移动与节奏提升。");
    private static SpellData MassBless() => BuffAoe("mass_bless", "群体祈福", 3, SpellData.SpellSchool.Enchantment, 5, 12, "bless", "半径 1 友军获得祈福。");
    private static SpellData HeroicImbue() => BuffSingle("heroic_imbue", "英勇加持", 4, SpellData.SpellSchool.Enchantment, 6, 16, "bless", "单体友军获得强力战斗加持。");
    private static SpellData BattleCommand() => BuffAoe("battle_command", "战斗号令", 5, SpellData.SpellSchool.Enchantment, 8, 24, "bless", "半径 2 友军获得爆发性加持。");

    private static SpellData MageArmor() => BuffSelf("mage_armor", "法师护甲", 1, SpellData.SpellSchool.Abjuration, 3, 4, "shield", "自身获得魔法护甲。");
    private static SpellData Counterspell() => BuffSelf("counterspell", "反法术", 2, SpellData.SpellSchool.Abjuration, 0, 8, "shield", "获得一层反制用防护。");
    private static SpellData MagicResistance() => BuffSingle("magic_resistance", "魔法抗性", 3, SpellData.SpellSchool.Abjuration, 5, 12, "shield", "单体友军获得魔法抗性。");
    private static SpellData ForceBarrier() => BuffSingle("force_barrier", "力场屏障", 4, SpellData.SpellSchool.Abjuration, 6, 16, "shield", "目标获得力场防护。");
    private static SpellData ArcaneBastion() => BuffAoe("arcane_bastion", "奥术壁垒", 5, SpellData.SpellSchool.Abjuration, 8, 24, "shield", "半径 1 友军获得强力防护。");

    private static SpellData CureLight() => HealSingle("cure_light", "治疗轻伤", 1, 3, 4, 1, 8, "单体友军恢复 1d8 生命。");
    private static SpellData CureSerious() => HealSingle("cure_serious", "治疗重伤", 2, 4, 8, 2, 8, "单体友军恢复 2d8 生命。");
    private static SpellData MassHeal() => HealAoe("mass_heal", "群体治疗", 3, 6, 14, 2, 6, "半径 1 友军恢复 2d6 生命。");
    private static SpellData Regeneration() => BuffSingle("regeneration", "再生术", 4, SpellData.SpellSchool.Necromancy, 6, 14, "regen", "单体友军获得再生。");
    private static SpellData SongOfLife() => HealAoe("song_of_life", "生命之歌", 5, 8, 24, 4, 8, "半径 2 友军恢复 4d8 生命。");

    private static SpellData BuffSelf(string id, string name, int tier, SpellData.SpellSchool school, int ap, int mana, string buff, string desc)
        => Buff(id, name, tier, school, ap, mana, 0, SpellData.SpellShape.Self, 0, buff, desc);

    private static SpellData BuffSingle(string id, string name, int tier, SpellData.SpellSchool school, int ap, int mana, string buff, string desc)
        => Buff(id, name, tier, school, ap, mana, 3, SpellData.SpellShape.Single, 1, buff, desc);

    private static SpellData BuffAoe(string id, string name, int tier, SpellData.SpellSchool school, int ap, int mana, string buff, string desc)
        => Buff(id, name, tier, school, ap, mana, 3, SpellData.SpellShape.Sphere, tier >= 5 ? 2 : 1, buff, desc);

    private static SpellData Buff(string id, string name, int tier, SpellData.SpellSchool school, int ap, int mana, int range, SpellData.SpellShape shape, int shapeSize, string buff, string desc)
    {
        var s = Base(id, name, tier, school, ap, mana, range, shape, shapeSize, SpellData.ResolutionType.AutoHit);
        s.Description = desc; s.AppliedStatusEffect = buff; s.StatusDuration = tier >= 5 ? 1 : 3;
        return s;
    }

    private static SpellData HealSingle(string id, string name, int tier, int ap, int mana, int diceCount, int diceSides, string desc)
    {
        var s = Base(id, name, tier, SpellData.SpellSchool.Necromancy, ap, mana, 3, SpellData.SpellShape.Single, 1, SpellData.ResolutionType.AutoHit);
        s.Description = desc; s.HealDiceCount = diceCount; s.HealDiceSides = diceSides;
        return s;
    }

    private static SpellData HealAoe(string id, string name, int tier, int ap, int mana, int diceCount, int diceSides, string desc)
    {
        var s = Base(id, name, tier, SpellData.SpellSchool.Necromancy, ap, mana, 3, SpellData.SpellShape.Sphere, tier >= 5 ? 2 : 1, SpellData.ResolutionType.AutoHit);
        s.Description = desc; s.HealDiceCount = diceCount; s.HealDiceSides = diceSides;
        return s;
    }
}
