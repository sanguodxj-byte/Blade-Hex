// CombatTextureLoader.cs
// 战斗场景纹理统一加载组件
// 职责：在战斗场景初始化时，一次性预加载所有视觉资源并缓存
// 覆盖：地图格顶面/侧面、角色渲染、武器运动动画、抛射物动画、场景2D精灵
//
// 设计原则：
//   - 单一入口：CombatSceneBase 在 InitSystems 后调用 PreloadAll()
//   - 懒加载 + 预热：首次战斗加载后缓存，后续战斗复用
//   - 池化友好：与 NodePool / ProjectilePool / VFXManager 协同
//   - 异步可选：提供 PreloadAllAsync() 用于加载屏显示进度
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.Data;
using BladeHex.View.Map;
using BladeHex.View.Unit;
using BladeHex.View.Unit.Slots;

namespace BladeHex.Combat;

/// <summary>
/// 战斗场景纹理统一加载器
/// 在战斗开始前预加载所有需要的纹理资源，避免战斗中出现加载卡顿。
/// </summary>
[GlobalClass]
public partial class CombatTextureLoader : RefCounted
{
    // ========================================
    // 单例
    // ========================================

    private static CombatTextureLoader? _instance;
    public static CombatTextureLoader Instance => _instance ??= new CombatTextureLoader();

    // ========================================
    // 加载状态
    // ========================================

    /// <summary>是否已完成预加载</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>加载进度 (0.0 ~ 1.0)</summary>
    public float Progress { get; private set; }

    /// <summary>上次加载耗时 (ms)</summary>
    public double LastLoadTimeMs { get; private set; }

    // ========================================
    // 缓存 — 地图格纹理
    // ========================================

    /// <summary>顶面纹理缓存：TerrainType → Texture2D</summary>
    private readonly Dictionary<int, Texture2D> _topTextures = new();

    /// <summary>侧面纹理缓存：cliffKey → Texture2D</summary>
    private readonly Dictionary<string, Texture2D> _cliffTextures = new();

    // ========================================
    // 缓存 — 角色渲染（仅静态单图，角色不使用多帧动画）
    // ========================================

    /// <summary>角色/装备单图缓存：id → Texture2D</summary>
    private readonly Dictionary<string, Texture2D> _characterTextures = new();

    // ========================================
    // 缓存 — 武器运动动画
    // ========================================

    /// <summary>武器攻击动画帧：weaponSubtype → SpriteFrames (含 attack/default 动画)</summary>
    private readonly Dictionary<string, SpriteFrames> _weaponAnimFrames = new();

    // ========================================
    // 缓存 — 抛射物纹理
    // ========================================

    /// <summary>抛射物纹理：projectileType → Texture2D</summary>
    private readonly Dictionary<string, Texture2D> _projectileTextures = new();

    // ========================================
    // 缓存 — 场景 2D 精灵
    // ========================================

    /// <summary>场景装饰精灵：spriteId → Texture2D</summary>
    private readonly Dictionary<string, Texture2D> _sceneSprites = new();

    // ========================================
    // 资源路径常量
    // ========================================

    private const string TopTextureDir = "res://src/assets/tiles/battle_ground/tops";
    private const string CliffTextureDir = "res://src/assets/tiles/battle_ground/cliffs";
    private const string ProjectileTextureDir = "res://assets/sprites/projectiles";
    private const string WeaponAnimDir = "res://assets/sprites/weapons";
    private const string SceneSpriteDir = "res://assets/sprites/combat_scene";

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 同步预加载所有战斗纹理。
    /// 在 CombatSceneBase._Ready() 中 GenerateBattlefield 之前调用。
    /// </summary>
    public void PreloadAll(BattleMapGenerator.BattleMapData? mapData = null,
                           List<UnitData>? playerUnits = null,
                           List<UnitData>? enemyUnits = null)
    {
        if (IsLoaded) return;

        double startMs = Time.GetTicksMsec();
        Progress = 0.0f;

        // 1. 地图格纹理 (顶面 + 侧面)
        LoadTerrainTextures(mapData);
        Progress = 0.2f;

        // 2. 角色渲染纹理
        LoadCharacterTextures(playerUnits, enemyUnits);
        Progress = 0.5f;

        // 3. 武器运动动画
        LoadWeaponAnimations(playerUnits, enemyUnits);
        Progress = 0.7f;

        // 4. 抛射物纹理
        LoadProjectileTextures(playerUnits, enemyUnits);
        Progress = 0.85f;

        // 5. 场景 2D 精灵
        LoadSceneSprites();
        Progress = 1.0f;

        IsLoaded = true;
        LastLoadTimeMs = Time.GetTicksMsec() - startMs;
        GD.Print($"[CombatTextureLoader] PreloadAll 完成: {LastLoadTimeMs:F0}ms");
    }

    /// <summary>
    /// 异步预加载（支持进度回调，用于加载屏）。
    /// </summary>
    public async Task PreloadAllAsync(BattleMapGenerator.BattleMapData? mapData = null,
                                      List<UnitData>? playerUnits = null,
                                      List<UnitData>? enemyUnits = null,
                                      Action<float>? onProgress = null)
    {
        if (IsLoaded) return;

        double startMs = Time.GetTicksMsec();

        LoadTerrainTextures(mapData);
        Progress = 0.2f;
        onProgress?.Invoke(Progress);
        await Task.Yield();

        LoadCharacterTextures(playerUnits, enemyUnits);
        Progress = 0.5f;
        onProgress?.Invoke(Progress);
        await Task.Yield();

        LoadWeaponAnimations(playerUnits, enemyUnits);
        Progress = 0.7f;
        onProgress?.Invoke(Progress);
        await Task.Yield();

        LoadProjectileTextures(playerUnits, enemyUnits);
        Progress = 0.85f;
        onProgress?.Invoke(Progress);
        await Task.Yield();

        LoadSceneSprites();
        Progress = 1.0f;
        onProgress?.Invoke(Progress);

        IsLoaded = true;
        LastLoadTimeMs = Time.GetTicksMsec() - startMs;
        GD.Print($"[CombatTextureLoader] PreloadAllAsync 完成: {LastLoadTimeMs:F0}ms");
    }

    /// <summary>清空所有缓存（战斗结束后释放内存）</summary>
    public void Unload()
    {
        _topTextures.Clear();
        _cliffTextures.Clear();
        _characterTextures.Clear();
        _weaponAnimFrames.Clear();
        _projectileTextures.Clear();
        _sceneSprites.Clear();
        IsLoaded = false;
        Progress = 0.0f;
    }

    // ========================================
    // 查询 API — 供渲染系统使用
    // ========================================

    /// <summary>获取地形顶面纹理</summary>
    public Texture2D? GetTopTexture(BattleCellData.TerrainType terrainType)
    {
        _topTextures.TryGetValue((int)terrainType, out var tex);
        return tex;
    }

    /// <summary>获取地形侧面纹理</summary>
    public Texture2D? GetCliffTexture(string cliffKey)
    {
        _cliffTextures.TryGetValue(cliffKey, out var tex);
        return tex;
    }

    /// <summary>获取角色/装备单图纹理</summary>
    public Texture2D? GetCharacterTexture(string textureId)
    {
        if (string.IsNullOrEmpty(textureId)) return null;
        _characterTextures.TryGetValue(textureId, out var tex);
        return tex ?? ResourceRegistry.GetIcon(textureId);
    }

    /// <summary>获取武器攻击动画帧</summary>
    public SpriteFrames? GetWeaponAnimFrames(string weaponSubtype)
    {
        if (string.IsNullOrEmpty(weaponSubtype)) return null;
        _weaponAnimFrames.TryGetValue(weaponSubtype, out var frames);
        return frames;
    }

    /// <summary>获取抛射物纹理</summary>
    public Texture2D? GetProjectileTexture(string projectileType)
    {
        if (string.IsNullOrEmpty(projectileType)) return null;
        _projectileTextures.TryGetValue(projectileType, out var tex);
        return tex;
    }

    /// <summary>获取场景装饰精灵</summary>
    public Texture2D? GetSceneSprite(string spriteId)
    {
        if (string.IsNullOrEmpty(spriteId)) return null;
        _sceneSprites.TryGetValue(spriteId, out var tex);
        return tex;
    }

    // ========================================
    // 内部加载 — 地图格纹理
    // ========================================

    private void LoadTerrainTextures(BattleMapGenerator.BattleMapData? mapData)
    {
        // 收集本次战斗用到的地形类型
        var terrainTypes = new HashSet<BattleCellData.TerrainType>();

        if (mapData != null)
        {
            foreach (var kvp in mapData.Cells)
            {
                var cellData = kvp.Value.As<BattleCellData>();
                if (cellData != null)
                    terrainTypes.Add(cellData.terrainType);
            }
        }
        else
        {
            // 无地图数据时预加载所有类型
            foreach (BattleCellData.TerrainType t in Enum.GetValues(typeof(BattleCellData.TerrainType)))
                terrainTypes.Add(t);
        }

        // 加载顶面纹理
        foreach (var terrainType in terrainTypes)
        {
            int key = (int)terrainType;
            if (_topTextures.ContainsKey(key)) continue;

            var profile = BattleTerrainBridge.GetProfile(terrainType);

            // 尝试加载顶面纹理
            string topPath = $"{TopTextureDir}/{profile.BattleTopKey}_0.png";
            var topTex = TryLoadTexture(topPath);
            if (topTex != null)
                _topTextures[key] = topTex;

            // 加载侧面纹理
            string cliffKey = profile.BattleCliffKey;
            if (!_cliffTextures.ContainsKey(cliffKey))
            {
                string cliffPath = $"{CliffTextureDir}/{cliffKey}.png";
                var cliffTex = TryLoadTexture(cliffPath);
                if (cliffTex != null)
                    _cliffTextures[cliffKey] = cliffTex;
            }
        }
    }

    // ========================================
    // 内部加载 — 角色渲染（仅静态单图）
    // ========================================

    private void LoadCharacterTextures(List<UnitData>? playerUnits, List<UnitData>? enemyUnits)
    {
        var allUnits = new List<UnitData>();
        if (playerUnits != null) allUnits.AddRange(playerUnits);
        if (enemyUnits != null) allUnits.AddRange(enemyUnits);

        foreach (var unitData in allUnits)
        {
            if (unitData == null) continue;

            // 角色本体单图
            if (!string.IsNullOrEmpty(unitData.BattleSpriteId) && !_characterTextures.ContainsKey(unitData.BattleSpriteId))
            {
                var tex = ResourceRegistry.GetIcon(unitData.BattleSpriteId);
                if (tex != null)
                    _characterTextures[unitData.BattleSpriteId] = tex;
            }

            // 装备纹理（仅单图）
            PreloadEquipTexture(unitData.Helmet);
            PreloadEquipTexture(unitData.Armor);
            PreloadEquipTexture(unitData.Shield);
            PreloadEquipTexture(unitData.PrimaryMainHand);
            PreloadEquipTexture(unitData.SecondaryMainHand);
            PreloadEquipTexture(unitData.PrimaryOffHand);
            PreloadEquipTexture(unitData.SecondaryOffHand);
        }
    }

    private void PreloadEquipTexture(ItemData? item)
    {
        if (item == null) return;

        // 装备单图
        if (!string.IsNullOrEmpty(item.EquipTextureId) && !_characterTextures.ContainsKey(item.EquipTextureId))
        {
            var tex = ResourceRegistry.GetIcon(item.EquipTextureId);
            if (tex != null)
                _characterTextures[item.EquipTextureId] = tex;
        }
    }

    // ========================================
    // 内部加载 — 武器运动动画
    // ========================================

    /// <summary>已知武器子类型 → 动画资源路径映射</summary>
    private static readonly Dictionary<string, string> WeaponAnimPaths = new()
    {
        { "Longsword",     $"{WeaponAnimDir}/longsword_anim.tres" },
        { "Greatsword",    $"{WeaponAnimDir}/greatsword_anim.tres" },
        { "Axe",           $"{WeaponAnimDir}/axe_anim.tres" },
        { "Greataxe",      $"{WeaponAnimDir}/greataxe_anim.tres" },
        { "Mace",          $"{WeaponAnimDir}/mace_anim.tres" },
        { "Dagger",        $"{WeaponAnimDir}/dagger_anim.tres" },
        { "Spear",         $"{WeaponAnimDir}/spear_anim.tres" },
        { "Halberd",       $"{WeaponAnimDir}/halberd_anim.tres" },
        { "Bow",           $"{WeaponAnimDir}/bow_anim.tres" },
        { "Crossbow",      $"{WeaponAnimDir}/crossbow_anim.tres" },
        { "Staff",         $"{WeaponAnimDir}/staff_anim.tres" },
        { "Wand",          $"{WeaponAnimDir}/wand_anim.tres" },
        { "ThrowingKnife", $"{WeaponAnimDir}/throwing_knife_anim.tres" },
        { "Shield",        $"{WeaponAnimDir}/shield_anim.tres" },
    };

    private void LoadWeaponAnimations(List<UnitData>? playerUnits, List<UnitData>? enemyUnits)
    {
        var subtypes = new HashSet<string>();

        // 收集本次战斗中出现的武器子类型
        CollectWeaponSubtypes(playerUnits, subtypes);
        CollectWeaponSubtypes(enemyUnits, subtypes);

        // 如果没有具体单位数据，预加载所有已知类型
        if (subtypes.Count == 0)
        {
            foreach (var key in WeaponAnimPaths.Keys)
                subtypes.Add(key);
        }

        foreach (var subtype in subtypes)
        {
            if (_weaponAnimFrames.ContainsKey(subtype)) continue;

            // 优先从路径表加载
            if (WeaponAnimPaths.TryGetValue(subtype, out var path) && ResourceLoader.Exists(path))
            {
                var frames = GD.Load<SpriteFrames>(path);
                if (frames != null)
                {
                    _weaponAnimFrames[subtype] = frames;
                    continue;
                }
            }

            // 回退：尝试通用路径
            string genericPath = $"{WeaponAnimDir}/{subtype.ToLower()}_anim.tres";
            if (ResourceLoader.Exists(genericPath))
            {
                var frames = GD.Load<SpriteFrames>(genericPath);
                if (frames != null)
                    _weaponAnimFrames[subtype] = frames;
            }
        }
    }

    private static void CollectWeaponSubtypes(List<UnitData>? units, HashSet<string> subtypes)
    {
        if (units == null) return;
        foreach (var unit in units)
        {
            if (unit == null) continue;
            AddWeaponSubtype(unit.PrimaryMainHand as WeaponData, subtypes);
            AddWeaponSubtype(unit.SecondaryMainHand as WeaponData, subtypes);
        }
    }

    private static void AddWeaponSubtype(WeaponData? weapon, HashSet<string> subtypes)
    {
        if (weapon == null) return;
        subtypes.Add(weapon.Subtype.ToString());
    }

    // ========================================
    // 内部加载 — 抛射物纹理
    // ========================================

    /// <summary>已知抛射物类型 → 纹理路径</summary>
    private static readonly Dictionary<string, string> ProjectileTexturePaths = new()
    {
        { "arrow",          $"{ProjectileTextureDir}/arrow.png" },
        { "crossbow_bolt",  $"{ProjectileTextureDir}/crossbow_bolt.png" },
        { "throwing_knife", $"{ProjectileTextureDir}/throwing_knife.png" },
        { "throwing_axe",   $"{ProjectileTextureDir}/throwing_axe.png" },
        { "fireball",       $"{ProjectileTextureDir}/fireball.png" },
        { "magic_bolt",     $"{ProjectileTextureDir}/magic_bolt.png" },
        { "ice_shard",      $"{ProjectileTextureDir}/ice_shard.png" },
        { "lightning",      $"{ProjectileTextureDir}/lightning.png" },
    };

    private void LoadProjectileTextures(List<UnitData>? playerUnits, List<UnitData>? enemyUnits)
    {
        var neededTypes = new HashSet<string>();

        // 根据武器类型推断需要的抛射物
        InferProjectileTypes(playerUnits, neededTypes);
        InferProjectileTypes(enemyUnits, neededTypes);

        // 如果没有具体数据，预加载所有已知类型
        if (neededTypes.Count == 0)
        {
            foreach (var key in ProjectileTexturePaths.Keys)
                neededTypes.Add(key);
        }

        foreach (var type in neededTypes)
        {
            if (_projectileTextures.ContainsKey(type)) continue;

            if (ProjectileTexturePaths.TryGetValue(type, out var path))
            {
                var tex = TryLoadTexture(path);
                if (tex != null)
                    _projectileTextures[type] = tex;
            }
            else
            {
                // 尝试通用路径
                string genericPath = $"{ProjectileTextureDir}/{type}.png";
                var tex = TryLoadTexture(genericPath);
                if (tex != null)
                    _projectileTextures[type] = tex;
            }
        }
    }

    private static void InferProjectileTypes(List<UnitData>? units, HashSet<string> types)
    {
        if (units == null) return;
        foreach (var unit in units)
        {
            if (unit == null) continue;
            InferFromWeapon(unit.PrimaryMainHand as WeaponData, types);
            InferFromWeapon(unit.SecondaryMainHand as WeaponData, types);
        }
    }

    private static void InferFromWeapon(WeaponData? weapon, HashSet<string> types)
    {
        if (weapon == null || !weapon.IsRanged) return;

        // 根据武器子类型推断抛射物类型
        string subtype = weapon.Subtype.ToString();
        switch (subtype)
        {
            case "Bow":
                types.Add("arrow");
                break;
            case "Crossbow":
                types.Add("crossbow_bolt");
                break;
            case "ThrowingKnife":
                types.Add("throwing_knife");
                break;
            case "ThrowingAxe":
                types.Add("throwing_axe");
                break;
            default:
                // 魔法武器默认 magic_bolt
                if (weapon.WeaponDamageType == WeaponData.DamageType.Fire)
                    types.Add("fireball");
                else if (weapon.WeaponDamageType == WeaponData.DamageType.Frost)
                    types.Add("ice_shard");
                else
                    types.Add("magic_bolt");
                break;
        }
    }

    // ========================================
    // 内部加载 — 场景 2D 精灵
    // ========================================

    /// <summary>场景装饰精灵 ID 列表（树木、岩石、旗帜等）</summary>
    private static readonly string[] SceneSpriteIds =
    {
        "tree_oak", "tree_pine", "tree_dead",
        "rock_small", "rock_large", "rock_moss",
        "bush_green", "bush_dry",
        "banner_red", "banner_blue",
        "campfire", "tent",
        "fence_wood", "fence_stone",
        "grave", "skull",
        "barrel", "crate",
    };

    private void LoadSceneSprites()
    {
        // 扫描场景精灵目录
        if (DirAccess.DirExistsAbsolute(SceneSpriteDir) || ResourceLoader.Exists($"{SceneSpriteDir}/tree_oak.png"))
        {
            foreach (var id in SceneSpriteIds)
            {
                if (_sceneSprites.ContainsKey(id)) continue;
                string path = $"{SceneSpriteDir}/{id}.png";
                var tex = TryLoadTexture(path);
                if (tex != null)
                    _sceneSprites[id] = tex;
            }
        }

        // 也从 ResourceRegistry 中查找已注册的场景精灵
        foreach (var id in SceneSpriteIds)
        {
            if (_sceneSprites.ContainsKey(id)) continue;
            var tex = ResourceRegistry.GetIcon(id);
            if (tex != null)
                _sceneSprites[id] = tex;
        }
    }

    // ========================================
    // 工具方法
    // ========================================

    /// <summary>安全加载纹理（不存在时返回 null，不抛异常）</summary>
    private static Texture2D? TryLoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!ResourceLoader.Exists(path)) return null;
        return GD.Load<Texture2D>(path);
    }

    /// <summary>获取加载统计信息</summary>
    public string GetStats()
    {
        return $"[CombatTextureLoader] 顶面:{_topTextures.Count} 侧面:{_cliffTextures.Count} " +
               $"角色图:{_characterTextures.Count} " +
               $"武器动画:{_weaponAnimFrames.Count} 抛射物:{_projectileTextures.Count} " +
               $"场景精灵:{_sceneSprites.Count} | 耗时:{LastLoadTimeMs:F0}ms";
    }
}
