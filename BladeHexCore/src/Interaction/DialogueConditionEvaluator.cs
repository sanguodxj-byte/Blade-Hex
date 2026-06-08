using System;
using System.Text.RegularExpressions;
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 对话条件表达式评估器 — 解析并执行 JSON 中的条件字符串。
///
/// 支持语法：
///   数值比较:  player_army >= npc_army * 3
///              relation &lt;= -50
///              faction_rep > 20
///              player_gold >= 150
///   字符串比较:npc_race == "orc"
///              player_race != "undead"
///   多条件:    player_army >= npc_army * 2 &amp;&amp; relation >= 0
///
/// 变量名映射到 DialogueContext 的属性，右侧可以是字面量或另一个变量乘积。
/// </summary>
public static class DialogueConditionEvaluator
{
    // ── 顶层入口 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 对外唯一接口：传入原始条件字符串（可为 null/空），返回是否满足。
    /// </summary>
    public static bool Evaluate(string? condition, DialogueContext ctx)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;

        // 先按 && 拆分为多个子条件（全部满足才通过）
        string[] parts = condition.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            if (!EvaluateSingle(part.Trim(), ctx))
                return false;
        }
        return true;
    }

    // ── 单条件解析 ────────────────────────────────────────────────────────────────

    private static readonly Regex CompareRegex = new(
        @"^(\w+)\s*(>=|<=|==|!=|>|<)\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool EvaluateSingle(string expr, DialogueContext ctx)
    {
        var match = CompareRegex.Match(expr);
        if (!match.Success)
        {
            GD.PrintErr($"[DialogueConditionEvaluator] 无法解析条件: '{expr}'");
            return false;
        }

        string leftToken = match.Groups[1].Value.Trim();
        string op        = match.Groups[2].Value.Trim();
        string rightExpr = match.Groups[3].Value.Trim();

        // 尝试数值比较
        if (TryResolveNumeric(leftToken, ctx, out double leftNum) &&
            TryEvalNumericExpr(rightExpr, ctx, out double rightNum))
        {
            return CompareNumbers(leftNum, op, rightNum);
        }

        // 尝试字符串比较
        string leftStr  = ResolveString(leftToken, ctx);
        string rightStr = rightExpr.Trim('"').Trim('\'').ToLowerInvariant();

        return CompareStrings(leftStr, op, rightStr);
    }

    // ── 变量解析 ─────────────────────────────────────────────────────────────────

    private static bool TryResolveNumeric(string token, DialogueContext ctx, out double value)
    {
        value = 0;
        switch (token.ToLowerInvariant())
        {
            case "player_army":  value = ctx.PlayerArmySize;           return true;
            case "npc_army":     value = ctx.NpcArmySize;              return true;
            case "relation":     value = ctx.PlayerNpcRelation;        return true;
            case "faction_rep":  value = ctx.PlayerFactionReputation;  return true;
            case "player_gold":  value = ctx.PlayerGold;               return true;
            case "player_power": value = ctx.PlayerPowerRating;        return true;
            case "npc_power":    value = ctx.NpcPowerRating;           return true;
        }
        if (double.TryParse(token, out double lit)) { value = lit; return true; }
        return false;
    }

    private static string ResolveString(string token, DialogueContext ctx) =>
        token.ToLowerInvariant() switch
        {
            "npc_race"    => ctx.npc_race,
            "player_race" => ctx.player_race,
            "npc_faction" => ctx.NpcFaction.ToLowerInvariant(),
            _             => token.Trim('"').Trim('\'').ToLowerInvariant()
        };

    // ── 右侧数值表达式（支持 npc_army * 3 这类乘积）────────────────────────────

    private static readonly Regex ArithRegex = new(
        @"^(\w+)\s*([*\/+\-])\s*(\d+(?:\.\d+)?)$",
        RegexOptions.Compiled);

    private static bool TryEvalNumericExpr(string expr, DialogueContext ctx, out double result)
    {
        result = 0;
        expr = expr.Trim();

        // 先尝试简单字面量
        if (double.TryParse(expr, out result)) return true;

        // 再尝试 token * literal 的形式
        var m = ArithRegex.Match(expr);
        if (m.Success)
        {
            string varToken = m.Groups[1].Value;
            string arithOp  = m.Groups[2].Value;
            double literal  = double.Parse(m.Groups[3].Value);

            if (TryResolveNumeric(varToken, ctx, out double varVal))
            {
                result = arithOp switch
                {
                    "*" => varVal * literal,
                    "/" => literal != 0 ? varVal / literal : 0,
                    "+" => varVal + literal,
                    "-" => varVal - literal,
                    _   => varVal
                };
                return true;
            }
        }

        // 尝试解析为单个变量
        return TryResolveNumeric(expr, ctx, out result);
    }

    // ── 比较操作 ─────────────────────────────────────────────────────────────────

    private static bool CompareNumbers(double left, string op, double right) => op switch
    {
        ">="  => left >= right,
        "<="  => left <= right,
        "=="  => Math.Abs(left - right) < 1e-9,
        "!="  => Math.Abs(left - right) >= 1e-9,
        ">"   => left >  right,
        "<"   => left <  right,
        _     => false
    };

    private static bool CompareStrings(string left, string op, string right) => op switch
    {
        "==" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
        "!=" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
        _    => false
    };
}
