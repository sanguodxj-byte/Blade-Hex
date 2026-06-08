using System;
using System.Collections.Generic;
using Godot;
using BladeHex.Strategic;

namespace BladeHex.Strategic.SubParty;

public static class SubPartyTickProcessor
{
    /// <summary>
    /// 推进 SubParty 每日 AI tick,并返回需要归队(回 PartyRoster)的 SubParty 列表。
    /// 归队条件:OverworldEntityRef.IsAlive == false 且 (currentDay - TaskStartDay) >= 7。
    /// 战败时机:OverworldEntityRef.IsAlive 由战斗结算路径置为 false,这里作为入口标记 TaskStartDay。
    /// </summary>
    public static List<SubParty> Tick(
        SubPartyRegistry registry,
        List<OverworldEntity> entities,
        List<OverworldPOI> pois,
        Vector2 playerPos,
        int currentDay)
    {
        var rejoiners = new List<SubParty>();
        if (registry == null || entities == null || pois == null) return rejoiners;

        foreach (var sp in registry.GetAll())
        {
            var entity = sp.OverworldEntityRef;

            // 战败/不在场处理:进入 Recovering(以 TaskStartDay 计时),7 天后归队
            if (entity == null || !entity.IsAlive)
            {
                // 第一次检测到战败 — 把 TaskStartDay 作为战败时间戳
                if (sp.Task != SubPartyTask.Idle || sp.TaskStartDay == 0)
                {
                    sp.Task = SubPartyTask.Idle;
                    sp.TaskStartDay = currentDay;
                    GD.Print($"[SubParty] {sp.LeaderUnitName} 战败,7 天后将归队。");
                }
                else if (currentDay - sp.TaskStartDay >= 7)
                {
                    rejoiners.Add(sp);
                }
                continue;
            }

            switch (sp.Task)
            {
                case SubPartyTask.Idle:
                    MoveTowards(entity, playerPos, 150f);
                    break;

                case SubPartyTask.PatrolRegion:
                    var poi = pois.Find(p => p.PoiName == sp.TargetPoiName);
                    if (poi != null)
                    {
                        if (entity.Position.DistanceTo(poi.Position) > 600.0f)
                        {
                            MoveTowards(entity, poi.Position, 150f);
                        }
                        else
                        {
                            // 圆圈巡逻
                            var angle = (currentDay * 45) * (Math.PI / 180.0f);
                            var offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 300.0f;
                            MoveTowards(entity, poi.Position + offset, 150f);
                        }
                    }
                    break;

                case SubPartyTask.EscortCaravan:
                    var caravan = entities.Find(e => e.EntityName == sp.TargetPoiName && e.IsAlive);
                    if (caravan != null)
                    {
                        MoveTowards(entity, caravan.Position, 150f);
                    }
                    else
                    {
                        sp.Task = SubPartyTask.Idle;
                    }
                    break;

                case SubPartyTask.HuntBandits:
                    OverworldEntity? nearestBandit = null;
                    float nearestDist = float.MaxValue;
                    foreach (var e in entities)
                    {
                        if (e.IsAlive && (e.EntityTypeEnum == OverworldEntity.EntityType.BanditParty ||
                                          e.EntityTypeEnum == OverworldEntity.EntityType.RobberParty ||
                                          e.EntityTypeEnum == OverworldEntity.EntityType.PirateCrew))
                        {
                            float d = entity.Position.DistanceTo(e.Position);
                            if (d < nearestDist)
                            {
                                nearestDist = d;
                                nearestBandit = e;
                            }
                        }
                    }

                    if (nearestBandit != null)
                    {
                        MoveTowards(entity, nearestBandit.Position, 150f);
                    }
                    else
                    {
                        sp.Task = SubPartyTask.Idle;
                    }
                    break;

                case SubPartyTask.Garrison:
                    var targetPoi = pois.Find(p => p.PoiName == sp.TargetPoiName);
                    if (targetPoi != null)
                    {
                        if (entity.Position.DistanceTo(targetPoi.Position) > 50.0f)
                        {
                            MoveTowards(entity, targetPoi.Position, 150f);
                        }
                        else
                        {
                            entity.Position = targetPoi.Position;
                            targetPoi.GarrisonCurrent = Math.Min(targetPoi.GarrisonCurrent + 1, targetPoi.GarrisonMax);
                        }
                    }
                    break;
            }

            sp.Position = entity.Position;
        }

        return rejoiners;
    }

    private static void MoveTowards(OverworldEntity entity, Vector2 target, float maxDist)
    {
        if (entity.Position.DistanceTo(target) <= 20.0f)
        {
            entity.Position = target;
            return;
        }
        var dir = (target - entity.Position).Normalized();
        entity.Position += dir * maxDist;
    }
}
