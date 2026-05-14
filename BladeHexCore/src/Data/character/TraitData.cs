// TraitData.cs
// 特质数据资源 — 角色生成时随机获得的特质
// 对应策划案 05-角色与职业.md → 随机特质
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class TraitData : Resource
{
    // ========================================
    // 枚举
    // ========================================

    public enum TraitType
    {
        Attribute,  // 属性类特质（增减基础属性值）
        Functional, // 功能性特质（提供独特效果）
    }

    // ========================================
    // 数据字段
    // ========================================

    [Export] public string TraitId { get; set; } = "";
    [Export] public string TraitName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public TraitType traitType = TraitType.Attribute;

    // 属性修正
    [Export] public int StrMod;
    [Export] public int DexMod;
    [Export] public int ConMod;
    [Export] public int IntMod;
    [Export] public int WisMod;
    [Export] public int ChaMod;

    // 非属性效果（功能性特质）
    [Export] public string FunctionalEffect { get; set; } = "";
    [Export] public float EffectValue;
    [Export] public float Weight { get; set; } = 1.0f;

    // AI加点方向微调
    [Export] public Godot.Collections.Dictionary AiDirectionBonus = new();

    // ========================================
    // 静态工厂：返回所有预定义特质
    // ========================================

    public static TraitData[] GetAllTraits()
    {
        var traits = new System.Collections.Generic.List<TraitData>();

        // ====== 属性类特质（19个）======
        traits.Add(Make("brute_force", "蛮力", "天生力气大，适合近战", 2, 0, 0, 0, 0, 0, 1.0f, new Godot.Collections.Dictionary { { "str", 0.2f } }));
        traits.Add(Make("long_arms", "长臂", "近战射程+1（可攻击2格）", 0, 0, 0, 0, 0, 0, 0.5f, new Godot.Collections.Dictionary { { "str", 0.2f } }, "long_arm"));
        traits.Add(Make("quick_hands", "快手", "灵活，适合远程/闪避", 0, 2, 0, 0, 0, 0, 1.0f, new Godot.Collections.Dictionary { { "dex", 0.2f } }));
        traits.Add(Make("eagle_eye", "猫眼", "远程命中+1", 0, 0, 0, 0, 0, 0, 0.5f, new Godot.Collections.Dictionary { { "dex", 0.2f } }, "eagle_eye"));
        traits.Add(Make("thick_bones", "硬骨头", "抗打，HP多", 0, 0, 2, 0, 0, 0, 1.0f, new Godot.Collections.Dictionary { { "con", 0.1f } }));
        traits.Add(Make("sturdy", "壮硕", "耐打但笨重，速度-1", 0, 0, 1, 0, 0, 0, 0.8f, new Godot.Collections.Dictionary { { "con", 0.1f } }, "speed", -1.0f));
        traits.Add(Make("great_mind", "过人慧心", "聪明，适合法术", 0, 0, 0, 2, 0, 0, 1.0f, new Godot.Collections.Dictionary { { "int", 0.2f } }));
        traits.Add(Make("spell_memory", "博闻强记", "法术位+1（每阶）", 0, 0, 0, 0, 0, 0, 0.3f, new Godot.Collections.Dictionary { { "int", 0.3f } }, "spell_memory"));
        traits.Add(Make("keen_intuition", "敏锐直觉", "洞察力强，适合治疗/侦察", 0, 0, 0, 0, 2, 0, 1.0f, new Godot.Collections.Dictionary { { "wis", 0.2f } }));
        traits.Add(Make("alertness", "警觉", "先攻+3，被动侦察范围+1", 0, 0, 0, 0, 0, 0, 0.5f, new Godot.Collections.Dictionary { { "wis", 0.1f } }, "alertness"));
        traits.Add(Make("born_leader", "天生统帅", "领袖气质，适合指挥", 0, 0, 0, 0, 0, 2, 1.0f, new Godot.Collections.Dictionary { { "cha", 0.2f } }));
        traits.Add(Make("affinity", "亲和力", "商店价格-15%，招募价格-10%", 0, 0, 0, 0, 0, 0, 0.5f, new Godot.Collections.Dictionary { { "cha", 0.1f } }, "affinity"));
        traits.Add(Make("reckless_brave", "蛮勇", "能打但鲁莽", 1, 0, 1, 0, -2, 0, 0.7f, new Godot.Collections.Dictionary { { "str", 0.1f } }));
        traits.Add(Make("sorcerer_blood", "术士血脉", "天生施法者", 0, 0, 0, 0, 0, 2, 0.3f, new Godot.Collections.Dictionary { { "int", 0.3f } }, "sorcerer_blood"));
        traits.Add(Make("frail_build", "瘦弱", "力量不足但灵活", -1, 1, 0, 0, 0, 0, 0.8f));
        traits.Add(Make("sluggish", "迟缓", "慢但结实", 0, -2, 1, 0, 0, 0, 0.8f));
        traits.Add(Make("fragile", "脆弱", "容易受伤", 0, 0, -2, 0, 0, 0, 0.8f));
        traits.Add(Make("dull", "愚钝", "不适合法术", 0, 0, 0, -2, 0, 0, 0.8f));
        traits.Add(Make("clumsy", "笨拙", "不适合远程", 0, -1, 0, 0, 0, 0, 0.8f, new Godot.Collections.Dictionary(), "ranged_hit_minus_1"));
        traits.Add(Make("bad_talker", "笨嘴拙舌", "社交困难", 0, 0, 0, 0, 0, -2, 0.8f));

        // ====== 功能性特质（11个）======
        traits.Add(MakeFunc("night_vision", "夜视", "获得黑暗视觉，洞穴/夜间无惩罚", "dark_vision", 0.5f, new Godot.Collections.Dictionary { { "wis", 0.1f } }));
        traits.Add(MakeFunc("iron_stomach", "铁胃", "免疫食物中毒，长途行军安全", "iron_stomach", 0.5f));
        traits.Add(MakeFunc("adaptability", "适应力", "疲劳惩罚减半，长途行军优势", "adaptability", 0.4f));
        traits.Add(MakeFunc("thick_skin_trait", "厚皮", "受到物理伤害-1，全局减伤", "thick_skin", 0.3f));
        traits.Add(MakeFunc("indomitable", "不屈", "HP归零时50%概率保持1HP（每战1次）", "indomitable", 0.2f));
        traits.Add(MakeFunc("ether_resonance", "以太共鸣", "施法时恢复1d4 HP", "ether_resonance", 0.3f, new Godot.Collections.Dictionary { { "int", 0.1f } }));
        traits.Add(MakeFunc("premonition", "预感", "被伏击时自动获得一轮准备", "premonition", 0.3f));
        traits.Add(MakeFunc("old_injury", "旧伤", "战斗开始时HP-10%", "old_wound", 0.6f, new Godot.Collections.Dictionary { { "con", 0.1f } }));
        traits.Add(MakeFunc("gluttony", "贪吃", "补给消耗×1.5", "gluttony", 0.5f));
        traits.Add(MakeFunc("timid", "胆小", "HP<50%时攻击-1", "timid", 0.5f));
        traits.Add(MakeFunc("xenophobia", "仇外", "与外族队友在一起时忠诚度-10", "xenophobia", 0.4f));

        return traits.ToArray();
    }

    public static TraitData[] GetAttributeTraits()
    {
        var result = new System.Collections.Generic.List<TraitData>();
        foreach (var t in GetAllTraits())
            if (t.traitType == TraitType.Attribute)
                result.Add(t);
        return result.ToArray();
    }

    public static TraitData[] GetFunctionalTraits()
    {
        var result = new System.Collections.Generic.List<TraitData>();
        foreach (var t in GetAllTraits())
            if (t.traitType == TraitType.Functional)
                result.Add(t);
        return result.ToArray();
    }

    // ========================================
    // 内部工厂方法
    // ========================================

    private static TraitData Make(string id, string name, string desc,
        int str, int dex, int con, int intel, int wis, int cha,
        float w, Godot.Collections.Dictionary? aiDir = null,
        string funcEff = "", float effVal = 0.0f)
    {
        var t = new TraitData();
        t.TraitId = id;
        t.TraitName = name;
        t.Description = desc;
        t.traitType = TraitType.Attribute;
        t.StrMod = str;
        t.DexMod = dex;
        t.ConMod = con;
        t.IntMod = intel;
        t.WisMod = wis;
        t.ChaMod = cha;
        t.FunctionalEffect = funcEff;
        t.EffectValue = effVal;
        t.Weight = w;
        t.AiDirectionBonus = aiDir ?? new Godot.Collections.Dictionary();
        return t;
    }

    private static TraitData MakeFunc(string id, string name, string desc,
        string effect, float w, Godot.Collections.Dictionary? aiDir = null)
    {
        var t = new TraitData();
        t.TraitId = id;
        t.TraitName = name;
        t.Description = desc;
        t.traitType = TraitType.Functional;
        t.FunctionalEffect = effect;
        t.Weight = w;
        t.AiDirectionBonus = aiDir ?? new Godot.Collections.Dictionary();
        return t;
    }
}
