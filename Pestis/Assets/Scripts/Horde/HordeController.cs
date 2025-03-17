using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Human;
using JetBrains.Annotations;
using KaimiraGames;
using Objectives;
using Players;
using POI;
using TMPro;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Horde
{
    public enum CombatOptions
    {
        FrontalAssault,
        ShockAndAwe,
        Envelopment,
        Fortify,
        Hedgehog,
        AllRound
    }


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
        public bool isHedgehogged;
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

        public bool isApparition;

        /// <summary>
        ///     Seconds until we can start simulating population again after combat.
        /// </summary>
        public float PopulationCooldown;

        [SerializeField] private GameObject hordeIcon;
        [SerializeField] private Sprite enemyIcon;
        [SerializeField] private Sprite ownIcon;

        [SerializeField] private GameObject speechBubble;

        [SerializeField] private PopulationController _populationController;

        private readonly List<RatController> _spawnedRats = new();

        private readonly Queue<Sprite> _speechBubbles = new();

        [CanBeNull] private PatrolController _attackingPatrol;
        private Camera _camera;

        [CanBeNull] private string _combatStrategy;
        private GameObject _combatText;
        private EvolutionManager _evolutionManager;

        /// <summary>
        ///     Mid-point of all the rats in the horde
        /// </summary>
        private Vector2 _hordeCenter;

        private GameObject _playerText;

        private Light2D _selectionLightPoi;

        private Light2D _selectionLightTerrain;
        private bool _speechBubbleActive;

        [CanBeNull] private HordeController _targetHorde;

        private CooldownBar[] fearAndMoraleBars;


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

        [Networked] internal float TotalHealth { get; set; } = 25.0f;

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
                    if (InCombat && CurrentCombatController!.boids.containedHordes.Contains(this))
                        HordeBounds = CurrentCombatController!.boids.GetBounds(this);
                    else
                        HordeBounds = boids.GetBounds();
                }
                else
                {
                    HordeBounds = new Bounds(targetLocation.transform.position, Vector3.zero);
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

        private void OnDestroy()
        {
            Player.Hordes.Remove(this);
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

            if (_attackingPatrol)
            {
                var damageToDeal = AliveRats / 50.0f * (GetPopulationState().Damage
                                                        * GetPopulationState().DamageMult
                                                        * GetPopulationState().SepticMult);
                _attackingPatrol.DealDamageRpc(damageToDeal);
            }

            if (InCombat && CurrentCombatController!.boids.containedHordes.Contains(this))
            {
                if (_evolutionManager.GetEvolutionaryState().AcquiredEffects.Contains("unlock_septic_bite"))
                {
                    var septicMult = _populationController.GetState().SepticMult;
                    _populationController.SetSepticMult(septicMult * 1.005f);
                }

                var enemyHordes =
                    CurrentCombatController!.boids.containedHordes.Where(horde => horde.Player != Player).ToArray();

                foreach (var enemy in enemyHordes)
                {
                    // Split damage dealt among enemy hordes
                    float bonusDamage = 0;
                    if (GetEvolutionState().AcquiredEffects.Contains("unlock_necrosis"))
                        bonusDamage += CurrentCombatController.boids.totalDeathsPerHorde.GetValueOrDefault(this, 0) *
                                       0.1f;

                    if (enemy.GetEvolutionState().AcquiredEffects.Contains("unlock_mentat"))
                    {
                        var random = Random.Range(0f, 1f);
                        if (random < 0.05) return;
                    }

                    var damageToDeal = AliveRats / 50.0f * ((GetPopulationState().Damage
                                                                * GetPopulationState().DamageMult
                                                                * GetPopulationState().SepticMult + bonusDamage)
                                                            / enemyHordes.Length);
                    enemy.DealDamageRpc(damageToDeal);
                    Player.TotalDamageDealt += damageToDeal;
                    if (enemy.isHedgehogged) DealDamageRpc(0.001f);
                }
            }
        }

        public void AddSpeechBubble(Sprite bubble)
        {
            _speechBubbles.Enqueue(bubble);
            if (!_speechBubbleActive) StartCoroutine(ShowSpeechBubble());
        }

        private IEnumerator ShowSpeechBubble()
        {
            if (_speechBubbles.Count == 0) yield break;
            _speechBubbleActive = true;
            var bubble = _speechBubbles.Dequeue();
            var icon = speechBubble.GetComponent<Image>();
            icon.sprite = bubble;
            yield return new WaitForSeconds(5f);
            icon.sprite = null;
            if (_speechBubbles.Count > 0)
                StartCoroutine(ShowSpeechBubble());
            else
                _speechBubbleActive = false;
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
            if (TargetPoi.StationedHordes.Count == 0 && TargetPoi.patrolController.HumanCount == 0)
            {
                var icon = Resources.Load<Sprite>("UI_design/Emotes/defend_emote");
                AddSpeechBubble(icon);
                TargetPoi.ChangeController(Player);
                StationAtRpc(TargetPoi);
                return;
            }

            if (TargetPoi.patrolController.HumanCount != 0)
            {
                _attackingPatrol = TargetPoi.patrolController;
                TargetPoi.patrolController.AttackRpc(this);
                TargetPoi = null;
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
            // Not targeting a horde
            if (!_targetHorde) return;

            // Already in combat
            if (CurrentCombatController) return;

            if (!HordeBounds.Intersects(_targetHorde.HordeBounds)) return;

            // Already arrived at combat
            if (boids.paused) return;

            _combatText.SetActive(true);
            if (_targetHorde.InCombat) // If the target is already in combat, join it
            {
                _targetHorde.CurrentCombatController!.AddHordeRpc(this, true);
            }
            else // Otherwise start new combat and add the target to it
            {
                CurrentCombatController =
                    Runner.Spawn(GameManager.Instance.CombatControllerPrefab).GetComponent<CombatController>();
                CurrentCombatController!.AddHordeRpc(this, true);
                CurrentCombatController.AddHordeRpc(_targetHorde, false);
            }

            Enum.TryParse(_combatStrategy!.Replace(" ", ""), out CombatOptions option);
            StartCoroutine(ApplyStrategy(option));
            if (_targetHorde.GetEvolutionState().AcquiredEffects.Contains("unlock_price_of_war"))
            {
                Player.RemoveCheeseRpc(Player.CurrentCheese * 0.1f);
                _targetHorde.Player.AddCheeseRpc(Player.CurrentCheese * 0.1f);
            }

            AddBoidsToCombatRpc(CurrentCombatController);
            _combatStrategy = null;
            _targetHorde = null;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void StationAtRpc(POIController poi)
        {
            Debug.Log($"Adding myself (horde {Object.Id}) to POI: {poi.Object.Id}");
            _attackingPatrol = null;
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


        public void IncreaseFear()
        {
            if (fearAndMoraleBars[0].name == "FearBar")
            {
                if (fearAndMoraleBars[0].current == 0) return;
                fearAndMoraleBars[0].current -= 2;
                if (fearAndMoraleBars[0].current != 0) return;
                StartCoroutine(FearDebuff(fearAndMoraleBars[0]));
            }
            else
            {
                if (fearAndMoraleBars[1].current == 0) return;
                fearAndMoraleBars[1].current -= 2;
                if (fearAndMoraleBars[1].current != 0) return;
                StartCoroutine(FearDebuff(fearAndMoraleBars[1]));
            }
        }

        private IEnumerator FearDebuff(CooldownBar bar)
        {
            var icon = Resources.Load<Sprite>("UI_design/Emotes/traumatised_emote");
            AddSpeechBubble(icon);
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
            boids = GetComponentInChildren<RatBoids>();
            Player.Hordes.Add(this);

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
            _combatText = transform.Find("Canvas/PlayerName/Combat").gameObject;

            var text = _playerText.transform.Find("Border/Background/Text").GetComponent<TMP_Text>();

            text.text = Player.Username;
            hordeIcon = transform.Find("Canvas/PlayerName/HordeIcon").gameObject;
            var icon = hordeIcon.GetComponent<Image>();


            speechBubble = transform.Find("Canvas/PlayerName/SpeechBubble").gameObject;

            if (Player.IsLocal)
            {
                var iconSprite = Resources.Load<Sprite>("UI_design/HordeIcons/rat_skull_self");
                text.color = Color.red;
                icon.sprite = iconSprite;
            }
            else
            {
                var iconSprite = Resources.Load<Sprite>("UI_design/HordeIcons/rat_skull_enemy");

                icon.sprite = iconSprite;
            }

            moraleAndFearInstance = Instantiate(FindFirstObjectByType<UI_Manager>().fearAndMorale);
            foreach (var bar in moraleAndFearInstance.GetComponentsInChildren<CooldownBar>()) bar.current = bar.maximum;
            moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 0;
            // Needed to spawn in rats from joined session
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
            TotalHealth -= damage * _populationController.GetState().DamageReduction
                                  * _populationController.GetState().DamageReductionMult;
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

            CurrentCombatController = null;
            _attackingPatrol = null;

            var baseCamp = transform.parent.position;
            if (Player.ControlledPOIs.Count != 0)
            {
                var closestPOI = Player.ControlledPOIs.Aggregate((closest, poi) =>
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
            }
            else
            {
                targetLocation.Teleport(baseCamp);
                StationedAt = null;
            }

            HordeBeingDamaged = null;
            PopulationCooldown = 15.0f;
            lastInCombat = Time.time;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void TeleportHordeRPC(Vector3 target)
        {
            boids.TeleportHorde(target, GetBounds());
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
            {
                Debug.Log($"HORDE {Object.Id}: Retreating from combat in order to start new one!");
                // Leave current combat
                CurrentCombatController.EventRetreatRpc(this);
            }


            if (StationedAt)
            {
                StationedAt.UnStationHordeRpc(this);
                StationedAt = null;
            }

            TargetPoi = null;

            targetLocation.Teleport(target.HordeBounds.center);
            _targetHorde = target;
            _combatStrategy = combatOption;
            var icon = Resources.Load<Sprite>("UI_design/Emotes/attack_emote");
            AddSpeechBubble(icon);
            moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 1;
            GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.CombatStarted, 1);
        }

        private IEnumerator ApplyStrategy(CombatOptions action)
        {
            var oldAlive = AliveRats;
            var oldEnemyAlive = CurrentCombatController!.GetNearestEnemy(this).AliveRats;
            var poiCount = 0;
            var poiMult = 1.0f;
            switch (action)
            {
                case CombatOptions.FrontalAssault:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * 1.2f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * 1.2f);
                    break;
                case CombatOptions.ShockAndAwe:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * 1.5f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * 1.5f);
                    _populationController.GetComponent<AbilityController>().abilityHaste += 10;

                    yield return new WaitForSeconds(10f);

                    _populationController.SetDamageMult(GetPopulationState().DamageMult / 1.5f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / 1.5f);
                    _populationController.GetComponent<AbilityController>().abilityHaste -= 10;
                    break;
                case CombatOptions.Envelopment:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * (1 + oldAlive / 1000f));
                    break;
                case CombatOptions.Fortify:
                    var colliders = Physics2D.OverlapCircleAll(GetBounds().center, 20f);
                    foreach (var col in colliders)
                    {
                        var poi = col.GetComponentInParent<POIController>();
                        if (poi) poiCount++;
                    }

                    poiMult = poiCount == 0 ? 1f : poiCount * 1.3f;
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * poiMult);
                    break;
                case CombatOptions.Hedgehog:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult * 0.8f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult * 0.8f);
                    isHedgehogged = true;
                    break;
                case CombatOptions.AllRound:
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult /
                                                                 (1f + 0.2f * Mathf.Log10(1f + oldEnemyAlive)));
                    break;
            }

            while (InCombat) yield return null;
            switch (action)
            {
                case CombatOptions.FrontalAssault:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / 1.2f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / 1.2f);
                    break;
                case CombatOptions.Envelopment:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / (1 + oldAlive / 1000f));
                    break;
                case CombatOptions.Hedgehog:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / 0.8f);
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult / 0.8f);
                    isHedgehogged = false;
                    break;
                case CombatOptions.Fortify:
                    _populationController.SetDamageMult(GetPopulationState().DamageMult / poiMult);
                    break;
                case CombatOptions.AllRound:
                    _populationController.SetDamageReductionMult(GetPopulationState().DamageReductionMult *
                                                                 (1f + 0.2f * Mathf.Log10(1f + oldEnemyAlive)));
                    break;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventWonCombatRpc(NetworkBehaviourId[] hordes)
        {
            Debug.Log($"We ({Object.Id}) won combat!");


            var state = GetEvolutionState();
            var newMutations = new WeightedList<ActiveMutation>();
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
                    if (state.ActiveMutations.Contains(mut))
                        newMutations.Add(mut, 1);
            }

            if (newMutations.Count > 0)
            {
                var newMutation = newMutations.Next();
                _evolutionManager.ApplyActiveEffects(newMutation);
                FindFirstObjectByType<UI_Manager>()
                    .AddNotification($"You acquired a mutation, {newMutation.MutationName}, from your enemy.",
                        Color.red);
            }

            if (state.PassiveEvolutions != GetEvolutionState().PassiveEvolutions)
                FindFirstObjectByType<UI_Manager>()
                    .AddNotification("In your conquests you have gained the strength of your subjects", Color.red);
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


        /// <summary>
        ///     Sent to *all* machines so they can update their local boid sims
        /// </summary>
        /// <param name="combat"></param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void AddBoidsToCombatRpc(CombatController combat)
        {
            Debug.Log($"HORDE {Object.Id} of {Player.Username}: Joining boids to combat");
            boids.JoinCombat(combat.boids, this);
            _combatText.SetActive(true);
        }

        /// <summary>
        ///     Despawns the current horde
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DestroyHordeRpc()
        {
            Player.Hordes.Remove(this);
            Runner.Despawn(Object);
        }

        /// <summary>
        ///     Sent to *all* machines so they can update their local boid sims
        /// </summary>
        /// <param name="combat"></param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RemoveBoidsFromCombatRpc(CombatController combat)
        {
            Debug.Log($"HORDE of {Player.Username}: Removing boids from combat");
            boids.GetBoidsBack(combat, this);
            _combatText.SetActive(false);
        }

        /// <summary>
        ///     Split our boids, sending some to the other horde
        /// </summary>
        /// <param name="other">The other horde controller that should receive boids</param>
        /// <param name="numToSplit">How many boids to send to the other horde</param>
        /// <param name="numBoids">How many boids we have on the state authority</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void SplitBoidsRpc(HordeController other, int numToSplit, int numBoids)
        {
            boids.SplitBoids(numBoids, numToSplit, other.boids);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void CreateApparitionRPC(HordeController other, int numToClone)
        {
            boids.CreateApparition(other.boids, numToClone);
        }
    }
}