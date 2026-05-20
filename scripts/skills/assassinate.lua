-- assassinate.lua
-- 暗杀：若目标生命低于30%则直接击杀（9999伤害），否则2倍伤害攻击

function execute(ctx)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    local threshold = math.floor(target.max_hp * 0.3)
    if target.hp < threshold then
        unit:take_damage(target, 9999)
        result:add_damage(target, 9999)
    else
        local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 2.0 })
        result:add_attack(r)
    end
end
