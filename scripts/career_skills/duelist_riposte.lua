-- duelist_riposte.lua
-- 以伤换伤 (双属性 DEX+CHA)
-- 给自己挂反击 buff: 被近战命中后 100% 伤害反击

function execute(ctx)
    -- 自身 buff 技能 (基线 3 回合)
    self_buff(ctx, "riposte", 3)
end
