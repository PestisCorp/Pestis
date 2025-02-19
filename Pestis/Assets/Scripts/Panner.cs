using Fusion;
using UnityEngine;

public class Panner : MonoBehaviour
{

    public bool shouldPan;
    private const float TimeToMove = 1f;

    public Vector3 target;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        shouldPan = false;
    }

    // Update is called once per frame
    void Update()
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
