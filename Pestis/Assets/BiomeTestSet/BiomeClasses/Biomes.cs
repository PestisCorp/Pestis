using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Tundra:BiomeClass 
{
    public BiomeInstance sowSeed(Tilemap map)
    {
        var bounds = map.cellBounds;
        var success = false;
        while (true)
        {

            bool topOrBottom = UnityEngine.Random.value > 0.5f; // Randomly pick top or bottom
            int randomY;
            if (topOrBottom)
            {// Top quarter of the map
                randomY = UnityEngine.Random.Range(bounds.yMax - (bounds.size.y / 4), bounds.yMax);
            }
            else
            {   // Bottom quarter of the map
                randomY = UnityEngine.Random.Range(bounds.yMin, bounds.yMin + (bounds.size.y / 4));
            }
            var randomX = UnityEngine.Random.Range(bounds.xMin, bounds.xMax);
            var randomPosition = new Vector3Int(randomX, randomY);
            var currentTile = map.GetTile(randomPosition);
            // Check if the current tile is of a specific type (e.g., Grass)
            if (CompatableTerrainTypes.Contains(currentTile))
            {
                List<TileBase> tiles = TilesInArea(randomX, randomY, distanceFromNearestSeed * 2, map);
                if (!tiles.OfType<BiomeTile>().Any())
                {
                    return new BiomeInstance(this, new Vector2Int(randomX, randomY));
                }
            }
        }
    }
}
