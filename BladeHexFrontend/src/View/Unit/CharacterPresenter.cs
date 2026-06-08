



using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.View.AssetSystem;
using BladeHex.View.Data;

namespace BladeHex.View.Unit;


public sealed class CharacterSlotResolution
{
    public ItemData.EquipSlot Slot { get; init; }
    public SpriteFrames? Frames { get; init; }
    public Texture2D? Texture { get; init; }
    public Color Modulate { get; init; } = Colors.White;

    public bool HasContent => Frames != null || Texture != null;
}


public sealed class CharacterResolution
{
    public Dictionary<ItemData.EquipSlot, CharacterSlotResolution> Slots { get; } = new();
    public float BodyTextureHeight { get; set; } = 120.0f;
    public bool BodyIsPlaceholder { get; set; }
    public Color PlaceholderModulate { get; set; } = Colors.White;
    public string DefaultAnimation { get; set; } = "default";
}


public static class CharacterPresenter
{

    public static readonly Color PlayerPlaceholder = new(0.4f, 0.7f, 1.0f);


    public static readonly Color EnemyPlaceholder = new(1.0f, 0.4f, 0.4f);



    public static CharacterResolution Resolve(UnitData data, bool useSecondaryWeapon = false)
    {
        var result = new CharacterResolution();




        if (CreatureTextureConfig.IsCreature(data))
        {
            var creatureTex = CreatureTextureConfig.TryLoadSprite(data)
                           ?? CreatureTextureConfig.GeneratePlaceholder(data);
            result.Slots[ItemData.EquipSlot.Body] = new CharacterSlotResolution
            {
                Slot = ItemData.EquipSlot.Body,
                Texture = creatureTex,
                Modulate = Colors.White,
            };
            result.BodyTextureHeight = creatureTex.GetHeight();
            result.BodyIsPlaceholder = data.EnemyTemplateId == null
                || CreatureTextureConfig.TryLoadSprite(data) == null;
            result.PlaceholderModulate = Colors.White;
            return result;
        }

        int seed = System.Math.Abs((data.UnitName ?? "").GetHashCode() + data.CharacterId * 31);
        string gender = (!string.IsNullOrEmpty(data.GenderCustom) && (data.GenderCustom == "male" || data.GenderCustom == "female"))
            ? data.GenderCustom
            : ((seed % 2 == 0) ? "male" : "female");
        int bodyIdx = (seed % 2) + 1;
        int headIdx = (seed % 9) + 1;
        string raceStr = (data.Race != null) ? data.Race.raceId.ToString().ToLower() : "human";


        var bodyTex = AvatarRenderer.LoadPartTexture("body", raceStr, gender, bodyIdx);
        if (bodyTex != null)
        {
            result.Slots[ItemData.EquipSlot.Body] = new CharacterSlotResolution
            {
                Slot = ItemData.EquipSlot.Body,
                Texture = bodyTex,
                Modulate = Colors.White,
            };
            result.BodyTextureHeight = bodyTex.GetHeight();
            result.BodyIsPlaceholder = false;
            result.PlaceholderModulate = Colors.White;
        }
        else
        {

            var bodySlot = ResolveBody(data, out float bodyHeight, out bool isPlaceholder, out Color placeholderColor);
            result.Slots[ItemData.EquipSlot.Body] = bodySlot;
            result.BodyTextureHeight = bodyHeight;
            result.BodyIsPlaceholder = isPlaceholder;
            result.PlaceholderModulate = placeholderColor;
        }


        int faceIdx = data.FaceIndex > 0 ? data.FaceIndex : headIdx;
       

        var faceTex = AvatarRenderer.LoadPartTexture("face", raceStr, gender, faceIdx);
        if (faceTex == null)
        {

        	faceTex = AvatarRenderer.LoadPartTexture("head", raceStr, gender, faceIdx);
        }
        if (faceTex != null)
        {
        	result.Slots[ItemData.EquipSlot.Face] = new CharacterSlotResolution
        	{
        		Slot = ItemData.EquipSlot.Face,
        		Texture = faceTex,
        		Modulate = Colors.White,
        	};

        	result.Slots[ItemData.EquipSlot.Head] = new CharacterSlotResolution
        	{
        		Slot = ItemData.EquipSlot.Head,
        		Texture = faceTex,
        		Modulate = Colors.White,
        	};
        }
       

        if (data.HairIndex > 0)
        {
        	var hairTex = AvatarRenderer.LoadPartTexture("hair", raceStr, gender, data.HairIndex);
        	if (hairTex != null)
        	{
        		result.Slots[ItemData.EquipSlot.Hair] = new CharacterSlotResolution
        		{
        			Slot = ItemData.EquipSlot.Hair,
        			Texture = hairTex,
        			Modulate = Colors.White,
        		};
        	}
        }


        TryAddEquip(result, data.Helmet);
        TryAddEquip(result, data.Armor);
        TryAddEquip(result, data.Shield);
        TryAddEquip(result, data.Gauntlets);
        TryAddEquip(result, data.Boots);

        var mainHand = useSecondaryWeapon ? data.SecondaryMainHand : data.PrimaryMainHand;
        TryAddEquip(result, mainHand);


        if (result.Slots.TryGetValue(ItemData.EquipSlot.Costume, out var costumeSlot) && costumeSlot.HasContent)
        {
            result.Slots.Remove(ItemData.EquipSlot.Body);
        }


        if (result.Slots.TryGetValue(ItemData.EquipSlot.Helmet, out var helmetSlot) && helmetSlot.HasContent)
        {
            result.Slots.Remove(ItemData.EquipSlot.Hair);
        }

        return result;
    }



    public static Texture2D? ResolvePortrait(UnitData data)
    {

        var avatar = data.GetAvatar();
        var rendered = AvatarRenderer.RenderToSquare(avatar, 128);
        if (rendered != null) return rendered;

        int seed = System.Math.Abs((data.UnitName ?? "").GetHashCode() + data.CharacterId * 31);

        string gender = (!string.IsNullOrEmpty(data.GenderCustom) && (data.GenderCustom == "male" || data.GenderCustom == "female"))
            ? data.GenderCustom
            : ((seed % 2 == 0) ? "male" : "female");
        int headIdx = (seed % 9) + 1;
        int faceIdx = data.FaceIndex > 0 ? data.FaceIndex : headIdx;
        string raceStr = (data.Race != null) ? data.Race.raceId.ToString().ToLower() : "human";

        var faceTex = AvatarRenderer.LoadPartTexture("face", raceStr, gender, faceIdx);
        faceTex ??= AvatarRenderer.LoadPartTexture("head", raceStr, gender, faceIdx);
        if (faceTex != null) return faceTex;


        var tex = TextureAssetResolver.LoadPortrait(data.PortraitId);
        if (tex != null) return tex;
        return TextureAssetResolver.LoadUnitSprite(data.BattleSpriteId);
    }






    private static CharacterSlotResolution ResolveBody(
        UnitData data, out float height, out bool isPlaceholder, out Color placeholderColor)
    {
        height = 120.0f;
        isPlaceholder = false;
        placeholderColor = data.IsEnemy ? EnemyPlaceholder : PlayerPlaceholder;


        var frames = SpriteFramesAssetResolver.Load(data.SpriteFramesId);
        if (frames != null)
        {
            if (frames.GetFrameCount("default") > 0)
            {
                var firstFrame = frames.GetFrameTexture("default", 0);
                if (firstFrame != null) height = firstFrame.GetHeight();
            }
            return new CharacterSlotResolution
            {
                Slot = ItemData.EquipSlot.Body,
                Frames = frames,
            };
        }


        var battleSprite = TextureAssetResolver.LoadUnitSprite(data.BattleSpriteId);
        if (battleSprite != null)
        {
            height = battleSprite.GetHeight();
            return new CharacterSlotResolution
            {
                Slot = ItemData.EquipSlot.Body,
                Texture = battleSprite,
            };
        }


        isPlaceholder = true;
        var placeholder = UnitPlaceholderRenderer.Generate(data, placeholderColor);
        height = placeholder.GetHeight();

        placeholderColor = Colors.White;
        return new CharacterSlotResolution
        {
            Slot = ItemData.EquipSlot.Body,
            Texture = placeholder,
            Modulate = Colors.White,
        };
    }

    private static void TryAddEquip(CharacterResolution result, ItemData? item)
    {
        if (item == null) return;
        var slot = item.EquipSlotTarget;

        if (slot == ItemData.EquipSlot.Body)
        {
            slot = item switch
            {
                WeaponData => ItemData.EquipSlot.Weapon,
                ArmorData a when a.armorType == ArmorData.ArmorType.Shield
                    => ItemData.EquipSlot.Weapon,
                ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Helmet
                                || a.EquipSlotTarget == ItemData.EquipSlot.Head
                    => ItemData.EquipSlot.Helmet,
                ArmorData a when a.EquipSlotTarget == ItemData.EquipSlot.Hands
                    => ItemData.EquipSlot.Hands,
                ArmorData => ItemData.EquipSlot.Costume,
                _ => ItemData.EquipSlot.Costume,
            };
        }

        var frames = SpriteFramesAssetResolver.Load(item.EquipSpriteFramesId);
        if (frames != null)
        {
            result.Slots[slot] = new CharacterSlotResolution { Slot = slot, Frames = frames };
            return;
        }


        var tex = TextureAssetResolver.LoadEquipmentTexture(item);
        if (tex != null)
        {
            result.Slots[slot] = new CharacterSlotResolution { Slot = slot, Texture = tex };
            return;
        }


        var placeholderTex = EquipmentPlaceholderRenderer.Generate(item, item.GetRarityColor());
        if (placeholderTex != null)
        {
            result.Slots[slot] = new CharacterSlotResolution { Slot = slot, Texture = placeholderTex };
            return;
        }

    }
}
