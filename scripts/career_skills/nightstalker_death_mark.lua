-- nightstalker_death_mark.lua
-- 暗杀标记 (三属性 DEX+INT+CHA)
-- 攻击忽略 DR + 给目标挂暗杀标记 debuff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行攻击 (忽略 DR)
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
    result:add_attack(r)

    -- 命中后挂 hunters_mark debuff (施法者对该目标攻击拥有优势)
    if r and r.hit then
        local duration = scale_duration(3, tier)
        buff:apply(target, "hunters_mark", duration)
        result:add_effect(target, "hunters_mark", duration)
    end
end
