using Fusion;
using Horde;
using UnityEngine;

public class Panner : MonoBehaviour
{

    public bool shouldPan = false;
    private const float TimeToMove = 1f;
    public Vector3 target;

    public void PanTo(HordeController horde)
    {
        if (!horde) return;
        shouldPan = true;
        target.x = horde.GetBounds().center.x;
        target.y = horde.GetBounds().center.y;
        target.z = -1;
    }
    
    // Update is called once per frame
    private void Update()
    {
        if (shouldPan)
        {
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime / TimeToMove);
            if (Vector3.Distance(transform.position, target) < 1f)
            {
                shouldPan = false;
            }
        }
    }
}
