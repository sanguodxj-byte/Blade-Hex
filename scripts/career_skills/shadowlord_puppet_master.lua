-- shadowlord_puppet_master.lua
-- 幕后操纵 (四属性 INT+DEX+CHA+WIS)
-- 控制低士气敌方，给自己挂操纵光环 buff

function execute(ctx)
    local tier = get_tier(ctx)

    -- 查找目标 (敌方)
    local target = require_enemy(ctx.target_q, ctx.target_r)
    if not target then return end

    -- 给自己挂 puppet_aura buff (士气低于 -30 的敌方跳过攻击)
    self_buff(ctx, "puppet_aura", 3)
end
