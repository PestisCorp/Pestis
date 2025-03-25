using Fusion;
using Horde;
using UnityEngine;

public class Panner : MonoBehaviour
{

    public bool shouldPan = false;
    private const float TimeToMove = 1f;
    private HordeController _target;

    public void PanTo(HordeController horde)
    {
        if (!horde) return;
        shouldPan = true;
        _target = horde;
    }
    
    // Update is called once per frame
    private void Update()
    {
        if (shouldPan)
        {
            var center = _target.GetBounds().center;
            var targetPosition = new Vector3(center.x, center.y, -1);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime / TimeToMove);
            if (Vector3.Distance(transform.position, targetPosition) < 1f)
            {
                shouldPan = false;
            }
        }
    }
}
