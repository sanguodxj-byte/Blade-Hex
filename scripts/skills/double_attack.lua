-- double_attack.lua
-- 连击：攻击目标 2 次，第二次 -3 命中

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r1 = result:resolve_attack(ctx.attacker, target)
    result:add_attack(r1)

    local r2 = result:resolve_attack(ctx.attacker, target, { hit_mod = -3 })
    result:add_attack(r2)
end
