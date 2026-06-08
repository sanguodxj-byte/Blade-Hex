// SkeletonAnimationLibrary.cs
// 骨骼动画资源加载与缓存管理
// 优先级: user://custom_animations/ (玩家自定义 JSON) > res://animations/ (.tres) > 程序化默认
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.View.AssetSystem;
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

        var res = AnimationAssetResolver.Load(animName, path);
        if (res != null)
        {
            anim = res;
            GD.Print($"[SkeletonAnimLib] Loaded external animation: {path}");
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
    /// 原参数签名向下兼容性重载。
    /// </summary>
    public static Animation? ConvertClipToAnimation(AnimClip clip, float pixelSize)
    {
        return ConvertClipToAnimation(clip, null, pixelSize);
    }

    /// <summary>
    /// 将 AnimClip（JSON 数据模型）转换为 Godot Animation 资源。
    /// 用于注入到 AnimationPlayer 中替代程序化动画。
    /// 包含高精度的 TRS 三轨动态内存烘焙和物理缓动重采样。
    /// </summary>
    public static Animation? ConvertClipToAnimation(AnimClip clip, BoneConfig? config = null, float pixelSize = 1.5f)
    {
        if (clip.Keyframes.Count == 0) return null;
        config ??= BoneConfig.Standard;

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
            ["Shield"] = "Bone_Torso/Bone_ArmL/Bone_ForearmL/Bone_Shield",
        };

        var boneDefaultOffsets = new Dictionary<string, Vector2>
        {
            ["Torso"] = Vector2.Zero,
            ["Head"] = new Vector2(0, -config.HeadOffsetY),
            ["ArmL"] = new Vector2(-config.ShoulderWidth, -config.ShoulderY),
            ["ForearmL"] = new Vector2(0, config.UpperArmLength),
            ["Shield"] = new Vector2(config.ShieldMountOffset.X, -config.ShieldMountOffset.Y),
            ["ArmR"] = new Vector2(config.ShoulderWidth, -config.ShoulderY),
            ["ForearmR"] = new Vector2(0, config.UpperArmLength),
            ["Weapon"] = new Vector2(config.WeaponMountOffset.X, -config.WeaponMountOffset.Y),
        };

        // 为了平滑渲染 BackOut, Bounce 等物理曲线，我们在内存烘焙时对相邻帧进行微采样
        var sampleTimes = new List<float>();
        
        if (clip.Keyframes.Count > 0)
        {
            for (int i = 0; i < clip.Keyframes.Count; i++)
            {
                var curr = clip.Keyframes[i];
                sampleTimes.Add(curr.Time);

                if (i < clip.Keyframes.Count - 1)
                {
                    var next = clip.Keyframes[i + 1];
                    float gap = next.Time - curr.Time;
                    
                    bool hasComplexEasing = false;
                    foreach (var bone in AnimClip.BoneNames)
                    {
                        var pose = curr.GetPose(bone);
                        if (pose.Easing != EasingType.Linear)
                        {
                            hasComplexEasing = true;
                            break;
                        }
                    }

                    // 若使用非 Linear 物理缓动，进行 30FPS 密集微帧插值重采样以实现亚像素平滑物理动画
                    if (hasComplexEasing && gap > 0.033f)
                    {
                        int steps = Mathf.Max(1, (int)(gap / 0.033f));
                        for (int s = 1; s < steps; s++)
                        {
                            float t = curr.Time + gap * ((float)s / steps);
                            sampleTimes.Add(t);
                        }
                    }
                }
            }
        }

        // 排序并去重
        sampleTimes = sampleTimes.Distinct().OrderBy(t => t).ToList();

        // 针对每一个骨骼，烘焙 TRS 三轨
        foreach (var (boneName, nodePath) in bonePathMap)
        {
            var defaultPos = boneDefaultOffsets.GetValueOrDefault(boneName, Vector2.Zero);

            // 1. Rotation 轨（Node2D.rotation_degrees 是 float 标量，不能用 :z 子属性。
            //    历史代码写成 ":rotation_degrees:z" → Godot 静默忽略整条轨道，
            //    导致攻击/idle 等所有非"位移"动画看起来完全没动。）
            int rotTrack = anim.AddTrack(Animation.TrackType.Value);
            anim.TrackSetPath(rotTrack, $"{nodePath}:rotation_degrees");

            // 2. Position 轨
            int posTrack = anim.AddTrack(Animation.TrackType.Value);
            anim.TrackSetPath(posTrack, $"{nodePath}:position");

            // 3. Scale 轨
            int scaleTrack = anim.AddTrack(Animation.TrackType.Value);
            anim.TrackSetPath(scaleTrack, $"{nodePath}:scale");

            // 在采样的每一个时间点计算并插入 TRS 关键帧
            foreach (var time in sampleTimes)
            {
                var poses = AnimClipInterpolator.Sample(clip, time);
                var pose = poses.GetValueOrDefault(boneName, BonePose.Zero);

                // 写入旋转
                anim.TrackInsertKey(rotTrack, time, pose.RotationZ);

                // 写入位置（在默认位置基础上叠加偏移，2D 画布中负 Y 向上）
                var finalPos = defaultPos + new Vector2(pose.PositionX, -pose.PositionY);
                anim.TrackInsertKey(posTrack, time, finalPos);

                // 写入缩放
                var finalScale = new Vector2(pose.ScaleX, pose.ScaleY);
                anim.TrackInsertKey(scaleTrack, time, finalScale);
            }
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
