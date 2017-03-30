﻿using Assets.General.DataStructures;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent( typeof( GameMap ) )]
public class MapDecorator : MonoBehaviour
{
    private CommandBuffer UnitMovementBuffer;
    private CommandBuffer PointsBuffer;
    private GameMap Map;

    public void Awake()
    {
        UnitMovementBuffer = new CommandBuffer();
        UnitMovementBuffer.name = "Unit Movement Path";
        PointsBuffer = new CommandBuffer();
        PointsBuffer.name = "Points";

        Map = GetComponent<GameMap>();
        Camera.main.AddCommandBuffer( CameraEvent.BeforeImageEffectsOpaque, UnitMovementBuffer );
        Camera.main.AddCommandBuffer( CameraEvent.BeforeImageEffectsOpaque, PointsBuffer );
    }

    public void Start() { }

    public void RenderForPath( IEnumerable<GameTile> path )
    {
        PointsBuffer.Clear();
        foreach ( var tile in path )
        {
            PointsBuffer.DrawMesh( tile.GetComponent<MeshFilter>().mesh,
                tile.transform.localToWorldMatrix,
                Map.SelectionMat, 0 );
        }
    }

    public void ShowUnitMovementIfHere( GameTile tile )
    {
        Unit unitThere;
        Map.UnitGametileMap.TryGetValue( tile, out unitThere );
        if ( unitThere != null )
            ShowUnitMovement( unitThere );
    }

    public void ShowUnitMovement( Unit unit )
    {
        UnitMovementBuffer.Clear();
        if ( unit == null )
            return;

        List<Vector2Int> validMovementTiles =
            Map.GetValidMovementPositions( unit, Map.UnitGametileMap[ unit ] ).ToList();

        foreach ( var tile in validMovementTiles.Select( tile => Map[ tile ] ) )
        {
            UnitMovementBuffer.DrawMesh( tile.GetComponent<MeshFilter>().mesh,
                tile.transform.localToWorldMatrix,
                Map.MovementMat, 0 );
        }

        foreach ( var tile in
            Map.GetFringeAttackTiles(
                new HashSet<Vector2Int>( validMovementTiles ), unit.AttackRange )
            .Select( tile => Map[ tile ] ) )
        {
            UnitMovementBuffer.DrawMesh( tile.GetComponent<MeshFilter>().mesh,
                tile.transform.localToWorldMatrix,
                Map.AttackRangeMat, 0 );
        }
    }
}