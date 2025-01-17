using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Random = System.Random;

public class HordeController : NetworkBehaviour
{
    
    public GameObject ratPrefab;

    [Networked, OnChangedRender(nameof(AliveRatsChanged))]
    public int AliveRats { get; set; } = 3;

    private List<GameObject> _spawnedRats = new List<GameObject>();
    private int _ratsToSpawn = 0;
    
    
    
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
        // Needed for if we
        AliveRatsChanged();
    }

    // Update is called once per frame
    void Update()
    {
        
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
    
    // Population manager. Update birth and death rates here.
    public class PopulationController
    {
        private int _initialPopulation;
        private double _birthRate;
        private double _deathRate;
        private HordeController _hordeController;
        private Random _random;

        public PopulationController(double birthRate, double deathRate, HordeController hordeController)
        {
            _birthRate = birthRate;
            _deathRate = deathRate;
            _hordeController = hordeController;
            _random = new Random();
        }

        void PopulationEvent()
        {
            double rMax = _birthRate + _deathRate;
            
            double r = _random.NextDouble() * rMax;
            if (r < _birthRate)
            {
                _hordeController.AliveRats++;
            }

            if ((_birthRate <= r) && (r < (_birthRate + _deathRate)))
            {
                _hordeController.AliveRats--;
            }
        }

    }
}
