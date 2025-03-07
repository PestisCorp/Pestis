using Objectives;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ObjectiveDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI objectiveText;
        private Objective _objective;
        
        public void Init(Objective objective)
        {
            _objective = objective;
            objectiveText.text = objective.GetStatusText();
            objective.OnValueChange += OnObjectiveValueChange;
            objective.OnComplete += OnObjectiveComplete;
            objectiveText.enabled = true;
        }
        
        private void OnObjectiveComplete()
        {
            objectiveText.text = $"<s>{_objective.GetStatusText()}</s>";
        }
        
        private void OnObjectiveValueChange()
        {
            objectiveText.text = _objective.GetStatusText();
        }
    }
}