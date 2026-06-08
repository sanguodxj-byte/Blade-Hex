-- purify_field.lua
-- 净化领域：周围友军移除负面状态并恢复 max(1,等级/4)d6 + WIS×1.0 HP

function execute(ctx)
    local targets = find_all_allies_in_range(ctx, 2)
    if #targets == 0 then return end

    -- 移除负面状态
    for _, ally in ipairs(targets) do
        remove_negative_effects(ally)
    end

    -- 恢复 HP
    local dice_count, dice_sides, total_heal = calc_skill_value(ctx, "purify_field")
    for _, ally in ipairs(targets) do
        local actual_heal = math.min(total_heal, ally.max_hp - ally.hp)
        if actual_heal > 0 then
            ally.hp = ally.hp + actual_heal
            result:add_heal(ally, actual_heal)
        end
    end
end
