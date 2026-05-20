-- stealth.lua
-- 潜行：进入隐身状态

function execute(ctx)
    result:add_effect(ctx.attacker, "invisibility", 99)
end
