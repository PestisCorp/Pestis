using System.Collections.Generic;
using System.Linq;
using Map;
using Players;
using TMPro;
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

    public TMP_Text fpsText;
    public TMP_Text boidText;

    private readonly float[] fpsWindow = new float[60];
    private int fpsIndex;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
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

        Application.targetFrameRate = 58;
        QualitySettings.vSyncCount = 0;
    }

    private void Update()
    {
#if UNITY_EDITOR
        fpsWindow[fpsIndex] = Time.unscaledDeltaTime;
        if (fpsIndex == 59)
            fpsIndex = 0;
        else
            fpsIndex++;

        fpsText.text = $"FPS: {60.0f / fpsWindow.Sum()}";
        boidText.text = $"Boids: {Players.Sum(player => player.Hordes.Sum(horde => horde.AliveRats))}";
#endif
    }
}