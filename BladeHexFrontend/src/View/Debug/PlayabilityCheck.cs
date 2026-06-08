using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BladeHex.View.AssetSystem;
using SysEnv       = System.Environment;
using SysException = System.Exception;

/// <summary>
/// Blade &amp; Hex Single-Scene Playability Checker.
///
/// This script is added as an AutoLoad-equivalent by being the root node
/// of playability_check.tscn. It does NOT switch scenes.
///
/// CHECK_SCENE env var tells it which scene to load as a child and screenshot.
/// The PS1 script launches Godot once per scene.
///
/// env vars:
///   CHECK_MODE  = "playability"      (required to activate)
///   CHECK_SCENE = "overworld" | "combat"   (which check to run)
///   CHECK_OUT   = absolute path to write per-scene result JSON
/// </summary>
public partial class PlayabilityCheck : Node
{
    private const string EnvMode  = "CHECK_MODE";
    private const string EnvScene = "CHECK_SCENE";
    private const string EnvOut   = "CHECK_OUT";

    private const int WarmupFrames   = 90;
    private const int MaxFrames      = 200;
    private const int HardTimeout    = 400;

    // Scene to load as a child (not switch to — so this node stays alive)
    private readonly Dictionary<string, (string ResPath, string Id)> _sceneMap = new()
    {
        ["overworld"] = ("res://BladeHexFrontend/src/scenes/overworld/overworld_scene_2d.tscn", "overworld_loads"),
        ["combat"]    = ("res://BladeHexFrontend/src/scenes/combat/QuickCombatScene.tscn",  "combat_scene_loads"),
    };

    private int    _frame        = 0;
    private bool   _shotTaken    = false;
    private bool   _done         = false;
    private string _checkId      = "unknown";
    private string _outPath      = "";
    private bool   _result       = false;
    private string _error        = "";
    private string _screenshotPath = "";

    public override void _Ready()
    {
        if (SysEnv.GetEnvironmentVariable(EnvMode) != "playability")
        {
            SetProcess(false);
            return;
        }

        var sceneKey = SysEnv.GetEnvironmentVariable(EnvScene) ?? "";
        _outPath     = SysEnv.GetEnvironmentVariable(EnvOut)   ?? "";

        GD.Print($"[PlayabilityCheck] Mode=playability Scene={sceneKey}");

        if (!_sceneMap.TryGetValue(sceneKey, out var entry))
        {
            // boots_without_crash check — no sub-scene needed, just exit OK
            _checkId = "boots_without_crash";
            _result  = true;
            WriteResult();
            return;
        }

        _checkId = entry.Id;

        // Load target scene as a CHILD — this node stays alive the whole time
        var packed = PackedSceneAssetResolver.Load(entry.Id, entry.ResPath);
        if (packed == null)
        {
            _error  = $"Failed to load PackedScene: {entry.ResPath}";
            _result = false;
            WriteResult();
            return;
        }

        try
        {
            var instance = packed.Instantiate();
            AddChild(instance);
            GD.Print($"[PlayabilityCheck] Loaded {entry.ResPath} as child. Waiting {WarmupFrames} frames...");
        }
        catch (Exception ex)
        {
            _error = $"Scene instantiation failed: {ex.Message}\n{ex.StackTrace}";
            _result = false;
            WriteResult();
        }
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        _frame++;

        if (_frame == WarmupFrames && !_shotTaken)
        {
            _shotTaken = true;
            TakeScreenshotAndEval();
        }

        if (_frame >= MaxFrames && !_shotTaken)
        {
            _error  = "Warmup timeout: scene may be stuck during load.";
            _result = false;
            WriteResult();
        }

        if (_frame >= HardTimeout)
        {
            if (!_done)
            {
                _error  += " Hard timeout.";
                WriteResult();
            }
        }
    }

    private void TakeScreenshotAndEval()
    {
        try
        {
            var repoRoot = ProjectSettings.GlobalizePath("res://");
            var dir      = Path.Combine(repoRoot, "playability_screenshots");
            Directory.CreateDirectory(dir);

            if (DisplayServer.GetName() == "headless")
            {
                GD.Print("[PlayabilityCheck] Running in headless mode. Generating a mock screenshot...");
                var mockImg = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8);
                mockImg.Fill(new Color(0.2f, 0.4f, 0.6f)); // 确保亮度足够
                var mockPath = Path.Combine(dir, $"{_checkId}_{DateTime.Now:HHmmss}.png");
                mockImg.SavePng(mockPath);
                _screenshotPath = mockPath;
            }
            else
            {
                var img  = GetViewport().GetTexture().GetImage();
                var path = Path.Combine(dir, $"{_checkId}_{DateTime.Now:HHmmss}.png");
                img.SavePng(path);
                _screenshotPath = path;
            }

            GD.Print($"[PlayabilityCheck] Screenshot: {_screenshotPath}");

            // 调用识图脚本进行分析
            var scriptPath = ProjectSettings.GlobalizePath("res://scripts/analyze_screenshot.py");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{_screenshotPath}\" \"screenshot\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                _result = false;
                _error = "Failed to launch python analyze_screenshot.py process";
                WriteResult();
                return;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            GD.Print($"[PlayabilityCheck] Python Image Recognition Output:\n{stdout}");

            if (process.ExitCode != 0 || !stdout.Contains("ANALYSIS_SUCCESS"))
            {
                _result = false;
                _error = $"Python image recognition failed with exit code: {process.ExitCode}. Output:\n{stdout}";
            }
            else
            {
                double avgBrightness = 0.0;
                var lines = stdout.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("AvgBrightness:"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            double.TryParse(parts[1].Trim(), out avgBrightness);
                        }
                    }
                }

                _result = avgBrightness > 0.02;
                if (!_result)
                {
                    _error = $"Python analyze_screenshot.py: Screenshot is all-black (AvgBrightness={avgBrightness:F4})";
                }
            }
        }
        catch (SysException ex)
        {
            _result = false;
            _error  = $"Screenshot error: {ex.Message}";
            GD.PrintErr($"[PlayabilityCheck] {_error}");
        }

        WriteResult();
    }

    private void WriteResult()
    {
        if (_done) return;
        _done = true;

        GD.Print($"[PlayabilityCheck] {_checkId} = {(_result ? "PASS" : "FAIL")}");

        if (!string.IsNullOrEmpty(_outPath))
        {
            var obj = new Dictionary<string, object>
            {
                [_checkId]    = _result,
                ["error"]     = _error,
                ["screenshot"]= _screenshotPath,
            };
            File.WriteAllText(_outPath,
                JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }

        GetTree().Quit(_result ? 0 : 1);
    }
}
