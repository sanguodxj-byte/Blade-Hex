﻿// ArenaPanel.cs
// 竞技场面板 — 选择难度档位，接入真实战斗系统
// 使用统一布局基类，只填充数据
using Godot;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class ArenaPanel : POIPanelBase
{
    [Signal]
    public delegate void ArenaFinishedEventHandler();

    /// <summary>请求进入竞技场战斗（BattleContext + 奖金）</summary>
    [Signal]
    public delegate void ArenaCombatRequestedEventHandler(BattleContext context, int prize);

    private EconomyManager? _economy;

    // ── 数据填充 ──

    protected override Color GetIllustrationColor() => new(0.14f, 0.08f, 0.06f, 1.0f);
    protected override string GetIllustrationText() => "[ 竞技场 ]";
    protected override string GetPanelTitle() => "";
    protected override string GetInfoText() => _economy != null ? $"竞技场 | 金币: {_economy.Gold}" : "竞技场";
    protected override string GetDescriptionText() => "在这里展示你的实力，赢取金币和声望。选择对手难度，报名参赛。";
    protected override string GetLeaveButtonText() => "离开竞技场";

    protected override void PopulateActions(VBoxContainer container)
    {
        int easyFee = FacilityPricingService.GetArenaEntryFee(1);
        int easyPrize = FacilityPricingService.GetArenaPrize(1);
        bool canEasy = _economy != null && _economy.Gold >= easyFee;
        var btnEasy = CreateActionButton($"新手挑战 (报名费{easyFee}金 | 奖金{easyPrize}金) -- 低等级对手", canEasy, "金币不足");
        btnEasy.Pressed += () => StartFight(easyFee, easyPrize, 1);
        container.AddChild(btnEasy);

        int medFee = FacilityPricingService.GetArenaEntryFee(3);
        int medPrize = FacilityPricingService.GetArenaPrize(3);
        bool canMed = _economy != null && _economy.Gold >= medFee;
        var btnMed = CreateActionButton($"精英挑战 (报名费{medFee}金 | 奖金{medPrize}金) -- 中等对手", canMed, "金币不足");
        btnMed.Pressed += () => StartFight(medFee, medPrize, 3);
        container.AddChild(btnMed);

        int hardFee = FacilityPricingService.GetArenaEntryFee(5);
        int hardPrize = FacilityPricingService.GetArenaPrize(5);
        bool canHard = _economy != null && _economy.Gold >= hardFee;
        var btnHard = CreateActionButton($"冠军挑战 (报名费{hardFee}金 | 奖金{hardPrize}金) -- 强力对手", canHard, "金币不足");
        btnHard.Pressed += () => StartFight(hardFee, hardPrize, 5);
        container.AddChild(btnHard);
    }

    // ── 公开接口 ──

    public void ShowArena(EconomyManager economy)
    {
        _economy = economy;
        ShowPanel();
    }

    protected override void OnCloseRequested()
    {
        EmitSignal(SignalName.ArenaFinished);
        HidePanel();
    }

    // ── 逻辑 ──

    private void StartFight(int entryFee, int prize, int difficulty)
    {
        if (_economy == null || !_economy.SpendGold(entryFee))
        {
            SetResult("[color=red]金币不足，无法报名![/color]");
            return;
        }

        // 构建竞技场战斗上下文
        var ctx = BattleContext.Create(
            BladeHex.Map.HexOverworldTile.TerrainType.Plains,
            difficulty <= 2 ? BattleContext.BattleSize.Mercenary : BattleContext.BattleSize.Knight,
            BattleContext.EngagementType.Normal,
            (int)GD.Randi()
        );
        ctx.EnvironmentOverride = "arena";

        // 发射信号，由 OverworldScene3D 处理场景切换
        EmitSignal(SignalName.ArenaCombatRequested, ctx, prize);
    }
}
