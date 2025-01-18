using System;
using System.Collections.Generic;
using Fusion;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

public class HordeController : NetworkBehaviour
{
    private static int StartingRatCount = 5; 
    
    public GameObject ratPrefab;

    [Networked, OnChangedRender(nameof(AliveRatsChanged))]
    public int AliveRats { get; set; }

    public NetworkTransform TargetLocation;

    private List<GameObject> _spawnedRats = new List<GameObject>();
    private int _ratsToSpawn = 0;

    private bool _highlighted = false;
    private Light2D _selectionLight;
    
    [CanBeNull] private PopulationController _populationController;
    
    
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
        if (HasStateAuthority)
        {
            _populationController = new PopulationController(0.000000000005, 0.0000000000048, this);
        }
        
        // Needed for if we join an in-progress game
        AliveRatsChanged();
        _selectionLight = GetComponentInChildren<Light2D>();
        if (!HasStateAuthority)
        {
            _selectionLight.color = Color.red;
        }
        TargetLocation = transform.Find("TargetLocation").gameObject.GetComponent<NetworkTransform>();
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
            Bounds b = new Bounds(GetComponentInChildren<SpriteRenderer>().transform.position, Vector2.zero);
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
            rat.GetComponent<Rigidbody2D>().mass = UnityEngine.Random.Range(0.8f, 1.2f); 
            _spawnedRats.Add(rat);
            _ratsToSpawn--;
        }
        
        foreach (GameObject rat in _spawnedRats)
        {
            // Slowly turn to face center of horde
            Vector3 direction = (TargetLocation.transform.position - rat.transform.position );
            Vector3 rotatedVectorToTarget = Quaternion.Euler(0, 0, 0) * direction;
            Quaternion targetRotation = Quaternion.LookRotation(forward: Vector3.forward, upwards: rotatedVectorToTarget);
            rat.transform.rotation = Quaternion.RotateTowards(rat.transform.rotation, targetRotation, 90 * Time.deltaTime);
            // Head forward
            rat.transform.Translate(Vector3.up * (0.3f * Time.deltaTime), Space.Self);
        }
    }

    public void Move(Vector2 target)
    {
        TargetLocation.Teleport( target);
    }
    
    public override void FixedUpdateNetwork()
    {
        _populationController?.PopulationEvent();
    }

    // Population manager. Update birth and death rates here
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
        
        // Check for birth or death events
        public void PopulationEvent()
        {
            double rMax = 1;
            
            double r = _random.NextDouble() * rMax; // Pick which event should happen
            // A birth event occurs here
            if (r < _birthRate || _hordeController.AliveRats < StartingRatCount)
            {
                _hordeController.AliveRats++;
            }
            // Death event occurs here
            if ((_birthRate <= r) && (r < (_birthRate + _deathRate)))
            {
                _hordeController.AliveRats--;
            }
        }
    }
}
