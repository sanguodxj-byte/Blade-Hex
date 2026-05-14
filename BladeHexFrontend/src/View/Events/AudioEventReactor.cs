// AudioEventReactor.cs
// 音频事件反应器 — 订阅 EventBus 信号，自动触发对应的 SFX / BGM
// 解耦 gameplay 代码与音频系统：gameplay 只发信号，Reactor 负责映射到 AudioManager
using Godot;
using System;
using BladeHex.Events;

namespace BladeHex.Audio;

/// <summary>
/// 全局音频事件反应器（作为 Autoload 或 CombatScene/OverworldScene 子节点使用）。
/// 订阅 EventBus 中的战斗/进度/UI 事件，调用 AudioManager (autoload) 播放对应音效。
/// </summary>
[GlobalClass]
public partial class AudioEventReactor : Node
{
    private BladeHex.Audio.AudioManager? _audio;

    public override void _Ready()
    {
        _audio = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
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

        // 战斗事件
        bus.Subscribe(EventBus.Signals.CombatStarted, OnCombatStarted);
        bus.Subscribe(EventBus.Signals.CombatEnded, OnCombatEnded);
        bus.Subscribe(EventBus.Signals.TurnStarted, OnTurnStarted);
        bus.Subscribe(EventBus.Signals.UnitDamaged, OnUnitDamaged);
        bus.Subscribe(EventBus.Signals.UnitDied, OnUnitDied);
        bus.Subscribe(EventBus.Signals.UnitHealed, OnUnitHealed);
        bus.Subscribe(EventBus.Signals.SkillUsed, OnSkillUsed);
        bus.Subscribe(EventBus.Signals.StatusEffectApplied, OnStatusEffectApplied);
        bus.Subscribe(EventBus.Signals.StatusEffectRemoved, OnStatusEffectRemoved);
        bus.Subscribe(EventBus.Signals.ProjectileImpact, OnProjectileImpact);

        // 经济/进度事件
        bus.Subscribe(EventBus.Signals.GoldChanged, OnGoldChanged);
        bus.Subscribe(EventBus.Signals.ItemAcquired, OnItemAcquired);
        bus.Subscribe(EventBus.Signals.QuestCompleted, OnQuestCompleted);
        bus.Subscribe(EventBus.Signals.EquipmentChanged, OnEquipmentChanged);
        bus.Subscribe(EventBus.Signals.MoraleChanged, OnMoraleChanged);
    }

    public override void _ExitTree()
    {
        var bus = EventBus.Instance;
        if (bus == null) return;

        bus.Unsubscribe(EventBus.Signals.CombatStarted, OnCombatStarted);
        bus.Unsubscribe(EventBus.Signals.CombatEnded, OnCombatEnded);
        bus.Unsubscribe(EventBus.Signals.TurnStarted, OnTurnStarted);
        bus.Unsubscribe(EventBus.Signals.UnitDamaged, OnUnitDamaged);
        bus.Unsubscribe(EventBus.Signals.UnitDied, OnUnitDied);
        bus.Unsubscribe(EventBus.Signals.UnitHealed, OnUnitHealed);
        bus.Unsubscribe(EventBus.Signals.SkillUsed, OnSkillUsed);
        bus.Unsubscribe(EventBus.Signals.StatusEffectApplied, OnStatusEffectApplied);
        bus.Unsubscribe(EventBus.Signals.StatusEffectRemoved, OnStatusEffectRemoved);
        bus.Unsubscribe(EventBus.Signals.ProjectileImpact, OnProjectileImpact);
        bus.Unsubscribe(EventBus.Signals.GoldChanged, OnGoldChanged);
        bus.Unsubscribe(EventBus.Signals.ItemAcquired, OnItemAcquired);
        bus.Unsubscribe(EventBus.Signals.QuestCompleted, OnQuestCompleted);
        bus.Unsubscribe(EventBus.Signals.EquipmentChanged, OnEquipmentChanged);
        bus.Unsubscribe(EventBus.Signals.MoraleChanged, OnMoraleChanged);
    }

    // ========================================================================
    // 战斗事件处理
    // ========================================================================

    private void OnCombatStarted(Godot.Collections.Dictionary data)
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

    private void OnTurnStarted(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("state")) return;
        int state = data["state"].AsInt32();
        // CombatState: PlayerTurn=1, EnemyTurn=2
        if (state == 1)
            PlaySfx("combat_turn_start");
        else if (state == 2)
            PlaySfx("combat_enemy_turn");
    }

    private void OnUnitDamaged(Godot.Collections.Dictionary data)
    {
        // 播放通用命中音效 — 具体伤害类型由 CombatResolver 结果决定
        // EventBus.PublishUnitDamaged 只传 unit/damage/remaining_hp
        // 我们播放一个通用的命中反馈音
        int damage = data.ContainsKey("damage") ? data["damage"].AsInt32() : 0;
        if (damage > 0)
        {
            // 根据伤害量选择音效强度
            if (damage >= 20)
                PlaySfx("combat_sword_crit"); // 重击
            else
                PlaySfx("combat_armor_hit"); // 普通命中反馈
        }
    }

    private void OnUnitDied(Godot.Collections.Dictionary data)
    {
        PlaySfx("combat_death");
    }

    private void OnUnitHealed(Godot.Collections.Dictionary data)
    {
        PlaySfx("skill_heal");
    }

    private void OnSkillUsed(Godot.Collections.Dictionary data)
    {
        if (!data.ContainsKey("skill_effect")) return;
        string skillEffect = data["skill_effect"].AsString();

        // 尝试按 skill_<effect> 名称播放
        string sfxName = "skill_" + skillEffect;
        if (HasSfx(sfxName))
        {
            PlaySfx(sfxName);
        }
        else
        {
            // 回退：播放通用技能音效
            PlaySfx("skill_melee_combo");
        }
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

    private void OnMoraleChanged(Godot.Collections.Dictionary data)
    {
        // 士气崩溃/高涨时播放对应音效
        if (!data.ContainsKey("new_morale")) return;
        // MoraleState: 0=Normal, 1=High, 2=Low, 3=Rout, 4=Rally
        int morale = data.ContainsKey("new_state") ? data["new_state"].AsInt32() : -1;
        if (morale == 4) // Rally
            PlaySfx("status_rally");
        else if (morale == 3) // Rout
            PlaySfx("status_rout");
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
