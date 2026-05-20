// CompositionTemplates.cs
// Sim 用阵容模板 — 每个模板 = 4 个角色（build profile + 队列位置）。
// 让 sim 反映"队伍 vs 队伍的战术配合"，而非单 build 互掐。
using System.Collections.Generic;

namespace BladeHex.Tests.Simulation;

/// <summary>单个阵容成员（build 名 + 队内列号 0=最前 / 3=最后）</summary>
public sealed record CompositionMember(string BuildName, int FrontalCol);

/// <summary>阵容模板</summary>
public sealed class CompositionTemplate
{
    public string ChineseName = "";
    public string Tag = "";
    public List<CompositionMember> Members = new();
}

/// <summary>
/// 8 个预设阵容。每个阵容固定 4 名成员，按 FrontalCol 0..3 排列：
/// 0 = 最前排（贴敌），3 = 最后排（远离敌）。Sim 会根据 col 分配实际坐标。
/// 摆位约定：玩家方 player col = teamCol；敌方 enemy col = 10 - teamCol；
/// 同 frontalCol 的成员按 row 错开。
/// </summary>
public static class CompositionTemplates
{
    private static List<CompositionTemplate>? _all;

    public static IReadOnlyList<CompositionTemplate> All
    {
        get { if (_all == null) Build(); return _all!; }
    }

    private static void Build()
    {
        _all = new List<CompositionTemplate>
        {
            // 平衡：每职责 1 名
            new() { ChineseName = "平衡阵", Tag = "balanced", Members = {
                new("守卫", 0),     // 前排
                new("重战士", 1),   // 二排
                new("游侠", 2),     // 后排
                new("法师", 3),     // 最后
            }},
            // 重甲：双坦
            new() { ChineseName = "重甲阵", Tag = "heavy", Members = {
                new("守卫", 0),
                new("守卫", 0),
                new("重战士", 1),
                new("法师", 3),
            }},
            // 狂攻：纯输出，无坦克
            new() { ChineseName = "狂攻阵", Tag = "berserk", Members = {
                new("重战士", 0),
                new("剑舞者", 1),
                new("决斗家", 1),
                new("决斗家", 2),
            }},
            // 远程：远射群
            new() { ChineseName = "远程阵", Tag = "ranged", Members = {
                new("守卫", 0),
                new("游侠", 2),
                new("猎人", 2),
                new("法师", 3),
            }},
            // 法术：双法师
            new() { ChineseName = "法术阵", Tag = "spell", Members = {
                new("守卫", 0),
                new("法师", 3),
                new("法师", 3),
                new("战法师", 1),
            }},
            // 机动：纯 DEX/CON
            new() { ChineseName = "机动阵", Tag = "mobile", Members = {
                new("决斗家", 0),
                new("决斗家", 1),
                new("决斗家", 1),
                new("决斗家", 2),
            }},
            // 统帅：CHA 系领袖 + 输出
            new() { ChineseName = "统帅阵", Tag = "command", Members = {
                new("重战士", 0),
                new("战神", 1),
                new("守卫", 0),
                new("法师", 3),
            }},
            // 专精坦克：纯守卫（经济警告 — 仅作参考）
            new() { ChineseName = "纯坦阵", Tag = "all_tank", Members = {
                new("守卫", 0),
                new("守卫", 0),
                new("守卫", 0),
                new("守卫", 0),
            }},
            // 刺杀阵：1 守卫 + 2 刺客 + 1 法师（暴击爆发 + 法术 + 1 坦顶前）
            new() { ChineseName = "刺杀阵", Tag = "assassin", Members = {
                new("守卫", 0),
                new("刺客", 1),
                new("刺客", 1),
                new("法师", 3),
            }},
            // 圣战阵：4 守护骑士（STR + WIS）— 重武器 + 高暴击曲线 + Mana 续航
            // 用技能跳跃跳过中间节点直奔 STR/WIS 高级
            new() { ChineseName = "圣战阵", Tag = "paladin", Members = {
                new("守护骑士", 0),
                new("守护骑士", 1),
                new("守护骑士", 1),
                new("守护骑士", 2),
            }},
            // 圣战混编：1 守卫 + 2 守护骑士 + 1 法师
            new() { ChineseName = "圣骑混编", Tag = "paladin_mix", Members = {
                new("守卫", 0),
                new("守护骑士", 1),
                new("守护骑士", 1),
                new("法师", 3),
            }},
            // 纯战士阵：4 战士（STR 单属性）— 对照看 WIS 暴击副属性是否真的有用
            new() { ChineseName = "纯战阵", Tag = "all_warrior", Members = {
                new("战士", 0),
                new("战士", 1),
                new("战士", 1),
                new("战士", 2),
            }},
            // 纯重战阵：4 重战士（STR+CON）— 对照圣战阵看 WIS 副 vs CON 副哪个强
            new() { ChineseName = "纯重战", Tag = "all_juggernaut", Members = {
                new("重战士", 0),
                new("重战士", 1),
                new("重战士", 1),
                new("重战士", 2),
            }},
        };
    }

    public static CompositionTemplate? GetByTag(string tag)
    {
        foreach (var t in All) if (t.Tag == tag) return t;
        return null;
    }
}
