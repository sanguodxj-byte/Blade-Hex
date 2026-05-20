-- trap_master.lua
-- 陷阱大师：为自身添加陷阱标记99回合，记录陷阱伤害2d6

function execute(ctx)
    local trap_dmg = combat:roll_dice(2, 6)
    result:add_effect(ctx.attacker, "trap_placed", 99, { trap_damage = trap_dmg })
end
