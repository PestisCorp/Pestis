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

}
