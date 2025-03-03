using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using JetBrains.Annotations;
using KaimiraGames;
using MoreLinq;
using Players;
using POI;
using TMPro;
using UI;
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
        public bool isHedgehogged = false;
        public GameObject moraleAndFearInstance;
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
        private EvolutionManager _evolutionManager;

        /// <summary>
        ///     How many rats we need to spawn to have the correct amount visible.
        /// </summary>
        private int _ratsToSpawn;

        private Light2D _selectionLightPoi;

        private Light2D _selectionLightTerrain;
        

        [CanBeNull] private Action OnArriveAtTarget;

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
        internal float TotalHealth { get; set; }

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
        private CombatController CurrentCombatController { get; set; }

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

            // Spawn at center of horde if there is one, or base if there isn't one yet.
            if (_spawnedRats.Count == 0)
                _hordeCenter = HordeBounds.center == Vector3.zero ? transform.position : HordeBounds.center;

            // Only spawn up to one rat each tick to avoid freezes
            if (_ratsToSpawn != 0)
            {
                // Spawn a Rat
                var rat = Instantiate(ratPrefab, _hordeCenter, Quaternion.identity, transform);
                var ratController = rat.GetComponent<RatController>();
                ratController.SetHordeController(this);
                ratController.SetColor(_hordeColor); //Apply horde color
                ratController.Start();
                _spawnedRats.Add(ratController);
                _ratsToSpawn--;
            }

            // Can't calculate the bounds of nothing
            if (_spawnedRats.Count == 0) return;


            devToolsTargetLocation = targetLocation.transform.position;

            // Calculate bounding box that contains all rats
            var b = new Bounds(_spawnedRats[0].transform.position, Vector2.zero);
            foreach (var rat in _spawnedRats) b.Encapsulate(rat.GetPosition());

            b.Expand(1.0f);

            // If we're the owner of this Horde, we are the authoritative source for the horde bounds
            if (HasStateAuthority) HordeBounds = b;

            _selectionLightTerrain.pointLightInnerRadius = b.extents.magnitude * 0.9f + 0.5f;
            _selectionLightTerrain.pointLightOuterRadius = b.extents.magnitude * 1.0f + 0.5f;
            _selectionLightTerrain.transform.position = b.center;


            _selectionLightPoi.pointLightInnerRadius = b.extents.magnitude * 0.9f + 0.5f;
            _selectionLightPoi.pointLightOuterRadius = b.extents.magnitude * 1.0f + 0.5f;
            _selectionLightPoi.transform.position = b.center;

            intraHordeTargets[0] = new Vector2(targetLocation.transform.position.x - b.extents.x * 0.65f,
                targetLocation.transform.position.y + b.extents.y * 0.65f);
            intraHordeTargets[1] = new Vector2(targetLocation.transform.position.x - b.extents.x * 0.65f,
                targetLocation.transform.position.y - b.extents.y * 0.65f);
            intraHordeTargets[2] = new Vector2(targetLocation.transform.position.x + b.extents.x * 0.65f,
                targetLocation.transform.position.y - b.extents.y * 0.65f);
            intraHordeTargets[3] = new Vector2(targetLocation.transform.position.x + b.extents.x * 0.65f,
                targetLocation.transform.position.y + b.extents.y * 0.65f);
            targetTolerance = b.extents.magnitude * 0.1f;

            _hordeCenter = b.center;
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

            if (InCombat)
            {
                var enemy = CurrentCombatController!.GetNearestEnemy(this);
                var damage = _populationController.GetState().Damage;
                if (_evolutionManager.GetEvolutionaryState().AcquiredEffects.Contains("unlock_septic_bite"))
                {
                    var septicMult = _populationController.GetState().SepticMult;
                    _populationController.SetSepticMult(septicMult * 1.005f);
                }
                if (enemy) // Could be no enemy if we just joined it
                {
                    // If we chose to be in combat, move towards enemy
                    if (CurrentCombatController.HordeIsVoluntary(this))
                        // Teleports target, not us
                        targetLocation.Teleport(enemy.GetBounds().center);

                    // If close enough, start dealing damage, and animating rats.
                    if (enemy.GetBounds().Intersects(HordeBounds))
                    {
                        enemy.DealDamageRpc(damage * _populationController.GetState().DamageMult * _populationController.GetState().SepticMult);
                        if (enemy.isHedgehogged) DealDamageRpc(0.001f);
                        HordeBeingDamaged = enemy;
                    }
                    else
                    {
                        HordeBeingDamaged = null;
                    }
                }
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
                    {
                        sortedByDistanceFromEnemy[sortedByDistanceFromEnemy.Count - 1 + i].Value.Kill();
                        IncreaseFear();
                    }
                    else
                        sortedByDistanceFromEnemy[sortedByDistanceFromEnemy.Count - 1 + i].Value.KillInstant();
                    indexesToRemove.Add(sortedByDistanceFromEnemy[sortedByDistanceFromEnemy.Count - 1 + i].Key);
                    
                }

                indexesToRemove.Sort();
                indexesToRemove.Reverse();
                foreach (var index in indexesToRemove) _spawnedRats.RemoveAt(index);
            }
        }

        private void IncreaseFear()
        {
            CooldownBar[] bars = moraleAndFearInstance.GetComponentsInChildren<CooldownBar>();
            if (bars[0].name == "FearBar")
            {
                if (bars[0].current == 0) return;
                bars[0].current -= 5;
                if (bars[0].current != 0) return;
                StartCoroutine(FearDebuff(bars[0]));

            }
            else
            {
                if (bars[1].current == 0) return;
                bars[1].current -= 5;
                if (bars[1].current != 0) return;
                StartCoroutine(FearDebuff(bars[1]));
            }
        }

        IEnumerator FearDebuff(CooldownBar bar)
        {
            GetComponent<AbilityController>().feared = true;
            var elapsedTime = 0.0f;
            while (elapsedTime < 10f)
            {
                elapsedTime += Time.deltaTime;
                bar.current = 0 + (int)(elapsedTime / 10 * bar.maximum);
                yield return null;
            }
            GetComponent<AbilityController>().feared = false;
        }

        public override void Spawned()
        {
            _populationController = GetComponent<PopulationController>();
            _evolutionManager = GetComponent<EvolutionManager>();
            Player = GetComponentInParent<Player>();


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
            moraleAndFearInstance = Instantiate(FindFirstObjectByType<UI_Manager>().fearAndMorale);
            foreach (CooldownBar bar in moraleAndFearInstance.GetComponentsInChildren<CooldownBar>())
            {
                bar.current = bar.maximum;
            }
            moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 0;
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
            TotalHealth -= damage * _populationController.GetState().DamageReduction 
                                  * _populationController.GetState().DamageReductionMult
                                  * _populationController.GetState().SepticMult;
            
            
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
            Vector3 baseCamp = transform.parent.position;
            POIController closestPOI = Player.ControlledPOIs.Aggregate((closest, poi) =>
                Vector3.Distance(HordeBounds.center, poi.transform.position) <
                Vector3.Distance(HordeBounds.center, closest.transform.position)
                    ? poi
                    : closest);

            if (Vector3.Distance(closestPOI.transform.position, HordeBounds.center) <
                Vector3.Distance(baseCamp, HordeBounds.center))
            {
                StationAtRpc(closestPOI);
            }
            else
            {
                targetLocation.Teleport(baseCamp);
                StationedAt = null;
            }
            HordeBeingDamaged = null;
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

        public void AttackHorde(HordeController target, string combatOption)
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

            if (target.InCombat) // If the target is already in combat, join it
            {
                target.CurrentCombatController!.AddHordeRpc(this, true);
            }
            else // Otherwise start new combat and add the target to it
            {
                CurrentCombatController = GetComponent<CombatController>();
                CurrentCombatController!.AddHordeRpc(this, true);
                CurrentCombatController.AddHordeRpc(target, false);
            }

            StartCoroutine(ApplyStrategy(combatOption));

        }

        IEnumerator ApplyStrategy(string action)
        {
            var oldAlive = AliveRats;
            var oldEnemyAlive = CurrentCombatController!.GetNearestEnemy(this).AliveRats;
            int poiCount = 0;
            float poiMult = 1.0f;
            switch (action)
            {
                case "Frontal Assault":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * 1.2f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * 1.2f);
                    break;
                case "Shock and Awe":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * 1.5f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * 1.5f);
                    _populationController.GetComponent<AbilityController>().abilityHaste += 10;
                    
                    yield return new WaitForSeconds(10f);
                    
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / 1.5f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / 1.5f);
                    _populationController.GetComponent<AbilityController>().abilityHaste -= 10;
                    break;
                case "Envelopment":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * (1 + oldAlive / 1000f));
                    break;
                case "Fortify":
                    Collider2D[] colliders = Physics2D.OverlapCircleAll(GetBounds().center, 20f);
                    foreach (var col in colliders)
                    {
                        POIController poi = col.GetComponentInParent<POIController>();
                        if (poi) poiCount++;
                    }

                    poiMult = poiCount == 0 ? 1f : poiCount * 1.3f;
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * (poiMult));
                    break;
                case "Hedgehog":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * 0.8f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * 0.8f);
                    isHedgehogged = true;
                    break;
                case "All Round":
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / (1f + 0.2f * Mathf.Log10(1f + oldEnemyAlive)));
                    break;
            }
            while (InCombat)
            {
                yield return null;
            }
            switch (action)
            {
                case "Frontal Assault":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / 1.2f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / 1.2f);
                    break;
                case "Envelopment":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / (1 + oldAlive / 1000f));
                    break;
                case "Hedgehog":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / 0.8f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / 0.8f);
                    isHedgehogged = false;
                    break;
                case "Fortify":
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / poiMult);
                    break;
                case "All Round":
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * (1f + 0.2f * Mathf.Log10(1f + oldEnemyAlive)));
                    break;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventWonCombatRpc(NetworkBehaviourId[] hordes)
        {
            Debug.Log($"We ({Object.Id}) won combat!");
            
            EvolutionaryState state = GetEvolutionState();
            WeightedList<ActiveMutation> newMutations = new WeightedList<ActiveMutation>();
            foreach (var hordeID in hordes)
            {
                CurrentCombatController.Runner.TryFindBehaviour(hordeID, out HordeController horde);
                if (horde.Id == hordeID) continue;
                state.PassiveEvolutions["attack"][1] = Math.Max(horde.GetPopulationState().Damage * 0.8,
                    state.PassiveEvolutions["attack"][1]);
                state.PassiveEvolutions["health"][1] = Math.Max(horde.GetPopulationState().HealthPerRat * 0.8,
                    state.PassiveEvolutions["health"][1]);
                state.PassiveEvolutions["defense"][1] = Math.Max(horde.GetPopulationState().DamageReduction * 0.8,
                    state.PassiveEvolutions["defense"][1]);
                foreach (var mut in horde.GetEvolutionState().AcquiredMutations)
                {
                    if (state.ActiveMutations.Contains(mut))
                    {
                        newMutations.Add(mut, 1);
                    }
                }
            }

            if (newMutations.Count > 0)
            {
                ActiveMutation newMutation = newMutations.Next();
                _evolutionManager.ApplyActiveEffects(newMutation);
                FindFirstObjectByType<UI_Manager>().AddNotification($"You acquired a mutation, {newMutation.MutationName}, from your enemy.", Color.red);
            }
            
            if (state.PassiveEvolutions != GetEvolutionState().PassiveEvolutions)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("In your conquests you have gained the strength of your subjects", Color.red);
            }
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

        public void SetPopulationInit(int initialPopulation)
        {
            _populationController.initialPopulation = initialPopulation;
        }

        public EvolutionaryState GetEvolutionState()
        {
            return _evolutionManager.GetEvolutionaryState();
        }

        public void SetEvolutionaryState(EvolutionaryState newState)
        {
            _evolutionManager.SetEvolutionaryState(newState);
        }
        
        public Vector2 GetCenter()
        {
            return _hordeCenter;
        }

        public void Select()
        {
            FindAnyObjectByType<InputHandler>().LocalPlayer?.SelectHorde(this);
        }
    }
}