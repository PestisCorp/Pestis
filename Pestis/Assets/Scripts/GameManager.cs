using System.IO;
using System.Linq;
using Map;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     Responsible for managing the game as a whole. Calls out to other managers to control their behaviour.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public MapScriptableObject map;
    public Tilemap terrainMap;

    public TMP_Text fpsText;
    public TMP_Text boidText;
    public float currentFps;
    private readonly float[] fpsWindow = new float[60];
    private int fpsIndex;

    private StreamWriter writer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        writer = new StreamWriter("/home/murrax2/Downloads/pestis-physics.csv");
        writer.WriteLine("Rats,FPS");
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
    }

    // Update is called once per frame
    private void Update()
    {
        fpsWindow[fpsIndex] = Time.unscaledDeltaTime;
        if (fpsIndex == 59)
            fpsIndex = 0;
        else
            fpsIndex++;

        currentFps = 60.0f / fpsWindow.Sum();

        fpsText.text = $"FPS: {currentFps}";
        if (!InputHandler.Instance.LocalPlayer) return;
        var numRats = InputHandler.Instance.LocalPlayer.player.Hordes.Sum(horde => horde.AliveRats);
        boidText.text = $"Boids: {numRats}";
        writer.WriteLine($"{numRats},{currentFps}");
    }

    private void OnDestroy()
    {
        writer.Close();
    }
}