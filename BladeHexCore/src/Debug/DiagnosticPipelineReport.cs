using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;

namespace BladeHex.Diagnostics;

public sealed class DiagnosticPipelineReport
{
    private readonly List<DiagnosticPipelineStep> _steps = new();
    private readonly Dictionary<string, object?> _metadata = new();
    private readonly ulong _startedTicks = Time.GetTicksMsec();

    public string Category { get; }
    public string ReportId { get; }
    public string Status { get; private set; } = "running";
    public string StartedAt { get; } = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
    public string FinishedAt { get; private set; } = "";
    public long ElapsedMs { get; private set; }
    public string? FailedStep { get; private set; }
    public string? ExceptionType { get; private set; }
    public string? ExceptionMessage { get; private set; }
    public string? ExceptionStackTrace { get; private set; }

    public DiagnosticPipelineReport(string category, string reportId, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        Category = category;
        ReportId = reportId;
        if (metadata != null)
        {
            foreach (var kv in metadata)
                _metadata[kv.Key] = kv.Value;
        }
    }

    public DiagnosticPipelineStep BeginStep(string name, IReadOnlyDictionary<string, object?>? snapshot = null)
    {
        var step = new DiagnosticPipelineStep
        {
            Index = _steps.Count + 1,
            Name = name,
            Status = "running",
            StartedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
            Before = snapshot != null ? new Dictionary<string, object?>(snapshot) : new Dictionary<string, object?>(),
        };
        step.StartTicks = Time.GetTicksMsec();
        _steps.Add(step);
        return step;
    }

    public void EndStep(DiagnosticPipelineStep step, IReadOnlyDictionary<string, object?>? snapshot = null)
    {
        step.EndedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        step.ElapsedMs = (long)(Time.GetTicksMsec() - step.StartTicks);
        step.Status = "ok";
        step.After = snapshot != null ? new Dictionary<string, object?>(snapshot) : new Dictionary<string, object?>();
    }

    public void FailStep(DiagnosticPipelineStep step, Exception ex, IReadOnlyDictionary<string, object?>? snapshot = null)
    {
        step.EndedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        step.ElapsedMs = (long)(Time.GetTicksMsec() - step.StartTicks);
        step.Status = "failed";
        step.After = snapshot != null ? new Dictionary<string, object?>(snapshot) : new Dictionary<string, object?>();
        step.ExceptionType = ex.GetType().FullName ?? ex.GetType().Name;
        step.ExceptionMessage = ex.Message;
        step.ExceptionStackTrace = ex.StackTrace ?? "";

        Status = "failed";
        FailedStep = step.Name;
        ExceptionType = step.ExceptionType;
        ExceptionMessage = step.ExceptionMessage;
        ExceptionStackTrace = step.ExceptionStackTrace;
    }

    public void Complete()
    {
        if (Status == "running")
            Status = "ok";
    }

    public void Fail(Exception ex)
    {
        Status = "failed";
        ExceptionType ??= ex.GetType().FullName ?? ex.GetType().Name;
        ExceptionMessage ??= ex.Message;
        ExceptionStackTrace ??= ex.StackTrace ?? "";
    }

    public IReadOnlyDictionary<string, string> FinishAndWrite()
    {
        FinishedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        ElapsedMs = (long)(Time.GetTicksMsec() - _startedTicks);
        if (Status == "running")
            Status = "aborted";

        return DiagnosticLog.WriteReport(GetReportLevel(), Category, ReportId, ToSerializable(), ToMarkdown());
    }

    public Dictionary<string, object?> ToSerializable() => new()
    {
        ["report_id"] = ReportId,
        ["report_level"] = GetReportLevel().ToString().ToLowerInvariant(),
        ["category"] = Category,
        ["status"] = Status,
        ["started_at"] = StartedAt,
        ["finished_at"] = FinishedAt,
        ["elapsed_ms"] = ElapsedMs,
        ["failed_step"] = FailedStep,
        ["exception_type"] = ExceptionType,
        ["exception_message"] = ExceptionMessage,
        ["exception_stack_trace"] = ExceptionStackTrace,
        ["metadata"] = _metadata,
        ["steps"] = _steps,
    };

    private DiagnosticReportLevel GetReportLevel()
    {
        return Status switch
        {
            "failed" => DiagnosticReportLevel.Error,
            "aborted" => DiagnosticReportLevel.Warn,
            _ => DiagnosticReportLevel.Debug,
        };
    }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Category} Diagnostic Report");
        sb.AppendLine();
        sb.AppendLine($"- Report: `{ReportId}`");
        sb.AppendLine($"- Status: `{Status}`");
        sb.AppendLine($"- Started: `{StartedAt}`");
        sb.AppendLine($"- Finished: `{FinishedAt}`");
        sb.AppendLine($"- Elapsed: `{ElapsedMs} ms`");
        if (!string.IsNullOrWhiteSpace(FailedStep))
            sb.AppendLine($"- Failed step: `{FailedStep}`");
        if (!string.IsNullOrWhiteSpace(ExceptionMessage))
            sb.AppendLine($"- Exception: `{ExceptionType}: {ExceptionMessage}`");

        if (_metadata.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Metadata");
            foreach (var kv in _metadata)
                sb.AppendLine($"- {kv.Key}: `{Format(kv.Value)}`");
        }

        sb.AppendLine();
        sb.AppendLine("## Timeline");
        sb.AppendLine();
        sb.AppendLine("| # | Step | Status | ms | Snapshot |");
        sb.AppendLine("|---:|---|---|---:|---|");
        foreach (var step in _steps)
        {
            sb.AppendLine($"| {step.Index} | {EscapeMd(step.Name)} | {step.Status} | {step.ElapsedMs} | {EscapeMd(Compact(step.After.Count > 0 ? step.After : step.Before))} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Step Details");
        foreach (var step in _steps)
        {
            sb.AppendLine();
            sb.AppendLine($"### {step.Index}. {step.Name}");
            sb.AppendLine();
            sb.AppendLine($"- Status: `{step.Status}`");
            sb.AppendLine($"- Elapsed: `{step.ElapsedMs} ms`");
            if (!string.IsNullOrWhiteSpace(step.ExceptionMessage))
                sb.AppendLine($"- Exception: `{step.ExceptionType}: {step.ExceptionMessage}`");
            AppendMap(sb, "Before", step.Before);
            AppendMap(sb, "After", step.After);
        }

        if (!string.IsNullOrWhiteSpace(ExceptionStackTrace))
        {
            sb.AppendLine();
            sb.AppendLine("## Exception Stack");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(ExceptionStackTrace);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private static void AppendMap(StringBuilder sb, string title, IReadOnlyDictionary<string, object?> values)
    {
        if (values.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine($"#### {title}");
        foreach (var kv in values)
            sb.AppendLine($"- {kv.Key}: `{Format(kv.Value)}`");
    }

    private static string Compact(IReadOnlyDictionary<string, object?> values)
    {
        var parts = new List<string>();
        foreach (var kv in values)
            parts.Add($"{kv.Key}={Format(kv.Value)}");
        return string.Join(", ", parts);
    }

    private static string Format(object? value)
    {
        return value switch
        {
            null => "null",
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private static string EscapeMd(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

public sealed class DiagnosticPipelineStep
{
    internal ulong StartTicks { get; set; }
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "running";
    public string StartedAt { get; set; } = "";
    public string EndedAt { get; set; } = "";
    public long ElapsedMs { get; set; }
    public Dictionary<string, object?> Before { get; set; } = new();
    public Dictionary<string, object?> After { get; set; } = new();
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionStackTrace { get; set; }
}
