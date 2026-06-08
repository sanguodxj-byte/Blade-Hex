-- loneshadow_lone_operative.lua
-- 孤影行者 (四属性 DEX+INT+CHA+WIS)
-- 隐身 + 给自己挂孤行 buff: 周围无友军全效, 有友军减半

function execute(ctx)
    local tier = get_tier(ctx)
    local duration = scale_duration(4, tier)

    -- 给自己隐身
    result:add_effect(ctx.attacker, "invisibility", duration)

    -- 给自己挂 lone_op buff (动态伤害基于 STR/DEX)
    local main_stat = math.max(ctx.attacker.str, ctx.attacker.dex)
    local dmg_bonus = stat_mod_x_level(main_stat, ctx.attacker.level, 0.25 * tier)
    buff:apply_custom(ctx.attacker, "lone_op", duration, { damage = dmg_bonus })
    result:add_effect(ctx.attacker, "lone_op", duration)
end
