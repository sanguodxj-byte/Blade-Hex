// OverworldScene2D.Navigation.cs
// 导航系统 — 玩家点击移动统一走 OverworldParty/OverworldNavigationAccess。
// NavigationServer2D 代码保留为实验路径，但不再用旧 HexOverworldGrid 生成主流程 NavMesh。
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.View.Map;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 导航字段
    // ========================================

    private bool _cameraFollowing = false;

    /// <summary>当前路径（像素坐标列表）</summary>
    private List<Vector2>? _currentPath;

    /// <summary>当前路径索引</summary>
    private int _pathIndex = 0;

    /// <summary>路径预览线</summary>
    private Line2D? _pathPreviewLine;

    /// <summary>最后移动方向（用于玩家朝向）</summary>
    private Vector2 _lastMoveDirection = Vector2.Right;

    // ========================================
    // 寻路 + 移动
    // ========================================

    /// <summary>开始寻路到目标像素位置</summary>
    private void StartPathfinding(Vector2 targetPixel)
    {
        if (PlayerParty == null)
            return;

        PlayerParty.MoveTo(targetPixel);
        if (!PlayerParty.IsMoving || PlayerParty.Path.Count == 0)
        {
            ClearDirectedInteraction();
            GD.Print($"[Nav] 无法找到路径: from={_playerPixelPos} to={targetPixel}");
            _toast?.Show("无法到达该位置", new Color(0.9f, 0.3f, 0.3f));
            return;
        }

        _currentPath = new List<Vector2>(PlayerParty.Path);
        _pathIndex = 0;
        _playerMoving = true;
        IsTimePaused = false;
        _cameraFollowing = true;
        ShowPathPreview(_currentPath);

        GD.Print($"[Nav] StartPathfinding (OverworldNavigationAccess): {_playerPixelPos} → {targetPixel}, path points={_currentPath.Count}");
    }

    /// <summary>每帧更新玩家移动</summary>
    private void UpdatePlayerMovement(float dt)
    {
        if (!_playerMoving || PlayerParty == null)
            return;

        // 追击移动实体动态更新终点
        if (_directedInteraction != null && _directedInteraction.Kind == DirectedInteractionKind.Entity)
        {
            var targetEntity = _directedInteraction.Entity;
            if (targetEntity != null && targetEntity.IsAlive)
            {
                float distanceToLastTarget = targetEntity.Position.DistanceTo(_directedInteraction.TargetPosition);
                if (distanceToLastTarget > 20.0f)
                {
                    _directedInteraction = new PlayerDirectedInteraction
                    {
                        Kind = DirectedInteractionKind.Entity,
                        Entity = targetEntity,
                        TargetPosition = targetEntity.Position
                    };
                    _pendingInteractionEntity = targetEntity;

                    PlayerParty.MoveTo(targetEntity.Position);
                    if (PlayerParty.IsMoving && PlayerParty.Path.Count > 0)
                    {
                        _currentPath = new List<Vector2>(PlayerParty.Path);
                        _pathIndex = 0;
                        ShowPathPreview(_currentPath);
                    }
                }
            }
        }

        // 从 NavigationAgent2D 获取位置
        _playerPixelPos = PlayerParty.Position;

        // 检查导航是否完成
        if (!PlayerParty.IsMoving)
        {
            // 导航完成
            _playerMoving = false;
            _cameraFollowing = false;
            ClearPathPreview();
            if (!_encounterActive && ResolveDirectedInteractionOnArrival())
                return;

            IsTimePaused = true; // 寻路到达目标地点时暂停游戏
            return;
        }

        // 更新路径预览（移除已经过的路径点）
        if (_currentPath != null && _pathIndex < _currentPath.Count)
        {
            while (_pathIndex < _currentPath.Count - 1)
            {
                float distToNext = _playerPixelPos.DistanceTo(_currentPath[_pathIndex]);
                if (distToNext < 50.0f) // 50px 阈值
                    _pathIndex++;
                else
                    break;
            }
            
            // 更新路径预览线
            UpdatePathPreview(_currentPath, _pathIndex);
        }

        // 摄像头跟随
        if (_cameraFollowing && _camera != null)
            _camera.FocusOn(_playerPixelPos);
    }

    /// <summary>更新路径预览线（移除已经过的部分）</summary>
    private void UpdatePathPreview(List<Vector2> path, int currentIndex)
    {
        if (_pathPreviewLine == null || !IsInstanceValid(_pathPreviewLine))
            return;

        if (currentIndex >= path.Count)
        {
            ClearPathPreview();
            return;
        }

        // 更新预览线的点（从当前位置开始）
        var remainingPoints = new List<Vector2> { _playerPixelPos };
        for (int i = currentIndex; i < path.Count; i++)
        {
            remainingPoints.Add(path[i]);
        }
        
        _pathPreviewLine.Points = remainingPoints.ToArray();
    }

    // ========================================
    // 路径预览
    // ========================================

    /// <summary>显示路径预览线（黄色）</summary>
    private void ShowPathPreview(List<Vector2> path)
    {
        ClearPathPreview();

        if (path.Count < 2) return;

        _pathPreviewLine = new Line2D();
        _pathPreviewLine.Name = "PathPreview";
        _pathPreviewLine.Width = 3.0f;
        _pathPreviewLine.DefaultColor = new Color(1.0f, 0.85f, 0.3f, 0.8f); // 黄色
        _pathPreviewLine.Points = path.ToArray();
        _pathPreviewLine.ZIndex = 100; // 显示在最上层
        _pathPreviewLine.JointMode = Line2D.LineJointMode.Round;
        _pathPreviewLine.BeginCapMode = Line2D.LineCapMode.Round;
        _pathPreviewLine.EndCapMode = Line2D.LineCapMode.Round;
        
        AddChild(_pathPreviewLine);
    }

    /// <summary>清除路径预览</summary>
    private void ClearPathPreview()
    {
        if (_pathPreviewLine != null && IsInstanceValid(_pathPreviewLine))
        {
            _pathPreviewLine.QueueFree();
            _pathPreviewLine = null;
        }
    }

    // ========================================
    // 辅助方法
    // ========================================

    /// <summary>检查像素位置是否可通行</summary>
    private bool IsPixelPassable(Vector2 pixelPos)
    {
        var coord = HexOverworldTile.PixelToAxial(pixelPos.X, pixelPos.Y);
        HexOverworldTile? tile = _mapAccess.GetActiveTile(coord.X, coord.Y);
        return tile != null && tile.IsPassable;
    }

    /// <summary>获取地形通行代价</summary>
    private float GetTerrainTravelCost(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            HexOverworldTile.TerrainType.Road => 0.5f,
            HexOverworldTile.TerrainType.Plains => 1.0f,
            HexOverworldTile.TerrainType.Grassland => 1.0f,
            HexOverworldTile.TerrainType.Savanna => 1.2f,
            HexOverworldTile.TerrainType.Sand => 1.5f,
            HexOverworldTile.TerrainType.Forest => 1.5f,
            HexOverworldTile.TerrainType.Taiga => 1.5f,
            HexOverworldTile.TerrainType.DenseForest => 2.0f,
            HexOverworldTile.TerrainType.Jungle => 2.0f,
            HexOverworldTile.TerrainType.Hills => 2.0f,
            HexOverworldTile.TerrainType.Rocky => 2.5f,
            HexOverworldTile.TerrainType.Swamp => 2.5f,
            HexOverworldTile.TerrainType.Bog => 2.5f,
            HexOverworldTile.TerrainType.Snow => 2.0f,
            HexOverworldTile.TerrainType.Ice => 2.5f,
            HexOverworldTile.TerrainType.Wasteland => 1.8f,
            _ => 1.0f,
        };
    }

    // ========================================
    // NavigationServer2D 初始化
    // ========================================

    /// <summary>
    /// 初始化大地图导航入口。
    /// 玩家移动由 OverworldParty/OverworldNavigationAccess 负责。
    /// </summary>
    private void InitNavigation()
    {
        GD.Print("[Navigation] NavigationServer2D 主流程已停用；玩家移动使用 OverworldNavigationAccess");
    }

    /// <summary>
    /// 每帧更新 NavMesh 分批生成
    /// </summary>
    private void UpdateNavMeshGeneration()
    {
        // No-op: old HexOverworldGrid-based NavMesh generation is intentionally disabled.
    }
}
