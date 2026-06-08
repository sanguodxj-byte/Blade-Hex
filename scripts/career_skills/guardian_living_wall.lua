-- guardian_living_wall.lua
-- 铜墙铁壁 (单属性 CON)
-- 给自己挂墙壁 buff: ac +3, dr_threshold +2, immune_displacement

function execute(ctx)
    -- 自身 buff 技能
    self_buff(ctx, "living_wall", 2)
end
