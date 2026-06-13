using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.AssetSystem;
using BladeHex.View.Data;
using BladeHex.View.Map;
using BladeHex.View.Unit;
using BladeHex.View.Unit.Slots;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BladeHex.Combat;

/// <summary>
/// Preloads and caches combat texture resources before a battle starts.
/// </summary>
[GlobalClass]
public partial class CombatTextureLoader : RefCounted
{
    private static CombatTextureLoader? _instance;
    public static CombatTextureLoader Instance => _instance ??= new CombatTextureLoader();

    public bool IsLoaded { get; private set; }
    public float Progress { get; private set; }
    public double LastLoadTimeMs { get; private set; }

    private readonly Dictionary<int, Texture2D> _topTextures = new();
    private readonly Dictionary<string, Texture2D> _cliffTextures = new();
    private readonly Dictionary<string, Texture2D> _characterTextures = new();
    private readonly Dictionary<string, SpriteFrames> _weaponAnimFrames = new();
    private readonly Dictionary<string, Texture2D> _projectileTextures = new();
    private readonly Dictionary<string, Texture2D> _sceneSprites = new();

    private const string TopTextureDir = "res://BladeHexFrontend/src/assets/tiles/battle_ground/tops";
    private const string CliffTextureDir = "res://assets/tiles/battle_ground/cliffs";
    private const string ProjectileTextureDir = "res://assets/sprites/projectiles";
    private const string WeaponAnimDir = "res://assets/sprites/weapons";
    private const string SceneSpriteDir = "res://assets/sprites/combat_scene";

    public void PreloadAll(
        BattleMapGenerator.BattleMapData? mapData = null,
        List<UnitData>? playerUnits = null,
        List<UnitData>? enemyUnits = null)
    {
        if (IsLoaded)
            return;

        double startMs = Time.GetTicksMsec();
        Progress = 0.0f;

        LoadTerrainTextures(mapData);
        Progress = 0.2f;

        LoadCharacterTextures(playerUnits, enemyUnits);
        Progress = 0.5f;

        LoadWeaponAnimations(playerUnits, enemyUnits);
        Progress = 0.7f;

        LoadProjectileTextures(playerUnits, enemyUnits);
        Progress = 0.85f;

        LoadSceneSprites();
        Progress = 1.0f;

        IsLoaded = true;
        LastLoadTimeMs = Time.GetTicksMsec() - startMs;
        GD.Print($"[CombatTextureLoader] PreloadAll completed: {LastLoadTimeMs:F0}ms");
    }

    public async Task PreloadAllAsync(
        BattleMapGenerator.BattleMapData? mapData = null,
        List<UnitData>? playerUnits = null,
        List<UnitData>? enemyUnits = null,
        Action<float>? onProgress = null)
    {
        if (IsLoaded)
            return;

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
        GD.Print($"[CombatTextureLoader] PreloadAllAsync completed: {LastLoadTimeMs:F0}ms");
    }

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

    public Texture2D? GetTopTexture(BattleCellData.TerrainType terrainType)
    {
        _topTextures.TryGetValue((int)terrainType, out var tex);
        return tex;
    }

    public Texture2D? GetCliffTexture(string cliffKey)
    {
        _cliffTextures.TryGetValue(cliffKey, out var tex);
        return tex;
    }

    public Texture2D? GetCharacterTexture(string textureId)
    {
        if (string.IsNullOrEmpty(textureId))
            return null;

        _characterTextures.TryGetValue(textureId, out var tex);
        return tex ?? TextureAssetResolver.Load(AssetKind.EquipmentTexture, textureId);
    }

    public SpriteFrames? GetWeaponAnimFrames(string weaponSubtype)
    {
        if (string.IsNullOrEmpty(weaponSubtype))
            return null;

        _weaponAnimFrames.TryGetValue(weaponSubtype, out var frames);
        return frames;
    }

    public Texture2D? GetProjectileTexture(string projectileType)
    {
        if (string.IsNullOrEmpty(projectileType))
            return null;

        if (_projectileTextures.TryGetValue(projectileType, out var tex))
            return tex;

        return TextureAssetResolver.LoadProjectileTexture(projectileType, GetProjectileFallbackPath(projectileType));
    }

    public Texture2D? GetSceneSprite(string spriteId)
    {
        if (string.IsNullOrEmpty(spriteId))
            return null;

        if (_sceneSprites.TryGetValue(spriteId, out var tex))
            return tex;

        tex = BattlePropRegistry.GetTexture(spriteId);
        _sceneSprites[spriteId] = tex;
        return tex;
    }

    private void LoadTerrainTextures(BattleMapGenerator.BattleMapData? mapData)
    {
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
            foreach (BattleCellData.TerrainType terrainType in Enum.GetValues(typeof(BattleCellData.TerrainType)))
                terrainTypes.Add(terrainType);
        }

        foreach (var terrainType in terrainTypes)
        {
            int key = (int)terrainType;
            if (_topTextures.ContainsKey(key))
                continue;

            var profile = BattleTerrainBridge.GetProfile(terrainType);
            string topPath = $"{TopTextureDir}/{profile.BattleTopKey}_0.png";
            var topTex = TryLoadTexture(topPath);
            if (topTex != null)
                _topTextures[key] = topTex;

            string cliffKey = profile.BattleCliffKey;
            if (_cliffTextures.ContainsKey(cliffKey))
                continue;

            string cliffPath = $"{CliffTextureDir}/{cliffKey}.png";
            var cliffTex = TryLoadTexture(cliffPath);
            if (cliffTex == null)
            {
                cliffPath = $"{CliffTextureDir}/{cliffKey}.jpeg";
                cliffTex = TryLoadTexture(cliffPath);
            }

            if (cliffTex != null)
                _cliffTextures[cliffKey] = cliffTex;
        }
    }

    private void LoadCharacterTextures(List<UnitData>? playerUnits, List<UnitData>? enemyUnits)
    {
        var allUnits = new List<UnitData>();
        if (playerUnits != null)
            allUnits.AddRange(playerUnits);
        if (enemyUnits != null)
            allUnits.AddRange(enemyUnits);

        foreach (var unitData in allUnits)
        {
            if (unitData == null)
                continue;

            if (!string.IsNullOrEmpty(unitData.BattleSpriteId) && !_characterTextures.ContainsKey(unitData.BattleSpriteId))
            {
                var tex = TextureAssetResolver.LoadUnitSprite(unitData.BattleSpriteId);
                if (tex != null)
                    _characterTextures[unitData.BattleSpriteId] = tex;
            }

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
        if (item == null)
            return;

        if (!string.IsNullOrEmpty(item.EquipTextureId) && !_characterTextures.ContainsKey(item.EquipTextureId))
        {
            var tex = TextureAssetResolver.LoadEquipmentTexture(item);
            if (tex != null)
                _characterTextures[item.EquipTextureId] = tex;
        }
    }

    private static readonly Dictionary<string, string> WeaponAnimPaths = new()
    {
        { "Longsword", $"{WeaponAnimDir}/longsword_anim.tres" },
        { "Greatsword", $"{WeaponAnimDir}/greatsword_anim.tres" },
        { "Axe", $"{WeaponAnimDir}/axe_anim.tres" },
        { "Greataxe", $"{WeaponAnimDir}/greataxe_anim.tres" },
        { "Mace", $"{WeaponAnimDir}/mace_anim.tres" },
        { "Dagger", $"{WeaponAnimDir}/dagger_anim.tres" },
        { "Spear", $"{WeaponAnimDir}/spear_anim.tres" },
        { "Halberd", $"{WeaponAnimDir}/halberd_anim.tres" },
        { "Bow", $"{WeaponAnimDir}/bow_anim.tres" },
        { "Crossbow", $"{WeaponAnimDir}/crossbow_anim.tres" },
        { "Staff", $"{WeaponAnimDir}/staff_anim.tres" },
        { "Wand", $"{WeaponAnimDir}/wand_anim.tres" },
        { "ThrowingKnife", $"{WeaponAnimDir}/throwing_knife_anim.tres" },
        { "Shield", $"{WeaponAnimDir}/shield_anim.tres" },
    };

    private void LoadWeaponAnimations(List<UnitData>? playerUnits, List<UnitData>? enemyUnits)
    {
        var subtypes = new HashSet<string>();
        CollectWeaponSubtypes(playerUnits, subtypes);
        CollectWeaponSubtypes(enemyUnits, subtypes);

        if (subtypes.Count == 0)
        {
            foreach (var key in WeaponAnimPaths.Keys)
                subtypes.Add(key);
        }

        foreach (var subtype in subtypes)
        {
            if (_weaponAnimFrames.ContainsKey(subtype))
                continue;

            if (WeaponAnimPaths.TryGetValue(subtype, out var path))
            {
                var frames = TryLoadSpriteFramesQuiet(subtype, path);
                if (frames != null)
                {
                    _weaponAnimFrames[subtype] = frames;
                    continue;
                }
            }

            string genericPath = $"{WeaponAnimDir}/{subtype.ToLower()}_anim.tres";
            var genericFrames = TryLoadSpriteFramesQuiet($"{subtype.ToLower()}_anim", genericPath);
            if (genericFrames != null)
                _weaponAnimFrames[subtype] = genericFrames;
        }
    }

    private static SpriteFrames? TryLoadSpriteFramesQuiet(string id, string path)
    {
        var frames = TryLoadSpriteFramesPath(path);
        if (frames != null)
            return frames;

        if (AssetCatalog.TryGetPath(AssetKind.SpriteFrames, id, out var catalogPath))
        {
            frames = TryLoadSpriteFramesPath(catalogPath);
            if (frames != null)
                return frames;
        }

        return ResourceRegistry.GetSpriteFrames(id);
    }

    private static SpriteFrames? TryLoadSpriteFramesPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!ResourceLoader.Exists(path))
            return null;

        return ResourceLoader.Load<SpriteFrames>(path);
    }

    private static void CollectWeaponSubtypes(List<UnitData>? units, HashSet<string> subtypes)
    {
        if (units == null)
            return;

        foreach (var unit in units)
        {
            if (unit == null)
                continue;

            AddWeaponSubtype(unit.PrimaryMainHand as WeaponData, subtypes);
            AddWeaponSubtype(unit.SecondaryMainHand as WeaponData, subtypes);
        }
    }

    private static void AddWeaponSubtype(WeaponData? weapon, HashSet<string> subtypes)
    {
        if (weapon == null)
            return;

        subtypes.Add(weapon.Subtype.ToString());
    }

    private static readonly Dictionary<string, string> ProjectileTexturePaths = new()
    {
        { "arrow", $"{ProjectileTextureDir}/arrow.png" },
        { "crossbow_bolt", $"{ProjectileTextureDir}/crossbow_bolt.png" },
        { "throwing_knife", $"{ProjectileTextureDir}/throwing_knife.png" },
        { "throwing_axe", $"{ProjectileTextureDir}/throwing_axe.png" },
        { "fireball", $"{ProjectileTextureDir}/fireball.png" },
        { "magic_bolt", $"{ProjectileTextureDir}/magic_bolt.png" },
        { "ice_shard", $"{ProjectileTextureDir}/ice_shard.png" },
        { "lightning", $"{ProjectileTextureDir}/lightning.png" },
    };

    private void LoadProjectileTextures(List<UnitData>? playerUnits, List<UnitData>? enemyUnits)
    {
        var neededTypes = new HashSet<string>();
        InferProjectileTypes(playerUnits, neededTypes);
        InferProjectileTypes(enemyUnits, neededTypes);

        if (neededTypes.Count == 0)
        {
            foreach (var key in ProjectileTexturePaths.Keys)
                neededTypes.Add(key);
        }

        foreach (var type in neededTypes)
        {
            if (_projectileTextures.ContainsKey(type))
                continue;

            var tex = TextureAssetResolver.LoadProjectileTexture(type, GetProjectileFallbackPath(type));
            if (tex != null)
                _projectileTextures[type] = tex;
        }
    }

    private static string GetProjectileFallbackPath(string projectileType)
    {
        return ProjectileTexturePaths.TryGetValue(projectileType, out var path)
            ? path
            : $"{ProjectileTextureDir}/{projectileType}.png";
    }

    private static void InferProjectileTypes(List<UnitData>? units, HashSet<string> types)
    {
        if (units == null)
            return;

        foreach (var unit in units)
        {
            if (unit == null)
                continue;

            InferFromWeapon(unit.PrimaryMainHand as WeaponData, types);
            InferFromWeapon(unit.SecondaryMainHand as WeaponData, types);
        }
    }

    private static void InferFromWeapon(WeaponData? weapon, HashSet<string> types)
    {
        if (weapon == null || !weapon.IsRanged)
            return;

        switch (weapon.Subtype.ToString())
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
                if (weapon.WeaponDamageType == WeaponData.DamageType.Fire)
                    types.Add("fireball");
                else if (weapon.WeaponDamageType == WeaponData.DamageType.Frost)
                    types.Add("ice_shard");
                else
                    types.Add("magic_bolt");
                break;
        }
    }

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
        if (DirAccess.DirExistsAbsolute(SceneSpriteDir) || ResourceLoader.Exists($"{SceneSpriteDir}/tree_oak.png"))
        {
            foreach (var id in SceneSpriteIds)
            {
                if (_sceneSprites.ContainsKey(id))
                    continue;

                string path = $"{SceneSpriteDir}/{id}.png";
                var tex = TryLoadTexture(path);
                if (tex != null)
                    _sceneSprites[id] = tex;
            }
        }

        foreach (var id in SceneSpriteIds)
        {
            if (_sceneSprites.ContainsKey(id))
                continue;

            _sceneSprites[id] = BattlePropRegistry.GetTexture(id);
        }
    }

    private static Texture2D? TryLoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        return TextureAssetResolver.LoadPath(path);
    }

    public string GetStats()
    {
        return $"[CombatTextureLoader] tops:{_topTextures.Count} cliffs:{_cliffTextures.Count} " +
               $"characters:{_characterTextures.Count} weapon_anims:{_weaponAnimFrames.Count} " +
               $"projectiles:{_projectileTextures.Count} scene_sprites:{_sceneSprites.Count} " +
               $"load_time:{LastLoadTimeMs:F0}ms";
    }
}
