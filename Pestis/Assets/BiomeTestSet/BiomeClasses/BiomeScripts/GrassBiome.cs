using Map;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Biomes/GrassBiome")]
public class GrassBiome : BiomeClass
{
    public override void CellGeneration(Tilemap map, BiomeInstance biomeInstance) //generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        BoundsInt bounds = map.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(pos);
            if (this.CompatableTerrainTypes.Contains(tile))
            {
                Debug.Log(tile.GetType()+ " "+ pos);
                biomeInstance.setTile(map, pos, getRandomBiomeTile());
            }
        }
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
            PlacePoisInCircle(cityWorldPos, circlePoiCount, avgRadius, radiusVariation, minPoiDistance, FeatureList,
                parent.transform);
        }


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
            PlacePoisInCircle(cityWorldPos, circlePoiCount, avgRadius, radiusVariation, minPoiDistance, FeatureList,
                parent.transform);
        }
    }
}
