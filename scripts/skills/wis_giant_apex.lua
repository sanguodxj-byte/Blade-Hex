-- wis_giant_apex.lua
-- Giant WIS apex: attacks during the window hit and crit.

function execute(ctx)
    buff:apply_custom(ctx.attacker, "wis_giant_apex", 1, {
        attack_advantage = true,
        force_attack_hit = 1,
        force_attack_crit = 1
    })
    result:add_effect(ctx.attacker, "wis_giant_apex", 1)
end
