using UnityEngine;
using System.Collections.Generic;

public class BiomeClass : MonoBehaviour
{
    public int foodLevel = 0;
    public int dangerLevel = 0;
    public int climate = 0;
    public List<Terrain> CompatableTerrainTypes; //every type of terrain that this biome can grow on
    public List<Terrain> VoronoiCells; //every voronoi cell that is part of this biome, alternatively could also be every tile part of this biome
    public List<Object> FeatureList; //set of features that spawn in this biome
    public virtual void FeatureGeneration()
    {
        // Empty method
    }

    public virtual void Growth()//generate biomes by seeding 1 biome and then repeatedly "growing" it
    {
        // Empty method
    }
}
