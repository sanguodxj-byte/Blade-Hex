-- paragon_hand_of_all.lua
-- 万象之王 (六属性 STR+DEX+CON+INT+WIS+CHA)
-- 恢复 30% HP + 30% 法力 + 清除所有负面 + 给自己挂万象之王 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 恢复 30% * tier HP
    local heal_hp = percent_of_max_hp(ctx.attacker, 0.30 * tier)
    unit:heal(ctx.attacker, heal_hp)
    result:add_heal(ctx.attacker, heal_hp)

    -- 恢复 30% * tier 法力
    local heal_mana = percent_of_max_mana(ctx.attacker, 0.30 * tier)
    ctx.attacker.mana = math.min(ctx.attacker.max_mana, ctx.attacker.mana + heal_mana)

    -- 清除所有负面 buff (通过 API 移除 IsNegative=true 的 buff)
    buff:remove_many(ctx.attacker, {"poison", "burning", "bleed", "slow", "stun", "fear", "silence", "armor_break", "death_mark", "hawks_mark", "misdirected", "gaze_ruin", "deep_chains", "hunters_mark"})

    -- 给自己挂 paragon buff (基线 5 回合)
    self_buff(ctx, "paragon", 5)
end
