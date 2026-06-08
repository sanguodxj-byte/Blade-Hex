-- lone_saint.lua
-- 独行术 (四属性 STR+DEX+INT+WIS)
-- 给自己挂独行 buff: 周围无友军时 AC+4, 伤害+4, 暴击阈值 -2

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    -- 条件由 buff modifier 内部处理 (Condition = "solo")
    self_buff(ctx, "lone_saint", 3)
end
