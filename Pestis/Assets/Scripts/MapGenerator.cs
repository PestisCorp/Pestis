using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProceduralToolkit;
using ProceduralToolkit.FastNoiseLib;

public class MapGenerator : MonoBehaviour
{
    public Tilemap tilemap;
    public int width;
    public int height;
    public List<TileBase> tiles;
    public float frequency;
    public int steps;

    private FastNoise _noiseGenerator;

    private void Awake()
    {
        bool[,] landWaterSplit = RandomWalk();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (landWaterSplit[x, y])
                {
                    tilemap.SetTile(new Vector3Int(x, y), tiles[0]);
                }
                else
                {
                    tilemap.SetTile(new Vector3Int(x, y), tiles[1]);
                }
            }
        }
    }

    public void Voronoi()
    {
        _noiseGenerator = new FastNoise();
        _noiseGenerator.SetNoiseType(FastNoise.NoiseType.Cellular);
        _noiseGenerator.SetFrequency(frequency);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float noiseVal = _noiseGenerator.GetNoise01(x, y);
                float probability = 1.0f / tiles.Count;
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (noiseVal > probability * i && noiseVal < probability * (i + 1))
                    {
                        tilemap.SetTile(new Vector3Int(x, y), tiles[i]);
                    }
                }
            }
        }
    }

    // Credit: https://www.noveltech.dev/procgen-random-walk
    public bool[,] RandomWalk()
    {
        // create the grid, which will be filled with false value
        // true values define valid cells which are part of our visited map
        bool[,] grid = new bool[width, height];

        // choose a random starting point
        System.Random rnd = new System.Random();
        Vector2Int curr_pos = new Vector2Int(rnd.Next(width), rnd.Next(height));

        // register this position in the grid
        grid[curr_pos.x, curr_pos.y] = true;
        //tilemap.SetTile(new Vector3Int(curr_pos.x, curr_pos.y), tiles[0]);

        // define allowed movements: left, up, right, down
        List<Vector2Int> allowed_movements = new List<Vector2Int>
        {
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down
        };

        // iterate on the number of steps and move around
        for (int id_step = 0; id_step < steps; id_step++)
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
                    grid[new_pos.x, new_pos.y] = true;
                    //tilemap.SetTile(new Vector3Int(new_pos.x, new_pos.y), tiles[0]);

                    // replace curr_pos with new_pos
                    curr_pos = new_pos;

                    // and finally break of the infinite loop
                    break;
                }
            }
        }

        return grid;
    }

}