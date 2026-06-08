-- emissary_jack_of_all_trades.lua
-- 万法通识 (四属性 INT+WIS+CHA+DEX)
-- 恢复 25% HP + 25% 法力 + 给自己挂万通通识 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 恢复 25% HP
    local heal_hp = percent_of_max_hp(ctx.attacker, 0.25)
    unit:heal(ctx.attacker, heal_hp)
    result:add_heal(ctx.attacker, heal_hp)

    -- 恢复 25% 法力
    local heal_mana = percent_of_max_mana(ctx.attacker, 0.25)
    ctx.attacker.mana = math.min(ctx.attacker.max_mana, ctx.attacker.mana + heal_mana)

    -- 给自己挂 jack_of_all buff (基线 4 回合)
    self_buff(ctx, "jack_of_all", 4)
end
