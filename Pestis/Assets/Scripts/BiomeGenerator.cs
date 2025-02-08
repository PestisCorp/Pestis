using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class BiomeGenerator : MonoBehaviour
{
    public BiomeClass[] BiomeClasses; //every type of biome
    public List<BiomeInstance> BiomeList = new List<BiomeInstance>(); //every instance of each biome
    public Tilemap map;
    private int width;

    private void Awake()
    {
        this.sowSeed();
        foreach (var biome in BiomeList)
        {
            biome.template.CellGeneration(map, biome);
        }
    }

    private void sowSeed()
    {
        BoundsInt bounds = map.cellBounds;

        for (int i = 0; i < BiomeClasses.Length; i++)
        {
            for (int j = 0; j < BiomeClasses[i].seedCount; j++)
            {
                BiomeList.Add(BiomeClasses[i].sowSeed(map));
            }
        }

    }

}

