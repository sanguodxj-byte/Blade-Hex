// SkeletonAnimationLibrary.cs
// 骨骼动画资源加载与缓存管理
// 优先级: user://custom_animations/ (玩家自定义 JSON) > res://animations/ (.tres) > 程序化默认
using Godot;
using System.Collections.Generic;
using BladeHex.View.Unit.Skeleton.Editor;

namespace BladeHex.View.Unit.Skeleton;

/// <summary>
/// 骨骼动画资源管理器。
/// 加载优先级：
/// 1. user://custom_animations/{name}.json（玩家自定义，运行时编辑器产出）
/// 2. res://animations/upper_body/{name}.tres（开发者 .tres 资源）
/// 3. 程序化默认动画（UpperBodySkeleton 内置）
/// </summary>
public static class SkeletonAnimationLibrary
{
    private const string AnimationBasePath = "res://animations/upper_body/";

    /// <summary>已知动画名列表</summary>
    public static readonly string[] KnownAnimations =
    [
        "idle",
        "attack_melee",
        "attack_ranged",
        "cast",
        "hit",
        "die",
        "block",
        "move",
    ];

    // 缓存: animName → Animation resource (null = 文件不存在，已检查过)
    private static readonly Dictionary<string, Animation?> _cache = new();

    // 缓存: animName → AnimClip (玩家自定义 JSON)
    private static readonly Dictionary<string, AnimClip?> _customCache = new();

    /// <summary>
    /// 尝试从文件系统加载指定动画（.tres 资源）。
    /// 返回 null 表示文件不存在（使用程序化默认动画）。
    /// </summary>
    public static Animation? LoadAnimation(string animName)
    {
        if (_cache.TryGetValue(animName, out var cached))
            return cached;

        string path = $"{AnimationBasePath}{animName}.tres";
        Animation? anim = null;

        if (ResourceLoader.Exists(path))
        {
            var res = GD.Load<Animation>(path);
            if (res != null)
            {
                anim = res;
                GD.Print($"[SkeletonAnimLib] Loaded external animation: {path}");
            }
        }

        _cache[animName] = anim;
        return anim;
    }

    /// <summary>
    /// 尝试加载玩家自定义动画（JSON 格式）。
    /// 按武器类别查找，返回 null 表示无自定义动画。
    /// </summary>
    public static AnimClip? LoadCustomAnimation(string animName, WeaponAnimCategory category = WeaponAnimCategory.Slash)
    {
        string key = $"{category}/{animName}";
        if (_customCache.TryGetValue(key, out var cached))
            return cached;

        // 优先按类别子目录查找
        var clip = AnimClipSerializer.Load(animName, category);
        // 回退：不分类别的旧格式
        clip ??= AnimClipSerializer.Load(animName);

        _customCache[key] = clip;

        if (clip != null)
            GD.Print($"[SkeletonAnimLib] Loaded custom animation: {category}/{animName}.json");

        return clip;
    }

    /// <summary>
    /// 将 AnimClip（JSON 数据模型）转换为 Godot Animation 资源。
    /// 用于注入到 AnimationPlayer 中替代程序化动画。
    /// </summary>
    public static Animation? ConvertClipToAnimation(AnimClip clip, float pixelSize = 1.5f)
    {
        if (clip.Keyframes.Count == 0) return null;

        var anim = new Animation();
        anim.Length = clip.Duration;
        anim.LoopMode = clip.Loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;

        // 骨骼名 → AnimationPlayer 中的节点路径
        var bonePathMap = new Dictionary<string, string>
        {
            ["Torso"] = "Bone_Torso",
            ["Head"] = "Bone_Torso/Bone_Head",
            ["ArmL"] = "Bone_Torso/Bone_ArmL",
            ["ArmR"] = "Bone_Torso/Bone_ArmR",
            ["ForearmL"] = "Bone_Torso/Bone_ArmL/Bone_ForearmL",
            ["ForearmR"] = "Bone_Torso/Bone_ArmR/Bone_ForearmR",
            ["Weapon"] = "Bone_Torso/Bone_ArmR/Bone_ForearmR/Bone_Weapon",
        };

        // 为每个骨骼创建 rotation_degrees:z 轨道
        foreach (var (boneName, nodePath) in bonePathMap)
        {
            int trackIdx = anim.AddTrack(Animation.TrackType.Value);
            anim.TrackSetPath(trackIdx, $"{nodePath}:rotation_degrees:z");

            foreach (var kf in clip.Keyframes)
            {
                var pose = kf.GetPose(boneName);
                anim.TrackInsertKey(trackIdx, kf.Time, pose.RotationZ);
            }
        }

        // Torso 额外的 position:y 轨道
        int posTrack = anim.AddTrack(Animation.TrackType.Value);
        anim.TrackSetPath(posTrack, "Bone_Torso:position:y");
        foreach (var kf in clip.Keyframes)
        {
            var pose = kf.GetPose("Torso");
            anim.TrackInsertKey(posTrack, kf.Time, pose.PositionY * pixelSize);
        }

        return anim;
    }

    /// <summary>
    /// 尝试用外部动画覆盖 AnimationPlayer 中的程序化动画。
    /// 优先级: 玩家自定义 JSON (按武器类别) > .tres 资源文件
    /// </summary>
    public static void TryOverrideFromFiles(AnimationPlayer player, float pixelSize = 1.5f,
        WeaponAnimCategory weaponCategory = WeaponAnimCategory.Slash)
    {
        if (player == null) return;

        foreach (var animName in KnownAnimations)
        {
            Animation? overrideAnim = null;

            // 优先级 1: 玩家自定义 JSON（按武器类别）
            var customClip = LoadCustomAnimation(animName, weaponCategory);
            if (customClip != null)
            {
                overrideAnim = ConvertClipToAnimation(customClip, pixelSize);
            }

            // 优先级 2: .tres 资源
            if (overrideAnim == null)
            {
                overrideAnim = LoadAnimation(animName);
            }

            if (overrideAnim == null) continue;

            // 注入到 AnimationPlayer
            var lib = player.GetAnimationLibrary("");
            if (lib == null) continue;

            if (lib.HasAnimation(animName))
                lib.RemoveAnimation(animName);
            lib.AddAnimation(animName, overrideAnim);
        }
    }

    /// <summary>清除所有缓存（热重载时调用）</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _customCache.Clear();
    }
}
