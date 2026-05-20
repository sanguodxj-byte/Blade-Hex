-- multi_shot.lua
-- 多重射击：对同一目标射击3次，命中-2，节点平伤50%

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    for i = 1, 3 do
        local r = result:resolve_attack(ctx.attacker, target, { hit_mod = -2, node_flat_scale = 0.5 })
        result:add_attack(r)
    end
end
