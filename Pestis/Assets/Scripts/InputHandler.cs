using System;
using System.IO.Pipes;
using Horde;
using JetBrains.Annotations;
using Players;
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

    /// <summary>
    /// Returns horde under mouse position, or null if no horde
    /// </summary>
    /// <param name="mousePos"></param>
    /// <returns></returns>
    [CanBeNull]
    public HordeController DidWeClickHorde(Vector3 mousePos)
    {
        Ray ray = _mainCamera.ScreenPointToRay(mousePos);
        int layerMask = LayerMask.GetMask("Selection Detection");
        RaycastHit2D hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        if (hit)
        {
            RatController rat = hit.collider.GetComponentInParent<RatController>();
            if (rat)
            {
                return rat.GetHordeController();
            }
        }

        return null;
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
            _mainCamera.orthographicSize = Mathf.Clamp(_mainCamera.orthographicSize - scroll.y, 1, 50);
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
            HordeController horde = DidWeClickHorde(mousePosition);
            if (horde)
            {
                LocalPlayer?.SelectHorde(horde);
            }
            else
            {
                LocalPlayer?.DeselectHorde();
            }
        }

        // If right-clicked, and local player is allowed to control the selected horde
        if (mouse.rightButton.wasPressedThisFrame && (LocalPlayer?.selectedHorde?.HasStateAuthority ?? false))
        {
            Vector3 mousePos = mouse.position.ReadValue();

            HordeController clickedHorde = DidWeClickHorde(mousePos);

            if (clickedHorde)
            {
                if (!LocalPlayer!.player.InCombat())
                {
                    LocalPlayer?.player.JoinHordeToCombat(LocalPlayer?.selectedHorde);
                }
                LocalPlayer?.player.JoinHordeToCombat(clickedHorde);
            }
            else
            {
                Vector2 position = _mainCamera.ScreenToWorldPoint(mouse.position.value);
                LocalPlayer?.MoveHorde(position);
            }

        }
    }
}
