-- mountainlord_mountain_stance.lua
-- 磐石姿态 (五属性 STR+CON+DEX+INT+WIS)
-- 大幅提升 AC、DR 与伤害，免疫移动与负面状态

function execute(ctx)
    local tier = get_tier(ctx)

    -- 给自己挂 mountain_stance buff (基线 4 回合)
    -- 动态计算伤害加成 (基于 STR/CON)
    local main_stat = math.max(ctx.attacker.str, ctx.attacker.con)
    local dmg_bonus = stat_mod_x_level(main_stat, ctx.attacker.level, 0.25 * tier)

    buff:apply_custom(ctx.attacker, "mountain_stance", scale_duration(4, tier), { damage = dmg_bonus })
    result:add_effect(ctx.attacker, "mountain_stance", scale_duration(4, tier))
end
