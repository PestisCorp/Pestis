using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeClass : ScriptableObject
{
    public BiomeTile[] TileList;
    public GameObject[] FeatureList; //set of features that spawn in this biome
    public TileBase[] CompatableTerrainTypes; //can be altered to just be some preexisting tile
    //generation stuff
    public int seedCount = 0;
    public int walkLength = 0;
    public int iteration = 0;
    public int strength = 0;
    //in game stuff


    public BiomeTile getRandomBiomeTile()
    {
        int index = Random.Range(0, TileList.Length);
        return TileList[index];
    }

    public GameObject getRandomFeature()
    {
        int index = Random.Range(0, FeatureList.Length);
        return FeatureList[index];
    }

    public virtual void FeatureGeneration(Tilemap map)
    {
        // Empty method
    }

    public virtual void Growth()//generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        // Empty method
    }

    public virtual void CellGeneration(Tilemap map)//generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        // Empty method
    }
}
public class BiomeInstance
{
    public BiomeClass template;  // Reference to the template (BiomeClass)
    public Vector2Int seedPosition;
    public int seedCount;
    public int walkLength;
    public int iteration;
    public int strength;

    public List<Vector2Int> tilePositions = new List<Vector2Int>(); // Stores generated tiles for this biome

    public BiomeInstance(BiomeClass template, Vector2Int position)
    {
        this.template = template;
        this.seedPosition = position;
        this.seedCount = template.seedCount;
        this.walkLength = template.walkLength;
        this.iteration = template.iteration;
        this.strength = template.strength;
    }

}

