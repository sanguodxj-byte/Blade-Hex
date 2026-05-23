// AnimEditorTexturePanel.cs
// 运行时骨骼动画编辑器 — 纹理选择面板
// 扫描 assets 目录中的装备纹理，按部件类型分组，选中后应用到预览骨骼
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>纹理选择面板 — 按部件分 Tab 展示可用纹理</summary>
public partial class AnimEditorTexturePanel : PanelContainer
{
    [Signal] public delegate void TextureSelectedEventHandler(string slotName, string texturePath);

    /// <summary>部件 → 扫描目录映射</summary>
    private static readonly Dictionary<string, string[]> SlotDirs = new()
    {
        ["身体"] = new[] { "res://assets/generated_class_icons" },
        ["护甲"] = new[] { "res://assets/generated_armor" },
        ["头盔"] = new[] { "res://assets/generated_helmets" },
        ["手甲"] = new[] { "res://assets/generated_armor" },
        ["武器"] = new[] { "res://assets/generated_weapons", "res://assets/generated_staves" },
        ["盾牌"] = new[] { "res://assets/generated_shields" },
    };

    private TabContainer _tabs = null!;
    private readonly Dictionary<string, ItemList> _lists = new();

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(220, 0);
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.9f) };
        style.SetContentMarginAll(4);
        AddThemeStyleboxOverride("panel", style);

        _tabs = new TabContainer();
        _tabs.SizeFlagsVertical = SizeFlags.ExpandFill;
        _tabs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_tabs);

        foreach (var (slotName, dirs) in SlotDirs)
        {
            var scroll = new ScrollContainer { Name = slotName };
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;

            var list = new ItemList();
            list.SizeFlagsVertical = SizeFlags.ExpandFill;
            list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            list.CustomMinimumSize = new Vector2(200, 300);
            list.IconMode = ItemList.IconModeEnum.Top;
            list.MaxColumns = 3;
            list.FixedIconSize = new Vector2I(48, 48);
            list.AllowReselect = true;

            // 第一项：占位色块（清除纹理）
            list.AddItem("默认", null);
            list.SetItemMetadata(0, "");

            // 扫描目录
            int idx = 1;
            foreach (var dir in dirs)
            {
                var textures = ScanPngFiles(dir);
                foreach (var path in textures)
                {
                    string fileName = path.GetFile().GetBaseName();
                    var tex = GD.Load<Texture2D>(path);
                    if (tex != null)
                    {
                        list.AddItem(fileName, tex);
                        list.SetItemMetadata(idx, path);
                        list.SetItemTooltip(idx, path);
                        idx++;
                    }
                }
            }

            string capturedSlot = slotName;
            list.ItemSelected += (long i) =>
            {
                string path = list.GetItemMetadata((int)i).AsString();
                EmitSignal(SignalName.TextureSelected, capturedSlot, path);
            };

            _lists[slotName] = list;
            scroll.AddChild(list);
            _tabs.AddChild(scroll);
        }
    }

    /// <summary>扫描目录下所有 .png 文件</summary>
    private static List<string> ScanPngFiles(string dirPath)
    {
        var results = new List<string>();
        var dir = DirAccess.Open(dirPath);
        if (dir == null) return results;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".png"))
            {
                results.Add($"{dirPath}/{fileName}");
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        results.Sort();
        return results;
    }
}
