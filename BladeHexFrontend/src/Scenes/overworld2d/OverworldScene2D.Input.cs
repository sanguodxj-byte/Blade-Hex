// OverworldScene2D.Input.cs
// 玩家输入与即时动作 — 从 OverworldScene3D.Input.cs 迁移
using Godot;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 快捷键
    // ========================================

    private void HandleHotkeys(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed) return;

        switch (keyEvent.Keycode)
        {
            case Key.M: // 打开/关闭小地图
                // TODO: 需要 MinimapController.ToggleVisibility()
                break;

            case Key.Tab: // 打开/关闭队伍面板
                // TODO: 需要 OverworldUI.TogglePartyPanel()
                break;

            case Key.J: // 打开/关闭任务日志
                // TODO: 需要 OverworldUI.ToggleQuestLog()
                break;

            case Key.Escape: // 取消当前操作或打开菜单
                if (_encounterActive)
                {
                    // 战斗中不处理
                }
                else if (_poiEntered)
                {
                    CleanupInteraction();
                }
                else
                {
                    // TODO: 需要 OverworldUI.ToggleGameMenu()
                }
                break;

            case Key.F5: // 快速存档
                QuickSave();
                break;

            case Key.F9: // 快速读档
                QuickLoad();
                break;
        }
    }

    // ========================================
    // 右键 Toast：实体 / POI / 地形
    // ========================================

    private BladeHex.View.UI.Overworld.ToastNotification? _toast;

    private void InitToast()
    {
        _toast = new BladeHex.View.UI.Overworld.ToastNotification();
        _toast.Name = "ToastNotification";
        AddChild(_toast);
    }

    private void ShowInfoTooltip(Vector2 pixelPos)
    {
        // 获取鼠标位置的 hex 坐标
        var axial = HexOverworldTile.PixelToAxial(pixelPos.X, pixelPos.Y);

        // 获取 tile 数据
        HexOverworldTile? tile = _mapAccess.GetActiveTile(axial.X, axial.Y);

        if (tile == null) return;

        // 构建提示信息
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"坐标: ({axial.X}, {axial.Y})");
        sb.AppendLine($"地形: {HexOverworldTile.TerrainToString(tile.Terrain)}");

        // 检查是否有 POI
        var poi = FindPOIAtPosition(pixelPos);
        if (poi != null)
        {
            sb.AppendLine($"POI: {poi.PoiName} ({poi.PoiTypeEnum})");
            if (!string.IsNullOrEmpty(poi.OwningFaction))
                sb.AppendLine($"所属: {poi.OwningFaction}");
        }

        // 检查是否有实体
        var entity = FindEntityAtPosition(pixelPos);
        if (entity != null)
        {
            sb.AppendLine($"实体: {entity.EntityName}");
            sb.AppendLine($"状态: {(entity.IsHostileToPlayer ? "敌对" : "友好")}");
        }

        // 显示 Toast
        _toast?.Show(sb.ToString(), new Color(0.9f, 0.85f, 0.7f));
    }

    /// <summary>查找指定位置的 POI</summary>
    private OverworldPOI? FindPOIAtPosition(Vector2 pixelPos)
    {
        float checkRadius = HexOverworldTile.HexSize * 2.0f;

        foreach (var poi in WorldPois)
        {
            float dist = (poi.Position - pixelPos).Length();
            if (dist < checkRadius)
                return poi;
        }

        return null;
    }

    /// <summary>查找指定位置的实体</summary>
    private OverworldEntity? FindEntityAtPosition(Vector2 pixelPos)
    {
        if (EntityMgr == null) return null;

        float checkRadius = HexOverworldTile.HexSize * 1.5f;

        foreach (var entity in EntityMgr.Entities)
        {
            float dist = (entity.Position - pixelPos).Length();
            if (dist < checkRadius)
                return entity;
        }

        return null;
    }

    // ========================================
    // 快速存档/读档
    // ========================================

    private void QuickSave()
    {
        var gs = BladeHex.Data.Globals.StateOrNull;
        if (gs == null) return;

        string saveId = $"quicksave_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        gs.Save.CurrentSaveId = saveId;

        SaveWorldData();
        _toast?.Show($"快速存档: {saveId}", new Color(0.6f, 0.9f, 0.6f));
    }

    private void QuickLoad()
    {
        var gs = BladeHex.Data.Globals.StateOrNull;
        if (gs == null) return;

        // TODO: 需要 ChunkPersistence.ListSaves() 方法
        _toast?.Show("快速读档功能待实现", new Color(0.9f, 0.6f, 0.6f));
    }
}
