using Horde;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HordeSplitManager : MonoBehaviour
    {
        public TMP_Text selectedAmountText;
        public TMP_Text maxAmountText;
        public Slider slider;
        public Button button;
        private float _splitPercentage;

        private int SplitAmount =>
            (int)(_splitPercentage * InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats);

        private void Update()
        {
            if (!InputHandler.Instance.LocalPlayer || !InputHandler.Instance.LocalPlayer!.selectedHorde) return;

            if (InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats <
                2 * PopulationController.INITIAL_POPULATION)
            {
                selectedAmountText.text = "Too small to split";
                button.interactable = false;
                return;
            }

            button.interactable = true;

            selectedAmountText.text = $"{SplitAmount}";
            maxAmountText.text = $"{InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats}";

            // Both hordes must stay above initial population!
            slider.minValue = (float)PopulationController.INITIAL_POPULATION /
                              InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;
            slider.maxValue = (float)(InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats -
                                      PopulationController.INITIAL_POPULATION) /
                              InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;
        }

        private void OnEnable()
        {
            _splitPercentage = slider.value;
        }

        public void SplitHorde()
        {
            var horde = InputHandler.Instance.LocalPlayer?.selectedHorde;
            horde?.Player.SplitHorde(horde, _splitPercentage);
            gameObject.SetActive(false);
        }

        public void SetHordeSplitNumber(float percentage)
        {
            _splitPercentage = percentage;
        }
    }
}