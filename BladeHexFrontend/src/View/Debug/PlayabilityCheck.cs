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

    private const int WarmupFrames   = 180;
    private const int MaxFrames      = 360;
    private const int HardTimeout    = 600;

    // Scene to load as a child (not switch to — so this node stays alive)
    private readonly Dictionary<string, (string ResPath, string Id)> _sceneMap = new()
    {
        ["overworld"] = ("res://BladeHexFrontend/src/Scenes/overworld/overworld_scene_2d.tscn", "overworld_loads"),
        ["combat"]    = ("res://BladeHexFrontend/src/Scenes/combat/QuickCombatScene.tscn",  "combat_scene_loads"),
    };

    private int    _frame        = 0;
    private bool   _shotTaken    = false;
    private bool   _done         = false;
    private string _checkId      = "unknown";
    private string _outPath      = "";
    private bool   _result       = false;
    private string _error        = "";
    private string _screenshotPath = "";
    private string _sceneKey = "";
    private string _validationDetails = "";
    private Node? _sceneInstance;
    private bool _scenePreparedForScreenshot = false;

    public override void _Ready()
    {
        if (SysEnv.GetEnvironmentVariable(EnvMode) != "playability")
        {
            SetProcess(false);
            return;
        }

        var sceneKey = SysEnv.GetEnvironmentVariable(EnvScene) ?? "";
        _sceneKey = sceneKey;
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
            _sceneInstance = instance;
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

        if (!_scenePreparedForScreenshot && _frame >= 60)
        {
            _scenePreparedForScreenshot = TryPrepareSceneForScreenshot();
        }

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
                _result = false;
                _error = "Playability screenshots require a real display driver. Run check_playability.ps1 without --headless.";
                WriteResult();
                return;
            }

            if (!ValidateSceneGraph(out var sceneDetails))
            {
                _validationDetails = sceneDetails;
                _result = false;
                _error = $"Scene graph validation failed: {sceneDetails}";
                WriteResult();
                return;
            }

            var img = GetViewport().GetTexture().GetImage();
            var imageValidation = ValidateScreenshotImage(img);
            _validationDetails = $"{sceneDetails}; {imageValidation.Details}";

            if (!imageValidation.Ok)
            {
                _result = false;
                _error = $"Screenshot validation failed: {imageValidation.Details}";
                WriteResult();
                return;
            }

            var path = Path.Combine(dir, $"{_checkId}_{DateTime.Now:HHmmss}.png");
            img.SavePng(path);
            _screenshotPath = path;

            GD.Print($"[PlayabilityCheck] Screenshot: {_screenshotPath}");
            GD.Print($"[PlayabilityCheck] Validation: {_validationDetails}");

            _result = true;

            // Keep the recognizer as a best-effort audit, but local render checks
            // are the pass/fail gate so network/API issues cannot mask playability.
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
                GD.PushWarning("[PlayabilityCheck] Failed to launch python analyze_screenshot.py process. Local validation already passed.");
                WriteResult();
                return;
            }

            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            GD.Print($"[PlayabilityCheck] Python Image Recognition Output:\n{stdout}");

            if (process.ExitCode != 0 || !stdout.Contains("ANALYSIS_SUCCESS"))
            {
                GD.PushWarning($"[PlayabilityCheck] Python image recognition failed with exit code {process.ExitCode}. Local validation already passed.\n{stdout}");
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

    private bool TryPrepareSceneForScreenshot()
    {
        if (_sceneKey != "combat")
            return true;

        var deployCtrl = FindFirstDescendant<BladeHex.Scenes.CombatDeploymentController>(_sceneInstance);
        if (deployCtrl == null)
            return false;

        if (!deployCtrl.IsActive)
            return true;

        bool prepared = deployCtrl.AutoPlaceUnitsAndConfirmForPlayabilityCheck();
        if (prepared)
            GD.Print("[PlayabilityCheck] Combat deployment auto-confirmed for screenshot validation.");
        return prepared;
    }

    private bool ValidateSceneGraph(out string details)
    {
        if (_checkId == "boots_without_crash")
        {
            details = "boot-only";
            return true;
        }

        if (_sceneInstance == null || !GodotObject.IsInstanceValid(_sceneInstance))
        {
            details = "target scene instance is missing";
            return false;
        }

        int nodeCount = CountDescendants(_sceneInstance, _ => true);
        int cameras2d = CountDescendants(_sceneInstance, node => node is Camera2D);
        int cameras3d = CountDescendants(_sceneInstance, node => node is Camera3D);
        int hexGrids = CountDescendants(_sceneInstance, node => node.Name == "HexGrid");
        int hexCells = CountDescendants(_sceneInstance, node => node.GetType().Name == "HexCell");
        int units = CountDescendants(_sceneInstance, node => node.GetType().Name == "Unit");

        details = $"scene={_sceneKey}, nodes={nodeCount}, camera2d={cameras2d}, camera3d={cameras3d}, hexGrid={hexGrids}, hexCells={hexCells}, units={units}";

        if (_sceneKey == "combat")
        {
            if (hexGrids < 1) return false;
            if (hexCells < 20) return false;
            if (cameras3d < 1) return false;
            if (units < 5) return false;

            bool renderLayersOk = ValidateCombatRenderLayers(out var renderLayerDetails);
            details = $"{details}; {renderLayerDetails}";
            if (!renderLayersOk) return false;
        }
        else if (_sceneKey == "overworld")
        {
            if (nodeCount < 5) return false;
            if (cameras2d + cameras3d < 1) return false;
        }

        return true;
    }

    private bool ValidateCombatRenderLayers(out string details)
    {
        var errors = new List<string>();
        var sceneRoot = _sceneInstance;
        if (sceneRoot == null || !GodotObject.IsInstanceValid(sceneRoot))
        {
            details = "renderLayers=missing scene root";
            return false;
        }

        float texture = BladeHex.View.Combat.CombatLayerHeight.TextureLayer;
        float overlay = BladeHex.View.Combat.CombatLayerHeight.OverlayLayer;
        float uiHint = BladeHex.View.Combat.CombatLayerHeight.UIHintLayer;
        float character = BladeHex.View.Combat.CombatLayerHeight.CharacterLayer;
        float top = BladeHex.View.Combat.CombatLayerHeight.HexTopOffset;

        if (!(0f <= texture && texture < overlay && overlay < uiHint && uiHint < character))
        {
            errors.Add($"static order invalid: texture={texture:F2}, overlay={overlay:F2}, uiHint={uiHint:F2}, character={character:F2}");
        }

        var grid = FindFirstDescendant<BladeHex.Map.HexGrid>(_sceneInstance);
        if (grid == null)
        {
            details = "renderLayers=missing HexGrid";
            return false;
        }

        int grassChecked = ValidateSurfaceSprites(
            FindFirstDescendant<BladeHex.View.Combat.GrassOverlayBatcher>(_sceneInstance),
            grid,
            "grass",
            expectedMinAboveTop: texture - 0.1f,
            expectedMaxAboveTop: overlay - 0.2f,
            requireRenderPriority: -1,
            errors);

        int decorationChecked = ValidateDecorationSprites(
            FindFirstDescendant<BladeHex.Combat.SceneDecorationPlacer>(_sceneInstance),
            grid,
            errors);

        int unitsChecked = 0;
        var units = FindDescendants<BladeHex.Combat.Unit>(_sceneInstance);
        foreach (var unit in units)
        {
            if (!GodotObject.IsInstanceValid(unit)) continue;
            var cell = grid.GetCell(unit.GridPos.X, unit.GridPos.Y);
            if (cell == null) continue;

            float aboveTop = unit.GlobalPosition.Y - (cell.GlobalPosition.Y + top);
            if (Mathf.Abs(aboveTop - character) > 0.25f)
                errors.Add($"unit {unit.Name} layer={aboveTop:F2}, expected={character:F2}");
            unitsChecked++;
        }

        bool hasCombatUi = CountDescendants(sceneRoot, node => node.Name == "CombatUI" && node is CanvasLayer) > 0;
        if (!hasCombatUi)
            errors.Add("CombatUI CanvasLayer missing");

        details = "renderLayers="
            + $"static(texture={texture:F2}<overlay={overlay:F2}<uiHint={uiHint:F2}<character={character:F2}), "
            + $"grassChecked={grassChecked}, decorationsChecked={decorationChecked}, unitsChecked={unitsChecked}";

        if (errors.Count > 0)
        {
            details += $", errors={JoinFirstErrors(errors, 4)}";
            return false;
        }

        return true;
    }

    private static int ValidateSurfaceSprites(
        Node3D? root,
        BladeHex.Map.HexGrid grid,
        string label,
        float expectedMinAboveTop,
        float expectedMaxAboveTop,
        int? requireRenderPriority,
        List<string> errors)
    {
        if (root == null || !GodotObject.IsInstanceValid(root))
        {
            errors.Add($"{label} layer missing");
            return 0;
        }

        int checkedCount = 0;
        foreach (var sprite in FindDescendants<Sprite3D>(root))
        {
            if (!GodotObject.IsInstanceValid(sprite)) continue;
            var cell = FindNearestCellByXZ(grid, sprite.GlobalPosition);
            if (cell == null) continue;

            float aboveTop = sprite.GlobalPosition.Y - (cell.GlobalPosition.Y + BladeHex.View.Combat.CombatLayerHeight.HexTopOffset);
            if (aboveTop < expectedMinAboveTop || aboveTop > expectedMaxAboveTop)
                errors.Add($"{label} {sprite.Name} layer={aboveTop:F2}, expected=[{expectedMinAboveTop:F2},{expectedMaxAboveTop:F2}]");

            if (requireRenderPriority.HasValue && sprite.RenderPriority != requireRenderPriority.Value)
                errors.Add($"{label} {sprite.Name} renderPriority={sprite.RenderPriority}, expected={requireRenderPriority.Value}");

            if (sprite.NoDepthTest)
                errors.Add($"{label} {sprite.Name} has NoDepthTest=true");

            checkedCount++;
        }

        if (checkedCount == 0)
            errors.Add($"{label} layer has no Sprite3D children");

        return checkedCount;
    }

    private static int ValidateDecorationSprites(
        Node3D? root,
        BladeHex.Map.HexGrid grid,
        List<string> errors)
    {
        if (root == null || !GodotObject.IsInstanceValid(root))
        {
            errors.Add("decoration layer missing");
            return 0;
        }

        int checkedCount = 0;
        foreach (var sprite in FindDescendants<Sprite3D>(root))
        {
            if (!GodotObject.IsInstanceValid(sprite)) continue;
            var cell = FindNearestCellByXZ(grid, sprite.GlobalPosition);
            if (cell == null) continue;

            float aboveTop = sprite.GlobalPosition.Y - (cell.GlobalPosition.Y + BladeHex.View.Combat.CombatLayerHeight.HexTopOffset);
            if (aboveTop < -0.1f || aboveTop > BladeHex.View.Combat.CombatLayerHeight.CharacterLayer)
                errors.Add($"decoration {sprite.Name} layer={aboveTop:F2}, expected=[0.00,{BladeHex.View.Combat.CombatLayerHeight.CharacterLayer:F2}]");

            if (sprite.Billboard == BaseMaterial3D.BillboardModeEnum.Disabled)
                errors.Add($"decoration {sprite.Name} billboard disabled");

            checkedCount++;
        }

        return checkedCount;
    }

    private static BladeHex.Map.HexCell? FindNearestCellByXZ(BladeHex.Map.HexGrid grid, Vector3 worldPosition)
    {
        BladeHex.Map.HexCell? best = null;
        float bestDistSq = float.MaxValue;

        foreach (var kvp in grid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;
            var delta = cell.GlobalPosition - worldPosition;
            float distSq = (delta.X * delta.X) + (delta.Z * delta.Z);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = cell;
            }
        }

        return best;
    }

    private static string JoinFirstErrors(List<string> errors, int maxCount)
    {
        int count = Math.Min(maxCount, errors.Count);
        var first = new string[count];
        for (int i = 0; i < count; i++)
            first[i] = errors[i];

        string joined = string.Join(" | ", first);
        if (errors.Count > maxCount)
            joined += $" | +{errors.Count - maxCount} more";
        return joined;
    }

    private static (bool Ok, string Details) ValidateScreenshotImage(Image img)
    {
        int width = img.GetWidth();
        int height = img.GetHeight();
        if (width <= 0 || height <= 0)
            return (false, "image has no pixels");

        int stepX = Math.Max(1, width / 96);
        int stepY = Math.Max(1, height / 96);
        int samples = 0;
        double sum = 0.0;
        double sumSq = 0.0;
        var buckets = new HashSet<int>();

        for (int y = 0; y < height; y += stepY)
        {
            for (int x = 0; x < width; x += stepX)
            {
                var c = img.GetPixel(x, y);
                double brightness = (0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B);
                sum += brightness;
                sumSq += brightness * brightness;
                samples++;

                int r = Math.Clamp((int)(c.R * 7.0f), 0, 7);
                int g = Math.Clamp((int)(c.G * 7.0f), 0, 7);
                int b = Math.Clamp((int)(c.B * 7.0f), 0, 7);
                buckets.Add((r << 6) | (g << 3) | b);
            }
        }

        if (samples == 0)
            return (false, "image sampling produced no samples");

        double avg = sum / samples;
        double variance = Math.Max(0.0, (sumSq / samples) - (avg * avg));
        double stdDev = Math.Sqrt(variance);
        string details = $"image={width}x{height}, avgBrightness={avg:F4}, brightnessStdDev={stdDev:F4}, colorBuckets={buckets.Count}, samples={samples}";

        if (avg < 0.02)
            return (false, details + ", rejected=too_dark");

        if (stdDev < 0.01 || buckets.Count < 4)
            return (false, details + ", rejected=flat_color");

        return (true, details);
    }

    private static int CountDescendants(Node root, Func<Node, bool> predicate)
    {
        int count = 0;
        foreach (Node child in root.GetChildren())
        {
            if (predicate(child))
                count++;
            count += CountDescendants(child, predicate);
        }
        return count;
    }

    private static T? FindFirstDescendant<T>(Node? root) where T : Node
    {
        if (root == null)
            return null;

        foreach (Node child in root.GetChildren())
        {
            if (child is T matched)
                return matched;

            var nested = FindFirstDescendant<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static List<T> FindDescendants<T>(Node? root) where T : Node
    {
        var results = new List<T>();
        CollectDescendants(root, results);
        return results;
    }

    private static void CollectDescendants<T>(Node? root, List<T> results) where T : Node
    {
        if (root == null)
            return;

        foreach (Node child in root.GetChildren())
        {
            if (child is T matched)
                results.Add(matched);

            CollectDescendants(child, results);
        }
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
                ["validation"]= _validationDetails,
            };
            File.WriteAllText(_outPath,
                JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }

        GetTree().Quit(_result ? 0 : 1);
    }
}
