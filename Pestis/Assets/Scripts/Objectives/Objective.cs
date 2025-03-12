using System;

namespace Objectives
{
    // https://www.jonathanyu.xyz/2023/11/29/dynamic-objective-system-tutorial-for-unity/
    public class Objective
    {
        private readonly string _statusText;

        /// Invoked when the objective is completed
        public Action OnComplete;

        /// Invoked when the objective's progress changes
        public Action OnValueChange;

        /// <summary>
        ///     Status text can have 2 parameters {0} and {1} for current and max value.
        ///     Example: "Kill {0} of {1} enemies".
        /// </summary>
        public Objective(ObjectiveTrigger objectiveTrigger, string statusText, int maxValue)
        {
            ObjectiveTrigger = objectiveTrigger;
            _statusText = statusText;
            MaxValue = maxValue;
        }

        /// Used to AddProgress from ObjectiveManager.
        /// Can be empty if objective progress is managed elsewhere.
        public ObjectiveTrigger ObjectiveTrigger { get; }

        public bool IsComplete { get; private set; }
        public int MaxValue { get; }
        public int CurrentValue { get; private set; }

        private void CheckCompletion()
        {
            if (CurrentValue >= MaxValue)
            {
                IsComplete = true;
                OnComplete?.Invoke();
            }
        }

        public void AddProgress(int value)
        {
            if (IsComplete) return;
            CurrentValue += value;
            if (CurrentValue > MaxValue) CurrentValue = MaxValue;
            OnValueChange?.Invoke();
            CheckCompletion();
        }

        public string GetStatusText()
        {
            return string.Format(_statusText, CurrentValue, MaxValue);
        }
    }
}