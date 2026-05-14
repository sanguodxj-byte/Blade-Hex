// EquipmentGeneratorProxy.cs
// RefCounted wrapper
// Nested enum ItemData.Rarity is passed as int from and cast inside
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat;

[GlobalClass]
public partial class EquipmentGeneratorProxy : RefCounted
{
    public static WeaponData? GenerateRandomWeapon(Godot.Collections.Array weaponPool, int targetRarity, int itemLevel, string difficulty)
    {
        string[]? pool = null;
        if (weaponPool != null && weaponPool.Count > 0)
        {
            pool = new string[weaponPool.Count];
            for (int i = 0; i < weaponPool.Count; i++)
                pool[i] = (string)weaponPool[i];
        }
        return EquipmentGenerator.GenerateRandomWeapon(pool, (ItemData.Rarity)targetRarity, itemLevel, difficulty);
    }

    public static ArmorData? GenerateRandomArmor(Godot.Collections.Array armorPool, int targetRarity, int itemLevel, string difficulty)
    {
        string[]? pool = null;
        if (armorPool != null && armorPool.Count > 0)
        {
            pool = new string[armorPool.Count];
            for (int i = 0; i < armorPool.Count; i++)
                pool[i] = (string)armorPool[i];
        }
        return EquipmentGenerator.GenerateRandomArmor(pool, (ItemData.Rarity)targetRarity, itemLevel, difficulty);
    }
}
