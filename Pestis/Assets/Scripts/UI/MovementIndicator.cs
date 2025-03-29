using Horde;
using ProceduralToolkit;
using UnityEngine;

public class MovementIndicator : MonoBehaviour
{
    public HordeController controller;
    public LineRenderer lineRenderer;

    private void FixedUpdate()
    {
        if (controller.player.IsLocal)
        {
            if ((controller.GetCenter().ToVector3XY() - controller.targetLocation.transform.position).magnitude <
                controller.GetBounds().size.magnitude / 2)
            {
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            var position = controller.targetLocation.transform.position;
            Vector3 center = controller.GetCenter();
            var radius = controller.GetBounds().size.magnitude / 2 + 0.5f;
            var direction = (position - center).normalized;

            var positions = new[] { center + direction * radius, position };
            lineRenderer.SetPositions(positions);
        }
    }
}