-- inspire.lua
-- 鼓舞：所有友军士气+2

function execute(ctx)
    local allies = ctx.allies
    for i = 0, allies.Length - 1 do
        local ally = allies[i]
        if unit:is_valid(ally) then
            unit:change_morale(ally, 2)
        end
    end
end
