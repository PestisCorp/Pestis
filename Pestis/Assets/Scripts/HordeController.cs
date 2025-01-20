using System;
using System.Collections.Generic;
using Fusion;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Random = System.Random;

public class HordeController : NetworkBehaviour
{
    private static int StartingRatCount = 5; 
    
    public GameObject ratPrefab;

    [Networked, OnChangedRender(nameof(AliveRatsChanged))]
    public int AliveRats { get; set; }

    public NetworkTransform targetLocation;
    private Vector2 _hordeCenter;
    public float targetTolerance;
    /// <summary>
    /// Points in the horde that individual rats will cycle between moving towards, to create continuous movement
    /// </summary>
    public Vector2[] intraHordeTargets = new Vector2[4];

    private List<RatController> _spawnedRats = new List<RatController>();
    private int _ratsToSpawn = 0;

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
                Destroy(_spawnedRats[_spawnedRats.Count - 1 + i].transform.gameObject);
                _spawnedRats.RemoveAt(_spawnedRats.Count - 1 + i);
            }
        }
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (HasStateAuthority)
        {
            _populationController = new PopulationController(0.001, 0.0009, this);
        }
        
        // Needed for if we join an in-progress game
        AliveRatsChanged();
        _selectionLight = GetComponentInChildren<Light2D>();
        if (!HasStateAuthority)
        {
            _selectionLight.color = Color.red;
        }
        targetLocation = transform.Find("TargetLocation").gameObject.GetComponent<NetworkTransform>();
    }

    public void Highlight()
    {
        _selectionLight.enabled = true;
    }

    public void UnHighlight()
    {
        _selectionLight.enabled = false;
    }

    void Update()
    {
        if (_spawnedRats.Count == 0)
        {
            _hordeCenter = transform.position;
            return;
        }
        
        // Calculate bounding box that contains all rats
        Bounds b = new Bounds(_spawnedRats[0].transform.position, Vector2.zero);
        foreach (RatController rat in _spawnedRats)
        {
            b.Encapsulate(rat.GetBounds());
        }

        _selectionLight.pointLightInnerRadius = b.extents.magnitude*0.8f;
        _selectionLight.pointLightOuterRadius = b.extents.magnitude*0.9f;
        _selectionLight.transform.position = b.center;

        intraHordeTargets[0] = new Vector2(targetLocation.transform.position.x - b.extents.x * 0.65f, targetLocation.transform.position.y + b.extents.y * 0.65f);
        intraHordeTargets[1] = new Vector2(targetLocation.transform.position.x - b.extents.x * 0.65f, targetLocation.transform.position.y - b.extents.y * 0.65f);
        intraHordeTargets[2] = new Vector2(targetLocation.transform.position.x + b.extents.x * 0.65f, targetLocation.transform.position.y - b.extents.y * 0.65f);
        intraHordeTargets[3] = new Vector2(targetLocation.transform.position.x + b.extents.x * 0.65f, targetLocation.transform.position.y + b.extents.y * 0.65f);
        targetTolerance = b.extents.magnitude * 0.1f;

        _hordeCenter = b.center;
    }

    private void FixedUpdate()
    {
        // Only spawn up to one rat each tick to avoid freezes
        if (_ratsToSpawn != 0)
        {
            // Spawn a Rat
            GameObject rat = Instantiate(ratPrefab, _hordeCenter, Quaternion.identity, this.transform);
            RatController ratController = rat.GetComponent<RatController>();
            ratController.SetHordeController(this);
            ratController.Start();
            _spawnedRats.Add(ratController);
            _ratsToSpawn--;
        }
    }

    public void Move(Vector2 target)
    {
        targetLocation.Teleport( target);
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
