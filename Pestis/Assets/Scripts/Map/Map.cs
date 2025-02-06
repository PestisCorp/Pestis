using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Map : ScriptableObject
{
    public Tilemap tilemap;
    public int width = 1024;
    public int height = 1024;
    public TileBase water;
    public LandBiomesList landBiomes;
    public float voronoiFrequency = 0.025f;
    public int randomWalkSteps = 5000000;
    public int smoothing = 10;
}
