using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

[CreateAssetMenu]
[Serializable]
public class BiomeClass : ScriptableObject
{
    public BiomeTile[] TileList;
    public GameObject[] FeatureList; //set of features that spawn in this biome

    public TileBase[] CompatableTerrainTypes; //can be altered to just be some preexisting tile
    public int distanceFromNearestSeed = 3;
    //generation stuff
    public int seedCount;
    public int walkLength;
    public int iteration;
    public int strength;

    //in game stuff


    private void Shuffle<T>(T[] array)
    {
        var rng = new Random(); // Use C# Random
        var n = array.Length;
        for (var i = n - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1); // Pick a random index
            (array[i], array[j]) = (array[j], array[i]); // Swap
        }
    }

    public BiomeTile getRandomBiomeTile()
    {
        var index = UnityEngine.Random.Range(0, TileList.Length);
        return TileList[index];
    }


    public GameObject getRandomFeature()
    {
        var index = UnityEngine.Random.Range(0, FeatureList.Length);
        return FeatureList[index];
    }

    public virtual void FeatureGeneration(Tilemap map, BiomeInstance biomeInstance, GameObject parent)
    {
        var numberPoi = (int)Math.Floor(biomeInstance.tilePositions.Count * UnityEngine.Random.Range(0, 0.005f));
        var rnd = new Random();
        var poiTiles = biomeInstance.tilePositions.OrderBy(x => rnd.Next()).Take(numberPoi);
        foreach (var pos in poiTiles)
        {
            var vector3 =
                map.CellToWorld((Vector3Int)pos);
            var feature = Instantiate(getRandomFeature(), vector3, Quaternion.identity);
            feature.transform.parent = parent.transform;
        }
    }

    List<TileBase> TilesInArea(int x, int y, int size, Tilemap map)
    {
        List<TileBase> tiles = new List<TileBase>();
        for (int dx = -size / 2; dx <= size / 2; dx++)
        {
            for (int dy = -size / 2; dy <= size / 2; dy++)
            {
                var checkPosition = new Vector3Int(x + dx, y + dy);
                tiles.Add(map.GetTile(checkPosition));
            }
        }
        return tiles;
    }
    public virtual BiomeInstance sowSeed(Tilemap map)
    {
        var bounds = map.cellBounds;
        int stoptrying = 1000;
        while (true)
        {
            var randomX = UnityEngine.Random.Range(bounds.xMin, bounds.xMax);
            var randomY = UnityEngine.Random.Range(bounds.yMin, bounds.yMax);
            var randomPosition = new Vector3Int(randomX, randomY);
            var currentTile = map.GetTile(randomPosition);
            // Check if the current tile is of a specific type (e.g., Grass)
            if (CompatableTerrainTypes.Contains(currentTile))
            {
                List < TileBase > tiles = TilesInArea(randomX, randomY, distanceFromNearestSeed*2, map);
                if (!tiles.OfType<BiomeTile>().Any())
                {
                    return new BiomeInstance(this, new Vector2Int(randomX, randomY));
                }
            }
        }
    }

    public virtual void
        Growth(Vector2Int currentPosition, Tilemap map, BiomeInstance biomeInstance,
            int walkLength) //generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        var bounds = map.cellBounds;
        // Perform the drunkard's walk
        for (var i = 0; i < walkLength; i++)
        {
            // Pick a random direction (Up, Down, Left, Right)
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            Shuffle(directions);
            var valid = 0;
            var index = 0;
            while (valid == 0 && index < 4)
            {
                var newPosition = currentPosition + directions[index];

                if (biomeInstance.tilePositions.Contains(newPosition))
                {
                    valid = 1;
                    currentPosition = newPosition;
                }
                else if (CompatableTerrainTypes.Contains(map.GetTile(new Vector3Int(newPosition.x, newPosition.y, 0))))
                {
                    valid = 2;
                    currentPosition = newPosition;
                    map.SetTile(new Vector3Int(newPosition.x, newPosition.y, 0), getRandomBiomeTile());
                    biomeInstance.tilePositions.Add(currentPosition);
                }

                index += 1;
            }

            if (valid == 0) i = walkLength;
            // Keep within map bounds
            currentPosition.x = Mathf.Clamp(currentPosition.x, bounds.xMin, bounds.xMax);
            currentPosition.y = Mathf.Clamp(currentPosition.y, bounds.yMin, bounds.yMax);
        }
    }

    public virtual void
        CellGeneration(Tilemap map,
            BiomeInstance biomeInstance) //generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        for (var i = 0; i < iteration; i++) Growth(biomeInstance.seedPosition, map, biomeInstance, walkLength);
    }
}

public class BiomeInstance
{
    public Vector2Int seedPosition;
    public BiomeClass template; // Reference to the template (BiomeClass)

    public List<Vector2Int> tilePositions = new(); // Stores generated tiles for this biome

    public BiomeInstance(BiomeClass template, Vector2Int position)
    {
        this.template = template;
        seedPosition = position;
    }
}