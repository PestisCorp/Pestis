using System.Collections.Generic;
using System.Linq;
using Map;
using Objectives;
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
    public ObjectiveManager ObjectiveManager;
    public TMP_Text fpsText;
    public TMP_Text boidText;

    private readonly float[] fpsWindow = new float[60];
    private int fpsIndex;

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

        Application.targetFrameRate = 58;
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

        fpsText.text = $"FPS: {60.0f / fpsWindow.Sum()}";
        boidText.text = $"Boids: {Players.Sum(player => player.Hordes.Sum(horde => horde.AliveRats))}";
#endif
    }
}