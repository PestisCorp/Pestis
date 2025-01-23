using System;
using UnityEngine;
using ProceduralToolkit;
using ProceduralToolkit.FastNoiseLib;
using UnityEngine.Tilemaps;

public class MapGenerator : MonoBehaviour
{
    public Tilemap tilemap;
    [Range(2, 1024)]
    public int width;
    [Range(2, 1024)]
    public int height;
    public RuleTile biome1Tile;
    public RuleTile biome2Tile;

    private FastNoise _noiseGenerator;

    private void Awake()
    {
        _noiseGenerator = new FastNoise();
        _noiseGenerator.SetNoiseType(FastNoise.NoiseType.Cellular);
        _noiseGenerator.SetFrequency(0.03f);
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float noiseVal = _noiseGenerator.GetNoise01(x, y);
                RuleTile temp;
                if (noiseVal < 0.5f)
                {
                    temp = biome1Tile;
                }
                else
                {
                    temp = biome2Tile;
                }
                tilemap.SetTile(new Vector3Int(x, y), temp);
            }
        }
    }
}