using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "Map", menuName = "Scriptable Objects/Map")]
[FilePath("Map/SavedMap.foo", FilePathAttribute.Location.ProjectFolder)]
public class Map : ScriptableSingleton<Map>
{
    public Tilemap tilemap;
    public int width = 1024;
    public int height = 1024;
    public TileBase water;
    public List<TileBase> landBiomes;
    public float voronoiFrequency = 0.025f;
    public int randomWalkSteps = 5000000;
    public int smoothing = 10;
}
