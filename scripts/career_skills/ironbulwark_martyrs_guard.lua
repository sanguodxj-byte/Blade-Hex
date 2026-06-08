-- ironbulwark_martyrs_guard.lua
-- 殉道守护 (三属性 STR+CON+CHA)
-- 给自己挂殉道 buff: 友军伤害代受 50% 并获临时 HP

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "martyrs_guard", 3)
end
