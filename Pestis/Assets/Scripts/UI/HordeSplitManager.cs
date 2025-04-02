using System;
using Objectives;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HordeSplitManager : MonoBehaviour
    {
        public TMP_Text selectedAmountText;
        public Slider slider;
        public Button button;
        public int spitRecommendedInt = 50;
        private int initialPopulation;
        private int maxPop;

        private int splitAmount;

        private void Start()
        {
            button.onClick.AddListener(SplitHorde);
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.value = 50;
        }

        private void Update()
        {
            if (!InputHandler.Instance.LocalPlayer || !InputHandler.Instance.LocalPlayer!.selectedHorde) return;

            maxPop = InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;
            if (maxPop < 10)
            {
                selectedAmountText.text = "Too small to split";
                button.interactable = false;
                return;
            }

            splitAmount = (int)Math.Floor(slider.value / 100.0f * maxPop);
            if (splitAmount < 5 || maxPop - splitAmount < 5)
            {
                selectedAmountText.text = "Too small to split with this amount";
                button.interactable = false;
                return;
            }

            button.interactable = true;


            selectedAmountText.text = $"{slider.value}%";
        }

        private void OnEnable()
        {
            slider.value = 50;
            splitAmount = (int)Math.Floor(slider.value / 100 * maxPop);
        }

        public void SplitHorde()
        {
            var horde = InputHandler.Instance.LocalPlayer?.selectedHorde;
            horde?.player.SplitHorde(horde,
                slider.value / 100);

            gameObject.SetActive(false);
            if (horde) GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.HordeSplit, 1);
        }
    }
}