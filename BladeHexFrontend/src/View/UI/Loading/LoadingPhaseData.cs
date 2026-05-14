// LoadingPhaseData.cs
// 定义加载过程中的阶段描述和进度权重
using System.Collections.Generic;
using Godot;

namespace BladeHex.UI;

[GlobalClass]
public partial class LoadingPhase : Resource
{
    [Export] public string Title { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public float StartProgress { get; set; } = 0.0f; // 0.0 ~ 1.0

    public LoadingPhase() { }

    public LoadingPhase(string title, string description, float startProgress)
    {
        Title = title;
        Description = description;
        StartProgress = startProgress;
    }
}

/// <summary>
/// 加载阶段数据源
/// </summary>
[GlobalClass]
public partial class LoadingPhaseData : RefCounted
{
    /// <summary>
    /// 获取新世界加载阶段
    /// </summary>
    public List<LoadingPhase> GetNewWorldPhases()
    {
        return new List<LoadingPhase>
        {
            new("起源", "正在编织世界的丝线...", 0.0f),
            new("山脉", "正在塑造大地的脊梁...", 0.2f),
            new("河流", "让生命之水流淌过平原...", 0.4f),
            new("城镇", "凡人的灯火开始在荒野闪烁...", 0.6f),
            new("英雄", "命运的轮盘选定了它的使者...", 0.8f),
            new("降临", "准备好开启你的旅程。", 0.95f)
        };
    }

    /// <summary>
    /// 获取加载存档阶段
    /// </summary>
    public List<LoadingPhase> GetLoadSavePhases()
    {
        return new List<LoadingPhase>
        {
            new("溯源", "正在从记忆的深处打捞过往...", 0.0f),
            new("重构", "正在恢复世界曾经的模样...", 0.3f),
            new("复苏", "时间再次开始了它的流动...", 0.7f),
            new("归来", "欢迎回到这个世界。", 0.95f)
        };
    }

    /// <summary>
    /// 获取战斗加载阶段
    /// </summary>
    public List<LoadingPhase> GetCombatPhases()
    {
        return new List<LoadingPhase>
        {
            new("对峙", "空气因敌意而变得沉重...", 0.0f),
            new("推演", "正在布下决定胜负的棋局...", 0.4f),
            new("死斗", "亮出你的武器，或者祈祷。", 0.9f)
        };
    }

    /// <summary>
    /// 获取快速游戏阶段
    /// </summary>
    public List<LoadingPhase> GetQuickGamePhases()
    {
        return new List<LoadingPhase>
        {
            new("瞬息", "正在快速构建一个临时的现实...", 0.0f),
            new("塑形", "角色正在混沌中成型...", 0.5f),
            new("跃迁", "即刻开始战斗。", 0.9f)
        };
    }

    /// <summary>
    /// 获取快速战斗阶段
    /// </summary>
    public List<LoadingPhase> GetQuickCombatPhases()
    {
        return new List<LoadingPhase>
        {
            new("集结", "勇士们已在角斗场就位...", 0.0f),
            new("演武", "武器在阳光下泛起寒芒...", 0.5f),
            new("开战", "鲜血将染红这片土地。", 0.9f)
        };
    }

    /// <summary>
    /// 根据当前进度获取对应的阶段文本
    /// </summary>
    public static LoadingPhase? GetPhaseAtProgress(List<LoadingPhase> phases, float progress)
    {
        if (phases == null || phases.Count == 0) return null;

        LoadingPhase? current = phases[0];
        foreach (var phase in phases)
        {
            if (progress >= phase.StartProgress)
            {
                current = phase;
            }
            else
            {
                break;
            }
        }
        return current;
    }
}
