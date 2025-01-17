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
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray);
            if (hit)
            {
                // If player clicked anything other 
                if (! hit.collider.GetComponentInParent<RatController>())
                {
                    LocalPlayer?.DeselectHorde();
                }
            }
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            Vector2 position = m_Camera.ScreenToWorldPoint(mouse.position.value);
            Debug.Log(LocalPlayer);
            LocalPlayer?.MoveHorde(position);
        }
    }
}
