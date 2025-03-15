using System.Collections.Generic;
using Objectives;
using UnityEngine;

// https://www.jonathanyu.xyz/2023/11/29/dynamic-objective-system-tutorial-for-unity/
namespace UI
{
    public class ObjectiveChecklist : MonoBehaviour
    {
        public GameObject objectiveDisplayPrefab;
        private readonly List<ObjectiveDisplay> _listDisplay = new();

        private static List<Objective> allObjectives = new()
        {
            new Objective(ObjectiveTrigger.CombatStarted, "Fight a horde", 1),
            new Objective(ObjectiveTrigger.POICaptured, "Capture a POI", 1),
            new Objective(ObjectiveTrigger.HordeSplit, "Split your horde", 1),
            new Objective(ObjectiveTrigger.HumanPatrolDefeated, "Defeat a human patrol", 1),
            new Objective(ObjectiveTrigger.SwimmingUnlocked, "Learn to swim", 1),
            new Objective(ObjectiveTrigger.BattleWon, "Win {0}/{1} battles", 10)
        };
        
        public void Start()
        {
            // Assumes you have a GameManager Singleton with the ObjectiveManager
            GameManager.Instance.ObjectiveManager = new ObjectiveManager(); 
            foreach (var objective in allObjectives)
            {
                AddObjective(objective);
                GameManager.Instance.ObjectiveManager.AddObjective(objective);
            }
            GameManager.Instance.ObjectiveManager.OnObjectiveAdded += AddObjective;
        }
        
        public void Reset()
        {
            for (var i = _listDisplay.Count - 1; i >= 0; i--)
            {
                Destroy(_listDisplay[i].gameObject);
            }
            _listDisplay.Clear();
        }
        
        private void AddObjective(Objective objective)
        {
            var display = Instantiate(objectiveDisplayPrefab, transform).GetComponent<ObjectiveDisplay>();
            display.Init(objective);
            _listDisplay.Add(display);
        }
    }
}