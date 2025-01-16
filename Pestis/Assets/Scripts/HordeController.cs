using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HordeController : NetworkBehaviour
{
    public GameObject ratPrefab;

    [Networked, OnChangedRender(nameof(AliveRatsChanged))]
    public int AliveRats { get; set; } = 3;

    private List<GameObject> _spawnedRats = new List<GameObject>();
    
    void AliveRatsChanged()
    {
        int difference = AliveRats - _spawnedRats.Count;
        if (difference > 0)
        {
            // Spawn a Rat
            for (int i = 0; i < difference; i++)
            {
                GameObject rat = Instantiate(ratPrefab, this.transform.position, Quaternion.identity, this.transform);
                _spawnedRats.Add(rat);
            }
        } else if (difference < 0)
        {
            // Kill a Rat
            for (int i = 0; i > difference; i--)
            {
                Destroy(_spawnedRats[_spawnedRats.Count - 1 + i]);
                _spawnedRats.RemoveAt(_spawnedRats.Count - 1 + i);
            }
        }
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        foreach (GameObject rat in _spawnedRats)
        {
            // Slowly turn to face center of horde
            Vector3 direction = (this.transform.position - rat.transform.position );
            Vector3 rotatedVectorToTarget = Quaternion.Euler(0, 0, 0) * direction;
            Quaternion targetRotation = Quaternion.LookRotation(forward: Vector3.forward, upwards: rotatedVectorToTarget);
            rat.transform.rotation = Quaternion.RotateTowards(rat.transform.rotation, targetRotation, 90 * Time.deltaTime);
            // Head forward
            rat.transform.Translate(Vector3.up * (0.3f * Time.deltaTime), Space.Self);
        }
    }
}
