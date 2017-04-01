﻿using UnityEngine;
using System;
using System.Collections.Generic;
using Assets.General.DataStructures;
using Assets.General.UnityExtensions;
using System.Linq;
using Assets.General;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

public class GameMap : MonoBehaviour, IEnumerable<GameTile>
{
    public int Width;
    public int Height;

    public GameTile TilePrefab;

    public GameTile[,] GameTiles;

    public Material NormalMat;
    public Material MovementMat;
    public Material AttackRangeMat;
    public Material DefaultMat;
    public Material SelectionMat;

    public DoubleDictionary<Unit, GameTile> UnitGametileMap = new DoubleDictionary<Unit, GameTile>();

    void Awake()
    {
        GameTiles = new GameTile[ Width, Height ];
        foreach ( GameTile tile in GameObject.FindObjectsOfType<GameTile>() )
        {
            this[ tile.Position ] = tile;
        }
    }

    public void AddUnit( Unit unit )
    {
        var unitPosition = unit.transform.localPosition;
        UnitGametileMap.Add( unit,
        this[ ( int )unitPosition.x, ( int )unitPosition.z ] );
    }

    private IEnumerable<int> TrianglesForPosition( Vector2Int pos )
    {
        return TrianglesForPosition( pos.x, pos.y );
    }

    private IEnumerable<int> TrianglesForPosition( int i, int j )
    {
        int indiceFormat = j + i * ( Height + 1 );

        yield return indiceFormat;
        yield return indiceFormat + 1;
        yield return indiceFormat + 1 + Height + 1;

        yield return indiceFormat;
        yield return indiceFormat + 1 + Height + 1;
        yield return indiceFormat + Height + 1;
    }

    /// <summary>
    /// Basic Utility Function that should probably not be here. Works like the python range function
    /// for simple iteration over a list of ints
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="step"></param>
    /// <returns></returns>
    public IEnumerable<int> Range( int start, int end, int step = 1 )
    {
        for ( int i = start ; i <= end ; i += step )
        {
            yield return i;
        }
    }

    public IEnumerable<Vector2Int> GetTilesWithinAbsoluteRange( Vector2Int startingPos, int range )
    {
        IEnumerable<int> rangeInterval = Range( -range, range );
        IEnumerable<int> xInterval = rangeInterval.Select( i => startingPos.x + i ).Where( i => !IsOverBound( i, 0, Width - 1 ) );
        IEnumerable<int> yInterval = rangeInterval.Select( i => startingPos.y + i ).Where( i => !IsOverBound( i, 0, Height - 1 ) );

        foreach ( var i in xInterval )
            foreach ( var j in yInterval )
            {
                Vector2Int toReturn = new Vector2Int( i, j );
                if ( ( startingPos - toReturn ).AbsoluteNormal() <= range )
                    yield return toReturn;
            }
    }

    public IEnumerable<Vector2Int> GetValidMovementPositions( Unit unit, GameTile unitsTile )
    {
        return GetTilesWithinAbsoluteRange( unitsTile.Position, unit.MovementRange )
            .Where( tile => MapSearcher.Search( unitsTile, this[ tile ], this, unit.MovementRange ) != null );
    }

    public HashSet<Vector2Int> GetAttackTiles( HashSet<Vector2Int> movementTiles, int attackRange )
    {
        var temp = GetFringeAttackTiles( movementTiles, attackRange );
        temp.UnionWith( movementTiles );
        return temp;
    }

    public HashSet<Vector2Int> GetFringeAttackTiles( HashSet<Vector2Int> movementTiles, int attackRange )
    {
        HashSet<Vector2Int> attackTiles = new HashSet<Vector2Int>();

        foreach ( Vector2Int tile in movementTiles )
            foreach ( Vector2Int direction in new Vector2Int[] { Vector2Int.Up, Vector2Int.Down, Vector2Int.Left, Vector2Int.Right } )
                foreach ( int coef in Enumerable.Range( 1, attackRange ) )
                {
                    Vector2Int neighbour = tile + direction * coef;
                    if ( IsOutOfBounds( neighbour ) == false && movementTiles.Contains( neighbour ) == false )
                        attackTiles.Add( neighbour );
                }

        return attackTiles;
    }

    private Mesh CreateGridMesh( int width, int height )
    {
        Vector2[] vertices = new Vector2[ ( width + 1 ) * ( height + 1 ) ];
        int[] triangles = new int[ width * height * 6 ];

        for ( int i = 0 ; i < width + 1 ; i++ )
            for ( int j = 0 ; j < height + 1 ; j++ )
            {
                int indiceFormat = j + i * ( height + 1 );
                vertices[ indiceFormat ] = new Vector2( i, j );
            }

        for ( int i = 0 ; i < width ; i++ )
            for ( int j = 0 ; j < height ; j++ )
            {
                int indiceFormat = j + i * ( height + 1 );
                int triIndiceFormat = ( j + ( i * height ) ) * 6;

                //Top Tri
                triangles[ triIndiceFormat ] = indiceFormat;
                triangles[ triIndiceFormat + 1 ] = indiceFormat + 1;
                triangles[ triIndiceFormat + 2 ] = indiceFormat + 1 + height + 1;


                //Lower Tri
                triangles[ triIndiceFormat + 3 ] = indiceFormat;
                triangles[ triIndiceFormat + 4 ] = indiceFormat + 1 + height + 1;
                triangles[ triIndiceFormat + 5 ] = indiceFormat + height + 1;
            }

        var mesh = new Mesh();
        mesh.vertices = vertices.Select( vert => new Vector3( vert.x, 0, vert.y ) ).ToArray<Vector3>();
        mesh.triangles = triangles;
        mesh.normals = vertices.Select( vert => Vector3.up ).ToArray();

        return mesh;
    }

    private static Mesh CreateVertexGrid( List<Vector2Int> positions )
    {
        Vector2[] vertices = new Vector2[ positions.Count * 4 ];
        int[] triangles = new int[ positions.Count * 6 ];

        {
            int i = 0;
            foreach ( Vector2Int pos in positions )
            {
                vertices[ i ] = new Vector2( pos.x, pos.y );
                vertices[ i + 1 ] = new Vector2( pos.x + 1, pos.y );
                vertices[ i + 2 ] = new Vector2( pos.x, pos.y + 1 );
                vertices[ i + 3 ] = new Vector2( pos.x + 1, pos.y + 1 );
                i += 4;
            }
        }

        {
            int i = 0;
            int j = 0;
            for ( int k = 0 ; k < positions.Count ; k++ )
            {
                //Assuming Clockwise orientation
                //Triangle 1
                triangles[ j ] = i;
                triangles[ j + 1 ] = i + 3;
                triangles[ j + 2 ] = i + 1;

                //Triangle 2
                triangles[ j + 3 ] = i;
                triangles[ j + 4 ] = i + 2;
                triangles[ j + 5 ] = i + 3;

                i += 4;
                j += 6;
            }
        }

        var mesh = new Mesh();
        mesh.vertices = vertices.Select( vert => new Vector3( vert.x, 0, vert.y ) ).ToArray<Vector3>();
        mesh.triangles = triangles;

        return mesh;
    }

    public void InitializeMap( int width, int height )
    {
        Width = width;
        Height = height;
        GameTiles = new GameTile[ width, height ];
        GameObject TileLayer = GameObject.Find( "TileLayer" );
        for ( int i = 0 ; i < width ; i++ )
            for ( int j = 0 ; j < height ; j++ )
            {
                var gameTile = Instantiate( TilePrefab, TileLayer.transform, false );
                gameTile.Position.x = i;
                gameTile.Position.y = j;
                this[ gameTile.Position ] = gameTile;
                gameTile.transform.localPosition = gameTile.Position.ToVector3();
                gameTile.name = gameTile.Position.ToString();
            }
    }

    public void ReInitializeMap( int width, int height )
    {
        foreach ( var tile in GameObject.FindObjectsOfType<GameTile>() )
        {
            GameObject.DestroyImmediate( tile.gameObject );
        }
        InitializeMap( width, height );
    }

    public GameTile this[ int x, int y ]
    {
        get { return GameTiles[ x, y ]; }
        set { GameTiles[ x, y ] = value; }
    }

    public GameTile this[ Vector2Int v ]
    {
        get { return GameTiles[ v.x, v.y ]; }
        set { GameTiles[ v.x, v.y ] = value; }
    }

    public bool Occupied( GameTile tile )
    {
        Unit u;
        return UnitGametileMap.TryGetValue( tile, out u );
    }

    public void PlaceUnit( Unit unit, GameTile b )
    {
        if ( Occupied( b ) == false )
            UnitGametileMap.Add( unit, b );
    }

    private static bool IsOverBound( int i, int lowerBound, int upperBound )
    {
        return i < lowerBound || i > upperBound;
    }

    private static T ClampNumber<T>( T i, T lowerBound, T upperBound ) where T : IComparable<T>
    {
        i = i.CompareTo( lowerBound ) < 0 ? lowerBound : i;
        i = i.CompareTo( upperBound ) > 0 ? upperBound : i;
        return i;
    }

    public Vector2Int ClampWithinMap( Vector2Int toClamp )
    {
        toClamp.x = ClampNumber( toClamp.x, 0, Width - 1 );
        toClamp.y = ClampNumber( toClamp.y, 0, Height - 1 );
        return toClamp;
    }

    public Vector3 ClampWithinMap( Vector3 toClamp )
    {
        toClamp.x = ClampNumber( toClamp.x, 0, Width - 1 );
        toClamp.z = ClampNumber( toClamp.z, 0, Height - 1 );
        return toClamp;
    }

    public bool IsOutOfBounds( Vector2Int v )
    {
        return IsOverBound( v.x, 0, Width - 1 ) || IsOverBound( v.y, 0, Height - 1 );
    }

    public IEnumerator<GameTile> GetEnumerator()
    {
        return GameTiles.Cast<GameTile>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GameTiles.GetEnumerator();
    }
}