using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeTile : IsometricRuleTile
{
    public int foodLevel, dangerLevel, climate = 0;
    public Terrain[] CompatableTerrainTypes; //every type of terrain that this biome can grow on
    public GameObject[] FeatureList; //set of features that spawn in this biome
    List<IsometricRuleTile> tiles;
    public virtual void FeatureGeneration()
    {
        // Empty method
    }
}
