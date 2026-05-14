// CareerSkillExecutor.cs
// 职业专属技能执行引擎 — 63个职业技能的战斗执行逻辑
// 数据来源: docs/职业专精技能.md
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 职业专属技能执行引擎 — 处理主动职业技能的战斗执行
/// 被动职业技能效果由 CareerSkillResolver 查询
/// </summary>
public static class CareerSkillExecutor
{
    // ============================================================================
    // 主入口
    // ============================================================================

    /// <summary>执行职业技能</summary>
    public static Godot.Collections.Dictionary ExecuteCareerSkill(
        Unit caster,
        Vector2I targetCell,
        HexGrid? grid,
        IEnumerable<Unit> allUnits,
        IEnumerable<Unit> playerUnits,
        IEnumerable<Unit> enemyUnits)
    {
        var skill = caster.GetCareerSkill();
        if (skill == null || !skill.IsActive)
            return Fail("没有可用的主动职业技能");

        if (!caster.CanUseCareerSkill())
            return Fail("职业技能本战斗已使用过");

        if (caster.CurrentAp < skill.ApCost)
            return Fail($"AP不足（需要{skill.ApCost}）");

        var allies = !caster.Data!.IsEnemy ? playerUnits : enemyUnits;
        var enemies = !caster.Data!.IsEnemy ? enemyUnits : playerUnits;

        var result = NewResult(skill);

        switch (skill.EffectId)
        {
            // 单属性 (2个主动)
            case "warrior_armor_break": ExecArmorBreak(caster, targetCell, grid, enemies, result); break;
            case "guardian_living_wall": ExecLivingWall(caster, result); break;
            case "mage_arcane_overload": ExecArcaneOverload(caster, result); break;
            case "assassin_expose_weakness": ExecExposeWeakness(caster, targetCell, grid, allUnits, result); break;
            case "bard_battle_hymn": ExecBattleHymn(caster, result); break;

            // 双属性 (9个主动)
            case "bladedancer_whirling_strike": ExecWhirlingStrike(caster, grid, enemies, result); break;
            case "spellsword_rune_imbue": ExecRuneImbue(caster, result); break;
            case "arcanearcher_homing_shot": ExecHomingShot(caster, targetCell, grid, enemies, result); break;
            case "falconer_hawks_mark": ExecHawksMark(caster, targetCell, grid, allUnits, result); break;
            case "rogue_misdirection": ExecMisdirection(caster, targetCell, enemies, result); break;
            case "ironcommander_hold_the_line": ExecHoldTheLine(caster, allies, result); break;
            case "sorcerer_blood_resonance": ExecBloodResonance(caster, result); break;

            // 三属性 (14个主动)
            case "bruiser_iron_rush": ExecIronRush(caster, targetCell, grid, enemies, allies, result); break;
            case "spellweaver_instant_glyph": ExecInstantGlyph(caster, targetCell, result); break;
            case "hawkeye_kill_shot": ExecKillShot(caster, targetCell, grid, enemies, result); break;
            case "champion_war_cry_charge": ExecWarCryCharge(caster, targetCell, grid, enemies, allies, result); break;
            case "ironweaver_rune_barricade": ExecRuneBarricade(caster, targetCell, result); break;
            case "conqueror_subjugate": ExecSubjugate(caster, targetCell, grid, enemies, result); break;
            case "doomknight_gaze_of_ruin": ExecGazeOfRuin(caster, targetCell, grid, allUnits, result); break;
            case "crusader_arcane_charge": ExecArcaneCharge(caster, targetCell, grid, enemies, result); break;
            case "shadowmage_shadow_swap": ExecShadowSwap(caster, targetCell, grid, result); break;
            case "nightstalker_death_mark": ExecDeathMark(caster, targetCell, grid, enemies, result); break;
            case "faceless_identity_theft": ExecIdentityTheft(caster, targetCell, grid, enemies, result); break;
            case "stargazer_star_map": ExecStarMap(caster, targetCell, result); break;
            case "illusionist_mirror_image": ExecMirrorImage(caster, result); break;
            case "windwalker_tailwind": ExecTailwind(caster, targetCell, grid, allies, result); break;
            case "ironsovereign_iron_law": ExecIronLaw(caster, result); break;

            // 四属性 (5个主动)
            case "shadowlord_puppet_master": ExecPuppetMaster(caster, targetCell, grid, enemies, allUnits, result); break;
            case "voidknight_chains_of_deep": ExecChainsOfDeep(caster, targetCell, grid, enemies, result); break;
            case "stormbanner_lightning_raid": ExecLightningRaid(caster, targetCell, grid, enemies, allies, result); break;
            case "tempestlord_inferno_surge": ExecInfernoSurge(caster, grid, enemies, result); break;
            case "stonesaint_stone_body": ExecStoneBody(caster, result); break;

            // 五属性 (1个主动)
            case "mountainlord_mountain_stance": ExecMountainStance(caster, result); break;

            default:
                // 被动技能不在这里执行
                return Fail($"职业技能 {skill.EffectId} 是被动技能或尚未实现");
        }

        if (result["success"].AsBool())
        {
            caster.ConsumeAp(skill.ApCost);
            caster.RecordCareerSkillUse();
        }

        return result;
    }

    // ============================================================================
    // 单属性主动技能
    // ============================================================================

    /// <summary>战士-碎甲打击: 近战攻击命中后 DR阈值-5 或 AC-1</summary>
    private static void ExecArmorBreak(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        var atkResult = CombatResolver.ResolveAttack(caster, target, grid, false);
        result["results"].AsGodotArray().Add(atkResult);

        if (atkResult.ContainsKey("hit") && atkResult["hit"].AsBool())
        {
            // 检查目标是否有DR（护甲）
            bool hasDr = target.GetDr() > 0 || target.GetDrThreshold() > 0;
            if (hasDr)
            {
                result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", target },
                    { "effect_id", "armor_break_dr" },
                    { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "dr_threshold_reduction", 5 } } }
                });
            }
            else
            {
                result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", target },
                    { "effect_id", "armor_break_ac" },
                    { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "ac", -1 } } }
                });
            }
        }
    }

    /// <summary>守卫-铜墙铁壁: 控制区扩展至6格</summary>
    private static void ExecLivingWall(Unit caster, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "living_wall" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "control_range_override", 6 } } }
        });
    }

    /// <summary>法师-以太过载: 消耗剩余法力，下个法术增强</summary>
    private static void ExecArcaneOverload(Unit caster, Godot.Collections.Dictionary result)
    {
        if (caster.Data == null || caster.Data.CurrentMana <= 0)
        { FailResult(result, "没有法力可消耗"); return; }

        int consumedMana = caster.Data.CurrentMana;
        caster.Data.CurrentMana = 0;

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "arcane_overload" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "dc_bonus", consumedMana },
                { "dice_bonus", consumedMana },
                { "range_bonus", consumedMana >= 5 ? 1 : 0 }
            } }
        });
    }

    /// <summary>刺客-弱点暴露: 标记目标2回合暴击阈值降低</summary>
    private static void ExecExposeWeakness(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allUnits, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allUnits.Where(u => u.Data != null && u.Data.IsEnemy));
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        int reduction = 3;
        if (target.CurrentHp < target.Model.GetMaxHp())
            reduction += 2;

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "expose_weakness" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "crit_threshold_reduction", reduction } } }
        });
    }

    /// <summary>诗人-战歌切换: 在三种战歌间切换</summary>
    private static void ExecBattleHymn(Unit caster, Godot.Collections.Dictionary result)
    {
        var current = CareerSkillResolver.GetCurrentHymn(caster);
        var next = current switch
        {
            CareerSkillResolver.HymnType.March => CareerSkillResolver.HymnType.Bulwark,
            CareerSkillResolver.HymnType.Bulwark => CareerSkillResolver.HymnType.Fury,
            _ => CareerSkillResolver.HymnType.March,
        };
        CareerSkillResolver.SetCurrentHymn(caster, next);

        string hymnName = next switch
        {
            CareerSkillResolver.HymnType.March => "进军号（移动-1AP/格）",
            CareerSkillResolver.HymnType.Bulwark => "铁壁颂（AC+1）",
            _ => "嗜血曲（伤害+10%）",
        };
        result["message"] = $"切换至{hymnName}";
    }

    // ============================================================================
    // 双属性主动技能
    // ============================================================================

    /// <summary>剑舞者-连旋斩: 正面3格扇形攻击+旋转位移</summary>
    private static void ExecWhirlingStrike(Unit caster, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        // 正面3格：使用正面3个方向的邻居
        int facing = caster.Data?.Runtime.Facing ?? 0;
        var neighbors = HexUtils.GetNeighbors(caster.GridPos.X, caster.GridPos.Y);
        // 取正面3格：facing方向 + 左右各1
        int[] fanDirs = { facing, (facing + 5) % 6, (facing + 1) % 6 };
        foreach (int dir in fanDirs)
        {
            var pos = HexUtils.GetNeighbor(caster.GridPos.X, caster.GridPos.Y, dir);
            var target = FindUnitAt(pos, enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(caster, target, grid, false);
                // 伤害×0.7
                if (r.ContainsKey("damage"))
                    r["damage"] = Math.Max(1, (int)(r["damage"].AsInt32() * 0.7f));
                result["results"].AsGodotArray().Add(r);
            }
        }
    }

    /// <summary>魔剑士-符文武器: 武器注入符文3回合</summary>
    private static void ExecRuneImbue(Unit caster, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "rune_imbue" },
            { "duration", 3 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "fire_damage_dice", "1d6" },
                { "burn_duration", 1 },
                { "burn_damage", "1d6" }
            } }
        });
    }

    /// <summary>秘射手-魔矢追踪: 无视掩体远程+3d8奥术</summary>
    private static void ExecHomingShot(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        // 远程攻击（优势，因为追踪）
        var r = CombatResolver.ResolveAttack(caster, target, grid, true);
        result["results"].AsGodotArray().Add(r);

        // 额外1d8奥术伤害
        if (r.ContainsKey("hit") && r["hit"].AsBool())
        {
            int arcaneDmg = RPGRuleEngine.RollDice(1, 8);
            target.TakeDamage(arcaneDmg);
            result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "type", "arcane_damage" },
                { "target", target },
                { "value", arcaneDmg }
            });
        }
    }

    /// <summary>猎鹰手-鹰眼锁定: 标记目标失去地形加成</summary>
    private static void ExecHawksMark(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allUnits, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allUnits.Where(u => u.Data != null && u.Data.IsEnemy));
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "hawks_mark" },
            { "duration", 3 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "lose_terrain_ac", true },
                { "lose_cover", true },
                { "attacker_range_bonus", 1 }
            } }
        });
    }

    /// <summary>浪客-声东击西: 强制转向+潜行</summary>
    private static void ExecMisdirection(Unit caster, Vector2I targetCell, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        // 目标下回合攻击劣势
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "misdirection" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_disadvantage", true } } }
        });
        // 自身潜行
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "invisibility" },
            { "duration", 99 }
        });
    }

    /// <summary>铁壁将军-坚守阵线: 友军不可移动AC+2</summary>
    private static void ExecHoldTheLine(Unit caster, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        foreach (var ally in allies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int dist = HexUtils.Distance(caster.GridPos.X, caster.GridPos.Y, ally.GridPos.X, ally.GridPos.Y);
                if (dist <= 2)
                {
                    result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                        { "target", ally },
                        { "effect_id", "hold_the_line" },
                        { "duration", 1 },
                        { "stat_modifiers", new Godot.Collections.Dictionary {
                            { "ac", 2 },
                            { "immobilized", true },
                            { "immune_fear", true }
                        } }
                    });
                }
            }
        }
    }

    /// <summary>术士-血脉共鸣: 消耗10%HP恢复法力</summary>
    private static void ExecBloodResonance(Unit caster, Godot.Collections.Dictionary result)
    {
        if (caster.Data == null) return;

        int maxHp = caster.Model.GetMaxHp();
        int hpCost = Math.Max(5, (int)(maxHp * 0.1f));
        float hpPct = (float)caster.CurrentHp / maxHp;
        bool isLowHp = hpPct < 0.3f;

        caster.CurrentHp = Math.Max(1, caster.CurrentHp - hpCost);

        int manaMultiplier = isLowHp ? 3 : 2;
        int manaRecover = hpCost * manaMultiplier;
        caster.Data.CurrentMana += manaRecover;

        result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "blood_resonance" },
            { "hp_cost", hpCost },
            { "mana_recovered", manaRecover },
            { "low_hp_mode", isLowHp }
        });
    }

    // ============================================================================
    // 三属性主动技能
    // ============================================================================

    /// <summary>斗士-铁壁冲锋: 冲锋3格期间AC+3</summary>
    private static void ExecIronRush(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        // 直线方向：从caster到targetCell的方向
        int dx = targetCell.X - caster.GridPos.X;
        int dy = targetCell.Y - caster.GridPos.Y;
        // 找到最接近的六角方向
        int dir = 0;
        int bestDot = int.MinValue;
        for (int d = 0; d < 6; d++)
        {
            var dVec = HexUtils.Directions[d];
            int dot = dVec.X * dx + dVec.Y * dy;
            if (dot > bestDot) { bestDot = dot; dir = d; }
        }
        var path = new List<Vector2I>();
        var current = caster.GridPos;
        for (int i = 0; i < 3; i++)
        {
            current = HexUtils.GetNeighbor(current.X, current.Y, dir);
            path.Add(current);
        }

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "iron_rush" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "ac", 3 } } }
        });

        // 命中路径上第一个敌人
        foreach (var pos in path)
        {
            var target = FindUnitAt(pos, enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(caster, target, grid, true);
                result["results"].AsGodotArray().Add(r);
                break;
            }
        }
    }

    /// <summary>魔武者-瞬发符印: 放置友军恢复/敌军伤害的符印</summary>
    private static void ExecInstantGlyph(Unit caster, Vector2I targetCell, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "instant_glyph" },
            { "duration", 3 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "glyph_pos_x", targetCell.X },
                { "glyph_pos_y", targetCell.Y },
                { "heal_dice", "1d4" },
                { "damage_dice", "1d4" }
            } }
        });
    }

    /// <summary>鹰眼猎手-致命弹道: 暴击阈值-4未命中返还4AP</summary>
    private static void ExecKillShot(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        var r = CombatResolver.ResolveAttack(caster, target, grid, false);
        result["results"].AsGodotArray().Add(r);

        if (r.ContainsKey("hit") && r["hit"].AsBool())
        {
            // 暴击额外+1.0x倍率
            if (r.ContainsKey("is_crit") && r["is_crit"].AsBool() && r.ContainsKey("damage"))
            {
                int dmg = r["damage"].AsInt32();
                r["damage"] = dmg + dmg; // 额外+1.0x = 基础伤害再加一次
            }
        }
        else
        {
            // 未命中返还4AP
            caster.CurrentAp += 4;
            result["ap_refunded"] = 4;
        }
    }

    /// <summary>战神-战吼冲锋: 冲锋+友军士气+8</summary>
    private static void ExecWarCryCharge(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        // 冲锋攻击
        var target = FindUnitAt(targetCell, enemies);
        if (target != null)
        {
            var r = CombatResolver.ResolveAttack(caster, target, grid, true);
            result["results"].AsGodotArray().Add(r);
        }
        // 周围3格友军士气+8
        foreach (var ally in allies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                int dist = HexUtils.Distance(caster.GridPos.X, caster.GridPos.Y, ally.GridPos.X, ally.GridPos.Y);
                if (dist <= 3)
                {
                    MoraleSystem.ChangeMorale(ally, 8);
                    result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                        { "target", ally },
                        { "effect_id", "war_cry_advantage" },
                        { "duration", 1 },
                        { "stat_modifiers", new Godot.Collections.Dictionary { { "next_attack_advantage", true } } }
                    });
                }
            }
        }
    }

    /// <summary>铁焰魔战-符文壁垒: 召唤符文屏障</summary>
    private static void ExecRuneBarricade(Unit caster, Vector2I targetCell, Godot.Collections.Dictionary result)
    {
        int conMod = caster.Data != null ? RPGRuleEngine.GetStatModifier(caster.Data.Con) : 0;
        int barrierHp = conMod * 5;
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "rune_barricade" },
            { "duration", 3 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "barricade_pos_x", targetCell.X },
                { "barricade_pos_y", targetCell.Y },
                { "barricade_hp", barrierHp },
                { "destroy_damage_dice", "1d6" }
            } }
        });
    }

    /// <summary>征服者-镇压: 近战+士气-15+恐惧</summary>
    private static void ExecSubjugate(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        var r = CombatResolver.ResolveAttack(caster, target, grid, false);
        result["results"].AsGodotArray().Add(r);

        if (r.ContainsKey("hit") && r["hit"].AsBool())
        {
            MoraleSystem.ChangeMorale(target, -15);
            int chaMod = caster.Data != null ? RPGRuleEngine.GetStatModifier(caster.Data.Cha) : 0;
            result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "target", target },
                { "effect_id", "fear" },
                { "duration", 1 },
                { "stat_modifiers", new Godot.Collections.Dictionary { { "fear_source_id", (long)caster.GetInstanceId() } } }
            });
        }
    }

    /// <summary>灭世骑士-毁灭凝视: 标记目标暴击阈值视为13</summary>
    private static void ExecGazeOfRuin(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allUnits, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allUnits.Where(u => u.Data != null && u.Data.IsEnemy));
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        int duration = 1; // 基础1回合
        // 有负面状态时2回合 — 简化判断
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "gaze_of_ruin" },
            { "duration", duration },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "crit_threshold_override", 13 } } }
        });
    }

    /// <summary>战术大师-奥术冲锋: 冲锋无视地形+路径治疗</summary>
    private static void ExecArcaneCharge(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        var target = FindUnitAt(targetCell, enemies);
        if (target != null)
        {
            var r = CombatResolver.ResolveAttack(caster, target, grid, true);
            result["results"].AsGodotArray().Add(r);
        }
        // 奥术痕迹效果
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "arcane_trail" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "heal_dice", "1d4" },
                { "ignore_terrain_penalty", true }
            } }
        });
    }

    /// <summary>影法师-暗影置换: 与记忆位置交换</summary>
    private static void ExecShadowSwap(Unit caster, Vector2I targetCell, HexGrid? grid, Godot.Collections.Dictionary result)
    {
        caster.GridPos = targetCell;
        result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "teleport" },
            { "target", caster },
            { "new_pos", targetCell }
        });
        // 交换后潜行1回合
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "invisibility" },
            { "duration", 1 }
        });
    }

    /// <summary>夜行者-暗杀标记: 标记2回合攻击无视DR</summary>
    private static void ExecDeathMark(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "death_mark" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "ignore_dr", true },
                { "marked_by", (long)caster.GetInstanceId() },
                { "ap_recovery_on_kill", 3 }
            } }
        });
    }

    /// <summary>千面客-身份窃取: 模仿敌方不被攻击</summary>
    private static void ExecIdentityTheft(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "identity_theft" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "mimic_target_id", (long)target.GetInstanceId() },
                { "ai_ignore", true }
            } }
        });
    }

    /// <summary>星辰行者-星图定位: 标记传送点</summary>
    private static void ExecStarMap(Unit caster, Vector2I targetCell, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "star_map" },
            { "duration", 99 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "teleport_target_x", targetCell.X },
                { "teleport_target_y", targetCell.Y },
                { "teleport_delay", 1 }
            } }
        });
    }

    /// <summary>幻术师-镜像分身: 生成幻影分身</summary>
    private static void ExecMirrorImage(Unit caster, Godot.Collections.Dictionary result)
    {
        int chaMod = caster.Data != null ? RPGRuleEngine.GetStatModifier(caster.Data.Cha) : 0;
        int maxPhantoms = Math.Max(2, chaMod);

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "mirror_image" },
            { "duration", 99 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "phantom_count", 2 },
                { "max_phantoms", maxPhantoms },
                { "redirect_chance", 0.5f },
                { "phantom_ac", 12 },
                { "phantom_hp", 1 }
            } }
        });
    }

    /// <summary>风语者-顺风传递: 传送友军+双方移动消耗降低</summary>
    private static void ExecTailwind(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allies);
        if (target == null) { FailResult(result, "目标格没有友军"); return; }

        // 传送到caster周围2格内
        var neighbors = HexUtils.GetNeighbors(caster.GridPos.X, caster.GridPos.Y);
        Vector2I dest = neighbors.FirstOrDefault(n => n != caster.GridPos, caster.GridPos);
        target.GridPos = dest;

        result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "teleport" },
            { "target", target },
            { "new_pos", dest }
        });
        // 双方移动消耗-1AP/格
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "tailwind" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "move_ap_reduction", 1 } } }
        });
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "tailwind" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "move_ap_reduction", 1 } } }
        });
    }

    /// <summary>铁幕领主-铁律: 区域禁止冲锋/潜行/士气÷2</summary>
    private static void ExecIronLaw(Unit caster, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "iron_law" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "range", 3 },
                { "no_charge", true },
                { "no_stealth", true },
                { "morale_change_divisor", 2 }
            } }
        });
    }

    // ============================================================================
    // 四属性主动技能
    // ============================================================================

    /// <summary>暗影领主-幕后操纵: 控制低士气敌人</summary>
    private static void ExecPuppetMaster(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, IEnumerable<Unit> allUnits, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "puppet_master" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "forced_move", 2 },
                { "forced_attack", true },
                { "damage_multiplier", 0.5f }
            } }
        });
    }

    /// <summary>渊狱骑士-深渊锁链: 束缚目标</summary>
    private static void ExecChainsOfDeep(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null) { FailResult(result, "目标格没有敌人"); return; }

        var r = CombatResolver.ResolveAttack(caster, target, grid, false);
        result["results"].AsGodotArray().Add(r);

        if (r.ContainsKey("hit") && r["hit"].AsBool())
        {
            result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "target", target },
                { "effect_id", "chains_of_deep" },
                { "duration", 1 },
                { "stat_modifiers", new Godot.Collections.Dictionary {
                    { "immobilized", true },
                    { "no_defend", true },
                    { "escape_dc", 15 }
                } }
            });
        }
    }

    /// <summary>疾风战旗-闪电突击: 群体突进+攻击</summary>
    private static void ExecLightningRaid(Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        // Caster attacks
        var target = FindUnitAt(targetCell, enemies);
        if (target != null)
        {
            var r = CombatResolver.ResolveAttack(caster, target, grid, false);
            result["results"].AsGodotArray().Add(r);
        }
        // 周围1格最多2友军也获得攻击行动
        int count = 0;
        foreach (var ally in allies)
        {
            if (count >= 2) break;
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0 || ally == caster) continue;
            int dist = HexUtils.Distance(caster.GridPos.X, caster.GridPos.Y, ally.GridPos.X, ally.GridPos.Y);
            if (dist <= 1)
            {
                result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", ally },
                    { "effect_id", "lightning_raid" },
                    { "duration", 1 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "bonus_attack", true } } }
                });
                count++;
            }
        }
    }

    /// <summary>烈焰魔将-烈焰喷涌: AOE火焰2d6+法力恢复</summary>
    private static void ExecInfernoSurge(Unit caster, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        int hits = 0;
        foreach (var enemy in enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
            int dist = HexUtils.Distance(caster.GridPos.X, caster.GridPos.Y, enemy.GridPos.X, enemy.GridPos.Y);
            if (dist <= 2)
            {
                int dmg = RPGRuleEngine.RollDice(2, 6);
                enemy.TakeDamage(dmg);
                result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "fire_damage" },
                    { "target", enemy },
                    { "value", dmg }
                });
                hits++;
            }
        }
        // 每命中1个恢复1法力
        if (caster.Data != null && hits > 0)
            caster.Data.CurrentMana += hits;
    }

    /// <summary>磐石守护-石化之躯: 石化2回合</summary>
    private static void ExecStoneBody(Unit caster, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "stone_body" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "immobilized", true },
                { "no_attack", true },
                { "no_cast", true },
                { "dr_threshold_bonus", 3 },
                { "immune_negative", true },
                { "aoe_damage_on_end", "1d6" },
                { "aoe_range", 1 }
            } }
        });
    }

    // ============================================================================
    // 五属性主动技能
    // ============================================================================

    /// <summary>山岳之主-磐石姿态: 不可移动+DR+3</summary>
    private static void ExecMountainStance(Unit caster, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "mountain_stance" },
            { "duration", 1 },
            { "stat_modifiers", new Godot.Collections.Dictionary {
                { "immobilized", true },
                { "dr_threshold_bonus", 3 },
                { "immune_negative", true }
            } }
        });
        // 下回合AC-2
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", caster },
            { "effect_id", "mountain_stance_aftermath" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "ac", -2 } } }
        });
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    private static Unit? FindUnitAt(Vector2I pos, IEnumerable<Unit> units)
        => units.FirstOrDefault(u => u.GridPos == pos);

    private static Godot.Collections.Dictionary NewResult(CareerSkillData skill)
        => new() {
            { "success", true },
            { "results", new Godot.Collections.Array() },
            { "action_cost", skill.ApCost },
            { "vfx_type", skill.EffectId },
            { "status_effects", new Godot.Collections.Array() }
        };

    private static Godot.Collections.Dictionary Fail(string reason)
        => new() { { "success", false }, { "reason", reason } };

    private static void FailResult(Godot.Collections.Dictionary result, string reason)
    {
        result["success"] = false;
        result["reason"] = reason;
    }
}
