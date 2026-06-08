-- myriad_battlemage.lua
-- 万象奥战 (四属性 STR+INT+DEX+CON)
-- 给自己挂万象 buff: 近战伤害+5, 法DC+3, 移耗-1, 护甲惩罚减半

function execute(ctx)
    local tier = get_tier(ctx)

    -- 给自己挂 myriad buff (基线 3 回合)
    -- 动态伤害基于 STR/INT
    local main_stat = math.max(ctx.attacker.str, ctx.attacker.intel)
    local dmg_bonus = stat_mod_x_level(main_stat, ctx.attacker.level, 0.25 * tier)
    buff:apply_custom(ctx.attacker, "myriad", scale_duration(3, tier), { damage = dmg_bonus })
    result:add_effect(ctx.attacker, "myriad", scale_duration(3, tier))
end
