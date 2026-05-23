-- bloodthirst.lua
-- 嗜血：攻击目标，若击杀且未使用额外行动则获得+4 AP额外行动

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local r = result:resolve_attack(ctx.attacker, target)
    result:add_attack(r)

    if target.hp <= 0 and ctx.attacker.extra_actions == 0 then
        buff:apply_custom(ctx.attacker, "bloodthirst_extra_action", 1, { extra_ap = 4 })
    end
end
