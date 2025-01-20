using System;
using System.IO.Pipes;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;

public class MapClickHandler : MonoBehaviour
{
    [CanBeNull] public HumanPlayer LocalPlayer;

    Camera m_Camera;
    void Awake()
    {
        m_Camera = Camera.main;
    }
    
    private void OnMouseDown()
    {
        LocalPlayer?.DeselectHorde();
    }
    
    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePosition = mouse.position.ReadValue();
            Ray ray = m_Camera.ScreenPointToRay(mousePosition);
            int layerMask = LayerMask.GetMask("Selection Detection");
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
            if (hit)
            {
                RatController rat = hit.collider.GetComponentInParent<RatController>();
                if (rat)
                {
                    LocalPlayer?.SelectHorde(rat.GetHordeController());
                }
                else
                {
                    LocalPlayer?.DeselectHorde();
                }
            } else {
                LocalPlayer?.DeselectHorde();
            }
        }

        if (mouse.rightButton.wasPressedThisFrame && (LocalPlayer?.SelectedHorde?.HasStateAuthority ?? false))
        {
            Vector2 position = m_Camera.ScreenToWorldPoint(mouse.position.value);
            LocalPlayer?.MoveHorde(position);
        }
    }
}
