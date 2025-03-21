using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UI
{
    public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string tooltipText;
        public GameObject tooltipObject;
        public GameObject tooltipInstance;
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            Vector3 mousePos = Mouse.current.position.ReadValue();
            Debug.Log("Pointer Entered");
            tooltipInstance = Instantiate(tooltipObject);
            tooltipInstance.transform.position =
                new Vector2(mousePos.x + tooltipObject.GetComponent<RectTransform>().sizeDelta.x * 2, mousePos.y);
            tooltipInstance.GetComponentInChildren<TextMeshProUGUI>().text = tooltipText;
            
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Destroy(tooltipInstance);
        }
    }
}