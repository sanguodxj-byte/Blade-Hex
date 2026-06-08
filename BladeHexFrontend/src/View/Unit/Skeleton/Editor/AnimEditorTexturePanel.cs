using BladeHex.View.AssetSystem;
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Unit.Skeleton.Editor;

public partial class AnimEditorTexturePanel : PanelContainer
{
    [Signal] public delegate void TextureSelectedEventHandler(string slotName, string texturePath);

    private readonly record struct SlotTextureSource(string SlotName, AssetKind Kind, string[] Directories);

    private static readonly SlotTextureSource[] SlotSources =
    [
        new("身体", AssetKind.Icon, ["res://assets/generated_class_icons"]),
        new("护甲", AssetKind.EquipmentTexture, ["res://assets/generated_armor"]),
        new("头盔", AssetKind.EquipmentTexture, ["res://assets/generated_helmets"]),
        new("手甲", AssetKind.EquipmentTexture, ["res://assets/generated_armor"]),
        new("武器", AssetKind.EquipmentTexture, ["res://assets/generated_weapons", "res://assets/generated_staves"]),
        new("盾牌", AssetKind.EquipmentTexture, ["res://assets/generated_shields"]),
    ];

    private TabContainer _tabs = null!;
    private readonly Dictionary<string, ItemList> _lists = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(220, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.9f) };
        style.SetContentMarginAll(4);
        AddThemeStyleboxOverride("panel", style);

        _tabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(_tabs);

        foreach (var source in SlotSources)
            AddSlotTab(source);
    }

    private void AddSlotTab(SlotTextureSource source)
    {
        var scroll = new ScrollContainer
        {
            Name = source.SlotName,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        var list = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(200, 300),
            IconMode = ItemList.IconModeEnum.Top,
            MaxColumns = 3,
            FixedIconSize = new Vector2I(48, 48),
            AllowReselect = true,
        };

        AddDefaultItem(list);
        AddTextureItems(list, source);

        list.ItemSelected += index =>
        {
            string path = list.GetItemMetadata((int)index).AsString();
            EmitSignal(SignalName.TextureSelected, source.SlotName, path);
        };

        _lists[source.SlotName] = list;
        scroll.AddChild(list);
        _tabs.AddChild(scroll);
    }

    private static void AddDefaultItem(ItemList list)
    {
        list.AddItem("默认", null);
        list.SetItemMetadata(0, "");
    }

    private static void AddTextureItems(ItemList list, SlotTextureSource source)
    {
        int index = list.ItemCount;
        foreach (var directory in source.Directories)
        {
            foreach (var path in ScanPngFiles(directory))
            {
                var texture = TextureAssetResolver.Load(source.Kind, path, path);
                if (texture == null)
                    continue;

                string fileName = path.GetFile().GetBaseName();
                list.AddItem(fileName, texture);
                list.SetItemMetadata(index, path);
                list.SetItemTooltip(index, path);
                index++;
            }
        }
    }

    private static List<string> ScanPngFiles(string dirPath)
    {
        var results = new List<string>();
        var dir = DirAccess.Open(dirPath);
        if (dir == null)
            return results;

        dir.ListDirBegin();
        try
        {
            string fileName = dir.GetNext();
            while (!string.IsNullOrEmpty(fileName))
            {
                if (!dir.CurrentIsDir() && fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    results.Add($"{dirPath}/{fileName}");

                fileName = dir.GetNext();
            }
        }
        finally
        {
            dir.ListDirEnd();
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }
}
