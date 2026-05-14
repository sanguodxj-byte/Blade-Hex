// GameSettings.cs
// 游戏设置数据模型 — 持久化到 user://settings.dat
using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 游戏设置 — 视频音频游戏控制
/// </summary>
[GlobalClass]
public partial class GameSettings : Resource
{
    public enum FullscreenModeEnum
    {
        Windowed,
        Borderless,
        ExclusiveFs,
    }

    public enum VSyncModeEnum
    {
        Disabled,
        Enabled,
        Adaptive,
    }

    private const string SettingsPath = "user://settings.dat";
    private const int CurrentVersion = 1;

    // ========================================
    // 视频设置
    // ========================================

    [Export] public FullscreenModeEnum FullscreenMode = FullscreenModeEnum.Windowed;
    [Export] public VSyncModeEnum VSyncMode = VSyncModeEnum.Enabled;
    [Export] public int ResolutionIndex { get; set; } = 0;

    // ========================================
    // 音频设置
    // ========================================

    [Export] public float MasterVolume { get; set; } = 1.0f;
    [Export] public float MusicVolume { get; set; } = 0.7f;
    [Export] public float SfxVolume { get; set; } = 1.0f;
    [Export] public float AmbientVolume { get; set; } = 0.6f;

    // ========================================
    // 游戏设置
    // ========================================

    [Export] public int Difficulty { get; set; } = 1;
    [Export] public float GameSpeed { get; set; } = 0.5f;
    [Export] public bool AutoSave { get; set; } = true;
    [Export] public int AutoSaveInterval { get; set; } = 10;
    [Export] public float CombatAnimSpeed { get; set; } = 1.0f;
    [Export] public int CombatLogDetail { get; set; } = 1;
    [Export] public bool ShowDamageNumbers { get; set; } = true;
    [Export] public bool ShowCombatGrid { get; set; } = true;
    [Export] public bool ConfirmEndTurn { get; set; } = true;
    [Export] public bool ShowMinimap { get; set; } = true;

    // ========================================
    // 控制设置
    // ========================================

    [Export] public float MouseSensitivity { get; set; } = 1.0f;
    [Export] public bool CameraEdgeScroll { get; set; } = true;
    [Export] public float EdgeScrollSpeed { get; set; } = 600.0f;
    [Export] public float CameraZoomSpeed { get; set; } = 1.0f;

    // ========================================
    // 预设
    // ========================================

    public static Godot.Collections.Array<Godot.Collections.Dictionary> GetResolutionPresets()
    {
        var arr = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        arr.Add(new Godot.Collections.Dictionary { { "label", "1280 x 720" }, { "w", 1280 }, { "h", 720 } });
        arr.Add(new Godot.Collections.Dictionary { { "label", "1366 x 768" }, { "w", 1366 }, { "h", 768 } });
        arr.Add(new Godot.Collections.Dictionary { { "label", "1600 x 900" }, { "w", 1600 }, { "h", 900 } });
        arr.Add(new Godot.Collections.Dictionary { { "label", "1920 x 1080" }, { "w", 1920 }, { "h", 1080 } });
        arr.Add(new Godot.Collections.Dictionary { { "label", "2560 x 1440" }, { "w", 2560 }, { "h", 1440 } });
        arr.Add(new Godot.Collections.Dictionary { { "label", "3840 x 2160" }, { "w", 3840 }, { "h", 2160 } });
        return arr;
    }

    public static string[] GetDifficultyNames() => new[] { "简单", "普通", "困难", "传奇" };
    public static string[] GetFullscreenModeNames() => new[] { "窗口化", "无边框窗口", "独占全屏" };
    public static string[] GetVsyncModeNames() => new[] { "关闭", "开启", "自适应" };
    public static string[] GetLogDetailNames() => new[] { "简洁", "标准", "详细" };

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            { "version", CurrentVersion },
            { "video", new Godot.Collections.Dictionary
                {
                    { "fullscreen_mode", (int)FullscreenMode },
                    { "vsync_mode", (int)VSyncMode },
                    { "resolution_index", ResolutionIndex },
                }
            },
            { "audio", new Godot.Collections.Dictionary
                {
                    { "master_volume", MasterVolume },
                    { "music_volume", MusicVolume },
                    { "sfx_volume", SfxVolume },
                    { "ambient_volume", AmbientVolume },
                }
            },
            { "game", new Godot.Collections.Dictionary
                {
                    { "difficulty", Difficulty },
                    { "game_speed", GameSpeed },
                    { "auto_save", AutoSave },
                    { "auto_save_interval", AutoSaveInterval },
                    { "combat_anim_speed", CombatAnimSpeed },
                    { "combat_log_detail", CombatLogDetail },
                    { "show_damage_numbers", ShowDamageNumbers },
                    { "show_combat_grid", ShowCombatGrid },
                    { "confirm_end_turn", ConfirmEndTurn },
                    { "show_minimap", ShowMinimap },
                }
            },
            { "control", new Godot.Collections.Dictionary
                {
                    { "mouse_sensitivity", MouseSensitivity },
                    { "camera_edge_scroll", CameraEdgeScroll },
                    { "edge_scroll_speed", EdgeScrollSpeed },
                    { "camera_zoom_speed", CameraZoomSpeed },
                }
            },
        };
    }

    public void Deserialize(Godot.Collections.Dictionary data)
    {
        var video = data.ContainsKey("video") ? (Godot.Collections.Dictionary)data["video"] : new Godot.Collections.Dictionary();
        FullscreenMode = video.ContainsKey("fullscreen_mode") ? (FullscreenModeEnum)(int)video["fullscreen_mode"] : FullscreenModeEnum.Windowed;
        VSyncMode = video.ContainsKey("vsync_mode") ? (VSyncModeEnum)(int)video["vsync_mode"] : VSyncModeEnum.Enabled;
        ResolutionIndex = video.ContainsKey("resolution_index") ? (int)video["resolution_index"] : 0;

        var audio = data.ContainsKey("audio") ? (Godot.Collections.Dictionary)data["audio"] : new Godot.Collections.Dictionary();
        MasterVolume = audio.ContainsKey("master_volume") ? (float)audio["master_volume"] : 1.0f;
        MusicVolume = audio.ContainsKey("music_volume") ? (float)audio["music_volume"] : 0.7f;
        SfxVolume = audio.ContainsKey("sfx_volume") ? (float)audio["sfx_volume"] : 1.0f;
        AmbientVolume = audio.ContainsKey("ambient_volume") ? (float)audio["ambient_volume"] : 0.6f;

        var game = data.ContainsKey("game") ? (Godot.Collections.Dictionary)data["game"] : new Godot.Collections.Dictionary();
        Difficulty = game.ContainsKey("difficulty") ? (int)game["difficulty"] : 1;
        GameSpeed = game.ContainsKey("game_speed") ? (float)game["game_speed"] : 0.5f;
        AutoSave = game.ContainsKey("auto_save") ? (bool)game["auto_save"] : true;
        AutoSaveInterval = game.ContainsKey("auto_save_interval") ? (int)game["auto_save_interval"] : 10;
        CombatAnimSpeed = game.ContainsKey("combat_anim_speed") ? (float)game["combat_anim_speed"] : 1.0f;
        CombatLogDetail = game.ContainsKey("combat_log_detail") ? (int)game["combat_log_detail"] : 1;
        ShowDamageNumbers = game.ContainsKey("show_damage_numbers") ? (bool)game["show_damage_numbers"] : true;
        ShowCombatGrid = game.ContainsKey("show_combat_grid") ? (bool)game["show_combat_grid"] : true;
        ConfirmEndTurn = game.ContainsKey("confirm_end_turn") ? (bool)game["confirm_end_turn"] : true;
        ShowMinimap = game.ContainsKey("show_minimap") ? (bool)game["show_minimap"] : true;

        var control = data.ContainsKey("control") ? (Godot.Collections.Dictionary)data["control"] : new Godot.Collections.Dictionary();
        MouseSensitivity = control.ContainsKey("mouse_sensitivity") ? (float)control["mouse_sensitivity"] : 1.0f;
        CameraEdgeScroll = control.ContainsKey("camera_edge_scroll") ? (bool)control["camera_edge_scroll"] : true;
        EdgeScrollSpeed = control.ContainsKey("edge_scroll_speed") ? (float)control["edge_scroll_speed"] : 600.0f;
        CameraZoomSpeed = control.ContainsKey("camera_zoom_speed") ? (float)control["camera_zoom_speed"] : 1.0f;
    }

    // ========================================
    // 保存/加载
    // ========================================

    public bool SaveToFile()
    {
        var data = Serialize();
        var json = Godot.Json.Stringify(data, "\t");
        var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
        if (file != null)
        {
            file.StoreString(json);
            file.Close();
            return true;
        }
        GD.PushWarning("[GameSettings] 保存设置失败");
        return false;
    }

    public bool LoadFromFile()
    {
        if (!FileAccess.FileExists(SettingsPath))
            return false;
        var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return false;

        var text = file.GetAsText();
        file.Close();

        if (string.IsNullOrEmpty(text))
            return false;

        var json = new Json();
        var err = json.Parse(text);
        if (err == Error.Ok && json.Data.VariantType == Variant.Type.Dictionary)
        {
            Deserialize((Godot.Collections.Dictionary)json.Data);
            return true;
        }

        // 兼容旧版二进制格式（迁移期）
        file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
        if (file == null) return false;
        var varData = file.GetVar();
        file.Close();
        if (varData.VariantType == Variant.Type.Dictionary)
        {
            Deserialize((Godot.Collections.Dictionary)varData);
            return true;
        }

        return false;
    }

    // ========================================
    // 应用到引擎
    // ========================================

    public void ApplyToEngine()
    {
        ApplyVideoSettings();
        ApplyAudioSettings();
    }

    private void ApplyVideoSettings()
    {
        switch (FullscreenMode)
        {
            case FullscreenModeEnum.Windowed:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                break;
            case FullscreenModeEnum.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                break;
            case FullscreenModeEnum.ExclusiveFs:
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
        }

        switch (VSyncMode)
        {
            case VSyncModeEnum.Disabled:
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                break;
            case VSyncModeEnum.Enabled:
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Enabled);
                break;
            case VSyncModeEnum.Adaptive:
                DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Adaptive);
                break;
        }

        var presets = GetResolutionPresets();
        if (ResolutionIndex >= 0 && ResolutionIndex < presets.Count)
        {
            var res = presets[ResolutionIndex];
            if (FullscreenMode == FullscreenModeEnum.Windowed)
            {
                DisplayServer.WindowSetSize(new Vector2I((int)res["w"], (int)res["h"]));
            }
        }

        GD.Print($"[GameSettings] 视频设置已应用: 全屏={FullscreenMode}, VSync={VSyncMode}, 分辨率索引={ResolutionIndex}");
    }

    private void ApplyAudioSettings()
    {
        const float minVolume = 0.001f;

        int masterIdx = AudioServer.GetBusIndex("Master");
        if (masterIdx >= 0)
        {
            float vol = Mathf.Clamp(MasterVolume, minVolume, 1.0f);
            AudioServer.SetBusVolumeDb(masterIdx, Mathf.LinearToDb(vol));
            AudioServer.SetBusMute(masterIdx, MasterVolume <= 0.001f);
        }

        int musicIdx = AudioServer.GetBusIndex("Music");
        if (musicIdx >= 0)
            AudioServer.SetBusVolumeDb(musicIdx, Mathf.LinearToDb(Mathf.Clamp(MusicVolume, minVolume, 1.0f)));

        int sfxIdx = AudioServer.GetBusIndex("SFX");
        if (sfxIdx >= 0)
            AudioServer.SetBusVolumeDb(sfxIdx, Mathf.LinearToDb(Mathf.Clamp(SfxVolume, minVolume, 1.0f)));

        int ambientIdx = AudioServer.GetBusIndex("Ambient");
        if (ambientIdx >= 0)
            AudioServer.SetBusVolumeDb(ambientIdx, Mathf.LinearToDb(Mathf.Clamp(AmbientVolume, minVolume, 1.0f)));
    }
}
