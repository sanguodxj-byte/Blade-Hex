using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 玩家所属国家解析器
/// </summary>
public class PlayerNationResolver
{
    private string? _currentNation;
    private string? _pendingNation;
    private int _pendingSinceDay = 0;

    /// <summary>
    /// 获取当前玩家所加入/代表的国家（声望最高且 >= 30，且稳定维持了 7 天）
    /// </summary>
    public string? GetCurrent(ReputationTracker reputation, int currentDay)
    {
        if (reputation == null) return _currentNation;

        // 1. 查找声望最高且 >= 30 的势力
        var reps = reputation.GetAllReputationsGd();
        string? maxNation = null;
        int maxVal = -999;

        foreach (var key in reps.Keys)
        {
            string nationId = key.AsString();
            int val = reps[key].AsInt32();
            if (val > maxVal)
            {
                maxVal = val;
                maxNation = nationId;
            }
        }

        // 必须声望最高且 >= 30 点才符合候选拥护势力条件
        string? candidate = (maxNation != null && maxVal >= 30) ? maxNation : null;

        if (candidate != _currentNation)
        {
            // 声望最高势力的候选人变化
            if (candidate != _pendingNation)
            {
                _pendingNation = candidate;
                _pendingSinceDay = currentDay;
                GD.Print($"[PlayerNationResolver] 玩家国家候选更改为: {candidate}，稳定期开始于 D{currentDay}");
            }
            else
            {
                // 稳定 7 天窗口后才正式切换生效
                if (currentDay - _pendingSinceDay >= 7)
                {
                    _currentNation = candidate;
                    _pendingNation = null;
                    _pendingSinceDay = 0;
                    GD.Print($"[PlayerNationResolver] 玩家拥护国家已正式切换为: {candidate} (当前游戏天数: {currentDay})");
                }
            }
        }
        else
        {
            // 如果候选人与当前拥护国重新保持一致，取消 Pending
            _pendingNation = null;
            _pendingSinceDay = 0;
        }

        return _currentNation;
    }
}
