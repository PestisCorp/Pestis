using Objectives;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ObjectiveDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI objectiveText;
        private Objective _objective;
        private static int numberOfObjectives = 0;
        
        public void Init(Objective objective)
        {
            _objective = objective;
            objectiveText.text = objective.GetStatusText();
            objective.OnValueChange += OnObjectiveValueChange;
            objective.OnComplete += OnObjectiveComplete;
            numberOfObjectives++;
            objectiveText.enabled = true;
            objectiveText.rectTransform.position =
                new Vector2(Screen.width - 20, Screen.height - 50 * (numberOfObjectives + 1));
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