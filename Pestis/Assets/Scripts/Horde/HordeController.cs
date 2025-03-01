using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using JetBrains.Annotations;
using Players;
using POI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Horde
{
    public class HordeController : NetworkBehaviour

    {
        private static readonly Color[] predefinedHordeColors =
        {
            new(1.0f, 0.0f, 0.0f, 1.0f), // Red
            new(0.0f, 1.0f, 0.0f, 1.0f), // Green
            new(0.0f, 0.0f, 1.0f, 1.0f), // Blue
            new(1.0f, 1.0f, 0.0f, 1.0f), // Yellow
            new(1.0f, 0.5f, 0.0f, 1.0f), // Orange
            new(0.5f, 0.0f, 0.5f, 1.0f), // Purple
            new(0.0f, 1.0f, 1.0f, 1.0f), // Cyan
            new(1.0f, 0.0f, 1.0f, 1.0f) // Magenta
        };

        private static int nextColorIndex; // Tracks the next color index


        public Player Player;

        public GameObject ratPrefab;

        // Do not use or edit yourself, used to expose internals to Editor
        [SerializeField] private int devToolsTotalRats;
        [SerializeField] private float devToolsTotalHealth;
        [SerializeField] private Vector2 devToolsTargetLocation;

        /// <summary>
        ///     Location rats are trying to get to, synced across network
        /// </summary>
        public NetworkTransform targetLocation;

        /// <summary>
        ///     Distance a rat must get to its current intra-horde target to move onto the next one
        /// </summary>
        public float targetTolerance;

        /// <summary>
        ///     Points in the horde that individual rats will cycle between moving towards, to create continuous movement
        /// </summary>
        public Vector2[] intraHordeTargets = new Vector2[4];

        /// <summary>
        ///     Seconds until we can start simulating population again after combat.
        /// </summary>
        public float PopulationCooldown;

        private readonly List<RatController> _spawnedRats = new();
        private Camera _camera;

        /// <summary>
        ///     Mid-point of all the rats in the horde
        /// </summary>
        private Vector2 _hordeCenter;

        private GameObject _playerText;

        private PopulationController _populationController;

        /// <summary>
        ///     How many rats we need to spawn to have the correct amount visible.
        /// </summary>
        private int _ratsToSpawn;

        private Light2D _selectionLightPoi;

        private Light2D _selectionLightTerrain;

        [CanBeNull] private Action OnArriveAtTarget;

        public RatBoids boids { get; private set; }

        /// <summary>
        ///     Time in seconds since game start when the horde last finished combat
        /// </summary>
        public float lastInCombat { get; private set; }

        /// <summary>
        ///     The horde we're currently damaging. Our rats will animate against them.
        /// </summary>
        [Networked]
        internal HordeController HordeBeingDamaged { get; set; }

        public int AliveRats => (int)Mathf.Max(TotalHealth / _populationController.GetState().HealthPerRat, 1.0f);

        [Networked]
        [OnChangedRender(nameof(TotalHealthChanged))]
        internal float TotalHealth { get; set; } = 25.0f;

        /// <summary>
        ///     Bounds containing every rat in Horde
        /// </summary>
        [Networked]
        private Bounds HordeBounds { set; get; }

        [Networked] [CanBeNull] public POIController StationedAt { get; private set; }

        [Networked] [CanBeNull] public POIController TargetPoi { get; private set; }

        [Networked] private Color _hordeColor { get; set; }

        [Networked] private int HordeColorIndex { get; set; } // Track assigned color index

        /// <summary>
        ///     Can only be in one combat instance at a time.
        /// </summary>
        [Networked]
        [CanBeNull]
        public CombatController CurrentCombatController { get; private set; }

        public bool InCombat => CurrentCombatController && CurrentCombatController.HordeInCombat(this);

        private void Awake()
        {
            _hordeCenter = transform.position;
            _camera = Camera.main;
        }

        private void Update()
        {
            if (_playerText)
                _playerText.transform.position = _camera.WorldToScreenPoint(HordeBounds.center);
        }

        private void FixedUpdate()
        {
            // If not spawned yet
            if (!Object.IsValid) return;

            if (PopulationCooldown > 0)
                PopulationCooldown -= Time.deltaTime;
            else if (PopulationCooldown < 0) PopulationCooldown = 0;

            if (OnArriveAtTarget != null && HordeBounds.Contains(targetLocation.transform.position))
            {
                OnArriveAtTarget();
                OnArriveAtTarget = null;
            }

            boids.AliveRats = AliveRats;
            boids.TargetPos = targetLocation.transform.position;

            // If we're the owner of this Horde, we are the authoritative source for the horde bounds
            if (HasStateAuthority)
            {
                if (AliveRats == 1)
                {
                    HordeBounds = boids.GetBounds();
                }
                else if (AliveRats > 0) // Move horde center slowly to avoid jitter due to center rat changing
                {
                    var newBounds = boids.GetBounds();
                    newBounds.center = Vector2.Lerp(HordeBounds.center, newBounds.center, Time.deltaTime);
                    HordeBounds = newBounds;
                    HordeBounds = boids.GetBounds();
                }
                else
                {
                    HordeBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(0, 0, 0));
                }
            }

            _selectionLightTerrain.pointLightInnerRadius = HordeBounds.extents.magnitude * 0.9f + 0.5f;
            _selectionLightTerrain.pointLightOuterRadius = HordeBounds.extents.magnitude * 1.0f + 0.5f;
            _selectionLightTerrain.transform.position = HordeBounds.center;


            _selectionLightPoi.pointLightInnerRadius = HordeBounds.extents.magnitude * 0.9f + 0.5f;
            _selectionLightPoi.pointLightOuterRadius = HordeBounds.extents.magnitude * 1.0f + 0.5f;
            _selectionLightPoi.transform.position = HordeBounds.center;

            _hordeCenter = HordeBounds.center;
        }

#if UNITY_EDITOR
        [DrawGizmo(GizmoType.Selected ^ GizmoType.NonSelected)]
        public void OnDrawGizmos()
        {
            if (!Object) return;
            var centeredStyle = GUI.skin.GetStyle("Label");
            centeredStyle.alignment = TextAnchor.MiddleCenter;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(HordeBounds.center, HordeBounds.size);
            Handles.Label(HordeBounds.center, $@"{Player.Username}
{Object.Id}
{(HasStateAuthority ? "Local" : "Remote")}
Combat: {InCombat}
Horde Target: {(HordeBeingDamaged ? HordeBeingDamaged.Object.Id : "None")}
Stationed At {(StationedAt ? StationedAt.Object.Id : "None")}
POI Target {(TargetPoi ? TargetPoi.Object.Id : "None")}
Count: {AliveRats}
");
            HandleUtility.Repaint();
        }
#endif

        // When inspector values change, update appropriate variables
        public void OnValidate()
        {
            if (!Application.isPlaying) return;

            // Don't allow changes, or it'll keep overwriting the horde's health
            if (InCombat) return;

            TotalHealth = _populationController.GetState().HealthPerRat * devToolsTotalRats;
            targetLocation.transform.position = devToolsTargetLocation;
        }


        public override void FixedUpdateNetwork()
        {
            CheckArrivedAtPoi();
            CheckArrivedAtCombat();

            if (InCombat && CurrentCombatController!.boids.containedHordes.Contains(this))
            {
                var enemyHordes =
                    CurrentCombatController!.boids.containedHordes.Where(horde => horde.Player != Player).ToArray();

                foreach (var enemy in enemyHordes)
                    // Split damage dealt among enemy hordes
                    enemy.DealDamageRpc(GetPopulationState().Damage / enemyHordes.Length);
            }
        }

        /// <summary>
        ///     Check if we've arrived at the target POI.
        ///     If we have then handle combat initation/POI takeover.
        /// </summary>
        private void CheckArrivedAtPoi()
        {
            // Not targeting a POI
            if (!TargetPoi) return;

            // We're already in combat, so no need to potentially start/join a new one!
            if (InCombat) return;

            // Not at POI yet
            if (!HordeBounds.Intersects(TargetPoi.Collider.bounds)) return;

            // We already control the POI, no need to start combat, we can just station ourselves again.
            if (TargetPoi.ControlledBy == Player)
            {
                StationAtRpc(TargetPoi);
                return;
            }

            // If the POI isn't being defended, we can just take over without combat.
            if (TargetPoi.StationedHordes.Count == 0)
            {
                TargetPoi.ChangeController(Player);
                StationAtRpc(TargetPoi);
                return;
            }

            // Arrived at POI, let's attack it!
            Debug.Log("Arrived at POI, initiating combat!");
            TargetPoi.AttackRpc(this);
            TargetPoi = null;
        }

        /// <summary>
        ///     Check if we've arrived at the combat we're in.
        ///     If we have, transfer control of our boids over to the combat controller.
        /// </summary>
        private void CheckArrivedAtCombat()
        {
            if (!CurrentCombatController) return;

            if (!HordeBounds.Intersects(CurrentCombatController.bounds)) return;

            // Already arrived at combat
            if (boids.paused) return;

            boids.JoinCombat(CurrentCombatController.boids, this);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void StationAtRpc(POIController poi)
        {
            Debug.Log($"Adding myself (horde {Object.Id}) to POI: {poi.Object.Id}");
            poi.AttackRpc(this);
            Move(poi.transform.position);
            StationedAt = poi;
            // We're not targeting a POI if we've just taken one over.
            TargetPoi = null;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UnStationAtRpc()
        {
            if (!StationedAt) throw new Exception("Tried to unstation, but not stationed anywhere!");
            StationedAt.UnStationHordeRpc(this);
            StationedAt = null;
        }


        /// <summary>
        ///     Update number of visible rats based on current health
        /// </summary>
        internal void TotalHealthChanged()
        {
            // Update values shown in inspector
            devToolsTotalRats = AliveRats;
            devToolsTotalHealth = TotalHealth;

            if (AliveRats < 0)
                // Initial value is bad
                return;

            var difference = AliveRats - _spawnedRats.Count;
            if (difference > 0)
            {
                _ratsToSpawn = difference;
            }
            else if (difference < 0)
            {
                var sortedByDistanceFromEnemy = _spawnedRats
                    .Select((rat, i) => new KeyValuePair<int, RatController>(i, rat)).OrderBy(kvp =>
                        -((Vector2)kvp.Value.transform.position - kvp.Value.targetPoint).sqrMagnitude).ToList();

                List<int> indexesToRemove = new();
                // Kill a Rat
                for (var i = 0; i > difference; i--)
                {
                    // Only leave corpse if in combat
                    if (InCombat)
                        sortedByDistanceFromEnemy[sortedByDistanceFromEnemy.Count - 1 + i].Value.Kill();
                    else
                        sortedByDistanceFromEnemy[sortedByDistanceFromEnemy.Count - 1 + i].Value.KillInstant();
                    indexesToRemove.Add(sortedByDistanceFromEnemy[sortedByDistanceFromEnemy.Count - 1 + i].Key);
                }

                indexesToRemove.Sort();
                indexesToRemove.Reverse();
                foreach (var index in indexesToRemove) _spawnedRats.RemoveAt(index);
            }
        }

        public override void Spawned()
        {
            _populationController = GetComponent<PopulationController>();
            Player = GetComponentInParent<Player>();
            boids = GetComponentInChildren<RatBoids>();

            if (HasStateAuthority) // Ensure only the host assigns colors
            {
                HordeColorIndex = (int)Object.Id.Raw % predefinedHordeColors.Length;
                _hordeColor =
                    predefinedHordeColors
                        [HordeColorIndex]; // Assign color based on index
            }

            _selectionLightTerrain = transform.Find("SelectionLightTerrain").gameObject.GetComponent<Light2D>();
            _selectionLightPoi = transform.Find("SelectionLightPOI").gameObject.GetComponent<Light2D>();
            if (!Player.IsLocal)
            {
                _selectionLightPoi.color = Color.red;
                _selectionLightTerrain.color = Color.red;
            }

            targetLocation = transform.Find("TargetLocation").gameObject.GetComponent<NetworkTransform>();

            _playerText = transform.Find("Canvas/PlayerName").gameObject;
            var text = _playerText.GetComponentInChildren<TMP_Text>();
            text.text = Player.Username;
            if (Player.IsLocal) text.color = Color.red;

            // Needed to spawn in rats from joined session
            TotalHealthChanged();
        }


        public void Highlight()
        {
            _selectionLightTerrain.enabled = true;
            _selectionLightPoi.enabled = true;
        }

        public void UnHighlight()
        {
            _selectionLightTerrain.enabled = false;
            _selectionLightPoi.enabled = false;
        }

        public void Move(Vector2 target)
        {
            targetLocation.Teleport(target);
            if (StationedAt)
            {
                StationedAt.UnStationHordeRpc(this);
                StationedAt = null;
            }

            if (CurrentCombatController)
            {
                CurrentCombatController.EventRetreatRpc(this);
                CurrentCombatController = null;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DealDamageRpc(float damage)
        {
            Debug.Log($"Damage Reduction: {_populationController.GetState().DamageReduction}");
            TotalHealth -= damage * _populationController.GetState().DamageReduction;
        }

        public Bounds GetBounds()
        {
            return HordeBounds;
        }

        //get the color of the horde
        public Color GetHordeColor()
        {
            return _hordeColor;
        }

        public RatController ClosestRat(Vector2 pos)
        {
            RatController bestTarget = null;
            var closestDistance = Mathf.Infinity;

            foreach (var rat in _spawnedRats)
            {
                var dist = ((Vector2)rat.transform.position - pos).sqrMagnitude;

                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestTarget = rat;
                }
            }

            return bestTarget;
        }

        /// <summary>
        ///     Run for your furry little lives to the nearest friendly POI
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RetreatRpc()
        {
            Debug.Log("Retreating!");
            // For now just retreat to spawn base
            targetLocation.Teleport(transform.parent.position);
            HordeBeingDamaged = null;
            StationedAt = null;
            CurrentCombatController = null;
            PopulationCooldown = 15.0f;
            lastInCombat = Time.time;
        }

        public void AttackPoi(POIController poi)
        {
            Debug.Log("Attacking POI");
            if (CurrentCombatController)
                // Leave current combat
                CurrentCombatController.EventRetreatRpc(this);

            if (StationedAt)
            {
                StationedAt.UnStationHordeRpc(this);
                StationedAt = null;
            }

            // Logic to attack TargetPoi is located in CheckArrivedPoi
            TargetPoi = poi;
            targetLocation.Teleport(poi.transform.position);
        }

        public void AttackHorde(HordeController target)
        {
            // Don't fight if we're below 10 rats
            if (TotalHealth < 10 * _populationController.GetState().HealthPerRat) return;

            // We're already fighting that horde!
            if (CurrentCombatController && CurrentCombatController.HordeInCombat(target)) return;

            if (CurrentCombatController)
                // Leave current combat
                CurrentCombatController.EventRetreatRpc(this);

            if (StationedAt)
            {
                StationedAt.UnStationHordeRpc(this);
                StationedAt = null;
            }

            TargetPoi = null;

            targetLocation.Teleport(target.HordeBounds.center);
            if (target.InCombat) // If the target is already in combat, join it
            {
                target.CurrentCombatController!.AddHordeRpc(this, true);
            }
            else // Otherwise start new combat and add the target to it
            {
                CurrentCombatController =
                    Runner.Spawn(GameManager.Instance.CombatControllerPrefab).GetComponent<CombatController>();
                CurrentCombatController!.AddHordeRpc(this, true);
                CurrentCombatController.AddHordeRpc(target, false);
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventWonCombatRpc()
        {
            Debug.Log($"We ({Object.Id}) won combat!");
            CurrentCombatController = null;
            HordeBeingDamaged = null;
            PopulationCooldown = 20.0f;
            lastInCombat = Time.time;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventAttackedRpc(CombatController combat)
        {
            CurrentCombatController = combat;
        }

        public PopulationState GetPopulationState()
        {
            return _populationController.GetState();
        }

        public void SetPopulationState(PopulationState newState)
        {
            _populationController.SetState(newState);
        }

        public void Select()
        {
            FindAnyObjectByType<InputHandler>().LocalPlayer?.SelectHorde(this);
        }

        /// <summary>
        ///     Sent to *all* machines so they can update their local boid sims
        /// </summary>
        /// <param name="combat"></param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void AddBoidsToCombatRpc(CombatController combat)
        {
            boids.JoinCombat(combat.boids, this);
        }
    }
}