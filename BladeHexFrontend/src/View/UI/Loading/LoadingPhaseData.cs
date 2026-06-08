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
            new("奠基", "混沌退散，天穹与深渊的边界在虚空中缓缓浮现...", 0.00f),
            new("大地", "正在向大地播撒生命的种子，沃野与荒漠随之成形...", 0.10f),
            new("山海", "山脉如利齿般刺破云层，冰川消融汇成江河奔涌而出...", 0.20f),
            new("烈焰", "正在给火山灌满岩浆，灰烬如雪般飘落在焦土之上...", 0.30f),
            new("森林", "古老的树苗从灰烬中破土而出，转瞬间古木参天...", 0.40f),
            new("生灵", "飞鸟掠过林梢，走兽踏破晨露，鱼群在溪流中闪烁如碎银...", 0.50f),
            new("文明", "篝火旁诞生了第一句话，城邦在河流交汇处拔地而起...", 0.60f),
            new("魔法", "六芒星从天穹坠落，魔力渗透进大地的每一寸脉络...", 0.70f),
            new("命运", "命运纺者正在编织丝线，将无数个名字系于同一根弦上...", 0.80f),
            new("启程", "世界已然成形，命运之书翻开了崭新的一页...", 0.90f)
        };
    }

    /// <summary>
    /// 获取加载存档阶段
    /// </summary>
    public List<LoadingPhase> GetLoadSavePhases()
    {
        return new List<LoadingPhase>
        {
            new("回忆", "正在从时间的长河中打捞那些散落的记忆碎片...", 0.00f),
            new("重建", "山川河流按照记忆中的模样逐一重现...", 0.15f),
            new("复苏", "沉睡的灵魂正在苏醒，那些曾经并肩的面孔从迷雾中浮现...", 0.30f),
            new("还原", "昔日的盟友与宿敌各归其位，未完成的战斗还在等你落下最后一子...", 0.45f),
            new("重连", "商人的账本翻开在那一页，酒馆老板娘留着你的老座位...", 0.60f),
            new("命运", "命运纺者轻轻拨弄属于你的丝线，将断裂处重新系紧...", 0.75f),
            new("归来", "晨光穿透云层照亮了你曾走过的路——欢迎回来，冒险者...", 0.90f)
        };
    }

    /// <summary>
    /// 获取战斗加载阶段
    /// </summary>
    public List<LoadingPhase> GetCombatPhases()
    {
        return new List<LoadingPhase>
        {
            new("集结", "号角撕裂了黎明的寂静，战士们沉默地系紧护甲...", 0.00f),
            new("布阵", "旗帜在寒风中猎猎作响，盾墙层层叠起，长矛如荆棘密布...", 0.20f),
            new("对峙", "空气中弥漫着铁锈与魔力的气息，所有人都在等第一滴血落下的声音...", 0.40f),
            new("蓄势", "弓弦拉满，法杖顶端奥术能量盘旋凝聚，只等一声令下...", 0.60f),
            new("交锋", "战鼓擂动，大地在万千脚步中颤抖——命运的齿轮开始转动...", 0.80f)
        };
    }

    /// <summary>
    /// 获取快速游戏阶段
    /// </summary>
    public List<LoadingPhase> GetQuickGamePhases()
    {
        return new List<LoadingPhase>
        {
            new("命运骰动", "一枚骨骰在虚空中翻转，每一面都映照着一个截然不同的人生...", 0.00f),
            new("灵魂降临", "一个崭新的灵魂带着尚未褪去的星尘，坠入这具躯壳之中...", 0.30f),
            new("世界回应", "大地感知到了新的脚步，远方的道路扬起尘土为你铺设前路...", 0.60f),
            new("启程", "你踩在未曾有人走过的土地上，冒险从这一步开始...", 0.85f)
        };
    }

    /// <summary>
    /// 获取快速战斗阶段
    /// </summary>
    public List<LoadingPhase> GetQuickCombatPhases()
    {
        return new List<LoadingPhase>
        {
            new("骰运", "命运之骰在掌心咔嗒作响，战场的天平尚未倾斜...", 0.00f),
            new("列阵", "旗帜猎猎作响，战士们沉默地在各自的位置上站定，等待命运的裁决...", 0.20f),
            new("号角", "号角声撕裂了黎明的寂静，铁与血的气息弥漫在每一寸空气中...", 0.40f),
            new("交锋", "战鼓擂动，大地在万千脚步中颤抖——命运的齿轮开始转动...", 0.60f),
            new("血战", "刀光剑影之间，生死只在一线——唯有胜利者才能书写历史...", 0.80f)
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
