using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Horde;
using Map;
using Objectives;
using Players;
using POI;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum SoundEffectType
{
    BattleStart,
    BattleLoss,
    BattleWin,
    POICapture,
    HumanScream
}

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
    public UI_Manager UIManager;

    [SerializeField] private AudioSource audioSource;

    /// <summary>
    ///     All POIs in the game, in no particular order
    /// </summary>
    public PoiController[] pois;

    public TMP_Text fpsText;
    public TMP_Text boidText;
    public float currentFps;

    /// <summary>
    ///     Increase the worse performance is to decrease frequency of some updates. Must not be zero, lowest is 1.
    /// </summary>
    public int recoverPerfLevel = 1;

    /// <summary>
    ///     Which perf bucket should currently be running
    /// </summary>
    public int currentPerfBucket;

    public float poiGridCellSize = 5;
    public int poiGridDimX;
    public int poiGridDimY;

    public string localUsername;

    /// <summary>
    ///     The mean health of all the hordes in game, calculated each fixed update
    /// </summary>
    public float meanHordeHealth;

    public List<HordeController> AllHordes;

    private readonly float[] fpsWindow = new float[60];

    private Dictionary<SoundEffectType, AudioClip> _soundEffects;

    public Human.BoidPoi[] BoidPois;

    private int fpsIndex;

    public ObjectiveManager ObjectiveManager;

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

        _soundEffects = new Dictionary<SoundEffectType, AudioClip>
        {
            { SoundEffectType.BattleStart, Resources.Load<AudioClip>("SFX/Combat Start 1") },
            { SoundEffectType.BattleLoss, Resources.Load<AudioClip>("SFX/Combat Loss  1") },
            { SoundEffectType.BattleWin, Resources.Load<AudioClip>("SFX/Combat Victory 1") },
            { SoundEffectType.POICapture, Resources.Load<AudioClip>("SFX/Imporant Location captured") },
            { SoundEffectType.HumanScream , Resources.Load<AudioClip>("SFX/Human Grunts Scared v4")}
        };

        pois = FindObjectsByType<PoiController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        terrainMap.CompressBounds();
        poiGridDimX =
            (int)Math.Ceiling(terrainMap.transform.localScale.x * terrainMap.localBounds.size.x / poiGridCellSize);
        poiGridDimY =
            (int)Math.Ceiling(terrainMap.transform.localScale.y * terrainMap.localBounds.size.y / poiGridCellSize);

        var poiGrid = new List<PoiController>[poiGridDimY * poiGridDimX];
        var grid = new List<BoidPoi>[poiGridDimX * poiGridDimY];
        for (var i = 0; i < poiGridDimX * poiGridDimY; i++)
        {
            grid[i] = new List<BoidPoi>();
            poiGrid[i] = new List<PoiController>();
        }

        BoidPois = new Human.BoidPoi[pois.Length];

        foreach (var poi in pois)
        {
            var x = Math.Floor(poi.transform.position.x / poiGridCellSize + poiGridDimX / 2);
            var y = Math.Floor(poi.transform.position.y / poiGridCellSize + poiGridDimY / 2);
            if (y >= poiGridDimY || y < 0) continue;
            var gridID = Convert.ToUInt32(poiGridDimX * y + x);
            var bounds = poi.GetComponent<Collider2D>().bounds;
            grid[gridID].Add(new BoidPoi(new float2(bounds.center.x, bounds.center.y), bounds.extents.sqrMagnitude));
            poiGrid[gridID].Add(poi);
        }


        var poigridOffsets = new uint[poiGridDimX * poiGridDimY];
        var gridOffsets = new uint[poiGridDimX * poiGridDimY];
        gridOffsets[0] = (uint)grid[0].Count;
        poigridOffsets[0] = (uint)grid[0].Count;
        for (var i = 1; i < poiGridDimX * poiGridDimY; i++) gridOffsets[i] = (uint)grid[i].Count + gridOffsets[i - 1];
        for (var i = 1; i < poiGridDimX * poiGridDimY; i++)
            poigridOffsets[i] = (uint)grid[i].Count + gridOffsets[i - 1];

        var orderedPoIs = new BoidPoi[pois.Length];

        var cellPoIs = grid[0].GetEnumerator();
        var poiCellPois = poiGrid[0].GetEnumerator();
        for (uint j = 0; j < gridOffsets[0]; j++)
        {
            cellPoIs.MoveNext();
            poiCellPois.MoveNext();
            orderedPoIs[j] = cellPoIs.Current;
            poiCellPois.Current.boidPoisIndex = j;
        }

        for (var i = 1; i < poiGridDimX * poiGridDimY; i++)
        {
            cellPoIs = grid[i].GetEnumerator();
            poiCellPois = poiGrid[i].GetEnumerator();
            for (var j = gridOffsets[i - 1]; j < gridOffsets[i]; j++)
            {
                cellPoIs.MoveNext();
                poiCellPois.MoveNext();
                orderedPoIs[j] = cellPoIs.Current;
                BoidPois[j] = new Human.BoidPoi(cellPoIs.Current.Pos, cellPoIs.Current.RadiusSq,
                    Convert.ToUInt32(poiCellPois.Current.patrolController.startingHumanCount));
                poiCellPois.Current.boidPoisIndex = j;
            }
        }

        poiBuffer = new ComputeBuffer(orderedPoIs.Length, Marshal.SizeOf(typeof(BoidPoi)));
        poiBuffer.SetData(orderedPoIs, 0, 0, orderedPoIs.Length);

        poiOffsetBuffer = new ComputeBuffer(poiGridDimX * poiGridDimY, Marshal.SizeOf(typeof(uint)));
        poiOffsetBuffer.SetData(gridOffsets);

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;
        Debug.Log($"Async GPU support: {SystemInfo.supportsAsyncCompute}");
    }

    private void Update()
    {
        fpsWindow[fpsIndex] = Time.unscaledDeltaTime;
        if (fpsIndex == 59)
            fpsIndex = 0;
        else
            fpsIndex++;

        currentFps = 60.0f / fpsWindow.Sum();

        var instantaneousFPS = Mathf.FloorToInt(1.0f / Time.unscaledDeltaTime);
        if (instantaneousFPS >= 30)
            recoverPerfLevel = 1;
        else
            recoverPerfLevel = 30 - instantaneousFPS;

#if UNITY_EDITOR
        fpsText.text = $"FPS: {currentFps}";
        boidText.text = $"Boids: {Players.Sum(player => player.Hordes.Sum(horde => horde.AliveRats))}";
#endif
    }

    private void FixedUpdate()
    {
        // Done as for loops to avoid alloc
        float sum = 0;
        var numHordes = 0;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var player = 0; player < Players.Count; player++)
            // ReSharper disable once ForCanBeConvertedToForeach
        for (var horde = 0; horde < Players[player].Hordes.Count; horde++)
        {
            sum += Players[player].Hordes[horde].TotalHealth;
            if (AllHordes.Count >= numHordes + 1)
            {
                AllHordes[numHordes] = Players[player].Hordes[horde];
            }
            else
            {
                AllHordes.Add(Players[player].Hordes[horde]);
            }
            numHordes++;
        }

        // Crop end of list when hordes removed so we don't realloc a new list
        if (numHordes < AllHordes.Count)
        {
            AllHordes.RemoveRange(numHordes, AllHordes.Count - numHordes);
        }
        
        meanHordeHealth = sum / numHordes;

        currentPerfBucket = Players.Count != 0 ? Players[0].Runner.Tick % recoverPerfLevel : 0;
    }

    public void PlaySfx(SoundEffectType type)
    {
        audioSource.Stop();
        audioSource.clip = _soundEffects[type];
        audioSource.Play();
    }
}