-- skullcrusher_crush_weakpoint.lua
-- 弱点粉碎 (三属性 STR+CON+DEX)
-- 近战攻击 + 额外伤害基于目标已损 HP% + 给自己挂弱点粉碎 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行近战攻击
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
    result:add_attack(r)

    -- 命中后额外伤害 (基于目标已损 HP%)
    if r and r.hit then
        local lost_hp_pct = 1.0 - (target.hp / target.max_hp)
        local extra_dmg = scale_int(math.floor(lost_hp_pct * 10), tier)
        if extra_dmg > 0 then
            unit:take_damage(target, extra_dmg)
            result:add_damage(target, extra_dmg, "crush")
        end
    end

    -- 给自己挂 crush_weak buff (护甲穿透 +3)
    self_buff(ctx, "crush_weak", 3)
end
