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
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Debug.Log("Pointer Entered");
            tooltipInstance = Instantiate(tooltipObject, mousePos, Quaternion.identity);
            tooltipInstance.transform.position = mousePos;
            tooltipInstance.GetComponentInChildren<TextMeshProUGUI>().text = tooltipText;
            
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Destroy(tooltipInstance);
        }
    }
}