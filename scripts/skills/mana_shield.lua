-- mana_shield.lua
-- 法力护盾：消耗 5 魔力，获得 3 回合护盾

function execute(ctx)
    buff:apply(ctx.attacker, "shield", 3)
end
