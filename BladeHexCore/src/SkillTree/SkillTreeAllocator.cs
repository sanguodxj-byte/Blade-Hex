// SkillTreeAllocator.cs
// Build a CharacterSkillTree for a UnitData and auto-allocate attribute points
// based on the unit's primary stat. Same policy used by QuickCombat / sim.
//
// Lives in Core so headless simulation can call it without depending on the
// Godot autoload SkillTreeManager.
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.Strategic;

/// <summary>
/// Static helper that creates a fresh skill tree for a unit and runs the AI
/// allocation policy ("most points into the unit's primary attribute").
/// </summary>
public static class SkillTreeAllocator
{
    /// <summary>
    /// Create a CharacterSkillTree for <paramref name="data"/> at the given
    /// level, allocate ~5 + (level-1) points, register jump charges, then run
    /// <see cref="CharacterSkillTree.AiAllocatePoints"/> with the unit's
    /// primary + secondary attributes.
    /// </summary>
    /// <param name="data">Unit to allocate for. Must have non-null stats.</param>
    /// <param name="treeData">
    /// Shared skill-tree graph. Pass <c>null</c> to build a fresh one (cheap).
    /// In production code you should pass the cached singleton from
    /// <c>SkillTreeManager.TreeData</c> to avoid redundant allocation.
    /// </param>
    /// <param name="aiAccuracy">0..1, how accurately AI follows the priority order.</param>
    /// <returns>The configured tree, or null if data is invalid.</returns>
    public static CharacterSkillTree? AllocateForUnit(
        UnitData data,
        SkillTreeData? treeData = null,
        float aiAccuracy = 0.8f)
    {
        if (data == null) return null;

        var graph = treeData ?? new SkillTreeData();
        var tree = new CharacterSkillTree(graph, data.Level, GetUnitRandomAttributeSeed(data));

        int points = 5 + (data.Level - 1);
        tree.AddAttributePoint(points);

        int jumps = 1 + data.Level / 5;
        if (jumps > 6) jumps = 6;
        for (int i = 0; i < jumps; i++)
            tree.RegisterJump();

        string primary = GetPrimaryAttr(data);
        string secondary = GetSecondaryAttr(data, primary);
        tree.AiAllocatePoints(aiAccuracy, primary, secondary);

        // v0.7: 自动装备前 N 个解锁的主动技能到 EquippedSkills（最多 10 个）。
        // 玩家可在技能盘 UI 底部手动调整。新角色一上来就能直接使用，不需要打开
        // 技能盘做初始化操作。已有装备配置不覆盖（保护跨战斗装备）。
        AutoEquipSkills(data, tree);

        return tree;
    }

    /// <summary>
    /// 把已激活的主动技能填入 UnitData.EquippedSkills（最多 10 个）。
    /// 已存在的装备配置不动；只填空槽。
    /// </summary>
    public static void AutoEquipSkills(UnitData data, CharacterSkillTree tree)
    {
        if (data == null || tree == null) return;

        // 扩容到 10 槽
        while (data.EquippedSkills.Count < UnitData.MaxEquippedSkills)
            data.EquippedSkills.Add("");

        LearnAndEquipKnownSpells(data, tree);

        foreach (var node in tree.GetActiveSkills())
        {
            string effect = node.SkillEffect;
            if (string.IsNullOrEmpty(effect)) continue;
            if (!IsAutoEquippableSkillEffect(effect)) continue;
            if (data.IsSkillEquipped(effect)) continue;
            int slot = data.FindFirstEmptyEquippedSlot();
            if (slot < 0) break; // 槽位已满
            data.SetEquippedSkill(slot, effect);
        }
    }

    private static void LearnAndEquipKnownSpells(UnitData data, CharacterSkillTree tree)
    {
        foreach (var effect in tree.GetActiveSkillEffects())
        {
            if (!SpellStudyCatalog.IsSpellSlotEffect(effect))
                continue;

            int tier = SpellStudyCatalog.GetTierFromSpellSlotEffect(effect);
            if (tier <= 0 || SpellStudyCatalog.GetKnownSpellNameForTier(data, tier) != "")
                continue;

            var spell = SpellStudyCatalog.CreateRandomSpellForTier(data, tier);
            if (spell != null && !SpellStudyCatalog.HasSpell(data, spell.SpellId))
                data.KnownSpells.Add(spell);
        }

        EquipKnownSpells(data);
    }

    private static void EquipKnownSpells(UnitData data)
    {
        foreach (var spell in data.KnownSpells)
        {
            if (spell == null || string.IsNullOrEmpty(spell.SpellId)) continue;

            string entry = SpellStudyCatalog.MakeEquippedSpellEntry(spell.SpellId);
            if (data.IsSkillEquipped(entry)) continue;

            int slot = data.FindFirstEmptyEquippedSlot();
            if (slot < 0) break;
            data.SetEquippedSkill(slot, entry);
        }
    }

    private static bool IsAutoEquippableSkillEffect(string skillEffect)
    {
        return !string.IsNullOrEmpty(skillEffect)
            && !SpellStudyCatalog.IsSpellSlotEffect(skillEffect);
    }

    /// <summary>Highest of STR / DEX / INT / WIS — lowercase for AiAllocatePoints' API.</summary>
    public static string GetPrimaryAttr(UnitData data)
    {
        if (data == null) return "str";
        int max = data.Str;
        string attr = "str";
        if (data.Dex > max)   { max = data.Dex; attr = "dex"; }
        if (data.Intel > max) { max = data.Intel; attr = "int"; }
        if (data.Wis > max)   { max = data.Wis; attr = "wis"; }
        return attr;
    }

    /// <summary>Second highest, excluding the primary.</summary>
    public static string GetSecondaryAttr(UnitData data, string primary)
    {
        if (data == null) return "dex";
        var attrs = new (string name, int val)[]
        {
            ("str", data.Str), ("dex", data.Dex), ("con", data.Con),
            ("int", data.Intel), ("wis", data.Wis), ("cha", data.Cha),
        };
        System.Array.Sort(attrs, (a, b) => b.val.CompareTo(a.val));
        return attrs[0].name == primary ? attrs[1].name : attrs[0].name;
    }

    private static int GetUnitRandomAttributeSeed(UnitData data)
    {
        unchecked
        {
            int id = data.CharacterId != 0 ? data.CharacterId : data.UnitName?.GetHashCode() ?? data.Level;
            return (id * 1103515245 + 12345) & 0x7fffffff;
        }
    }
}
