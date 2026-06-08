-- trap_master.lua
-- 陷阱大师：为自身添加陷阱标记，触发时造成等级*骰 + DEX 修正的伤害

function execute(ctx)
    local _, _, trap_dmg = calc_skill_value(ctx, "trap_master")
    buff:apply_custom(ctx.attacker, "trap_placed", 99, { trap_damage = trap_dmg })
end
