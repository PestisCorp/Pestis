using System.Collections.Generic;
using Objectives;
using UnityEngine;

// https://www.jonathanyu.xyz/2023/11/29/dynamic-objective-system-tutorial-for-unity/
namespace UI
{
    public class ObjectiveChecklist : MonoBehaviour
    {
        [SerializeField] private ObjectiveDisplay objectiveDisplayPrefab;
        [SerializeField] private Transform objectiveDisplayParent;
        private readonly List<ObjectiveDisplay> _listDisplay = new();
        
        public void Start()
        {
            // Assumes you have a GameManager Singleton with the ObjectiveManager
            foreach (var objective in GameManager.Instance.ObjectiveManager.Objectives)
            {
                AddObjective(objective);
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
        
        private void AddObjective(Objective obj)
        {
            var display = Instantiate(objectiveDisplayPrefab, objectiveDisplayParent);
            display.Init(obj);
            _listDisplay.Add(display);
        }
    }
}