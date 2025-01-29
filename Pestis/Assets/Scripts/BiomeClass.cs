using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeClass : ScriptableObject
{
    public BiomeTile[] TileList;
    public GameObject[] FeatureList; //set of features that spawn in this biome
    public IsometricRuleTile[] CompatableTerrainTypes; //can be altered to just be some preexisting tile
    //generation stuff
    public int seedCount = 0;
    public int walkLength = 0;
    public int iteration = 0;
    public int strength = 0;
    //in game stuff
    IsometricRuleTile seedTile;
    List<IsometricRuleTile> tiles;
    public BiomeClass(BiomeClass other, IsometricRuleTile seedTile)
    {
        this.tiles = new List<IsometricRuleTile> { seedTile };
        this.seedTile = seedTile;
    
    }
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

    public void addTile(IsometricRuleTile tile)
    {
        tiles.Add(tile);
    }
    public virtual void FeatureGeneration()
    {
        // Empty method
    }

    public virtual void Growth()//generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        // Empty method
    }

    public virtual void CellGeneration()//generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        // Empty method
    }
}
