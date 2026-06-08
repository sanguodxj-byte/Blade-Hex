-- hawkeye_kill_shot.lua
-- 致命弹道 (三属性 DEX+WIS+INT)
-- 暴击阈值大减，暴击伤害暴增，给自己挂鹰眼 Buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 执行攻击
    local r = result:resolve_attack(ctx.attacker, target, { damage_mult = 1.0 })
    result:add_attack(r)

    -- 命中后检查暴击 (降低暴击阈值)
    if r and r.hit then
        local roll = r.roll or 0
        local crit_reduction = scale_int(3, tier)
        -- 暴击阈值降低 (由战斗系统处理)
        result:add_effect(ctx.attacker, "crit_boost", 1)
    end

    -- 给自己挂 hawkeye_aura buff (远程伤害 +25%)
    self_buff(ctx, "hawkeye_aura", 2)
end
