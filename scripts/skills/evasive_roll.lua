-- evasive_roll.lua
function execute(ctx)
    local caster = ctx.attacker
    if hex:distance(caster.q, caster.r, ctx.target_q, ctx.target_r) > 4 then
        result:fail("目标格超出翻滚距离")
        return
    end
    if not unit:can_push_to(ctx.target_q, ctx.target_r) then
        result:fail("翻滚落点被阻挡")
        return
    end
    unit:teleport(caster, ctx.target_q, ctx.target_r)
end
