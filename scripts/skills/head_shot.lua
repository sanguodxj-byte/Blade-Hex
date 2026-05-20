-- head_shot.lua
-- 爆头：对目标造成2倍伤害，优势攻击

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 2.0, advantage = true })
    result:add_attack(r)
end
