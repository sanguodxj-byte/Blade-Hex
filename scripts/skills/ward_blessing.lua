-- ward_blessing.lua
-- 守护祝福：周围友军获得 AC+2 和豁免+2，持续 2 回合

function execute(ctx)
    local targets = find_all_allies_in_range(ctx, 2)
    if #targets == 0 then return end

    for _, ally in ipairs(targets) do
        buff:apply_custom(ally, "ward_blessing_buff", 2, { ac_bonus = 2, save_bonus = 2 })
        result:add_effect(ally, "ward_blessing_buff", 2)
    end
end
