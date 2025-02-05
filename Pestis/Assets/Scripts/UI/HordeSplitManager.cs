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
        private float _splitPercentage;

        private int SplitAmount =>
            (int)(_splitPercentage * InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats);

        private void Update()
        {
            if (!InputHandler.Instance.LocalPlayer || !InputHandler.Instance.LocalPlayer!.selectedHorde) return;
            maxAmountText.text = $"{InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats}";
            selectedAmountText.text = $"{SplitAmount}";
            // Minimum 1 rat in new horde
            slider.minValue = 1.0f / InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats;
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