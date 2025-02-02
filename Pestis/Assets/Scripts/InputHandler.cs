using Horde;
using JetBrains.Annotations;
using Players;
using POI;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [CanBeNull] public HumanPlayer LocalPlayer;
    private InputAction _cameraZoom;

    private Camera _mainCamera;

    private InputAction _moveCamAction;

    private void Awake()
    {
        _mainCamera = Camera.main;
        _moveCamAction = InputSystem.actions.FindAction("Navigate");
        _cameraZoom = InputSystem.actions.FindAction("ScrollWheel");
    }

    private void Update()
    {
        var mouse = Mouse.current;

        var moveCam = _moveCamAction.ReadValue<Vector2>();

        _mainCamera.transform.Translate(moveCam * (0.01f * _mainCamera.orthographicSize));

        var scroll = _cameraZoom.ReadValue<Vector2>();
        if (scroll.y != 0)
        {
            Vector2 oldTarget = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            _mainCamera.orthographicSize = Mathf.Clamp(_mainCamera.orthographicSize - scroll.y, 1, 50);
            Vector2 newTarget = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());

            _mainCamera.transform.Translate(oldTarget - newTarget);
        }

        if (mouse.middleButton.isPressed)
        {
            var oldPos = mouse.position.ReadValue() - mouse.delta.ReadValue();
            var newPos = mouse.position.ReadValue();

            Vector2 oldWorldPos = _mainCamera.ScreenToWorldPoint(oldPos);
            Vector2 newWorldPos = _mainCamera.ScreenToWorldPoint(newPos);

            _mainCamera.transform.Translate(oldWorldPos - newWorldPos);
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector3 mousePosition = mouse.position.ReadValue();
            var horde = DidWeClickHorde(mousePosition);
            if (horde)
                LocalPlayer?.SelectHorde(horde);
            else
                LocalPlayer?.DeselectHorde();
        }

        // If right-clicked, and local player is allowed to control the selected horde
        if (mouse.rightButton.wasPressedThisFrame && (LocalPlayer?.selectedHorde?.HasStateAuthority ?? false))
        {
            Vector3 mousePos = mouse.position.ReadValue();

            var clickedHorde = DidWeClickHorde(mousePos);

            if (DidWeClickPOI(mousePos, out var poiController))
            {
                LocalPlayer.selectedHorde.AttackPoi(poiController);
            }
            else if (clickedHorde)
            {
                LocalPlayer?.selectedHorde.AttackHorde(clickedHorde);
            }
            else
            {
                Vector2 position = _mainCamera.ScreenToWorldPoint(mouse.position.value);
                LocalPlayer?.MoveHorde(position);
            }
        }
    }

    private void OnMouseDown()
    {
        LocalPlayer?.DeselectHorde();
    }

    /// <summary>
    ///     Returns horde under mouse position, or null if no horde
    /// </summary>
    /// <param name="mousePos"></param>
    /// <returns></returns>
    [CanBeNull]
    public HordeController DidWeClickHorde(Vector2 mousePos)
    {
        var ray = _mainCamera.ScreenPointToRay(mousePos);
        var layerMask = LayerMask.GetMask("Rat Selection");
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        if (hit)
        {
            var rat = hit.collider.GetComponentInParent<RatController>();
            if (rat) return rat.GetHordeController();
        }

        return null;
    }

    /// <summary>
    ///     Returns POI under mouse position, or null if no POI
    /// </summary>
    /// <param name="mousePos"></param>
    /// <param name="poiController"></param>
    /// <returns></returns>
    public bool DidWeClickPOI(Vector2 mousePos, out POIController poiController)
    {
        var ray = _mainCamera.ScreenPointToRay(mousePos);
        var layerMask = LayerMask.GetMask("POI Selection");
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        if (hit)
        {
            var poi = hit.collider.GetComponentInParent<POIController>();
            if (poi)
            {
                poiController = poi;
                return true;
            }
        }

        poiController = null;
        return false;
    }
}