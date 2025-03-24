using Horde;
using ProceduralToolkit;
using ProceduralToolkit.LibTessDotNet;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UIElements;

public class MovementIndicator : MonoBehaviour
{
    public HordeController controller;
    public LineRenderer lineRenderer;
    private void FixedUpdate()
    {
        if (controller.Player.IsLocal )
        {
            if ((controller.GetCenter().ToVector3XY() - controller.targetLocation.transform.position).magnitude < controller.GetBounds().size.magnitude / 2)
            { lineRenderer.enabled = false; return; }

            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            Vector3 position = controller.targetLocation.transform.position;
            Vector3 center  = controller.GetCenter();
            float radius = controller.GetBounds().size.magnitude/2 + 0.5f;
            Vector3 direction = (position - center).normalized;

            Vector3[] positions = new Vector3[] { center + direction * radius, position };
            lineRenderer.SetPositions(positions);
        }
    }
}
