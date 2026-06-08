-- irontyrant_wrath.lua
-- 暴君之怒 (四属性 STR+CON+CHA+DEX)
-- 给自己挂暴君之怒 buff: 免损伤, 伤+30%, 暴击阈固定 20

function execute(ctx)
    -- 自身 buff 技能 (基线 4 回合)
    self_buff(ctx, "tyrant_wrath", 4)
end
