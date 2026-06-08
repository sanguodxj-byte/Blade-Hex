using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic.WorldEvents;
using BladeHex.Strategic.Hero;

namespace BladeHex.Strategic.Army;

public static class ArmyTickProcessor
{
    /// <summary>
    /// 集结军团每日推进。
    ///
    /// 关系偏好(M4-C3):由 <see cref="HeroRelationMatrix"/> 在 Forming 阶段筛选/排序候选成员,
    /// 而不是在 <see cref="MarshalSelector"/> 选元帅时介入 — 因为 MarshalSelector 必须先确定
    /// 元帅人选,关系才有意义(关系是相对值)。本实现:
    ///   1. 排除与元帅关系 ≤ -30 的候选(纯敌对者拒绝同行);
    ///   2. 同分时关系高者优先入队。
    /// </summary>
    public static void Tick(ArmyRegistry registry, List<OverworldEntity> allLords, List<OverworldPOI> allPois, int currentDay, WorldEventEngine? engine, HeroRelationMatrix? relations = null)
    {
        if (registry == null || allLords == null || allPois == null) return;

        var armiesToProcess = registry.All().ToList();

        foreach (var army in armiesToProcess)
        {
            // 0. 清理死亡成员
            army.Members.RemoveAll(m => !m.IsAlive);

            // 1. 检查解散条件
            if (ShouldDisband(army, allPois, engine))
            {
                GD.Print($"[ArmyTickProcessor] 军团 {army.ArmyId} 满足解散条件，执行解散。目标: {army.TargetPoiName}");
                // F1: 推送军团解散新闻
                if (engine != null && army.Marshal != null)
                {
                    var targetPoi = allPois.FirstOrDefault(p => p.PoiName == army.TargetPoiName);
                    bool captured = targetPoi != null && targetPoi.OwningFaction == army.Faction;
                    string reason = captured ? "目标已完全占领" : "任务结束或元帅阵亡";
                    engine.AddNews("army_disbanded",
                        $"🗃 【军团解散】{army.Faction} 的 {army.Marshal.EntityName} 帅率领军团解散，原因: {reason}。",
                        army.Marshal.Position);
                }
                registry.Remove(army.ArmyId);
                continue;
            }

            // 2. Forming 集结拉人推进
            if (army.State == ArmyState.Forming)
            {
                var marshal = army.Marshal;
                if (marshal != null && marshal.IsAlive)
                {
                    // 扫描 1200px 范围内的本国 Active 独立 Lord
                    var nearbyLords = allLords.Where(l =>
                        l.IsAlive &&
                        l.Faction == army.Faction &&
                        l.EntityTypeEnum == OverworldEntity.EntityType.LordArmy &&
                        string.IsNullOrEmpty(l.ArmyId) &&
                        l.Lod == OverworldEntity.EntityLod.Active &&
                        l != marshal &&
                        l.Position.DistanceTo(marshal.Position) <= 1200.0f
                    ).ToList();

                    if (relations != null && !string.IsNullOrEmpty(marshal.HeroId))
                    {
                        // 1. 排除与元帅关系 <= -30 的人
                        nearbyLords = nearbyLords.Where(l => 
                            string.IsNullOrEmpty(l.HeroId) || 
                            relations.Get(marshal.HeroId, l.HeroId) > -30
                        ).ToList();

                        // 2. 按关系降序排序，好的优先加入
                        nearbyLords = nearbyLords.OrderByDescending(l => 
                            string.IsNullOrEmpty(l.HeroId) ? 0 : relations.Get(marshal.HeroId, l.HeroId)
                        ).ToList();
                    }

                    foreach (var lord in nearbyLords)
                    {
                        if (!army.Members.Contains(lord))
                        {
                            army.Members.Add(lord);
                            lord.ArmyId = army.ArmyId;
                            GD.Print($"[ArmyTickProcessor] 领主 {lord.EntityName} 加入了军团 {army.ArmyId}。");
                        }
                    }

                    // Forming 超过 3 天的处理:
                    //  - 招到 ≥ 2 人 → 进入 Marching
                    //  - 仍只有元帅 1 人 → 招集失败,清军团 + 元帅进入 7 天 cooldown(防止 Create-Disband 循环抖动)
                    if (currentDay - army.FormedDay >= 3)
                    {
                        if (army.LivingMemberCount >= 2)
                        {
                            army.State = ArmyState.Marching;
                            GD.Print($"[ArmyTickProcessor] 军团 {army.ArmyId} 集结完毕,进入行军状态!成员数: {army.LivingMemberCount}");
                            engine?.AddNews("army_marching",
                                $"🗡 【军团行军】{army.Faction} 的 {army.Marshal?.EntityName} 元帅率军 {army.LivingMemberCount} 人开始行军,目标: {army.TargetPoiName}!",
                                army.Marshal?.Position ?? Vector2.Zero);
                        }
                        else
                        {
                            // 招集失败:撤销军团,Marshal 进入 cooldown
                            const int CooldownDays = 7;
                            marshal.MarshalCooldownUntilDay = currentDay + CooldownDays;
                            GD.Print($"[ArmyTickProcessor] 军团 {army.ArmyId} 招集失败(3 天内仅元帅一人),解散。元帅 {marshal.EntityName} 进入 {CooldownDays} 天 cooldown 至 D{marshal.MarshalCooldownUntilDay}。");
                            registry.Remove(army.ArmyId);
                            // engine 不发新闻,这是常见 AI 行为,避免噪音
                        }
                    }
                    else if (army.LivingMemberCount >= 4)
                    {
                        // 提前满员触发行军
                        army.State = ArmyState.Marching;
                        GD.Print($"[ArmyTickProcessor] 军团 {army.ArmyId} 满员提前行军!成员数: {army.LivingMemberCount}");
                        engine?.AddNews("army_marching",
                            $"🗡 【军团行军】{army.Faction} 的 {army.Marshal?.EntityName} 元帅率军 {army.LivingMemberCount} 人开始行军,目标: {army.TargetPoiName}!",
                            army.Marshal?.Position ?? Vector2.Zero);
                    }
                }
            }
        }
    }

    private static bool ShouldDisband(Army army, List<OverworldPOI> allPois, WorldEventEngine? engine)
    {
        // 元帅不存在或已死
        if (army.Marshal == null || !army.Marshal.IsAlive) return true;

        // 仅剩自己或无成员;但 Forming 阶段刚创建只有 Marshal 1 人是正常的,不该立即解散
        if (army.State != ArmyState.Forming && army.LivingMemberCount <= 1) return true;

        // 找到目标 POI
        var targetPoi = allPois.FirstOrDefault(p => p.PoiName == army.TargetPoiName);
        if (targetPoi == null) return true;

        // 目标 POI 已经被我方势力攻陷夺取
        if (targetPoi.OwningFaction == army.Faction) return true;

        // 战争已经结束 (两个势力间不再处于战争状态)
        if (engine != null)
        {
            if (!engine.AreAtWar(army.Faction, targetPoi.OwningFaction))
            {
                return true;
            }
        }

        return false;
    }
}
