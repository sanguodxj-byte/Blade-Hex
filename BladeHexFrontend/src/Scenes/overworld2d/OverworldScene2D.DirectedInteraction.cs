using Godot;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    private enum DirectedInteractionKind
    {
        None,
        Entity,
        BattleJoin,
        Poi,
    }

    private sealed class PlayerDirectedInteraction
    {
        public DirectedInteractionKind Kind { get; init; }
        public Vector2 TargetPosition { get; init; }
        public OverworldEntity? Entity { get; init; }
        public JoinOpportunity? JoinOpportunity { get; init; }
        public OverworldPOI? Poi { get; init; }
    }

    private PlayerDirectedInteraction? _directedInteraction;

    private void SetDirectedBattleJoin(JoinOpportunity opportunity, Vector2 targetPosition)
    {
        _directedInteraction = new PlayerDirectedInteraction
        {
            Kind = DirectedInteractionKind.BattleJoin,
            JoinOpportunity = opportunity,
            TargetPosition = targetPosition,
        };

        _pendingInteractionEntity = null;
        _pendingBattleJoinOpportunity = opportunity;
        _pendingBattleJoinPosition = targetPosition;
        _targetPoi = null;
        if (_poiController != null)
            _poiController.TargetPOI = null;
    }

    private void SetDirectedEntityInteraction(OverworldEntity entity)
    {
        _directedInteraction = new PlayerDirectedInteraction
        {
            Kind = DirectedInteractionKind.Entity,
            Entity = entity,
            TargetPosition = entity.Position,
        };

        _pendingInteractionEntity = entity;
        _pendingBattleJoinOpportunity = null;
        _targetPoi = null;
        if (_poiController != null)
            _poiController.TargetPOI = null;
    }

    private void SetDirectedPoiInteraction(OverworldPOI poi, Vector2 targetPosition)
    {
        _directedInteraction = new PlayerDirectedInteraction
        {
            Kind = DirectedInteractionKind.Poi,
            Poi = poi,
            TargetPosition = targetPosition,
        };

        _pendingInteractionEntity = null;
        _pendingBattleJoinOpportunity = null;
        _targetPoi = poi;
        if (_poiController != null)
            _poiController.TargetPOI = poi;
    }

    private void ClearDirectedInteraction()
    {
        _directedInteraction = null;
        _pendingInteractionEntity = null;
        _pendingBattleJoinOpportunity = null;
        _targetPoi = null;
        if (_poiController != null)
            _poiController.TargetPOI = null;
    }

    private bool ResolveDirectedInteractionOnArrival()
    {
        return TryResolveDirectedInteraction(forceBattleJoin: true, forcePoi: true);
    }

    private bool TryResolveDirectedInteraction(bool forceBattleJoin = false, bool forcePoi = false)
    {
        if (_directedInteraction == null)
            return false;

        switch (_directedInteraction.Kind)
        {
            case DirectedInteractionKind.BattleJoin:
                return TryResolveDirectedBattleJoin(_directedInteraction, forceBattleJoin);

            case DirectedInteractionKind.Entity:
                return TryResolveDirectedEntity(_directedInteraction);

            case DirectedInteractionKind.Poi:
                return TryResolveDirectedPoi(_directedInteraction, forcePoi);

            default:
                return false;
        }
    }

    private bool TryResolveDirectedBattleJoin(PlayerDirectedInteraction interaction, bool force)
    {
        var opp = interaction.JoinOpportunity;
        if (opp == null)
        {
            ClearDirectedInteraction();
            return false;
        }

        float dist = _playerPixelPos.DistanceTo(interaction.TargetPosition);
        if (!force && dist > BATTLEFIELD_DIST)
            return false;

        ClearDirectedInteraction();
        ShowBattleJoinPrompt(opp, fromClick: false);
        return true;
    }

    private bool TryResolveDirectedEntity(PlayerDirectedInteraction interaction)
    {
        var entity = interaction.Entity;
        if (entity == null || !entity.IsAlive)
        {
            ClearDirectedInteraction();
            return false;
        }

        float dist = _playerPixelPos.DistanceTo(entity.Position);
        if (dist > INTERACT_DIST)
            return false;

        ClearDirectedInteraction();
        if (!IsHostileToCurrentPlayer(entity))
            TriggerFriendlyInteraction(entity);
        else
            TriggerHostileEncounter(entity);

        return true;
    }

    private bool TryResolveDirectedPoi(PlayerDirectedInteraction interaction, bool force)
    {
        var poi = interaction.Poi;
        if (poi == null)
        {
            ClearDirectedInteraction();
            return false;
        }

        if (!force)
            return false;

        if (!IsPlayerInsidePoiFootprint(poi))
            return false;

        _poiEntered = true;
        ClearDirectedInteraction();
        _playerMoving = false;
        IsWaiting = false;
        TriggerPOIInteraction(poi);
        return true;
    }

    private bool IsPlayerInsidePoiFootprint(OverworldPOI poi)
    {
        var tile = _mapAccess.GetActiveTileAtPixel(_playerPixelPos);
        return tile != null && poi.ContainsHex(tile.Coord);
    }
}
