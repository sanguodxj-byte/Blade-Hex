// AudioEventReactor.cs
// 音频事件反应器 — 订阅 EventBus 信号，自动触发对应的 SFX / BGM
// 解耦 gameplay 代码与音频系统：gameplay 只发信号，Reactor 负责映射到 AudioManager
//
// Sprint 5 (R6) 迁移：核心事件已切到强类型 API，剩余事件保留弱类型直到补 Payload。
using Godot;
using System;
using BladeHex.Events;
using BladeHex.Events.Payloads;

namespace BladeHex.Audio;

/// <summary>
/// [Autoload Singleton] 全局音频事件反应器。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>AudioEventReactor</c>。</para>
/// <para>生命周期：应用全局。</para>
/// <para>访问方式：通常无需直接访问；自动订阅 <see cref="EventBus"/> 信号触发音效。</para>
/// <para>职责：解耦 gameplay 与音频系统 — gameplay 只发事件，Reactor 负责映射到 <see cref="AudioManager"/>。</para>
/// </summary>
[GlobalClass]
public partial class AudioEventReactor : Node
{
    private BladeHex.Audio.AudioManager? _audio;

    public override void _Ready()
    {
        _audio = BladeHex.Data.Globals.AudioOrNull;
        if (_audio == null)
        {
            GD.PushWarning("AudioEventReactor: 未找到 AudioManager autoload。");
            return;
        }

        var bus = EventBus.Instance;
        if (bus == null)
        {
            GD.PushWarning("AudioEventReactor: EventBus 尚未初始化。");
            return;
        }

        // 战斗事件 — 5 个已迁移到强类型（CombatStarted / TurnStarted / UnitDamaged / UnitDied / SkillUsed）
        bus.Subscribe<CombatStartedEvent>(OnCombatStarted);
        bus.Subscribe(EventBus.Signals.CombatEnded, OnCombatEnded);
        bus.Subscribe<TurnStartedEvent>(OnTurnStarted);
        bus.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
        bus.Subscribe<UnitDiedEvent>(OnUnitDied);
        bus.Subscribe(EventBus.Signals.UnitHealed, OnUnitHealed);
        bus.Subscribe<SkillUsedEvent>(OnSkillUsed);
        bus.Subscribe(EventBus.Signals.StatusEffectApplied, OnStatusEffectApplied);
        bus.Subscribe(EventBus.Signals.StatusEffectRemoved, OnStatusEffectRemoved);
        bus.Subscribe(EventBus.Signals.ProjectileImpact, OnProjectileImpact);

        // 经济/进度事件
        bus.Subscribe(EventBus.Signals.GoldChanged, OnGoldChanged);
        bus.Subscribe(EventBus.Signals.ItemAcquired, OnItemAcquired);
        bus.Subscribe(EventBus.Signals.QuestCompleted, OnQuestCompleted);
        bus.Subscribe(EventBus.Signals.EquipmentChanged, OnEquipmentChanged);
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        if (bus == null) return;

        bus.Unsubscribe<CombatStartedEvent>(OnCombatStarted);
        bus.Unsubscribe(EventBus.Signals.CombatEnded, OnCombatEnded);
        bus.Unsubscribe<TurnStartedEvent>(OnTurnStarted);
        bus.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
        bus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        bus.Unsubscribe(EventBus.Signals.UnitHealed, OnUnitHealed);
        bus.Unsubscribe<SkillUsedEvent>(OnSkillUsed);
        bus.Unsubscribe(EventBus.Signals.StatusEffectApplied, OnStatusEffectApplied);
        bus.Unsubscribe(EventBus.Signals.StatusEffectRemoved, OnStatusEffectRemoved);
        bus.Unsubscribe(EventBus.Signals.ProjectileImpact, OnProjectileImpact);
        bus.Unsubscribe(EventBus.Signals.GoldChanged, OnGoldChanged);
        bus.Unsubscribe(EventBus.Signals.ItemAcquired, OnItemAcquired);
        bus.Unsubscribe(EventBus.Signals.QuestCompleted, OnQuestCompleted);
        bus.Unsubscribe(EventBus.Signals.EquipmentChanged, OnEquipmentChanged);
    }

    // ========================================================================
    // 战斗事件处理
    // ========================================================================

    private void OnCombatStarted(CombatStartedEvent ev)
    {
        // BGM 由 CombatScene 自行处理（需要 threat 判断 boss/normal）
        // 这里只播放战斗开始的 stinger
        PlaySfx("ow_combat_trigger");
    }

    private void OnCombatEnded(Godot.Collections.Dictionary data)
    {
        bool victory = data.ContainsKey("victory") && data["victory"].AsBool();
        if (victory)
            PlaySfx("combat_victory");
        else
            PlaySfx("combat_defeat");
    }

    private void OnTurnStarted(TurnStartedEvent ev)
    {
        // CombatState: PlayerTurn=1, EnemyTurn=2
        if (ev.State == 1)
            PlaySfx("combat_turn_start");
        else if (ev.State == 2)
            PlaySfx("combat_enemy_turn");
    }

    private void OnUnitDamaged(UnitDamagedEvent ev)
    {
        // 根据伤害量选择音效强度
        if (ev.Damage <= 0) return;
        if (ev.Damage >= 20)
            PlaySfx("combat_sword_crit");
        else
            PlaySfx("combat_armor_hit");
    }

    private void OnUnitDied(UnitDiedEvent ev)
    {
        PlaySfx("combat_death");
    }

    private void OnUnitHealed(Godot.Collections.Dictionary data)
    {
        PlaySfx("skill_heal");
    }

    private void OnSkillUsed(SkillUsedEvent ev)
    {
        if (string.IsNullOrEmpty(ev.SkillEffect)) return;

        // 尝试按 skill_<effect> 名称播放
        string sfxName = "skill_" + ev.SkillEffect;
        if (HasSfx(sfxName))
            PlaySfx(sfxName);
        else
            PlaySfx("skill_melee_combo"); // 回退：通用技能音效
    }

    private void OnStatusEffectApplied(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("effect_id")) return;
        string effectId = data["effect_id"].AsString();

        // 尝试按 status_<id> 播放
        string sfxName = "status_" + effectId;
        if (HasSfx(sfxName))
            PlaySfx(sfxName);
    }

    private void OnStatusEffectRemoved(Godot.Collections.Dictionary data)
    {
        PlaySfx("status_cure");
    }

    private void OnProjectileImpact(Godot.Collections.Dictionary data)
    {
        PlaySfx("combat_arrow_hit");
    }

    // ========================================================================
    // 经济/进度事件处理
    // ========================================================================

    private void OnGoldChanged(Godot.Collections.Dictionary data)
    {
        int delta = data.ContainsKey("delta") ? data["delta"].AsInt32() : 0;
        if (delta > 0)
            PlaySfx("ui_gold_change"); // gain variant
        else if (delta < 0)
            PlaySfx("ui_gold_change"); // spend variant (random from pool)
    }

    private void OnItemAcquired(Godot.Collections.Dictionary data)
    {
        PlaySfx("ui_notification");
    }

    private void OnQuestCompleted(Godot.Collections.Dictionary data)
    {
        PlaySfx("quest_complete");
    }

    private void OnEquipmentChanged(Godot.Collections.Dictionary data)
    {
        PlaySfx("char_equip_change");
    }

    // ========================================================================
    // 辅助方法
    // ========================================================================

    private void PlaySfx(string sfxName)
    {
        _audio?.PlaySfxName(sfxName);
    }

    private void PlaySfxRandomPitch(string sfxName, float volumeDb = 0.0f, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        _audio?.PlaySfxNameRandomPitch(sfxName, volumeDb, minPitch, maxPitch);
    }

    private bool HasSfx(string sfxName)
    {
        if (_audio == null) return false;
        return _audio.HasSfx(sfxName);
    }
}
