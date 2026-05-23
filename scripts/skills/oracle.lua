-- oracle.lua
-- 神谕：揭示所有拥有隐身/潜行效果的敌人（移除这些效果）

function execute(ctx)
    local enemies = ctx.enemies
    for i = 0, enemies.Length - 1 do
        local enemy = enemies[i]
        if unit:is_valid(enemy) then
            if enemy:has_effect("invisibility") or enemy:has_effect("stealth") then
                buff:remove_many(enemy, { "invisibility", "stealth" })
                result:add_remove_effect(enemy, { "invisibility", "stealth" })
            end
        end
    end
end
