using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DetectBackgroundClick : MonoBehaviour
{
    public HumanPlayer LocalPlayer;

    Camera m_Camera;
    void Awake()
    {
        m_Camera = Camera.main;
    }
    
    private void OnMouseDown()
    {
        Debug.Log("clicked");
        LocalPlayer.DeselectHorde();
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
                    LocalPlayer.DeselectHorde();
                }
            }
        }
    }
}
