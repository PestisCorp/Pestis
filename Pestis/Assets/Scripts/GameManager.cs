using Map;
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

        Application.targetFrameRate = 58;
        QualitySettings.vSyncCount = 0;
    }

    // Update is called once per frame
    private void Update()
    {
    }
}