using System.Collections.Generic;
using Math = System.Math;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProceduralToolkit;
using ProceduralToolkit.FastNoiseLib;

public class MapGenerator : MonoBehaviour
{
    public Tilemap tilemap;
    public int width = 1024;
    public int height = 1024;
    public TileBase water;
    public List<TileBase> landBiomes;
    public float voronoiFrequency = 0.025f;
    public int randomWalkSteps = 5000000;
    public int smoothing = 10;

    private FastNoise _noiseGenerator;
    private enum TileType
    {
        Water = -2,
        UnassignedLand = -1,
        AssignedLand = 0
    }

    private void Awake()
    {
        Voronoi(Dilation(RandomWalk()));
    }

    private TileType[,] Voronoi(TileType[,] map)
    {
        _noiseGenerator = new FastNoise();
        _noiseGenerator.SetNoiseType(FastNoise.NoiseType.Cellular);
        _noiseGenerator.SetFrequency(voronoiFrequency);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float noiseVal = _noiseGenerator.GetNoise01(x, y);
                float probability = 1.0f / landBiomes.Count;
                if (map[x, y] == TileType.UnassignedLand)
                {
                    for (int i = 0; i < landBiomes.Count; i++)
                    {
                        if (noiseVal > probability * i && noiseVal < probability * (i + 1))
                        {
                            map[x, y] = TileType.AssignedLand;
                            tilemap.SetTile(new Vector3Int(x, y), landBiomes[i]);
                        }
                    }
                }
                else if (map[x, y] == TileType.Water)
                {
                    tilemap.SetTile(new Vector3Int(x, y), water);
                }
            }
        }

        return map;
    }

    // Credit: https://www.noveltech.dev/procgen-random-walk
    // Splits land and water
    private TileType[,] RandomWalk()
    {
        // create the grid, which will be filled with false value
        // true values define valid cells which are part of our visited map
        TileType[,] grid = new TileType[width, height];
        
        // fill with entirely water
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                grid[i, j] = TileType.Water;
            }
        }

        // choose a random starting point
        System.Random rnd = new System.Random();
        Vector2Int curr_pos = new Vector2Int(rnd.Next(width), rnd.Next(height));

        // register this position in the grid as land
        grid[curr_pos.x, curr_pos.y] = TileType.UnassignedLand;

        // define allowed movements: left, up, right, down
        List<Vector2Int> allowed_movements = new List<Vector2Int>
        {
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down
        };

        // iterate on the number of steps and move around
        for (int id_step = 0; id_step < randomWalkSteps; id_step++)
        {
            // for each step, we try to find a new cell to go to.
            // We are not guaranteed to find a position that is valid (i.e. inside the grid)
            // So we use a while loop to allow us to check multiple positions and break out of it
            // when we find a valid one
            while (true)
            {
                // choose a random direction
                Vector2Int new_pos = curr_pos + allowed_movements[rnd.Next(allowed_movements.Count)];
                // check if the new position is in the grid
                if (new_pos.x >= 0 && new_pos.x < width && new_pos.y >= 0 && new_pos.y < height)
                {
                    // this is a valid position, we set it in the grid
                    grid[new_pos.x, new_pos.y] = TileType.UnassignedLand;

                    // replace curr_pos with new_pos
                    curr_pos = new_pos;

                    // and finally break of the infinite loop
                    break;
                }
            }
        }

        return grid;
    }
    
    // Credit: https://www.noveltech.dev/unity-procgen-diffusion-aggregation
    private int[,] DiffusionAggregation(int[,] map)
    {
        // at this point water = -2 and land = -1
        // create a tracker for valid points by subsection for the diffusion process
        List<List<Vector2Int>> recorded_points = new();
        for (int i = 0; i < landBiomes.Count; i++)
        {
            recorded_points.Add(new());
        }

        // create a counter to keep track of the number of unallocated cells 
        int nb_free_cells = width * height;

        // set up the initial points for the process
        System.Random rng = new();
        for (int id_subsection = 0; id_subsection < landBiomes.Count; id_subsection++)
        {
            while (true)
            {
                // find a random point 
                Vector2Int point = new(
                    rng.Next(width),
                    rng.Next(height)
                );

                // check if it's land, else find another point
                if (map[point.x, point.y] == -1)
                {
                    // if it is, add it to tracking and grid then proceed to next subsection
                    map[point.x, point.y] = id_subsection;
                    recorded_points[id_subsection].Add(point);
                    nb_free_cells -= 1;

                    break;
                }
            }
        }

        // Diffusion process moves from a given point to another point in a one cell movement so we're setting the directions here as a reusable element
        Vector2Int[] directions = new Vector2Int[4] { Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down };

        // now we can start filling the grid 
        while (nb_free_cells > 0)
        {
            for (int id_subsection = 0; id_subsection < landBiomes.Count; id_subsection++)
            {
                // check if there are tracked points for this subsection 
                if (recorded_points[id_subsection].Count == 0)
                {
                    continue;
                }

                // choose a random point from the tracked points 
                int id_curr_point = rng.Next(recorded_points[id_subsection].Count);

                Vector2Int curr_point = recorded_points[id_subsection][id_curr_point];

                // choose a direction at random
                Vector2Int new_point = curr_point + directions[rng.Next(4)];

                // check if the new point is in the grid
                if (new_point.x < 0 || new_point.y < 0 || new_point.x >= width || new_point.y >= height)
                {
                    continue;
                }

                // next check if the new point is already occupied and skip this direction if it is
                if (map[new_point.x, new_point.y] != -1)
                {
                    continue;
                }

                // else we can record this new point in our tracker and set it in the grid 
                map[new_point.x, new_point.y] = id_subsection;
                recorded_points[id_subsection].Add(new_point);
                nb_free_cells -= 1;
            }
        }

        return map;
    }

    private TileType[,] Closing(TileType[,] map)
    {
        return Erosion(Dilation(map));
    }
    
    private TileType[,] Opening(TileType[,] map)
    {
        return Dilation(Erosion(map));
    }
    
    private TileType[,] Dilation(TileType[,] map)
    {
        TileType[,] result = new TileType[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType maxVal = map[x, y];
                for (int i = -smoothing; i <= smoothing; i++)
                {
                    for (int j = -smoothing; j <= smoothing; j++)
                    {
                        int newX = x + i;
                        int newY = y + j;
                        if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                        {
                            if (Math.Max((int)maxVal, (int)map[newX, newY]) == -1)
                            {
                                maxVal = TileType.UnassignedLand;
                            }
                            else if (Math.Max((int)maxVal, (int)map[newX, newY]) == -2)
                            {
                                maxVal = TileType.Water;
                            }
                        }
                    }
                }
                result[x, y] = maxVal;
            }
        }

        return result;
    }
    
    private TileType[,] Erosion(TileType[,] map)
    {
        TileType[,] result = new TileType[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType minVal = map[x, y];
                for (int i = -smoothing; i <= smoothing; i++)
                {
                    for (int j = -smoothing; j <= smoothing; j++)
                    {
                        int newX = x + i;
                        int newY = y + j;
                        if (newX >= 0 && newX < width && newY >= 0 && newY < height)
                        {
                            if (Math.Min((int)minVal, (int)map[newX, newY]) == -2)
                            {
                                minVal = TileType.Water;
                            }
                            else if (Math.Min((int)minVal, (int)map[newX, newY]) == -1)
                            {
                                minVal = TileType.UnassignedLand;
                            }
                        }
                    }
                }
                result[x, y] = minVal;
            }
        }

        return result;
    }
}