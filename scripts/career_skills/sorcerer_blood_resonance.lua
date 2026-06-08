-- sorcerer_blood_resonance.lua
-- 血脉共鸣 (双属性 CON+CHA)
-- 消耗 15% HP, 恢复 15% * tier 法力, 挂共鸣 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 消耗 15% HP (至少保留 1 HP)
    local hp_cost = percent_of_max_hp(ctx.attacker, 0.15)
    ctx.attacker.hp = math.max(1, ctx.attacker.hp - hp_cost)

    -- 恢复 15% * tier 法力
    local mana_recover = percent_of_max_mana(ctx.attacker, 0.15 * tier)
    ctx.attacker.mana = math.min(ctx.attacker.max_mana, ctx.attacker.mana + mana_recover)

    -- 记录结果
    result:add_damage(ctx.attacker, hp_cost, "blood_resonance_cost")
    result:add_heal(ctx.attacker, mana_recover)

    -- 给自己挂 blood_resonance buff
    self_buff(ctx, "blood_resonance", 2)
end
