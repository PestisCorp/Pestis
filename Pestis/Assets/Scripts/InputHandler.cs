using System;
using System.IO.Pipes;
using Horde;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [CanBeNull] public HumanPlayer LocalPlayer;

    private Camera _mainCamera;

    private InputAction _moveCamAction;
    private InputAction _cameraZoom;
    
    void Awake()
    {
        _mainCamera = Camera.main;
        _moveCamAction = InputSystem.actions.FindAction("Navigate");
        _cameraZoom = InputSystem.actions.FindAction("ScrollWheel");
    }
    
    private void OnMouseDown()
    {
        LocalPlayer?.DeselectHorde();
    }
    
    void Update()
    {
        Mouse mouse = Mouse.current;

        Vector2 moveCam = _moveCamAction.ReadValue<Vector2>();
        
        _mainCamera.transform.Translate(moveCam * (0.01f * _mainCamera.orthographicSize));
        
        Vector2 scroll = _cameraZoom.ReadValue<Vector2>();
        if (scroll.y != 0)
        {
            Vector2 oldTarget = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            _mainCamera.orthographicSize = Mathf.Clamp(_mainCamera.orthographicSize - scroll.y, 1, Mathf.Infinity);
            Vector2 newTarget = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            
            _mainCamera.transform.Translate(oldTarget - newTarget);
        }

        if (mouse.middleButton.isPressed)
        {
            Vector2 oldPos = mouse.position.ReadValue() - mouse.delta.ReadValue();
            Vector2 newPos = mouse.position.ReadValue();

            Vector2 oldWorldPos = _mainCamera.ScreenToWorldPoint(oldPos);
            Vector2 newWorldPos = _mainCamera.ScreenToWorldPoint(newPos);
            
            _mainCamera.transform.Translate((oldWorldPos - newWorldPos));
        }
        
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePosition = mouse.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);
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
            Vector2 position = _mainCamera.ScreenToWorldPoint(mouse.position.value);
            LocalPlayer?.MoveHorde(position);
        }
    }
}
