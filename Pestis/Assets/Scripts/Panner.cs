using Fusion;
using UnityEngine;

public class Panner : MonoBehaviour
{

    public bool shouldPan = false;
    private const float TimeToMove = 1f;
    public Vector3 target;


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
