using System;
using System.Collections.Generic;
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 势力影响力追踪器
/// </summary>
public class InfluenceTracker
{
    private readonly Dictionary<string, int> _influence = new();

    /// <summary>
    /// 获取指定国家的影响力余额
    /// </summary>
    public int Get(string nationId)
    {
        if (string.IsNullOrEmpty(nationId)) return 0;
        return _influence.TryGetValue(nationId, out int val) ? val : 0;
    }

    /// <summary>
    /// 增加指定国家的影响力
    /// </summary>
    public void Add(string nationId, int delta, string reason = "")
    {
        if (string.IsNullOrEmpty(nationId)) return;
        int current = Get(nationId);
        int next = Math.Clamp(current + delta, 0, 200);
        _influence[nationId] = next;
        GD.Print($"[Influence] {nationId} 增加 {delta} 点影响力 (原因: {reason})，当前: {next}/200");
    }

    /// <summary>
    /// 尝试扣减/花费指定国家的影响力
    /// </summary>
    public bool TrySpend(string nationId, int cost, string reason = "")
    {
        if (string.IsNullOrEmpty(nationId)) return false;
        int current = Get(nationId);
        if (current < cost)
        {
            GD.Print($"[Influence] {nationId} 扣减 {cost} 失败，余额不足，当前: {current}");
            return false;
        }
        _influence[nationId] = current - cost;
        GD.Print($"[Influence] {nationId} 扣除 {cost} 点影响力 (原因: {reason})，当前: {current - cost}/200");
        return true;
    }

    /// <summary>
    /// 序列化
    /// </summary>
    public Godot.Collections.Dictionary Serialize()
    {
        var data = new Godot.Collections.Dictionary();
        foreach (var kvp in _influence)
        {
            data[kvp.Key] = kvp.Value;
        }
        return data;
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    public void Deserialize(Godot.Collections.Dictionary data)
    {
        _influence.Clear();
        if (data == null) return;
        foreach (var key in data.Keys)
        {
            _influence[key.AsString()] = data[key].AsInt32();
        }
    }
}
