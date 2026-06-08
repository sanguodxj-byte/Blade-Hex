-- zephyrmaster_wind_favor.lua
-- 风之眷顾 (三属性 DEX+WIS+INT)
-- 给自己挂风眷 buff: 每移动获得风痕, 提升伤害与射程 (可叠层)

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合, MaxStacks = 5)
    self_buff(ctx, "wind_favor", 3)
end
