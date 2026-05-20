-- scatter_shot.lua
-- 散射：AOE区域（目标格+邻格），攻击每个敌人，命中-2

function execute(ctx)
    aoe_area(ctx.target_q, ctx.target_r, "enemies", function(target)
        local r = result:resolve_attack(ctx.attacker, target, { hit_mod = -2 })
        result:add_attack(r)
    end)
end
