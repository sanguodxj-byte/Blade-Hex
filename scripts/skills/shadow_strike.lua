-- shadow_strike.lua
-- 暗影突袭：若处于隐身状态，2倍伤害并解除隐身；否则普通攻击

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    if ctx.attacker:has_effect("invisibility") then
        local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 2.0 })
        result:add_attack(r)
        result:add_remove_effect(ctx.attacker, { "invisibility" })
    else
        local r = result:resolve_attack(ctx.attacker, target)
        result:add_attack(r)
    end
end
