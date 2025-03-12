using System;
using System.Collections.Generic;

namespace Objectives
{
    public enum ObjectiveTrigger
    {
        CombatStarted,
        POICaptured,
        HordeSplit,
        HumanPatrolDefeated,
        SwimmingUnlocked,
        BattleWon
    }
    
    // https://www.jonathanyu.xyz/2023/11/29/dynamic-objective-system-tutorial-for-unity/
    public class ObjectiveManager
    {
        public Action<Objective> OnObjectiveAdded;
        public List<Objective> Objectives { get; } = new();
        
        private readonly Dictionary<ObjectiveTrigger, List<Objective>> _objectiveMap = new();

        /// <summary>
        /// Adds an objective to the objective manager.
        /// If the objective has an EventTrigger, its progress will be incremented
        /// by AddProgress when the event is triggered. Multiple objectives can have
        /// the same EventTrigger (i.e. MobKilled, ItemCollected, etc.)
        /// </summary>
        public void AddObjective(Objective objective)
        {
            Objectives.Add(objective);


            if (!_objectiveMap.ContainsKey(objective.ObjectiveTrigger))
            {
                _objectiveMap.Add(objective.ObjectiveTrigger, new List<Objective>());
            }

            _objectiveMap[objective.ObjectiveTrigger].Add(objective);
            OnObjectiveAdded?.Invoke(objective);
        }

        public void AddProgress(ObjectiveTrigger objectiveTrigger, int value)
        {
            if (!_objectiveMap.ContainsKey(objectiveTrigger))
            {
                return;
            }

            foreach (var objective in _objectiveMap[objectiveTrigger])
            {
                objective.AddProgress(value);
            }
        }
    }
}