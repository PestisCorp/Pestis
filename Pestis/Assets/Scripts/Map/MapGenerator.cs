using System;
using System.Collections.Generic;
using System.Linq;
using ProceduralToolkit;
using ProceduralToolkit.FastNoiseLib;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

namespace Map
{
    public class Generator
    {
        private FastNoise _noiseGenerator;
        public List<BiomeInstance> BiomeList = new(); //every instance of each biome
        public MapBehaviour Map;
        public int RandomWalkSteps = 700000;
        public int Smoothing = 8;
        public float VoronoiFrequency = 0.00f;
        public static List<Vector3> cityPositions = new List<Vector3>();
        public void GenerateMap()
        {
            BiomeList.Clear(); // Clear old map biome lists
            cityPositions.Clear(); // Clear old city references
            Map.mapObject.tileIndices = new int[Map.mapObject.width * Map.mapObject.height];
            Voronoi(Dilation(RandomWalk()));
            GenerateBiomes();
        }





        private void Callautomata()
        {
            BoundsInt bounds = Map.tilemap.cellBounds;
            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                UnityEngine.Tilemaps.TileBase tile = Map.tilemap.GetTile(pos);
                if (tile is BiomeTile)
                {
                    BiomeTile tile1 = (BiomeTile)tile;
                    tile1.automata(Map.tilemap, pos, BiomeList);
                }
                else
                {
                    Debug.Log(pos);
                    Debug.Log(tile);
                }
            }
        }


        private void GenerateBiomes()
        {
            BiomeList = new();
            //Map.poi = new();
            sowSeed();
            foreach (var biome in BiomeList)
            {
                biome.template.CellGeneration(Map.tilemap, biome);
            }

            //automata(); automata(); automata();
            foreach (var biome in BiomeList)
            {
                biome.template.FeatureGeneration(Map.tilemap, biome, Map.poi);
                biome.template.FeatureGeneration(Map.tilemap, biome, Map.poi);
            }
        }

        private void sowSeed()
        {
            
            var bounds = Map.tilemap.cellBounds;

            for (var i = 0; i < Map.mapObject.BiomeClasses.Length; i++)
            for (var j = 0; j < Map.mapObject.BiomeClasses[i].seedCount; j++)
                BiomeList.Add(Map.mapObject.BiomeClasses[i].sowSeed(Map.tilemap));
        }

        private TileType[,] Voronoi(TileType[,] grid)
        {
            _noiseGenerator = new FastNoise();
            _noiseGenerator.SetNoiseType(FastNoise.NoiseType.Cellular);
            _noiseGenerator.SetFrequency(VoronoiFrequency);

            for (var x = 0; x < Map.mapObject.width; x++)
            for (var y = 0; y < Map.mapObject.height; y++)
            {
                var noiseVal = _noiseGenerator.GetNoise01(x, y);
                var probability = 1.0f / Map.mapObject.landTiles.Length;
                if (grid[x, y] == TileType.UnassignedLand)
                {
                    for (var i = 0; i < Map.mapObject.landTiles.Length; i++)
                        if (noiseVal > probability * i && noiseVal < probability * (i + 1))
                        {
                            grid[x, y] = TileType.AssignedLand;
                            Map.tilemap.SetTile(new Vector3Int(x, y), Map.mapObject.landTiles[i]);
                            Map.mapObject.tileIndices[Map.mapObject.width * y + x] = i;
                        }
                }
                else if (grid[x, y] == TileType.Water)
                {
                    Map.tilemap.SetTile(new Vector3Int(x, y), Map.mapObject.water);
                    Map.mapObject.tileIndices[Map.mapObject.width * y + x] = MapScriptableObject.WaterValue;
                }
            }

            return grid;
        }

        // Credit: https://www.noveltech.dev/procgen-random-walk
        // Splits land and water
        private TileType[,] RandomWalk()
        {
            // create the grid, which will be filled with false value
            // true values define valid cells which are part of our visited map
            var grid = new TileType[Map.mapObject.width, Map.mapObject.height];

            // fill with entirely water
            for (var x = 0; x < Map.mapObject.width; x++)
            for (var y = 0; y < Map.mapObject.height; y++)
                grid[x, y] = TileType.Water;

            // choose a random starting point
            var rnd = new Random();
            var curr_pos = new Vector2Int(rnd.Next(Map.mapObject.width), rnd.Next(Map.mapObject.height));

            // register this position in the grid as land
            grid[curr_pos.x, curr_pos.y] = TileType.UnassignedLand;

            // define allowed movements: left, up, right, down
            var allowed_movements = new List<Vector2Int>
            {
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down
            };

            // iterate on the number of steps and move around
            for (var id_step = 0; id_step < RandomWalkSteps; id_step++)
                // for each step, we try to find a new cell to go to.
                // We are not guaranteed to find a position that is valid (i.e. inside the grid)
                // So we use a while loop to allow us to check multiple positions and break out of it
                // when we find a valid one
                while (true)
                {
                    // choose a random direction
                    var new_pos = curr_pos + allowed_movements[rnd.Next(allowed_movements.Count)];
                    // check if the new position is in the grid
                    if (new_pos.x >= 0 && new_pos.x < Map.mapObject.width && new_pos.y >= 0 &&
                        new_pos.y < Map.mapObject.height)
                    {
                        // this is a valid position, we set it in the grid
                        grid[new_pos.x, new_pos.y] = TileType.UnassignedLand;

                        // replace curr_pos with new_pos
                        curr_pos = new_pos;

                        // and finally break of the infinite loop
                        break;
                    }
                }

            return grid;
        }

        // Credit: https://www.noveltech.dev/unity-procgen-diffusion-aggregation
        private int[,] DiffusionAggregation(int[,] grid)
        {
            // at this point water = -2 and land = -1
            // create a tracker for valid points by subsection for the diffusion process
            List<List<Vector2Int>> recorded_points = new();
            for (var i = 0; i < Map.mapObject.landTiles.Length; i++) recorded_points.Add(new List<Vector2Int>());

            // create a counter to keep track of the number of unallocated cells 
            var nb_free_cells = Map.mapObject.width * Map.mapObject.height;

            // set up the initial points for the process
            Random rng = new();
            for (var id_subsection = 0; id_subsection < Map.mapObject.landTiles.Length; id_subsection++)
                while (true)
                {
                    // find a random point 
                    Vector2Int point = new(
                        rng.Next(Map.mapObject.width),
                        rng.Next(Map.mapObject.height)
                    );

                    // check if it's land, else find another point
                    if (grid[point.x, point.y] == -1)
                    {
                        // if it is, add it to tracking and grid then proceed to next subsection
                        grid[point.x, point.y] = id_subsection;
                        recorded_points[id_subsection].Add(point);
                        nb_free_cells -= 1;

                        break;
                    }
                }

            // Diffusion process moves from a given point to another point in a one cell movement so we're setting the directions here as a reusable element
            Vector2Int[] directions = { Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down };

            // now we can start filling the grid 
            while (nb_free_cells > 0)
                for (var id_subsection = 0; id_subsection < Map.mapObject.landTiles.Length; id_subsection++)
                {
                    // check if there are tracked points for this subsection 
                    if (recorded_points[id_subsection].Count == 0) continue;

                    // choose a random point from the tracked points 
                    var id_curr_point = rng.Next(recorded_points[id_subsection].Count);

                    var curr_point = recorded_points[id_subsection][id_curr_point];

                    // choose a direction at random
                    var new_point = curr_point + directions[rng.Next(4)];

                    // check if the new point is in the grid
                    if (new_point.x < 0 || new_point.y < 0 || new_point.x >= Map.mapObject.width ||
                        new_point.y >= Map.mapObject.height) continue;

                    // next check if the new point is already occupied and skip this direction if it is
                    if (grid[new_point.x, new_point.y] != -1) continue;

                    // else we can record this new point in our tracker and set it in the grid 
                    grid[new_point.x, new_point.y] = id_subsection;
                    recorded_points[id_subsection].Add(new_point);
                    nb_free_cells--;
                }

            return grid;
        }

        private TileType[,] Closing(TileType[,] grid)
        {
            return Erosion(Dilation(grid));
        }

        private TileType[,] Opening(TileType[,] grid)
        {
            return Dilation(Erosion(grid));
        }

        private TileType[,] Dilation(TileType[,] grid)
        {
            var result = new TileType[Map.mapObject.width, Map.mapObject.height];

            for (var x = 0; x < Map.mapObject.width; x++)
            for (var y = 0; y < Map.mapObject.height; y++)
            {
                var maxVal = grid[x, y];
                for (var i = -Smoothing; i <= Smoothing; i++)
                for (var j = -Smoothing; j <= Smoothing; j++)
                {
                    var newX = x + i;
                    var newY = y + j;
                    if (newX >= 0 && newX < Map.mapObject.width && newY >= 0 && newY < Map.mapObject.height)
                    {
                        if (Math.Max((int)maxVal, (int)grid[newX, newY]) == -1)
                            maxVal = TileType.UnassignedLand;
                        else if (Math.Max((int)maxVal, (int)grid[newX, newY]) == -2) maxVal = TileType.Water;
                    }
                }

                result[x, y] = maxVal;
            }

            return result;
        }

        private TileType[,] Erosion(TileType[,] grid)
        {
            var result = new TileType[Map.mapObject.width, Map.mapObject.height];

            for (var x = 0; x < Map.mapObject.width; x++)
            for (var y = 0; y < Map.mapObject.height; y++)
            {
                var minVal = grid[x, y];
                for (var i = -Smoothing; i <= Smoothing; i++)
                for (var j = -Smoothing; j <= Smoothing; j++)
                {
                    var newX = x + i;
                    var newY = y + j;
                    if (newX >= 0 && newX < Map.mapObject.width && newY >= 0 && newY < Map.mapObject.height)
                    {
                        if (Math.Min((int)minVal, (int)grid[newX, newY]) == -2)
                            minVal = TileType.Water;
                        else if (Math.Min((int)minVal, (int)grid[newX, newY]) == -1) minVal = TileType.UnassignedLand;
                    }
                }

                result[x, y] = minVal;
            }

            return result;
        }

        internal enum TileType
        {
            Water = -2,
            UnassignedLand = -1,
            AssignedLand = 0
        }
    }
}