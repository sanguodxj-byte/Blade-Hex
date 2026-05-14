// migrate_tres_icon_paths.cs — T-302
// .tres 资源迁移工具：ExtResource 图标引用 → string icon_id
//
// 迁移模式：
//   icon = ExtResource("abc123")         → icon_id = "icon_of_x"
//   icon = ExtResource("res://...png")   → icon_id = "icon_of_x"  (ID = 文件名去扩展名)
//
// 使用方式（从 Godot 场景或 standalone harness 调用）：
//   TresIconMigrator.RunDryScan()        — 仅扫描输出报告
//   TresIconMigrator.Run(migrate: true)  — 执行迁移（自动备份）
//
// 退出条件：
//   grep -r 'ExtResource' --include=*.tres 中 Texture2D 类型为 0
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace BladeHex.Tools;

public static class TresIconMigrator
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>.tres 扫描根目录（相对项目根）</summary>
    private static readonly string[] SearchRoots = { "src" };

    /// <summary>备份目录名</summary>
    private const string BackupDirSuffix = ".bak_migrate_tres";

    /// <summary>项目根目录（调用方通过 AppContext.BaseDirectory 推导，或手动传入）</summary>
    public static string ProjectRoot { get; set; } = GetDefaultProjectRoot();

    // ========================================
    // 数据结构
    // ========================================

    /// <summary>单次迁移结果</summary>
    public sealed record MigrationEntry(
        string FilePath,
        int LineNumber,
        string OriginalLine,
        string NewLine,
        string ExtractedId,
        string ExtractedPath,
        bool IsIcon
    );

    /// <summary>迁移报告</summary>
    public sealed record MigrationReport(
        int FilesScanned,
        int FilesModified,
        int EntriesFound,
        int EntriesMigrated,
        List<MigrationEntry> Entries,
        string BackupPath,
        List<string> Errors
    );

    // ========================================
    // 正则
    // ========================================

    // 匹配: icon = ExtResource("abc123")   或 icon = ExtResource("res://path/icon.png")
    // 或任何字段 = ExtResource(...) where ExtResource type is Texture2D/SpriteFrames
    private static readonly Regex ExtResourcePattern = new(
        @"^\s*(?<field>\w+)\s*=\s*ExtResource\(\s*""(?<id>[^""]+)""\s*\)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // 匹配文件头的 ExtResource 声明区:
    // [ext_resource type="Texture2D" path="res://assets/.../icon.png" id="abc123"]
    private static readonly Regex ExtResourceDeclPattern = new(
        @"\[ext_resource\s+type=""(?<type>[^""]+)""\s+path=""(?<path>[^""]+)""\s+id=""(?<id>[^""]+)""\]",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ========================================
    // 主入口
    // ========================================

    /// <summary>
    /// 执行迁移
    /// </summary>
    /// <param name="migrate">true=执行写入, false=仅 dry-run 扫描</param>
    /// <param name="projectRoot">项目根目录（默认自动推导）</param>
    public static MigrationReport Run(bool migrate = false, string? projectRoot = null)
    {
        if (projectRoot != null) ProjectRoot = projectRoot;
        var entries = new List<MigrationEntry>();
        var errors = new List<string>();
        var modifiedFiles = new HashSet<string>();

        // 搜集所有 .tres 文件
        var tresFiles = new List<string>();
        foreach (var root in SearchRoots)
        {
            var dir = Path.Combine(ProjectRoot, root);
            if (Directory.Exists(dir))
                tresFiles.AddRange(Directory.GetFiles(dir, "*.tres", SearchOption.AllDirectories));
        }

        int filesScanned = tresFiles.Count;

        foreach (var file in tresFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                var original = content;

                // Step 1: 解析 [ext_resource] 声明区中的 Texture2D 资源
                var extDecls = new Dictionary<string, (string type, string path)>();
                foreach (Match m in ExtResourceDeclPattern.Matches(content))
                {
                    var type = m.Groups["type"].Value;
                    var path = m.Groups["path"].Value;
                    var id = m.Groups["id"].Value;
                    extDecls[id] = (type, path);
                }

                // Step 2: 匹配内容行中的 ExtResource 引用
                bool fileChanged = false;
                foreach (Match m in ExtResourcePattern.Matches(content))
                {
                    var field = m.Groups["field"].Value;
                    var extId = m.Groups["id"].Value;
                    var lineNum = LineNumber(content, m.Index);

                    // 只处理 icon/sprite/icon_id 等渲染类字段
                    if (!IsRenderField(field))
                        continue;

                    var entry = new MigrationEntry(
                        FilePath: file,
                        LineNumber: lineNum,
                        OriginalLine: m.Value.Trim(),
                        NewLine: "",
                        ExtractedId: extId,
                        ExtractedPath: "",
                        IsIcon: IsIconField(field)
                    );

                    // 查 ExtResource 声明
                    if (extDecls.TryGetValue(extId, out var decl))
                    {
                        entry = entry with
                        {
                            ExtractedPath = decl.path,
                            NewLine = GenerateNewLine(field, decl.path),
                        };
                    }
                    else
                    {
                        // 无声明：用 extId 作为 icon_id（可能是直接资源路径）
                        entry = entry with
                        {
                            ExtractedPath = extId,
                            NewLine = $"{field}_id = \"{Path.GetFileNameWithoutExtension(extId)}\"",
                        };
                    }

                    entries.Add(entry);

                    // 执行替换
                    if (migrate)
                    {
                        content = content.Replace(m.Value.Trim(), entry.NewLine);
                        fileChanged = true;
                        modifiedFiles.Add(file);
                    }
                }

                // 写入（migrate=true 时）
                if (migrate && fileChanged)
                {
                    var backupPath = CreateBackup(file);
                    File.WriteAllText(file, content);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{file}: {ex.Message}");
            }
        }

        var backupDir = migrate ? CreateProjectBackupDir() : "";

        return new MigrationReport(
            FilesScanned: filesScanned,
            FilesModified: modifiedFiles.Count,
            EntriesFound: entries.Count,
            EntriesMigrated: migrate ? entries.Count : 0,
            Entries: entries,
            BackupPath: backupDir,
            Errors: errors
        );
    }

    /// <summary>仅 dry-run 扫描</summary>
    public static MigrationReport RunDryScan(string? projectRoot = null)
        => Run(migrate: false, projectRoot);

    // ========================================
    // 辅助方法
    // ========================================

    private static bool IsRenderField(string field) => field switch
    {
        "icon" => true,
        "portrait" => true,
        "sprite" => true,
        "texture" => true,
        "image" => true,
        _ => false,
    };

    private static bool IsIconField(string field) => field switch
    {
        "icon" => true,
        "portrait" => true,
        _ => false,
    };

    private static string GenerateNewLine(string field, string resPath)
    {
        // 从 res://.../icon_name.png 提取 "icon_name"
        var id = Path.GetFileNameWithoutExtension(resPath);
        // 如果路径包含子目录，用子目录名作为前缀
        var dir = Path.GetDirectoryName(resPath);
        if (!string.IsNullOrEmpty(dir) && dir.Contains('/'))
        {
            var subDir = dir.Substring(dir.LastIndexOf('/') + 1);
            // 仅当文件名不带目录信息时使用
        }
        return $"    {field}_id = \"{id}\"";
    }

    private static int LineNumber(string content, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n') line++;
        }
        return line;
    }

    private static string CreateBackup(string filePath)
    {
        var backupDir = Path.Combine(Path.GetDirectoryName(filePath)!, BackupDirSuffix);
        Directory.CreateDirectory(backupDir);
        var backupFile = Path.Combine(backupDir, Path.GetFileName(filePath));
        File.Copy(filePath, backupFile, overwrite: true);
        return backupFile;
    }

    private static string CreateProjectBackupDir()
    {
        var dir = Path.Combine(ProjectRoot, BackupDirSuffix);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string GetDefaultProjectRoot()
    {
        // 从 AppContext.BaseDirectory 向上找包含 BladeHex.sln 的目录
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 5; i++)
        {
            if (File.Exists(Path.Combine(dir, "BladeHex.sln")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return AppContext.BaseDirectory;
    }
}
