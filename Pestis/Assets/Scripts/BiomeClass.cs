using System.Collections.Generic;
using System.Linq;
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


    void Shuffle<T>(T[] array)
    {
        System.Random rng = new System.Random(); // Use C# Random
        int n = array.Length;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1); // Pick a random index
            (array[i], array[j]) = (array[j], array[i]); // Swap
        }
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

    public virtual void FeatureGeneration(Tilemap map)
    {
        // Empty method
    }


    public virtual BiomeInstance sowSeed(Tilemap map)
    {
        BoundsInt bounds = map.cellBounds;
        bool success = false;
        while (true)
        {
            int randomX = Random.Range(bounds.xMin, bounds.xMax);
            int randomY = Random.Range(bounds.yMin, bounds.yMax);
            Vector3Int randomPosition = new Vector3Int(randomX, randomY);
            TileBase currentTile = (TileBase)map.GetTile(randomPosition);
            // Check if the current tile is of a specific type (e.g., Grass)
            if (CompatableTerrainTypes.Contains(currentTile))
            {
                map.SetTile(randomPosition, getRandomBiomeTile());
                return (new BiomeInstance(this, new Vector2Int(randomX, randomY)));
            }
        }
    }
    public virtual void Growth(Vector2Int currentPosition, Tilemap map, BiomeInstance biomeInstance, int walkLength)//generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
         BoundsInt bounds = map.cellBounds;
        // Perform the drunkard's walk
        for (int i = 0; i < walkLength; i++)
        {

            // Pick a random direction (Up, Down, Left, Right)
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Shuffle(directions);
            int valid = 0;
            int index = 0;
            while (valid == 0 && index < 4)
            {
                Vector2Int newPosition = currentPosition + directions[index];

                if (biomeInstance.tilePositions.Contains(newPosition))
                {
                    valid = 1;
                    currentPosition = newPosition;
                }
                else if (CompatableTerrainTypes.Contains((TileBase)map.GetTile(new Vector3Int(newPosition.x, newPosition.y, 0))))
                {
                    valid = 2;
                    currentPosition = newPosition;
                    map.SetTile(new Vector3Int(newPosition.x, newPosition.y, 0), getRandomBiomeTile());
                    biomeInstance.tilePositions.Add(currentPosition);

                }
                index += 1;
            }
            if (valid == 0)
            {
                i = walkLength;
            }
            // Keep within map bounds
            currentPosition.x = Mathf.Clamp(currentPosition.x, bounds.xMin, bounds.xMax);
            currentPosition.y = Mathf.Clamp(currentPosition.y, bounds.yMin, bounds.yMax);
        }
    }

    public virtual void CellGeneration(Tilemap map, BiomeInstance biomeInstance)//generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        for (int i = 0; i < iteration; i++)
        {
            Growth(biomeInstance.seedPosition, map, biomeInstance, walkLength);
        }
    }

}
public class BiomeInstance
{
    public BiomeClass template;  // Reference to the template (BiomeClass)
    public Vector2Int seedPosition;

    public List<Vector2Int> tilePositions = new List<Vector2Int>(); // Stores generated tiles for this biome

    public BiomeInstance(BiomeClass template, Vector2Int position)
    {
        this.template = template;
        this.seedPosition = position;
    }

}

