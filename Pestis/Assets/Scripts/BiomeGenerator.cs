using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class BiomeGenerator : MonoBehaviour
{
    public BiomeClass[] BiomeClasses; //every type of biome
    public List<BiomeInstance> BiomeList; //every instance of each biome
    public Tilemap map;
    private int width;

    private void Awake()
    {
        sowSeed();
        foreach (var biome in BiomeList)
        {
            for (int i = 0; i < biome.iteration; i++)
            {
                RandomWalk(biome.seedPosition, biome);
            }
        }
    }

    private void sowSeed()
    {
        BoundsInt bounds = map.cellBounds;

        for (int i = 0; i < BiomeClasses.Length; i++)
        {
            for (int j = 0; j < BiomeClasses[i].seedCount; j++)
            {
                bool success = false;
                while (success == false)
                {
                    int randomX = Random.Range(bounds.xMin, bounds.xMax);
                    int randomY = Random.Range(bounds.yMin, bounds.yMax);
                    Vector3Int randomPosition = new Vector3Int(randomX, randomY);
                    TileBase currentTile = (TileBase)map.GetTile(randomPosition);
                    // Check if the current tile is of a specific type (e.g., Grass)
                    if (BiomeClasses[0].CompatableTerrainTypes.Contains(currentTile))
                    {
                        map.SetTile(randomPosition, BiomeClasses[i].getRandomBiomeTile());
                        BiomeList.Add(new BiomeInstance(BiomeClasses[i], new Vector2Int(randomX, randomY)));
                        success = true;
                    }
                }
            }
        }

    }
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

    private void RandomWalk(Vector2Int currentPosition, BiomeInstance biome)
    {
        BoundsInt bounds = map.cellBounds;
        // Perform the drunkard's walk
        for (int i = 0; i < biome.walkLength; i++)
        {

            // Pick a random direction (Up, Down, Left, Right)
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Shuffle(directions);
            int valid = 0;
            int index = 0;
            while (valid == 0 && index < 4)
            {
                Vector2Int newPosition = currentPosition + directions[index];

                if (biome.tilePositions.Contains(newPosition))
                {
                    valid = 1;
                    currentPosition = newPosition;
                }
                else if (biome.template.CompatableTerrainTypes.Contains((TileBase)map.GetTile(new Vector3Int(newPosition.x, newPosition.y, 0))))
                {
                    valid = 2;
                    currentPosition = newPosition;
                    map.SetTile(new Vector3Int(newPosition.x, newPosition.y, 0), biome.template.getRandomBiomeTile());
                    biome.tilePositions.Add(currentPosition);

                }
                index += 1;
            }
            if (valid == 0)
            {
                i = biome.walkLength;
            }
            // Keep within map bounds
            currentPosition.x = Mathf.Clamp(currentPosition.x, bounds.xMin, bounds.xMax);
            currentPosition.y = Mathf.Clamp(currentPosition.y, bounds.yMin, bounds.yMax);
        }
    }
}

