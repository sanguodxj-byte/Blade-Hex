-- twilight_walker_stride.lua
-- 暮光步 (四属性 DEX+WIS+INT+CHA)
-- 恢复 30% HP + 给自己挂暮光步 buff: 不触发借机, 可穿过敌方, 命中+2

function execute(ctx)
    local tier = get_tier(ctx)

    -- 恢复 30% * tier HP
    local heal_hp = percent_of_max_hp(ctx.attacker, 0.30 * tier)
    unit:heal(ctx.attacker, heal_hp)
    result:add_heal(ctx.attacker, heal_hp)

    -- 给自己挂 twilight_stride buff (基线 4 回合)
    self_buff(ctx, "twilight_stride", 4)
end
