using Objectives;
using TMPro;
using UnityEngine;

namespace UI
{
    // https://www.jonathanyu.xyz/2023/11/29/dynamic-objective-system-tutorial-for-unity/
    public class ObjectiveDisplay : MonoBehaviour
    {
        internal Objective objective;
        
        private TextMeshProUGUI _objectiveText;

        public void Init(Objective objective)
        {
            this.objective = objective;
            _objectiveText = GetComponent<TextMeshProUGUI>();
            _objectiveText.text = objective.GetStatusText();
            this.objective.OnValueChange += OnObjectiveValueChange;
            this.objective.OnComplete += OnObjectiveComplete;
        }

        private void OnObjectiveComplete()
        {
            _objectiveText.text = $"- <s>{objective.GetStatusText()}</s>";
        }

        private void OnObjectiveValueChange()
        {
            _objectiveText.text = objective.GetStatusText();
        }
    }
}