-- stealth.lua
-- 潜行：进入隐身状态

function execute(ctx)
    buff:apply_custom(ctx.attacker, "invisibility", 99, { untargetable = true })
end
