-- arcanewarden_bulwark_of_lore.lua
-- 知识壁垒 (三属性 INT+WIS+CON)
-- 给自己挂知识壁垒 buff: 受法术伤害时 DC 检定加值 +5

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "bulwark_lore", 3)
end
