// OverworldScene2D.Diplomacy.cs
// 外交子系统 — 负责与玩家交互的事件订阅及 UI 弹窗挂载
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Strategic;
using BladeHex.Strategic.Diplomacy;
using BladeHex.UI.Common;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // 利用 Godot 节点被加入场景树时的生命周期来订阅 UI 级的外交事件
    public override void _EnterTree()
    {
        BladeHex.Events.EventBus.Instance?.Subscribe("ai_propose_peace_to_player", OnAiProposePeace);
        
        // 订阅自身的 TreeExiting 信号以在销毁时安全退订，避免与 Weather 分部类的 _ExitTree 重写发生冲突
        TreeExiting += OnTreeExiting;
    }

    private void OnTreeExiting()
    {
        BladeHex.Events.EventBus.Instance?.Unsubscribe("ai_propose_peace_to_player", OnAiProposePeace);
        TreeExiting -= OnTreeExiting;
    }

    /// <summary>
    /// 当 AI 主动向玩家提出求和协议时的事件回调
    /// </summary>
    private void OnAiProposePeace(Godot.Collections.Dictionary data)
    {
        if (data == null || _overworldUi == null || EntityMgr == null) return;
        string proposer = data.ContainsKey("proposer") ? data["proposer"].AsString() : "";
        int warDays = data.ContainsKey("war_days") ? data["war_days"].AsInt32() : 30;

        if (string.IsNullOrEmpty(proposer)) return;

        // 实例化精美的议和确认对话框
        var dialog = new BladeHex.View.UI.Overworld.AiPeaceProposalDialog();
        dialog.Init(proposer, warDays, _worldNations ?? new List<NationConfig>(), 
            onAccept: () => 
            {
                var result = DiplomacyService.ProposePeace(
                    proposer, "player", EntityMgr.WorldEngine, EntityMgr.WorldEngine.FactionRelations, 
                    EntityMgr.Relations, EntityMgr.Entities, skipAiCheck: true);
                if (result == DiplomacyResult.Success)
                {
                    _toast?.Show($"[World] 我们已与 {GetFactionDisplayName(proposer)} 达成停战协议，重归和平！", new Color(0.4f, 1.0f, 0.5f));
                }
            },
            onReject: () =>
            {
                var config = DiplomacyBalanceConfig.Load();
                EntityMgr.WorldEngine.FactionRelations.SetProposePeaceCooldown(
                    proposer, "player", config.ProposePeaceCooldownDays, EntityMgr.WorldEngine.CurrentDay);
                _toast?.Show($"[World] 我们拒绝了 {GetFactionDisplayName(proposer)} 的和谈请求，战争将继续！", new Color(1.0f, 0.4f, 0.4f));
            }
        );

        // 挂载模态框到 OverworldUI 容器下，显示精美暗色遮罩
        OverlayPanelLayout.AttachModal(_overworldUi, dialog);
    }

    /// <summary>
    /// 获取势力友好展示名辅助函数
    /// </summary>
    private string GetFactionDisplayName(string factionId)
    {
        if (factionId == "player") return "玩家王国";
        if (factionId == "neutral") return "中立";
        var nation = _worldNations?.FirstOrDefault(n => n.Id == factionId);
        return nation != null ? nation.DisplayName : factionId;
    }
}
