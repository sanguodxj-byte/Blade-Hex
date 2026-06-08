-- ironsovereign_iron_law.lua
-- 铁律 (三属性 STR+CON+CHA)
-- 给自己挂铁律光环 buff: 禁冲锋与潜行, 士气变化减半

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "iron_law", 3)
end
