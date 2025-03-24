using Horde;
using UnityEngine;

public class MovementIndicator : MonoBehaviour
{
    public HordeController controller;
    public LineRenderer lineRenderer;
    private void FixedUpdate()
    {
        if (controller.Player.IsLocal)
        {
            lineRenderer.enabled = true;
            lineRenderer.positionCount = 2;
            Vector3[] positions = new Vector3[] { controller.targetLocation.transform.position, controller.GetCenter() };
            lineRenderer.SetPositions(positions);
        }
    }
}
