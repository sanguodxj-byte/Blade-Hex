// AnimEditorData.cs
// 运行时骨骼动画编辑器 — 数据模型
// 纯数据类，不依赖 Godot 节点。包含动画片段、关键帧、骨骼姿态、插值、序列化。
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>单骨骼姿态</summary>
public struct BonePose
{
    /// <summary>Z 轴旋转角度（度）</summary>
    public float RotationZ { get; set; }

    /// <summary>Y 轴位移（Torso 用于呼吸浮动，Weapon 用于握柄偏移）</summary>
    public float PositionY { get; set; }

    /// <summary>X 轴位移（Weapon 用于水平偏移）</summary>
    public float PositionX { get; set; }

    /// <summary>Sprite 自身旋转（度，仅 Weapon 使用，控制武器图片角度）</summary>
    public float SpriteRotation { get; set; }

    public static BonePose Zero => new() { RotationZ = 0, PositionY = 0, PositionX = 0, SpriteRotation = 0 };

    /// <summary>线性插值</summary>
    public static BonePose Lerp(BonePose a, BonePose b, float t)
    {
        return new BonePose
        {
            RotationZ = a.RotationZ + (b.RotationZ - a.RotationZ) * t,
            PositionY = a.PositionY + (b.PositionY - a.PositionY) * t,
            PositionX = a.PositionX + (b.PositionX - a.PositionX) * t,
            SpriteRotation = a.SpriteRotation + (b.SpriteRotation - b.SpriteRotation) * t,
        };
    }
}

/// <summary>
/// 武器动画类别 — 决定使用哪套攻击/待机动画。
/// 同一类别的武器共享动画（如所有砍伤武器用同一套挥砍动画）。
/// </summary>
public enum WeaponAnimCategory
{
    /// <summary>砍伤近战（剑/斧/刀）— 横挥</summary>
    Slash,
    /// <summary>刺伤近战（矛/刺剑/匕首）— 前刺</summary>
    Thrust,
    /// <summary>钝伤近战（锤/棍/连枷）— 砸击</summary>
    Crush,
    /// <summary>弓类（短弓/长弓/复合弓）— 拉弦释放</summary>
    Bow,
    /// <summary>弩类 — 上弦射击</summary>
    Crossbow,
    /// <summary>投掷（飞刀/标枪/飞斧）— 投掷动作</summary>
    Throw,
    /// <summary>法术媒介（法杖/宝珠/魔杖）— 施法姿态</summary>
    Catalyst,
    /// <summary>徒手</summary>
    Unarmed,
}

/// <summary>武器动画类别工具</summary>
public static class WeaponAnimCategoryUtil
{
    /// <summary>从 WeaponSubtype 推导动画类别</summary>
    public static WeaponAnimCategory FromSubtype(BladeHex.Data.WeaponData.WeaponSubtype subtype)
    {
        return subtype switch
        {
            // 砍伤
            BladeHex.Data.WeaponData.WeaponSubtype.Dagger or
            BladeHex.Data.WeaponData.WeaponSubtype.Seax or
            BladeHex.Data.WeaponData.WeaponSubtype.Kukri or
            BladeHex.Data.WeaponData.WeaponSubtype.ArmingSword or
            BladeHex.Data.WeaponData.WeaponSubtype.BattleAxe or
            BladeHex.Data.WeaponData.WeaponSubtype.NomadSaber or
            BladeHex.Data.WeaponData.WeaponSubtype.Greatsword or
            BladeHex.Data.WeaponData.WeaponSubtype.GreatAxe or
            BladeHex.Data.WeaponData.WeaponSubtype.Glaive => WeaponAnimCategory.Slash,

            // 刺伤
            BladeHex.Data.WeaponData.WeaponSubtype.Stiletto or
            BladeHex.Data.WeaponData.WeaponSubtype.SpikedDagger or
            BladeHex.Data.WeaponData.WeaponSubtype.Rapier or
            BladeHex.Data.WeaponData.WeaponSubtype.InfantrySpear or
            BladeHex.Data.WeaponData.WeaponSubtype.BroadSpear or
            BladeHex.Data.WeaponData.WeaponSubtype.Awlpike or
            BladeHex.Data.WeaponData.WeaponSubtype.Lance or
            BladeHex.Data.WeaponData.WeaponSubtype.Voulge or
            BladeHex.Data.WeaponData.WeaponSubtype.Trident => WeaponAnimCategory.Thrust,

            // 钝伤
            BladeHex.Data.WeaponData.WeaponSubtype.Club or
            BladeHex.Data.WeaponData.WeaponSubtype.LightHammer or
            BladeHex.Data.WeaponData.WeaponSubtype.Cestus or
            BladeHex.Data.WeaponData.WeaponSubtype.WingedMace or
            BladeHex.Data.WeaponData.WeaponSubtype.MilitaryHammer or
            BladeHex.Data.WeaponData.WeaponSubtype.Flail or
            BladeHex.Data.WeaponData.WeaponSubtype.Maul or
            BladeHex.Data.WeaponData.WeaponSubtype.Greatclub or
            BladeHex.Data.WeaponData.WeaponSubtype.Polehammer => WeaponAnimCategory.Crush,

            // 投掷
            BladeHex.Data.WeaponData.WeaponSubtype.ThrowingKnife or
            BladeHex.Data.WeaponData.WeaponSubtype.Dart or
            BladeHex.Data.WeaponData.WeaponSubtype.Francisca or
            BladeHex.Data.WeaponData.WeaponSubtype.Javelin or
            BladeHex.Data.WeaponData.WeaponSubtype.Pilum or
            BladeHex.Data.WeaponData.WeaponSubtype.Harpoon or
            BladeHex.Data.WeaponData.WeaponSubtype.StoneThrow or
            BladeHex.Data.WeaponData.WeaponSubtype.HeavyJavelin or
            BladeHex.Data.WeaponData.WeaponSubtype.ThrowingHammer => WeaponAnimCategory.Throw,

            // 弓
            BladeHex.Data.WeaponData.WeaponSubtype.Shortbow or
            BladeHex.Data.WeaponData.WeaponSubtype.HuntingBow or
            BladeHex.Data.WeaponData.WeaponSubtype.NomadBow or
            BladeHex.Data.WeaponData.WeaponSubtype.Strongbow or
            BladeHex.Data.WeaponData.WeaponSubtype.RecurveBow or
            BladeHex.Data.WeaponData.WeaponSubtype.WarBow or
            BladeHex.Data.WeaponData.WeaponSubtype.Longbow or
            BladeHex.Data.WeaponData.WeaponSubtype.CompositeLongbow or
            BladeHex.Data.WeaponData.WeaponSubtype.Greatbow => WeaponAnimCategory.Bow,

            // 弩
            BladeHex.Data.WeaponData.WeaponSubtype.LightCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.HuntingCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.PistolCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.StandardCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.StrongCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.SniperCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.HeavyCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.SiegeCrossbow or
            BladeHex.Data.WeaponData.WeaponSubtype.Ballista => WeaponAnimCategory.Crossbow,

            // 法术媒介
            BladeHex.Data.WeaponData.WeaponSubtype.Wand or
            BladeHex.Data.WeaponData.WeaponSubtype.Orb or
            BladeHex.Data.WeaponData.WeaponSubtype.Staff => WeaponAnimCategory.Catalyst,

            // 徒手
            BladeHex.Data.WeaponData.WeaponSubtype.Unarmed => WeaponAnimCategory.Unarmed,

            _ => WeaponAnimCategory.Slash,
        };
    }

    /// <summary>动画类别的显示名</summary>
    public static string GetDisplayName(WeaponAnimCategory cat) => cat switch
    {
        WeaponAnimCategory.Slash => "砍伤(剑/斧)",
        WeaponAnimCategory.Thrust => "刺伤(矛/剑)",
        WeaponAnimCategory.Crush => "钝伤(锤/棍)",
        WeaponAnimCategory.Bow => "弓",
        WeaponAnimCategory.Crossbow => "弩",
        WeaponAnimCategory.Throw => "投掷",
        WeaponAnimCategory.Catalyst => "法杖",
        WeaponAnimCategory.Unarmed => "徒手",
        _ => "未知",
    };

    /// <summary>所有动画类别</summary>
    public static readonly WeaponAnimCategory[] All =
    [
        WeaponAnimCategory.Slash,
        WeaponAnimCategory.Thrust,
        WeaponAnimCategory.Crush,
        WeaponAnimCategory.Bow,
        WeaponAnimCategory.Crossbow,
        WeaponAnimCategory.Throw,
        WeaponAnimCategory.Catalyst,
        WeaponAnimCategory.Unarmed,
    ];
}

/// <summary>一个关键帧</summary>
public sealed class AnimKeyframe
{
    /// <summary>时间点（秒）</summary>
    public float Time { get; set; }

    /// <summary>各骨骼姿态（key = 骨骼名）</summary>
    public Dictionary<string, BonePose> Bones { get; set; } = new();

    /// <summary>获取指定骨骼的姿态，不存在则返回 Zero</summary>
    public BonePose GetPose(string boneName)
        => Bones.TryGetValue(boneName, out var pose) ? pose : BonePose.Zero;

    /// <summary>设置指定骨骼的姿态</summary>
    public void SetPose(string boneName, BonePose pose)
        => Bones[boneName] = pose;

    /// <summary>深拷贝</summary>
    public AnimKeyframe Clone()
    {
        var clone = new AnimKeyframe { Time = Time };
        foreach (var (k, v) in Bones)
            clone.Bones[k] = v;
        return clone;
    }
}

/// <summary>一个完整动画片段</summary>
public sealed class AnimClip
{
    /// <summary>动画名称（如 attack_melee, idle）</summary>
    public string Name { get; set; } = "untitled";

    /// <summary>武器动画类别（决定存储子目录）</summary>
    public WeaponAnimCategory WeaponCategory { get; set; } = WeaponAnimCategory.Slash;

    /// <summary>总时长（秒）</summary>
    public float Duration { get; set; } = 1.0f;

    /// <summary>是否循环</summary>
    public bool Loop { get; set; } = false;

    /// <summary>关键帧列表（按时间排序）</summary>
    public List<AnimKeyframe> Keyframes { get; set; } = new();

    /// <summary>所有可编辑的骨骼名</summary>
    public static readonly string[] BoneNames =
    [
        "Torso", "Head", "ArmL", "ArmR", "ForearmL", "ForearmR", "Weapon"
    ];

    /// <summary>完整标识（武器类别 + 动画名，用于文件名）</summary>
    public string FullId => $"{WeaponCategory.ToString().ToLower()}_{Name}";


    /// <summary>添加关键帧并保持时间排序</summary>
    public int AddKeyframe(AnimKeyframe kf)
    {
        Keyframes.Add(kf);
        Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
        return Keyframes.IndexOf(kf);
    }

    /// <summary>在指定时间插入关键帧（姿态为当前插值结果）</summary>
    public int InsertKeyframeAt(float time)
    {
        var pose = AnimClipInterpolator.Sample(this, time);
        var kf = new AnimKeyframe { Time = time };
        foreach (var (bone, p) in pose)
            kf.Bones[bone] = p;
        return AddKeyframe(kf);
    }

    /// <summary>删除指定索引的关键帧</summary>
    public void RemoveKeyframe(int index)
    {
        if (index >= 0 && index < Keyframes.Count)
            Keyframes.RemoveAt(index);
    }

    /// <summary>修改时长，等比缩放所有关键帧时间</summary>
    public void SetDuration(float newDuration)
    {
        if (Duration <= 0 || newDuration <= 0) return;
        float scale = newDuration / Duration;
        foreach (var kf in Keyframes)
            kf.Time *= scale;
        Duration = newDuration;
    }

    /// <summary>创建默认 idle 动画</summary>
    public static AnimClip CreateDefaultIdle()
    {
        var clip = new AnimClip { Name = "idle", Duration = 2.0f, Loop = true };
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 0.0f,
            Bones = new() { ["Torso"] = new BonePose { PositionY = 0 } }
        });
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 1.0f,
            Bones = new() { ["Torso"] = new BonePose { PositionY = 3 } }
        });
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 2.0f,
            Bones = new() { ["Torso"] = new BonePose { PositionY = 0 } }
        });
        return clip;
    }

    /// <summary>创建默认近战攻击动画</summary>
    public static AnimClip CreateDefaultAttackMelee()
    {
        var clip = new AnimClip { Name = "attack_melee", Duration = 0.5f, Loop = false };
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 0.0f,
            Bones = new()
            {
                ["Torso"] = BonePose.Zero,
                ["ArmR"] = BonePose.Zero,
                ["Weapon"] = BonePose.Zero,
            }
        });
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 0.15f,
            Bones = new()
            {
                ["Torso"] = new BonePose { RotationZ = -10 },
                ["ArmR"] = new BonePose { RotationZ = 45 },
                ["Weapon"] = new BonePose { RotationZ = 30 },
            }
        });
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 0.3f,
            Bones = new()
            {
                ["Torso"] = new BonePose { RotationZ = 15 },
                ["ArmR"] = new BonePose { RotationZ = -60 },
                ["Weapon"] = new BonePose { RotationZ = -45 },
            }
        });
        clip.Keyframes.Add(new AnimKeyframe
        {
            Time = 0.5f,
            Bones = new()
            {
                ["Torso"] = BonePose.Zero,
                ["ArmR"] = BonePose.Zero,
                ["Weapon"] = BonePose.Zero,
            }
        });
        return clip;
    }
}

/// <summary>动画插值器 — 给定时间采样所有骨骼姿态</summary>
public static class AnimClipInterpolator
{
    /// <summary>
    /// 在指定时间采样动画，返回所有骨骼的插值姿态。
    /// </summary>
    public static Dictionary<string, BonePose> Sample(AnimClip clip, float time)
    {
        var result = new Dictionary<string, BonePose>();
        if (clip.Keyframes.Count == 0)
        {
            foreach (var bone in AnimClip.BoneNames)
                result[bone] = BonePose.Zero;
            return result;
        }

        // 处理循环
        if (clip.Loop && clip.Duration > 0)
            time = time % clip.Duration;
        else
            time = Math.Clamp(time, 0, clip.Duration);

        // 找到前后两个关键帧
        AnimKeyframe? prev = null;
        AnimKeyframe? next = null;

        for (int i = 0; i < clip.Keyframes.Count; i++)
        {
            if (clip.Keyframes[i].Time <= time)
                prev = clip.Keyframes[i];
            if (clip.Keyframes[i].Time >= time && next == null)
                next = clip.Keyframes[i];
        }

        // 边界情况
        prev ??= clip.Keyframes[0];
        next ??= clip.Keyframes[^1];

        // 计算插值因子
        float t = 0;
        float gap = next.Time - prev.Time;
        if (gap > 0.0001f)
            t = (time - prev.Time) / gap;

        // 对每个骨骼插值
        foreach (var bone in AnimClip.BoneNames)
        {
            var poseA = prev.GetPose(bone);
            var poseB = next.GetPose(bone);
            result[bone] = BonePose.Lerp(poseA, poseB, t);
        }

        return result;
    }
}

/// <summary>动画片段 JSON 序列化/反序列化</summary>
public static class AnimClipSerializer
{
    private const string SaveDir = "user://custom_animations";

    /// <summary>获取指定武器类别的存储目录</summary>
    private static string GetCategoryDir(WeaponAnimCategory cat)
        => $"{SaveDir}/{cat.ToString().ToLower()}";

    /// <summary>保存动画到 user:// 目录（按武器类别分子目录）</summary>
    public static void Save(AnimClip clip)
    {
        string dir = GetCategoryDir(clip.WeaponCategory);
        DirAccess.MakeDirRecursiveAbsolute(dir);
        string path = $"{dir}/{clip.Name}.json";
        string json = ToJson(clip);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"[AnimClipSerializer] 无法写入: {path}");
            return;
        }
        file.StoreString(json);
        GD.Print($"[AnimClipSerializer] 已保存: {path}");
    }

    /// <summary>从 user:// 目录加载动画（按武器类别查找）</summary>
    public static AnimClip? Load(string animName, WeaponAnimCategory category)
    {
        // 优先 user:// 自定义
        string userPath = $"{GetCategoryDir(category)}/{animName}.json";
        if (FileAccess.FileExists(userPath))
        {
            using var file = FileAccess.Open(userPath, FileAccess.ModeFlags.Read);
            if (file != null) return FromJson(file.GetAsText());
        }

        // 回退 res://assets/animations/ 内置
        string resPath = $"res://assets/animations/{category.ToString().ToLower()}/{animName}.json";
        if (FileAccess.FileExists(resPath))
        {
            using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
            if (file != null)
            {
                GD.Print($"[AnimClipSerializer] 从内置加载: {resPath}");
                return FromJson(file.GetAsText());
            }
        }

        return null;
    }

    /// <summary>向后兼容：不指定类别时从根目录加载</summary>
    public static AnimClip? Load(string animName)
    {
        // 先尝试所有类别子目录
        foreach (var cat in WeaponAnimCategoryUtil.All)
        {
            var clip = Load(animName, cat);
            if (clip != null) return clip;
        }
        // 回退：旧格式根目录
        string path = $"{SaveDir}/{animName}.json";
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;
        return FromJson(file.GetAsText());
    }

    /// <summary>列出指定武器类别下所有已保存的动画名</summary>
    public static List<string> ListSaved(WeaponAnimCategory category)
    {
        var names = new List<string>();

        // user:// 自定义
        string dirPath = GetCategoryDir(category);
        ScanJsonDir(dirPath, names);

        // res:// 内置
        string resDir = $"res://assets/animations/{category.ToString().ToLower()}";
        ScanJsonDir(resDir, names);

        return names;
    }

    private static void ScanJsonDir(string dirPath, List<string> names)
    {
        var dir = DirAccess.Open(dirPath);
        if (dir == null) return;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
            {
                string name = fileName.Replace(".json", "");
                if (!names.Contains(name))
                    names.Add(name);
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
    }

    /// <summary>列出所有类别下所有已保存的动画名（向后兼容）</summary>
    public static List<string> ListSaved()
    {
        var names = new List<string>();
        foreach (var cat in WeaponAnimCategoryUtil.All)
            names.AddRange(ListSaved(cat));

        // 也扫描根目录（旧格式兼容）
        var dir = DirAccess.Open(SaveDir);
        if (dir != null)
        {
            dir.ListDirBegin();
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
                {
                    string name = fileName.Replace(".json", "");
                    if (!names.Contains(name))
                        names.Add(name);
                }
                fileName = dir.GetNext();
            }
            dir.ListDirEnd();
        }
        return names;
    }

    /// <summary>序列化为 JSON 字符串</summary>
    public static string ToJson(AnimClip clip)
    {
        var dto = new AnimClipDto
        {
            name = clip.Name,
            weapon_category = clip.WeaponCategory.ToString().ToLower(),
            duration = clip.Duration,
            loop = clip.Loop,
            keyframes = clip.Keyframes.Select(kf => new KeyframeDto
            {
                time = kf.Time,
                bones = kf.Bones.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new BonePoseDto { rotation_z = kvp.Value.RotationZ, position_y = kvp.Value.PositionY, position_x = kvp.Value.PositionX, sprite_rotation = kvp.Value.SpriteRotation }
                )
            }).ToList()
        };
        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>从 JSON 字符串反序列化</summary>
    public static AnimClip? FromJson(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<AnimClipDto>(json);
            if (dto == null) return null;

            var clip = new AnimClip
            {
                Name = dto.name ?? "untitled",
                Duration = dto.duration,
                Loop = dto.loop,
                WeaponCategory = Enum.TryParse<WeaponAnimCategory>(dto.weapon_category ?? "slash", true, out var cat)
                    ? cat : WeaponAnimCategory.Slash,
            };

            foreach (var kfDto in dto.keyframes ?? new())
            {
                var kf = new AnimKeyframe { Time = kfDto.time };
                foreach (var (boneName, poseDto) in kfDto.bones ?? new())
                {
                    kf.Bones[boneName] = new BonePose
                    {
                        RotationZ = poseDto.rotation_z,
                        PositionY = poseDto.position_y,
                        PositionX = poseDto.position_x,
                        SpriteRotation = poseDto.sprite_rotation,
                    };
                }
                clip.Keyframes.Add(kf);
            }
            return clip;
        }
        catch (Exception ex)
        {
            GD.PushError($"[AnimClipSerializer] JSON 解析失败: {ex.Message}");
            return null;
        }
    }

    // ─── JSON DTO ───

    private class AnimClipDto
    {
        public string? name { get; set; }
        public string? weapon_category { get; set; }
        public float duration { get; set; }
        public bool loop { get; set; }
        public List<KeyframeDto>? keyframes { get; set; }
    }

    private class KeyframeDto
    {
        public float time { get; set; }
        public Dictionary<string, BonePoseDto>? bones { get; set; }
    }

    private class BonePoseDto
    {
        public float rotation_z { get; set; }
        public float position_y { get; set; }
        public float position_x { get; set; }
        public float sprite_rotation { get; set; }
    }
}
