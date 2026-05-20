-- mana_shield.lua
-- 法力护盾：消耗 5 魔力，获得 3 回合护盾

function execute(ctx)
    if not check_mana(ctx.attacker, 5) then return end
    result:add_effect(ctx.attacker, "shield", 3)
end
