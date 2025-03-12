using Objectives;
using TMPro;
using UnityEngine;

namespace UI
{
    // https://www.jonathanyu.xyz/2023/11/29/dynamic-objective-system-tutorial-for-unity/
    public class ObjectiveDisplay : MonoBehaviour
    {
        private static int _numberOfObjectives;
        private static readonly int TextSpacingHeight = Screen.height / 10;
        private static readonly int TextSpacingWidth = Screen.height / 5;
        [SerializeField] private TextMeshProUGUI objectiveText;
        private Objective _objective;

        public void Init(Objective objective)
        {
            _objective = objective;
            objectiveText.text = objective.GetStatusText();
            objective.OnValueChange += OnObjectiveValueChange;
            objective.OnComplete += OnObjectiveComplete;
            _numberOfObjectives++;
            objectiveText.enabled = true;
            objectiveText.transform.parent.transform.position = new Vector2(Screen.width - TextSpacingWidth,
                Screen.height - TextSpacingHeight * (_numberOfObjectives + 3));
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