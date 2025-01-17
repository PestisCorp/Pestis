using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class HordeController : NetworkBehaviour
{
    public GameObject ratPrefab;

    [Networked, OnChangedRender(nameof(AliveRatsChanged))]
    public int AliveRats { get; set; } = 3;

    private List<GameObject> _spawnedRats = new List<GameObject>();
    private int _ratsToSpawn = 0;

    private bool _highlighted = false;
    private Light2D _selectionLight;
    
    void AliveRatsChanged()
    {
        int difference = AliveRats - _spawnedRats.Count;
        if (difference > 0)
        {
            _ratsToSpawn = difference;
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
        // Needed for if we join an in-progress game
        AliveRatsChanged();
        _selectionLight = GetComponentInChildren<Light2D>();
    }

    public void Highlight()
    {
        _highlighted = true;
        _selectionLight.enabled = true;
    }

    public void UnHighlight()
    {
        _highlighted = false;
        _selectionLight.enabled = false;
    }

    void Update()
    {
        if (_highlighted)
        {
            // Calculate bounding box that contains all rats
            Bounds b = new Bounds(transform.position, Vector2.zero);
            foreach (SpriteRenderer rat in GetComponentsInChildren<SpriteRenderer>())
            {
                b.Encapsulate(rat.bounds);
            }

            _selectionLight.pointLightInnerRadius = b.extents.magnitude;
            _selectionLight.pointLightOuterRadius = b.extents.magnitude + 1;
            _selectionLight.transform.position = b.center;
        }
    }

    private void FixedUpdate()
    {
        // Only spawn up to one rat each tick to avoid freezes
        if (_ratsToSpawn != 0)
        {
            // Spawn a Rat
            GameObject rat = Instantiate(ratPrefab, this.transform.position, Quaternion.identity, this.transform);
            _spawnedRats.Add(rat);
            _ratsToSpawn--;
        }
        
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
