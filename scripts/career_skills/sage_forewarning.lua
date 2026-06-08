-- sage_forewarning.lua
-- 预知回避 (双属性 INT+WIS)
-- 给自己挂预知 buff: 豁免 +2, 每回合首次被攻击消耗法力使其劣势

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "forewarning", 3)
end
