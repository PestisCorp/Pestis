using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UI
{
    public class CombatOptionInfo : MonoBehaviour, IPointerEnterHandler
    {

        public string optionText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var optionInfo = GameObject.FindGameObjectWithTag("combat_info").GetComponentInChildren<TextMeshProUGUI>();
            optionInfo.text = optionText;
        }
    }
}

