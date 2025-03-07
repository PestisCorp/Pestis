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
        private int maxPop;

        private int splitAmount;

        private void Update()
        {
            if (!InputHandler.Instance.LocalPlayer || !InputHandler.Instance.LocalPlayer!.selectedHorde) return;

            maxPop = InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;

            if (maxPop < 2 * PopulationController.INITIAL_POPULATION)
            {
                selectedAmountText.text = "Too small to split";
                maxAmountText.text = "";
                button.interactable = false;
                return;
            }

            button.interactable = true;

            splitAmount = (int)slider.value;
            selectedAmountText.text = "Horde 1: " + $"{splitAmount}";
            maxAmountText.text = "Horde 2: " + $"{maxPop - splitAmount}";

            // Both hordes must stay above initial population!
            slider.minValue = PopulationController.INITIAL_POPULATION;
            slider.maxValue = maxPop - PopulationController.INITIAL_POPULATION;
        }

        private void OnEnable()
        {
            splitAmount = maxPop / 2;
            slider.value = splitAmount;
        }

        public void SplitHorde()
        {
            var horde = InputHandler.Instance.LocalPlayer?.selectedHorde;
            horde?.Player.SplitHorde(horde,
                (float)splitAmount / InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats);
            gameObject.SetActive(false);
        }
    }
}