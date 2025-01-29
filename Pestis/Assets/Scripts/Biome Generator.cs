using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;
public class BiomeGenerator : MonoBehaviour
{
    public BiomeClass[] BiomeClasses; //every type of biome
    public List<BiomeClass> BiomeList; //every instance of each biome
    public Tilemap map;
    private int width;



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
                    Vector3Int randomPosition = new Vector3Int(randomX, randomY, 0);
                    IsometricRuleTile currentTile = (IsometricRuleTile)map.GetTile(randomPosition);
                    // Check if the current tile is of a specific type (e.g., Grass)
                    if (BiomeClasses[0].CompatableTerrainTypes.Contains(currentTile))
                    {
                        map.SetTile(randomPosition, BiomeClasses[0].getRandomBiomeTile());
                        new BiomeClass(BiomeClasses[i], currentTile);
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

    private void RandomWalk(Vector2Int currentPosition, BiomeClass biome)
    {
        HashSet<Vector2Int> floorPositions = new HashSet<Vector2Int>();
        BoundsInt bounds = map.cellBounds;
        // Perform the drunkard's walk
        for (int i = 0; i < biome.walkLength; i++)
        {
            // Mark this position as floor
            floorPositions.Add(currentPosition);

            // Pick a random direction (Up, Down, Left, Right)
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Shuffle(directions);
            int valid = 0;
            int index = 0;
            while (valid == 0 && index < 4)
            {
                Vector2Int newPosition = currentPosition + directions[index];
                IsometricRuleTile newTile = (IsometricRuleTile)map.GetTile(new Vector3Int(newPosition.x, newPosition.y, 0));

                if (biome.TileList.Contains(newTile))
                {
                    valid = 1;
                    currentPosition = newPosition;
                }
                else if (biome.CompatableTerrainTypes.Contains(newTile))
                {
                    valid = 2;
                    currentPosition = newPosition;
                    map.SetTile(new Vector3Int(newPosition.x, newPosition.y, 0), biome.getRandomBiomeTile());
                    biome.addTile(newTile);

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
