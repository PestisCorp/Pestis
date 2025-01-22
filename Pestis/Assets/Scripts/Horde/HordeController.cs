using System.Collections.Generic;
using Fusion;
using JetBrains.Annotations;
using Players;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Horde
{
    public class HordeController : NetworkBehaviour
    {
        public Player Player;
        
        public GameObject ratPrefab;

        public int AliveRats => (int)(TotalHealth / _populationController.GetState().HealthPerRat);

        [Networked, OnChangedRender(nameof(TotalHealthChanged))] 
        internal float TotalHealth { get; set; }

        // Do not use or edit yourself, used to expose internals to Editor
        [SerializeField] private int devToolsTotalRats;
        [SerializeField] private float devToolsTotalHealth;
        [SerializeField] private Vector2 devToolsTargetLocation;

        // When inspector values change, update appropriate variables
        public void OnValidate()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            
            TotalHealth = _populationController.GetState().HealthPerRat * devToolsTotalRats;
            targetLocation.transform.position = devToolsTargetLocation;
        }

        /// <summary>
        /// Location rats are trying to get to, synced across network
        /// </summary>
        public NetworkTransform targetLocation;
        /// <summary>
        /// Mid-point of all the rats in the horde
        /// </summary>
        private Vector2 _hordeCenter;
        /// <summary>
        /// Distance a rat must get to its current intra-horde target to move onto the next one
        /// </summary>
        public float targetTolerance;
        /// <summary>
        /// Points in the horde that individual rats will cycle between moving towards, to create continuous movement
        /// </summary>
        public Vector2[] intraHordeTargets = new Vector2[4];

        private List<RatController> _spawnedRats = new List<RatController>();
        /// <summary>
        /// How many rats we need to spawn to have the correct amount visible.
        /// </summary>
        private int _ratsToSpawn = 0;

        private Light2D _selectionLight;
    
        private PopulationController _populationController;


        /// <summary>
        /// Update number of visible rats based on current health
        /// </summary>
        internal void TotalHealthChanged()
        {
            // Update values shown in inspector
            devToolsTotalRats = AliveRats;
            devToolsTotalHealth = TotalHealth;
            
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

        public override void Spawned()
        {
            _populationController = GetComponent<PopulationController>();
        }
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // Needed for if we join an in-progress game
            TotalHealthChanged();
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
            devToolsTargetLocation = targetLocation.transform.position;
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

            _selectionLight.pointLightInnerRadius = b.extents.magnitude*0.9f;
            _selectionLight.pointLightOuterRadius = b.extents.magnitude*1.0f;
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

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DealDamageRpc(float damage)
        {
            TotalHealth -= damage;
        }
    }
}
