using Horde;
using UnityEngine;

public class MovementIndicator : MonoBehaviour
{
    public HordeController controller;
    public LineRenderer lineRenderer;

    private bool _disabled = true;

    private void Start()
    {
        if (controller.player.IsLocal) Invoke(nameof(Enable), 3);
    }

    private void FixedUpdate()
    {
        if (_disabled) return;

        if (controller.GetBounds().center.x == 0 || controller.targetLocation.transform.position.x == 0) return;

        if ((controller.GetBounds().center - controller.targetLocation.transform.position).magnitude <
            controller.GetBounds().size.magnitude / 2)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 2;
        var position = controller.targetLocation.transform.position;
        var center = controller.GetBounds().center;
        var radius = controller.GetBounds().size.magnitude / 2 + 0.5f;
        var direction = (position - center).normalized;

        var positions = new[] { center + direction * radius, position };
        lineRenderer.SetPositions(positions);
    }

    private void Enable()
    {
        _disabled = false;
    }
}