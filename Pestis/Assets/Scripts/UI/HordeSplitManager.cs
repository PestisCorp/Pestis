using System;
using Horde;
using Objectives;
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
        public int spitRecommendedInt = 50;
        private int initialPopulation;
        private int maxPop;

        private int splitAmount;

        private void Start()
        {
            button.onClick.AddListener(SplitHorde);
        }

        private void Update()
        {
            if (!InputHandler.Instance.LocalPlayer || !InputHandler.Instance.LocalPlayer!.selectedHorde) return;

            maxPop = InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;
            initialPopulation = InputHandler.Instance.LocalPlayer!.selectedHorde!.GetComponent<PopulationController>()
                .initialPopulation;
            if (maxPop < 10)
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
            slider.minValue = initialPopulation;
            slider.maxValue = maxPop - initialPopulation;
            
        }

        private void OnEnable()
        {
            splitAmount = maxPop / 2;
            slider.value = splitAmount;
        }

        public void SplitHorde()
        {
            var horde = InputHandler.Instance.LocalPlayer?.selectedHorde;
            horde?.Player.SplitHorde(horde,(float)splitAmount / InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats);
            gameObject.SetActive(false);
            if (horde)
            {
                GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.HordeSplit, 1);
            }
        }
    }
}