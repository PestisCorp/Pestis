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
    public GameObject[] FeatureList; //set of features that spawn in this biome

    public GameObject cityPrefab; // The City POI


    public TileBase[] CompatableTerrainTypes; //can be altered to just be some preexisting tile

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
        // Convert our tile list to a random order.
        System.Random rng = new System.Random();
        List<Vector2Int> shuffledTiles = biomeInstance.tilePositions.OrderBy(t => rng.Next()).ToList();

        // We'll place exactly one city, plus some other POIs in a circle.
        int totalTiles = shuffledTiles.Count;

        // We can also do some random number of circle POIs if you like:
        int circlePoiCount = 4; // how many POIs in a circle
        float circleRadius = 10f; // how far around the city

        // A local list for POIs in this biome
        List<Vector3> placedPoiPositions = new List<Vector3>();

        // STEP 1: Place exactly one city in this biome
        if (shuffledTiles.Count > 0)
        {
            Vector3 cityWorldPos = Vector3.zero;
            bool placedCity = false;
            float minCityDistance = 30.0f; // distance required between new city and all other cities (global check)

            foreach (var tilePos in shuffledTiles)
            {
                cityWorldPos = map.CellToWorld((Vector3Int)tilePos);

                // Check distance to all previously placed cities
                bool tooCloseToCity = false;
                foreach (var cPos in Generator.cityPositions) // e.g., a static list in your MapGenerator
                {
                    if (Vector3.Distance(cityWorldPos, cPos) < minCityDistance)
                    {
                        tooCloseToCity = true;
                        break;
                    }
                }

                if (!tooCloseToCity)
                {
                    // Place city here
                    GameObject cityObj = Instantiate(cityPrefab, cityWorldPos, Quaternion.identity, parent.transform);
                    Debug.Log($"City placed at {cityWorldPos}");

                    // Add this city position to the global cityPositions so future biomes won't overlap
                    Generator.cityPositions.Add(cityWorldPos);

                    // Also track it locally, so if you do further random POI checks, you skip the city spot
                    placedPoiPositions.Add(cityWorldPos);

                    placedCity = true;
                    break; // done placing city
                }
            }

            // If no tile is found (all too close), skip city in this biome
            if (!placedCity)
            {
                Debug.LogWarning("Could not place city in this biome due to minCityDistance constraints.");
                return; // exit early or keep going without city
            }

            // STEP 2: Place other POIs in a circle around the city
            PlacePoisInCircle(cityWorldPos, circlePoiCount, circleRadius, FeatureList, parent.transform);
        }
    }

    private void PlacePoisInCircle(
        Vector3 cityPos,
        int numberPois,
        float radius,
        GameObject[] poiPrefabs,
        Transform parent)
    {
        // Evenly space POIs by angle
        float angleIncrement = 360f / numberPois;

        for (int i = 0; i < numberPois; i++)
        {
            float angle = angleIncrement * i;
            float radians = angle * Mathf.Deg2Rad;

            // Compute offset from cityPos
            float offsetX = Mathf.Cos(radians) * radius;
            float offsetY = Mathf.Sin(radians) * radius;

            Vector3 spawnPos = new Vector3(cityPos.x + offsetX, cityPos.y + offsetY, cityPos.z);

            // Randomly pick a prefab from your "other" POIs
            GameObject selectedPOI = poiPrefabs[UnityEngine.Random.Range(0, poiPrefabs.Length)];

            Instantiate(selectedPOI, spawnPos, Quaternion.identity, parent);
            Debug.Log($"Placed {selectedPOI.name} around city at angle {angle}, radius {radius}");
        }
    }

    public virtual BiomeInstance sowSeed(Tilemap map)
    {
        var bounds = map.cellBounds;
        var success = false;
        while (true)
        {
            var randomX = UnityEngine.Random.Range(bounds.xMin, bounds.xMax);
            var randomY = UnityEngine.Random.Range(bounds.yMin, bounds.yMax);
            var randomPosition = new Vector3Int(randomX, randomY);
            var currentTile = map.GetTile(randomPosition);
            // Check if the current tile is of a specific type (e.g., Grass)
            if (CompatableTerrainTypes.Contains(currentTile))
            {
                map.SetTile(randomPosition, getRandomBiomeTile());
                return new BiomeInstance(this, new Vector2Int(randomX, randomY));
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