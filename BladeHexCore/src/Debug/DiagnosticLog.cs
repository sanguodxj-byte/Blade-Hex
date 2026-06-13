using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace BladeHex.Diagnostics;

public enum DiagnosticReportLevel
{
    Error,
    Warn,
    Debug,
}

public static class DiagnosticLog
{
    private const string CompanyOrProductDir = "blade&hex";
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly Dictionary<DiagnosticReportLevel, FileStream> Streams = new();
    private static readonly Dictionary<DiagnosticReportLevel, StreamWriter> Writers = new();
    private static string _rootPath = "";
    private static bool _hooksInstalled;

    public static string CurrentLogPath
    {
        get
        {
            EnsureOpen();
            return GetLogPath(DiagnosticReportLevel.Debug);
        }
    }

    public static string GetLogPathForLevel(DiagnosticReportLevel level)
    {
        EnsureOpen();
        return GetLogPath(level);
    }

    public static string CurrentLogRoot
    {
        get
        {
            EnsureOpen();
            return _rootPath;
        }
    }

    public static void Info(string message) => Write(DiagnosticReportLevel.Debug, "INFO", message);
    public static void Warn(string message) => Write(DiagnosticReportLevel.Warn, "WARN", message);
    public static void Error(string message) => Write(DiagnosticReportLevel.Error, "ERROR", message);

    public static void Exception(string context, Exception ex)
    {
        Write(DiagnosticReportLevel.Error, "EXCEPTION", $"{context}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }

    public static void Event(string scope, string name, IReadOnlyDictionary<string, object?>? fields = null)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(scope).Append("] ").Append(name);
        if (fields != null)
        {
            foreach (var kv in fields)
            {
                sb.Append(' ')
                    .Append(kv.Key)
                    .Append('=')
                    .Append(FormatValue(kv.Value));
            }
        }

        Write(DiagnosticReportLevel.Debug, "EVENT", sb.ToString());
    }

    public static IReadOnlyDictionary<string, string> WriteReport(
        DiagnosticReportLevel level,
        string category,
        string baseName,
        object jsonReport,
        string markdownReport)
    {
        lock (Sync)
        {
            try
            {
                EnsureOpenLocked();

                string safeLevel = level.ToString().ToLowerInvariant();
                string safeCategory = SanitizePathSegment(category);
                string safeBaseName = SanitizePathSegment(baseName);
                string dir = Path.Combine(_rootPath, "reports", safeLevel, safeCategory);
                Directory.CreateDirectory(dir);

                string jsonPath = Path.Combine(dir, safeBaseName + ".json");
                string mdPath = Path.Combine(dir, safeBaseName + ".md");

                File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonReport, JsonOptions), new UTF8Encoding(false));
                File.WriteAllText(mdPath, markdownReport, new UTF8Encoding(false));

                WriteLocked(level, "INFO", $"[DiagnosticLog] report written level={safeLevel} category={safeCategory} json={jsonPath} md={mdPath}");
                return new Dictionary<string, string>
                {
                    ["level"] = safeLevel,
                    ["json"] = jsonPath,
                    ["markdown"] = mdPath,
                };
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[DiagnosticLog] report write failed: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }
    }

    public static void Flush()
    {
        lock (Sync)
        {
            try
            {
                foreach (var writer in Writers.Values)
                    writer.Flush();
                foreach (var stream in Streams.Values)
                    stream.Flush(flushToDisk: true);
            }
            catch
            {
                // Logging must never become the cause of a crash.
            }
        }
    }

    private static void Write(DiagnosticReportLevel reportLevel, string level, string message)
    {
        lock (Sync)
        {
            try
            {
                WriteLocked(reportLevel, level, message);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[DiagnosticLog] write failed: {ex.Message}");
            }
        }
    }

    private static void WriteLocked(DiagnosticReportLevel reportLevel, string level, string message)
    {
        EnsureOpenLocked();
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        var writer = Writers[reportLevel];
        var stream = Streams[reportLevel];
        writer.WriteLine(line);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void EnsureOpen()
    {
        lock (Sync)
        {
            EnsureOpenLocked();
        }
    }

    private static void EnsureOpenLocked()
    {
        if (Writers.Count > 0)
            return;

        InstallHooksLocked();

        _rootPath = ResolveLogRoot();
        Directory.CreateDirectory(_rootPath);

        foreach (var level in new[] { DiagnosticReportLevel.Error, DiagnosticReportLevel.Warn, DiagnosticReportLevel.Debug })
        {
            string path = GetLogPath(level);
            var stream = new FileStream(path, FileMode.Append, System.IO.FileAccess.Write, FileShare.ReadWrite);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };

            Streams[level] = stream;
            Writers[level] = writer;

            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [INFO] [DiagnosticLog] session started root={_rootPath} path={path}");
            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [INFO] [DiagnosticLog] godot={Engine.GetVersionInfo().GetValueOrDefault("string", "unknown")}");
            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [INFO] [DiagnosticLog] os={OS.GetName()} pid={System.Environment.ProcessId}");
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }

    private static string GetLogPath(DiagnosticReportLevel level)
    {
        string suffix = level.ToString().ToLowerInvariant();
        return Path.Combine(_rootPath, $"player_{suffix}.log");
    }

    private static string ResolveLogRoot()
    {
        try
        {
            if (OS.GetName().Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                string local = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                string? appData = Directory.GetParent(local)?.FullName;
                if (!string.IsNullOrWhiteSpace(appData))
                    return Path.Combine(appData, "LocalLow", CompanyOrProductDir);
            }
        }
        catch
        {
            // Fall through to Godot's user path.
        }

        return Path.Combine(ProjectSettings.GlobalizePath("user://"), "AppData", "LocalLow", CompanyOrProductDir);
    }

    private static void InstallHooksLocked()
    {
        if (_hooksInstalled)
            return;

        _hooksInstalled = true;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Exception("UnhandledException", ex);
            else
                Error($"UnhandledException: {args.ExceptionObject}");
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Info("[DiagnosticLog] process exit");
            Flush();
        };
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
            return "null";

        string s = value.ToString() ?? "";
        if (s.Length == 0)
            return "\"\"";

        return s.Contains(' ') || s.Contains('\n') || s.Contains('\r')
            ? '"' + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + '"'
            : s;
    }

    private static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "report";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (Array.IndexOf(invalid, c) >= 0 || char.IsControl(c))
                sb.Append('_');
            else
                sb.Append(c);
        }

        return sb.ToString().Trim().Replace(' ', '_');
    }
}
