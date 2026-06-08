-- ranger_evade_volley.lua
-- 箭雨 (单属性 DEX)
-- 给自己挂箭雨 buff: attack_bonus +2 (ranged_only)

function execute(ctx)
    -- 自身 buff 技能
    self_buff(ctx, "volley", 2)
end
