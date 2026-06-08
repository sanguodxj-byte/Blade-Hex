-- lordofruin_harbinger.lua
-- 毁灭预兆 (四属性 STR+INT+CON+CHA)
-- 给自己挂毁灭预兆 buff: 每损 10% HP 提升 DC, AC 固定 10

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "harbinger", 3)
end
