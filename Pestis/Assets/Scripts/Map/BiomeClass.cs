using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using Random = System.Random;
using Map;

[CreateAssetMenu]
[Serializable]
public class BiomeClass : ScriptableObject
{
    public BiomeTile[] TileList;
    public float[] Weights;
    public GameObject[] FeatureList; //set of features that spawn in this biome

    public GameObject cityPrefab; // The City POI
    public TileBase[] CompatableTerrainTypes; //can be altered to just be some preexisting tile
    public BiomeTile[] CompatableBiomeTiles; //can be altered to just be some preexisting tile
    public int distanceFromNearestSeed = 20;
    //generation stuff
    public int seedCount;
    public int walkLength;
    public int iteration;
    public float strength;

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
        while (Weights[index] < UnityEngine.Random.Range(0f, 1f)) index = UnityEngine.Random.Range(0, TileList.Length);
        return TileList[index];
    }


    public GameObject getRandomFeature()
    {
        var index = UnityEngine.Random.Range(0, FeatureList.Length);
        return FeatureList[index];
    }

    public virtual void FeatureGeneration(Tilemap map, BiomeInstance biomeInstance, GameObject parent)
    {
        // Convert tile list to a random order.
        System.Random rng = new System.Random();
        List<Vector3Int> shuffledTiles = biomeInstance.tilePositions.OrderBy(t => rng.Next()).ToList();

        // We'll place exactly one city, plus circle POIs around it.
        int totalTiles = shuffledTiles.Count;

        // Decide how many circle POIs to place around the city
        int circlePoiCount = 7; // e.g. 6 other POIs
        float avgRadius = 18f; // typical distance from the city
        float radiusVariation = 7f; // how much the radius can vary
        float minPoiDistance = 3f; // distance required between POIs

        // Local list for placed POI positions in this biome
        List<Vector3> placedPoiPositions = new List<Vector3>();

        // 1: Place exactly one city in this biome
        if (shuffledTiles.Count > 0)
        {
            Vector3 cityWorldPos = Vector3.zero;
            bool placedCity = false;
            float minCityDistance = 15.0f; // required distance from any other city (global check)

            foreach (var tilePos in shuffledTiles)
            {
                cityWorldPos = map.CellToWorld((Vector3Int)tilePos);

                // Check distance to previously placed cities (Generator.cityPositions)
                bool tooCloseToCity = false;
                foreach (var existingCityPos in Generator.cityPositions)
                {
                    if (Vector3.Distance(cityWorldPos, existingCityPos) < minCityDistance)
                    {
                        tooCloseToCity = true;
                        break;
                    }
                }

                if (!tooCloseToCity)
                {
                    // Place the city
                    GameObject cityObj = Instantiate(cityPrefab, cityWorldPos, Quaternion.identity, parent.transform);
                    Generator.cityPositions.Add(cityWorldPos);

                    placedPoiPositions.Add(cityWorldPos);
                    Debug.Log($"City placed at {cityWorldPos}");

                    placedCity = true;
                    break;
                }
            }

            if (!placedCity)
            {
                Debug.LogWarning("Could not place city in this biome due to minCityDistance constraints.");
                //return if each biome must have exactly one city
                return;
            }

            //2: Place other POIs in a circle around the city
            PlacePoisInCircle(cityWorldPos, circlePoiCount, avgRadius, radiusVariation, minPoiDistance, FeatureList, map,
                parent.transform);
        }
    }



    /// <summary>
    /// Spawns a number of POIs around a city, with random offsets in angle & radius,
    /// giving a more natural placement.
    /// </summary>
    internal void PlacePoisInCircle(
        Vector3 cityPos,
        int numberPois,
        float averageRadius, // The typical distance from the city
        float radiusSpread, // How much the radius can vary
        float minDistance, // Minimum distance between POIs
        GameObject[] poiPrefabs, Tilemap map,
        Transform parent
    )
    {
        // Keep track of all placed POI positions so they don’t overlap
        List<Vector3> placedPositions = new List<Vector3>();

        // We'll distribute angles roughly evenly, but allow random offsets
        float angleStep = 360f / numberPois;

        for (int i = 0; i < numberPois; i++)
        {
            // Try up to 10 times to find a spot that isn't too close
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // Base angle is i * angleStep, but add random offset within ±half step
                float angle = (angleStep * i) + UnityEngine.Random.Range(-angleStep / 2f, angleStep / 2f);
                float radians = angle * Mathf.Deg2Rad;

                // Radius is average ± some random offset
                float r = averageRadius + UnityEngine.Random.Range(-radiusSpread, radiusSpread);

                // Compute the final spawn position
                float offsetX = Mathf.Cos(radians) * r;
                float offsetY = Mathf.Sin(radians) * r;
                Vector3 spawnPos = cityPos + new Vector3(offsetX, offsetY, 0);

                // Check if this spawnPos is too close to any already placed POI
                bool tooClose = false;

                Vector3Int cellPosition = map.WorldToCell(spawnPos); // Convert world to tilemap cell position
                if (!map.HasTile(cellPosition))
                {
                    tooClose = true;
                }
                else
                {
                    foreach (var existingPos in placedPositions)
                    {
                        if (Vector3.Distance(spawnPos, existingPos) < minDistance)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }
                // If not too close, place the POI here
                if (!tooClose)
                {
                    GameObject selectedPOI = poiPrefabs[UnityEngine.Random.Range(0, poiPrefabs.Length)];
                    Instantiate(selectedPOI, spawnPos, Quaternion.identity, parent);
                    placedPositions.Add(spawnPos);
                    break; // success, move on to next i
                }
            }
        }
    }

    internal List<TileBase> TilesInArea(int x, int y, int size, Tilemap map)
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
                    return new BiomeInstance(this, new Vector3Int(randomX, randomY));
                }
            }
        }
    }

    public virtual void
        drunkyardGrowth(Vector3Int currentPosition, Tilemap map, BiomeInstance biomeInstance,
            int walkLength) 
    {
        var bounds = map.cellBounds;
        // Perform the drunkard's walk
        for (var i = 0; i < walkLength; i++)
        {
            // Pick a random direction (Up, Down, Left, Right)
            Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
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
                    biomeInstance.setTile(map, new Vector3Int(newPosition.x, newPosition.y, 0), getRandomBiomeTile());
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
        for (var i = 0; i < iteration; i++) drunkyardGrowth(biomeInstance.seedPosition, map, biomeInstance, walkLength);
    }

    public virtual void callAutomata(BiomeInstance biome, Tilemap map, List<BiomeInstance> biomeInstances)
    {
        foreach (Vector3Int tilepos in biome.tilePositions)
        {
            BiomeTile tile = (BiomeTile)map.GetTile(tilepos);
            tile.automata(map, tilepos, biomeInstances);
        }
    }
}


public class BiomeInstance
{
    public Vector3Int seedPosition;
    public BiomeClass template; // Reference to the template (BiomeClass)

    public List<Vector3Int> tilePositions = new(); // Stores generated tiles for this biome


    public void setTile(Tilemap map, Vector3Int position, BiomeTile tile)
    {
        map.SetTile(position, tile);
        tilePositions.Add(position);
    }

    public void swapTile(Tilemap map, Vector3Int position, BiomeTile tile, BiomeInstance newbiome) //removes tile and gives it to new biome instance
    {
        tilePositions.Remove(position);
        newbiome.setTile(map, position, tile);
    }
    public BiomeInstance(BiomeClass template, Vector3Int position)
    {
        this.template = template;
        seedPosition = position;
    }
}