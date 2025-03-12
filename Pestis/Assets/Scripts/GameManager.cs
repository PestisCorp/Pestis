using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Map;
using Objectives;
using Players;
using POI;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Responsible for managing the game as a whole. Calls out to other managers to control their behaviour.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameObject CombatControllerPrefab;
    public MapScriptableObject map;
    public Tilemap terrainMap;
    public List<Player> Players;

    /// <summary>
    ///     All POIs in the game, in no particular order
    /// </summary>
    public POIController[] pois;

    public ObjectiveManager ObjectiveManager;
    public TMP_Text fpsText;
    public TMP_Text boidText;
    public float currentFps;

    public float poiGridCellSize = 5;
    public int poiGridDimX;
    public int poiGridDimY;

    public string localUsername;

    private readonly float[] fpsWindow = new float[60];
    private int fpsIndex;

    /// <summary>
    ///     All POIs in the game, in grid order
    /// </summary>
    public ComputeBuffer poiBuffer;

    /// <summary>
    ///     Each element represents a grid cell, and the index in poiBuffer of the last poi in that grid cell
    /// </summary>
    public ComputeBuffer poiOffsetBuffer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
#if UNITY_EDITOR
        fpsText.gameObject.SetActive(true);
        boidText.gameObject.SetActive(true);
#endif

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        pois = FindObjectsByType<POIController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        terrainMap.CompressBounds();
        poiGridDimX =
            (int)Math.Ceiling(terrainMap.transform.localScale.x * terrainMap.localBounds.size.x / poiGridCellSize);
        poiGridDimY =
            (int)Math.Ceiling(terrainMap.transform.localScale.y * terrainMap.localBounds.size.y / poiGridCellSize);

        var grid = new List<BoidPoi>[poiGridDimX * poiGridDimY];
        for (var i = 0; i < poiGridDimX * poiGridDimY; i++) grid[i] = new List<BoidPoi>();
        foreach (var poi in pois)
        {
            var x = Math.Floor(poi.transform.position.x / poiGridCellSize + poiGridDimX / 2);
            var y = Math.Floor(poi.transform.position.y / poiGridCellSize + poiGridDimY / 2);
            var gridID = Convert.ToUInt32(poiGridDimX * y + x);
            var bounds = poi.GetComponentInChildren<Collider2D>().bounds;
            grid[gridID].Add(new BoidPoi(new float2(bounds.center.x, bounds.center.y), bounds.extents.sqrMagnitude));
        }


        var gridOffsets = new uint[poiGridDimX * poiGridDimY];
        gridOffsets[0] = (uint)grid[0].Count;
        for (var i = 1; i < poiGridDimX * poiGridDimY; i++) gridOffsets[i] = (uint)grid[i].Count + gridOffsets[i - 1];

        var orderedPoIs = new BoidPoi[pois.Length];

        var cellPoIs = grid[0].GetEnumerator();
        for (uint j = 0; j < gridOffsets[0]; j++)
        {
            cellPoIs.MoveNext();
            orderedPoIs[j] = cellPoIs.Current;
        }

        for (var i = 1; i < poiGridDimX * poiGridDimY; i++)
        {
            cellPoIs = grid[i].GetEnumerator();
            for (var j = gridOffsets[i - 1]; j < gridOffsets[i]; j++)
            {
                cellPoIs.MoveNext();
                orderedPoIs[j] = cellPoIs.Current;
            }
        }

        poiBuffer = new ComputeBuffer(orderedPoIs.Length, Marshal.SizeOf(typeof(BoidPoi)));
        poiBuffer.SetData(orderedPoIs, 0, 0, orderedPoIs.Length);

        poiOffsetBuffer = new ComputeBuffer(poiGridDimX * poiGridDimY, Marshal.SizeOf(typeof(uint)));
        poiOffsetBuffer.SetData(gridOffsets);

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        
        ObjectiveManager = new ObjectiveManager();
        Objective fightHorde = new Objective(ObjectiveTrigger.CombatStarted, "Fight a horde", 1);
        Objective capturePOI = new Objective(ObjectiveTrigger.POICaptured, "Capture a POI", 1);
        Objective splitHorde = new Objective(ObjectiveTrigger.HordeSplit, "Split your horde", 1);
        Objective defeatHumanPatrol = new Objective(ObjectiveTrigger.HumanPatrolDefeated, "Defeat a human patrol", 1);
        Objective learnToSwim = new Objective(ObjectiveTrigger.SwimmingUnlocked, "Learn to swim", 1);
        Objective winBattles = new Objective(ObjectiveTrigger.BattleWon, "Win {0}/{1} battles", 10);
        ObjectiveManager.AddObjective(fightHorde);
        ObjectiveManager.AddObjective(capturePOI);
        ObjectiveManager.AddObjective(splitHorde);
        ObjectiveManager.AddObjective(defeatHumanPatrol);
        ObjectiveManager.AddObjective(learnToSwim);
        ObjectiveManager.AddObjective(winBattles);
    }

    private void Update()
    {
#if UNITY_EDITOR
        fpsWindow[fpsIndex] = Time.unscaledDeltaTime;
        if (fpsIndex == 59)
            fpsIndex = 0;
        else
            fpsIndex++;

        currentFps = 60.0f / fpsWindow.Sum();
        fpsText.text = $"FPS: {currentFps}";
        boidText.text = $"Boids: {Players.Sum(player => player.Hordes.Sum(horde => horde.AliveRats))}";
#endif
    }
}