using Horde;
using JetBrains.Annotations;
using Players;
using POI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance;

    [CanBeNull] public HumanPlayer LocalPlayer;
    
    private Camera _mainCamera;
    private InputAction _cameraZoom;
    private InputAction _moveCamAction;

    private void Awake()
    {
        Instance = this;
        _mainCamera = Camera.main;
        _moveCamAction = InputSystem.actions.FindAction("Navigate");
        _cameraZoom = InputSystem.actions.FindAction("ScrollWheel");
    }

    private void Update()
    {
        if (!LocalPlayer) return;

        var mouse = Mouse.current;
        
        var moveCam = _moveCamAction.ReadValue<Vector2>();

        _mainCamera.transform.Translate(moveCam * (0.025f * _mainCamera.orthographicSize));

        var scroll = _cameraZoom.ReadValue<Vector2>();
        // Map should not zoom if we are hovering over a UI element since we are using scroll boxes
        if (scroll.y != 0 && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector2 oldTarget = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());

            if (scroll.y < 0)
                _mainCamera.orthographicSize *= 2;
            else
                _mainCamera.orthographicSize /= 2;
            _mainCamera.orthographicSize = math.clamp(_mainCamera.orthographicSize, 1, 32);

            if (scroll.y > 0)
            {
                Vector2 newTarget = _mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                _mainCamera.transform.Translate(oldTarget - newTarget);
            }
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

            // Only select and deselect horde if we are not clicking on a UI element
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                var horde = DidWeClickHorde(mousePosition);
                if (horde)
                    LocalPlayer?.SelectHorde(horde);

                else
                    LocalPlayer?.DeselectHorde();
            }
        }

        // If right-clicked, and local player is allowed to control the selected horde
        if (mouse.rightButton.wasPressedThisFrame && (LocalPlayer?.selectedHorde?.player.IsLocal ?? false))
        {
            Vector3 mousePos = mouse.position.ReadValue();

            var clickedHorde = DidWeClickHorde(mousePos);

            if (MoveToPoiIfClicked(mousePos)) return;
            if (clickedHorde && clickedHorde.player != LocalPlayer?.selectedHorde.player)
            {
                Debug.Log("Attacking horde");
                LocalPlayer!.selectedHorde.AttackHorde(clickedHorde);
            }
            else if (!(clickedHorde && clickedHorde.player == LocalPlayer?.selectedHorde.player))
            {
                var position = _mainCamera.ScreenToWorldPoint(mouse.position.value);
                var tilemap = GameManager.Instance.terrainMap;
                var possibleCellPosition = tilemap.WorldToCell(position);
                possibleCellPosition.z = 0; // since tilemap is 2D

                if (tilemap.HasTile(possibleCellPosition)) LocalPlayer?.MoveHorde(position);
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
        var worldPoint = _mainCamera.ScreenToWorldPoint(mousePos);
        foreach (var player in GameManager.Instance.Players)
        foreach (var horde in player.Hordes)
            if (horde.Boids.PosInHorde(worldPoint, 2.0f, horde.GetBounds()))
                return horde;

        return null;
    }

    /// <summary>
    ///     Returns POI under mouse position, or null if no POI
    /// </summary>
    /// <param name="mousePos"></param>
    /// <param name="poiController"></param>
    /// <returns></returns>
    public bool DidWeClickPOI(Vector2 mousePos, out PoiController poiController)
    {
        var ray = _mainCamera.ScreenPointToRay(mousePos);
        var layerMask = LayerMask.GetMask("POI Selection");
        var hit = Physics2D.GetRayIntersection(ray, Mathf.Infinity, layerMask);
        if (hit)
        {
            var poi = hit.collider.GetComponentInParent<PoiController>();
            if (poi)
            {
                poiController = poi;
                return true;
            }
        }

        poiController = null;
        return false;
    }

    public bool MoveToPoiIfClicked(Vector2 mousePos)
    {
        if (DidWeClickPOI(mousePos, out var poiController))
        {
            // Already stationed at POI
            if (poiController.StationedHordes.Contains(LocalPlayer?.selectedHorde)) return false;

            LocalPlayer?.selectedHorde?.AttackPoi(poiController);

            return true;
        }

        return false;
    }
}