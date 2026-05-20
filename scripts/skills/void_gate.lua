-- void_gate.lua
-- 虚空之门：传送施法者到目标格

function execute(ctx)
    local caster = ctx.attacker
    local oq = caster.q
    local or2 = caster.r
    result:add_teleport(caster, ctx.target_q, ctx.target_r, oq, or2)
end
