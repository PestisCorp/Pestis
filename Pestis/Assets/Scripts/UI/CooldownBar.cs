using System;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CooldownBar : MonoBehaviour
    {
        public int maximum;
        public int current;
        public Image mask;


        private void Update()
        {
            GetCurrentFill();
        }

        private void GetCurrentFill()
        {
            float fillAmount = (float)current / maximum;
            mask.fillAmount = fillAmount;
        }
    
    }
}

