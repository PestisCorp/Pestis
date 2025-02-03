using System.Collections.Generic;
using Math = System.Math;
using UnityEngine;
using UnityEngine.Tilemaps;
using ProceduralToolkit;
using ProceduralToolkit.FastNoiseLib;
using UnityEditor;
using UnityEditorInternal;

public class MapGeneratorWindow : EditorWindow
{
    private enum TileType
    {
        Water = -2,
        UnassignedLand = -1,
        AssignedLand = 0
    }
    private FastNoise _noiseGenerator;
    // Editor variables
    private const string _helpText = "Cannot find 'Land Biomes List' component on any GameObject in the scene!";
    private static Rect _helpRect = new Rect(0f, 0f, 400f, 100f);
    // Box size of the list
    private static Vector2 _windowsMinSize = Vector2.one * 500f;
    private static Rect _listRect = new Rect(Vector2.zero, _windowsMinSize);
    // did we start generation
    private bool _isGenerating;

    private SerializedObject _objectSO = null;
    private ReorderableList _listRE = null;
    LandBiomesList _landBiomesList;

    [MenuItem("Window/Map Generator")]
    public static void ShowWindow()
    {
        GetWindow<MapGeneratorWindow>("Map Generator");
    }

    private void OnEnable()
    {
        _landBiomesList = FindFirstObjectByType<LandBiomesList>();

        if (_landBiomesList)
        {
            _objectSO = new SerializedObject(_landBiomesList);
            _listRE = new ReorderableList(_objectSO, _objectSO.FindProperty("landTiles"), true, true, true, true);
            
            _listRE.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "Tiles");
            _listRE.drawElementCallback = (Rect rect, int index, bool isGenerating, bool isFocused) =>
            {
                rect.y += 10f;
                rect.height = EditorGUIUtility.singleLineHeight;

                GUIContent tileLabel = new GUIContent($"Tile {index}");

                EditorGUI.PropertyField(rect, _listRE.serializedProperty.GetArrayElementAtIndex(index), tileLabel);
            };
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        GUILayout.Space(100f);
        EditorGUILayout.BeginVertical();
        
        Map.instance.tilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", Map.instance.tilemap, typeof(Tilemap));
        Map.instance.width = EditorGUILayout.IntField("Width", Map.instance.width);
        Map.instance.height = EditorGUILayout.IntField("Height", Map.instance.height);
        Map.instance.water = (TileBase)EditorGUILayout.ObjectField("Water tile", Map.instance.water, typeof(TileBase));
        Map.instance.voronoiFrequency = EditorGUILayout.FloatField("Voronoi frequency", Map.instance.voronoiFrequency);
        Map.instance.randomWalkSteps = EditorGUILayout.IntField("Random walk steps", Map.instance.randomWalkSteps);
        Map.instance.smoothing = EditorGUILayout.IntField("Water-land Smoothing", Map.instance.smoothing);
        
        EditorGUILayout.EndVertical();

        GUILayout.Space(20f);
        
        if (_objectSO == null)
        {
            EditorGUI.HelpBox(_helpRect, _helpText, MessageType.Warning);
            return;
        }
        
        _objectSO.Update();
        _listRE.DoList(_listRect);
        _objectSO.ApplyModifiedProperties();
        
        GUILayout.Space(_listRE.GetHeight() + 30f);
        GUILayout.Label("Select land tiles to use");
        GUILayout.Space(10f);

        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Generate Map"))
        {
            _isGenerating = true;
            Voronoi(Dilation(RandomWalk()));
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private TileType[,] Voronoi(TileType[,] grid)
    {
        _noiseGenerator = new FastNoise();
        _noiseGenerator.SetNoiseType(FastNoise.NoiseType.Cellular);
        _noiseGenerator.SetFrequency(Map.instance.voronoiFrequency);

        for (int x = 0; x < Map.instance.width; x++)
        {
            for (int y = 0; y < Map.instance.height; y++)
            {
                float noiseVal = _noiseGenerator.GetNoise01(x, y);
                float probability = 1.0f / Map.instance.landBiomes.Count;
                if (grid[x, y] == TileType.UnassignedLand)
                {
                    for (int i = 0; i < Map.instance.landBiomes.Count; i++)
                    {
                        if (noiseVal > probability * i && noiseVal < probability * (i + 1))
                        {
                            grid[x, y] = TileType.AssignedLand;
                            Map.instance.tilemap.SetTile(new Vector3Int(x, y), Map.instance.landBiomes[i]);
                        }
                    }
                }
                else if (grid[x, y] == TileType.Water)
                {
                    Map.instance.tilemap.SetTile(new Vector3Int(x, y), Map.instance.water);
                }
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
        TileType[,] grid = new TileType[Map.instance.width, Map.instance.height];
        
        // fill with entirely water
        for (int i = 0; i < Map.instance.width; i++)
        {
            for (int j = 0; j < Map.instance.height; j++)
            {
                grid[i, j] = TileType.Water;
            }
        }

        // choose a random starting point
        System.Random rnd = new System.Random();
        Vector2Int curr_pos = new Vector2Int(rnd.Next(Map.instance.width), rnd.Next(Map.instance.height));

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
        for (int id_step = 0; id_step < Map.instance.randomWalkSteps; id_step++)
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
                if (new_pos.x >= 0 && new_pos.x < Map.instance.width && new_pos.y >= 0 && new_pos.y < Map.instance.height)
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
    private int[,] DiffusionAggregation(int[,] grid)
    {
        // at this point water = -2 and land = -1
        // create a tracker for valid points by subsection for the diffusion process
        List<List<Vector2Int>> recorded_points = new();
        for (int i = 0; i < Map.instance.landBiomes.Count; i++)
        {
            recorded_points.Add(new());
        }

        // create a counter to keep track of the number of unallocated cells 
        int nb_free_cells = Map.instance.width * Map.instance.height;

        // set up the initial points for the process
        System.Random rng = new();
        for (int id_subsection = 0; id_subsection < Map.instance.landBiomes.Count; id_subsection++)
        {
            while (true)
            {
                // find a random point 
                Vector2Int point = new(
                    rng.Next(Map.instance.width),
                    rng.Next(Map.instance.height)
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
        }

        // Diffusion process moves from a given point to another point in a one cell movement so we're setting the directions here as a reusable element
        Vector2Int[] directions = new Vector2Int[4] { Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down };

        // now we can start filling the grid 
        while (nb_free_cells > 0)
        {
            for (int id_subsection = 0; id_subsection < Map.instance.landBiomes.Count; id_subsection++)
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
                if (new_point.x < 0 || new_point.y < 0 || new_point.x >= Map.instance.width || new_point.y >= Map.instance.height)
                {
                    continue;
                }

                // next check if the new point is already occupied and skip this direction if it is
                if (grid[new_point.x, new_point.y] != -1)
                {
                    continue;
                }

                // else we can record this new point in our tracker and set it in the grid 
                grid[new_point.x, new_point.y] = id_subsection;
                recorded_points[id_subsection].Add(new_point);
                nb_free_cells -= 1;
            }
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
        TileType[,] result = new TileType[Map.instance.width, Map.instance.height];

        for (int x = 0; x < Map.instance.width; x++)
        {
            for (int y = 0; y < Map.instance.height; y++)
            {
                TileType maxVal = grid[x, y];
                for (int i = -Map.instance.smoothing; i <= Map.instance.smoothing; i++)
                {
                    for (int j = -Map.instance.smoothing; j <= Map.instance.smoothing; j++)
                    {
                        int newX = x + i;
                        int newY = y + j;
                        if (newX >= 0 && newX < Map.instance.width && newY >= 0 && newY < Map.instance.height)
                        {
                            if (Math.Max((int)maxVal, (int)grid[newX, newY]) == -1)
                            {
                                maxVal = TileType.UnassignedLand;
                            }
                            else if (Math.Max((int)maxVal, (int)grid[newX, newY]) == -2)
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
    
    private TileType[,] Erosion(TileType[,] grid)
    {
        TileType[,] result = new TileType[Map.instance.width, Map.instance.height];

        for (int x = 0; x < Map.instance.width; x++)
        {
            for (int y = 0; y < Map.instance.height; y++)
            {
                TileType minVal = grid[x, y];
                for (int i = -Map.instance.smoothing; i <= Map.instance.smoothing; i++)
                {
                    for (int j = -Map.instance.smoothing; j <= Map.instance.smoothing; j++)
                    {
                        int newX = x + i;
                        int newY = y + j;
                        if (newX >= 0 && newX < Map.instance.width && newY >= 0 && newY < Map.instance.height)
                        {
                            if (Math.Min((int)minVal, (int)grid[newX, newY]) == -2)
                            {
                                minVal = TileType.Water;
                            }
                            else if (Math.Min((int)minVal, (int)grid[newX, newY]) == -1)
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