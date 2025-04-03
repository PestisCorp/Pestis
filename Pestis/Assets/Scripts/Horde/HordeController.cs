using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat;
using Fusion;
using Human;
using JetBrains.Annotations;
using KaimiraGames;
using Networking;
using Objectives;
using Players;
using POI;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Bounds = UnityEngine.Bounds;
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

    public enum EmoteType
    {
        CombatLoss,
        Attack,
        DamageBuff,
        DamageDebuff,
        DamageReductionBuff,
        DamageReductionDebuff,
        Defend,
        Evolution,
        Hungry,
        Traumatised,
        Victory
    }

    public class HordeController : NetworkBehaviour
    {
        private static Dictionary<EmoteType, Sprite> EmoteSprites = new();
        [SerializeField] private Vector2 devToolsTargetLocation;
        [SerializeField] private float devToolsTotalHealth;

        // Do not use or edit yourself, used to expose internals to Editor
        [SerializeField] private int devToolsTotalRats;
        [SerializeField] private Sprite enemyIcon;

        [SerializeField] private GameObject hordeIcon;

        public bool isApparition;
        public bool isHedgehogged;
        [SerializeField] private Sprite ownIcon;

        public Player player;

        [SerializeField] private PopulationController populationController;

        /// <summary>
        ///     Seconds until we can start simulating population again after combat.
        /// </summary>
        public float populationCooldown;

        [SerializeField] private GameObject speechBubble;

        /// <summary>
        ///     Location rats are trying to get to, synced across network
        /// </summary>
        public NetworkTransform targetLocation;

        private readonly Queue<EmoteType> _speechBubbles = new();

        private float _aliveRatsRemainder;

        [CanBeNull] private PatrolController _attackingPatrol;
        private Camera _camera;

        [CanBeNull] private string _combatStrategy;
        private GameObject _combatText;
        private EvolutionManager _evolutionManager;

        /// <summary>
        ///     Mid-point of all the rats in the horde
        /// </summary>
        private Vector2 _hordeCenter;

        [CanBeNull] private Action _onArriveAtTarget;

        private GameObject _playerText;

        private Light2D _selectionLightPoi;

        private Light2D _selectionLightTerrain;
        private bool _speechBubbleActive;
        private Image _speechBubbleImage;

        [CanBeNull] private HordeController _targetHorde;

        public RatBoids Boids { get; private set; }

        /// <summary>
        ///     Time in seconds since game start when the horde last finished combat
        /// </summary>
        public float LastInCombat { get; private set; }

        [Networked] public IntPositive AliveRats { get; set; }

        /// <summary>
        ///     Will be slightly inaccurate if the client you're accessing this on isn't the horde's state authority
        /// </summary>
        internal float TotalHealth
        {
            get => (Convert.ToSingle(AliveRats) + _aliveRatsRemainder) * populationController.GetState().HealthPerRat;
            set
            {
                AliveRats = new IntPositive(
                    Convert.ToUInt32(Mathf.FloorToInt(value / populationController.GetState().HealthPerRat)));
                _aliveRatsRemainder = value % populationController.GetState().HealthPerRat /
                                      populationController.GetState().HealthPerRat;
            }
        }

        /// <summary>
        ///     Bounds containing every rat in Horde
        /// </summary>
        [Networked]
        private Networking.Bounds HordeBoundsNetworked { set; get; }

        private Bounds HordeBounds
        {
            set => HordeBoundsNetworked = value;

            get => HordeBoundsNetworked;
        }

        [CanBeNull] [Networked] public PoiController StationedAt { get; private set; }

        [CanBeNull] public PoiController TargetPoi { get; private set; }

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
            _speechBubbleImage = speechBubble.GetComponentInChildren<Image>();
            if (EmoteSprites.Count == 0)
                EmoteSprites = new Dictionary<EmoteType, Sprite>
                {
                    { EmoteType.Attack, Resources.Load<Sprite>("UI_design/Emotes/attack_emote") },
                    { EmoteType.CombatLoss, Resources.Load<Sprite>("UI_design/Emotes/combat_loss_emote") },
                    { EmoteType.DamageBuff, Resources.Load<Sprite>("UI_design/Emotes/damage_buff_emote") },
                    { EmoteType.DamageDebuff, Resources.Load<Sprite>("UI_design/Emotes/damage_debuff_emote") },
                    {
                        EmoteType.DamageReductionBuff,
                        Resources.Load<Sprite>("UI_design/Emotes/damage_reduction_buff_emote")
                    },
                    {
                        EmoteType.DamageReductionDebuff,
                        Resources.Load<Sprite>("UI_design/Emotes/damage_reduction_debuff_emote")
                    },
                    { EmoteType.Defend, Resources.Load<Sprite>("UI_design/Emotes/defend_emote") },
                    { EmoteType.Evolution, Resources.Load<Sprite>("UI_design/Emotes/evolution_emote") },
                    { EmoteType.Hungry, Resources.Load<Sprite>("UI_design/Emotes/hungry_emote") },
                    { EmoteType.Traumatised, Resources.Load<Sprite>("UI_design/Emotes/traumatised_emote") },
                    { EmoteType.Victory, Resources.Load<Sprite>("UI_design/Emotes/victory_emote") }
                };
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

            if (_combatText.activeSelf &&
                (!CurrentCombatController || CurrentCombatController.state == CombatState.Finished))
                Debug.LogError($"HORDE {Object.Id} Combat text active but no combat");

            if (populationCooldown > 0)
                populationCooldown -= Time.deltaTime;
            else if (populationCooldown < 0) populationCooldown = 0;

            if (_onArriveAtTarget != null && HordeBounds.Contains(targetLocation.transform.position))
            {
                _onArriveAtTarget();
                _onArriveAtTarget = null;
            }

            Boids.AliveRats = AliveRats;
            Boids.TargetPos = targetLocation.transform.position;

            // If we're the owner of this Horde, we are the authoritative source for the horde bounds
            if (HasStateAuthority)
            {
                if (AliveRats == 1)
                {
                    if (Boids.Bounds.HasValue) HordeBounds = Boids.Bounds.Value;
                }
                else if (AliveRats > 0) // Move horde center slowly to avoid jitter due to center rat changing
                {
                    if (InCombat && CurrentCombatController!.boids.containedHordes.Contains(this))
                    {
                        if (CurrentCombatController!.boids.hordeBounds.TryGetValue(this, out var newBounds))
                            HordeBounds = newBounds;
                    }
                    else
                    {
                        if (Boids.Bounds.HasValue) HordeBounds = Boids.Bounds.Value;
                    }
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

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            if (!Object) return;
            var centeredStyle = GUI.skin.GetStyle("Label");
            centeredStyle.alignment = TextAnchor.MiddleCenter;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(HordeBounds.center, HordeBounds.size);
            Handles.Label(HordeBounds.center, $@"{player.Username}
{Object.Id}
{(HasStateAuthority ? "Local" : "Remote")}
Combat: {InCombat}
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

            TotalHealth = populationController.GetState().HealthPerRat * devToolsTotalRats;
            targetLocation.transform.position = devToolsTargetLocation;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // If player's Network Object is null, it's been despawned too!
            if (player.Object) player.Hordes.Remove(this);
            if (CurrentCombatController) CurrentCombatController.PlayerLeftRpc(player.usernameOffline);
            Debug.Log($"HORDE: Destroyed self, hasState: {hasState}");
        }


        public override void FixedUpdateNetwork()
        {
            CheckArrivedAtPoi();
            CheckArrivedAtCombat();

            if (_attackingPatrol)
            {
                var damageToDeal = (uint)AliveRats / 50.0f * (GetPopulationState().Damage
                                                              * GetPopulationState().DamageMult
                                                              * GetPopulationState().SepticMult);
                _attackingPatrol.DealDamageRpc(damageToDeal);
            }

            if (InCombat && CurrentCombatController!.boids.containedHordes.Contains(this))
            {
                if (_evolutionManager.GetEvolutionaryState().AcquiredEffects.Contains("unlock_septic_bite"))
                {
                    var septicMult = populationController.GetState().SepticMult;
                    populationController.SetSepticMultRpc(septicMult * 1.0001f);
                }

                var enemyHordes =
                    CurrentCombatController!.boids.containedHordes.Select(id =>
                    {
                        if (!Runner.TryFindBehaviour<HordeController>(id, out var horde))
                            throw new NullReferenceException("Couldn't find horde from combat");

                        return horde;
                    }).Where(horde => horde.player != player).ToArray();

                foreach (var enemy in enemyHordes)
                {
                    // Split damage dealt among enemy hordes
                    float bonusDamage = 0;
                    if (GetEvolutionState().AcquiredEffects.Contains("unlock_necrosis"))
                        bonusDamage += CurrentCombatController.boids.totalDeathsPerHorde.GetValueOrDefault(this, 0) *
                                       0.1f;

                    if (enemy.GetEvolutionState().AcquiredEffects.Contains("unlock_mentat")
                        || enemy.GetEvolutionState().AcquiredEffects.Contains("unlock_eyeless"))
                    {
                        var random = Random.Range(0f, 1f);
                        if (random < 0.05) return;
                    }

                    if (GetEvolutionState().AcquiredEffects.Contains("unlock_eyeless"))
                    {
                        var random = Random.Range(0f, 1f);
                        if (random < 0.01) return;
                    }

                    var damageToDeal = (uint)AliveRats / 50.0f * ((GetPopulationState().Damage
                                                                      * GetPopulationState().DamageMult
                                                                      * GetPopulationState().SepticMult + bonusDamage)
                                                                  / enemyHordes.Length);
                    enemy.DealDamageRpc(damageToDeal);
                    player.TotalDamageDealt += damageToDeal;
                    if (enemy.isHedgehogged) DealDamageRpc(0.001f);
                    if (enemy.GetEvolutionState().AcquiredEffects.Contains("unlock_blood_pocket"))
                    {
                        var random = Random.Range(0f, 1f);
                        if (random < 0.05) DealDamageRpc(1f);
                    }
                }
            }
        }

        public void AddSpeechBubbleRpc(EmoteType type)
        {
            _speechBubbles.Enqueue(type);
            if (!_speechBubbleActive) StartCoroutine(ShowSpeechBubble());
        }

        private IEnumerator ShowSpeechBubble()
        {
            if (_speechBubbles.Count == 0) yield break;
            _speechBubbleActive = true;
            _speechBubbleImage.enabled = true;
            var type = _speechBubbles.Dequeue();
            _speechBubbleImage.sprite = EmoteSprites[type];
            yield return new WaitForSeconds(5f);
            _speechBubbleImage.sprite = null;
            if (_speechBubbles.Count > 0)
            {
                StartCoroutine(ShowSpeechBubble());
            }
            else
            {
                _speechBubbleActive = false;
                _speechBubbleImage.enabled = false;
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
            if (TargetPoi.ControlledBy == player)
            {
                StationAtRpc(TargetPoi);
                return;
            }

            // If the POI isn't being defended, we can just take over without combat.
            if (TargetPoi.StationedHordes.Count == 0 && TargetPoi.patrolController.HumanCount == 0)
            {
                AddSpeechBubbleRpc(EmoteType.Defend);
                TargetPoi.ChangeControllerRpc(player);
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
            if (Boids.paused) return;

            _combatText.SetActive(true);
            if (_targetHorde.InCombat) // If the target is already in combat, join it
            {
                CurrentCombatController = _targetHorde.CurrentCombatController;
                _targetHorde.CurrentCombatController!.AddHordeRpc(this);
            }
            else // Otherwise start new combat and add the target to it
            {
                CurrentCombatController =
                    Runner.Spawn(GameManager.Instance.CombatControllerPrefab).GetComponent<CombatController>();
                CurrentCombatController!.AddHordeRpc(this);
                CurrentCombatController.AddHordeRpc(_targetHorde);
            }


            Enum.TryParse(_combatStrategy!.Replace(" ", ""), out CombatOptions option);
            StartCoroutine(ApplyStrategy(option));
            if (_targetHorde.GetEvolutionState().AcquiredEffects.Contains("unlock_price_of_war"))
            {
                player.RemoveCheeseRpc(player.CurrentCheese * 0.1f);
                _targetHorde.player.AddCheeseRpc(player.CurrentCheese * 0.1f);
            }
            
            _targetHorde = null;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void StationAtRpc(PoiController poi)
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

        public override void Spawned()
        {
            populationController = GetComponent<PopulationController>();
            _evolutionManager = GetComponent<EvolutionManager>();
            player = GetComponentInParent<Player>();
            Boids = GetComponentInChildren<RatBoids>();
            player.Hordes.Add(this);
            _combatStrategy = "Frontal Assault";
            _selectionLightTerrain = transform.Find("SelectionLightTerrain").gameObject.GetComponent<Light2D>();
            _selectionLightPoi = transform.Find("SelectionLightPOI").gameObject.GetComponent<Light2D>();

            _selectionLightPoi.color = _selectionLightTerrain.color;
            _selectionLightPoi.intensity = _selectionLightTerrain.intensity;
            
            targetLocation = transform.Find("TargetLocation").gameObject.GetComponent<NetworkTransform>();

            Canvas canvas = transform.Find("Canvas").gameObject.GetComponent<Canvas>();
            _playerText = transform.Find("Canvas/PlayerName").gameObject;
            _combatText = transform.Find("Canvas/PlayerName/Combat").gameObject;

            var text = _playerText.transform.Find("Border/Background/Text").GetComponent<TMP_Text>();
            var textBackground = _playerText.transform.Find("Border/Background").GetComponent<Image>();
            var textSize = _playerText.transform.Find("Border/Background/Text").GetComponent<Transform>();

            text.text = player.Username;
            hordeIcon = transform.Find("Canvas/PlayerName/HordeIcon").gameObject;
            var icon = hordeIcon.GetComponent<Image>();


            if (player.IsLocal)
            {
                canvas.sortingOrder = 0;
                
                var iconSprite = Resources.Load<Sprite>("UI_design/HordeIcons/rat_skull_self");
                icon.sprite = iconSprite;
                icon.color = new Color(1f, 1f, 1f);
                hordeIcon.transform.localScale = new Vector3(1f, 1f, 1f);

                textBackground.color = new Color(0.9137255f, 0.7568628f, 0.4666667f);
                textSize.localScale = new Vector3(1f, 1f, 1f);
                
                GameManager.Instance.UIManager.AbilityBars[this] =
                    Instantiate(GameManager.Instance.UIManager.abilityToolbar,
                        GameManager.Instance.UIManager.abilityPanel.transform);

                GameManager.Instance.UIManager.AbilityBars[this].transform.localPosition = Vector3.zero;
            }
            else
            {
                canvas.sortingOrder = -1;
                
                var iconSprite = Resources.Load<Sprite>("UI_design/HordeIcons/rat_skull_enemy");
                icon.sprite = iconSprite;
                icon.color = new Color(0.85f, 0.85f, 0.85f);
                hordeIcon.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                
                textBackground.color = new Color(0.6f, 0f, 0f);
                textSize.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            }
            
            Boids.SetBoidsMat();
            if (CurrentCombatController)
            {
                var boids = new Boid[AliveRats];
                
                for (var i = 0; i < AliveRats; i++)
                {
                    boids[i].pos = new float2(HordeBounds.center.x, HordeBounds.center.y);
                    boids[i].vel = new float2(1.0f / (int)AliveRats, 1.0f / (int)AliveRats);
                }

                Boids.Start();
                CurrentCombatController.boids.Start();
                Boids.SetBoids(boids);
                Boids.JoinCombat(CurrentCombatController.boids, this);
            }
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
                CurrentCombatController.EventRetreatDesiredRpc(this);
                CurrentCombatController = null;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DealDamageRpc(float damage)
        {
            TotalHealth -= damage * populationController.GetState().DamageReduction
                                  * populationController.GetState().DamageReductionMult;
        }

        public Bounds GetBounds()
        {
            return HordeBounds;
        }


        /// <summary>
        ///     Run for your furry little lives to the nearest friendly POI
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RetreatRpc()
        {
            Debug.Log($"HORDE {Object.Id}: Told to retreat!");

            CurrentCombatController = null;
            _attackingPatrol = null;

            if (player.IsLocal) GameManager.Instance.PlaySfx(SoundEffectType.BattleEnd);

            var baseCamp = transform.parent.position;
            if (player.ControlledPOIs.Count != 0)
            {
                var closestPoi = player.ControlledPOIs.Aggregate((closest, poi) =>
                    Vector3.Distance(HordeBounds.center, poi.transform.position) <
                    Vector3.Distance(HordeBounds.center, closest.transform.position)
                        ? poi
                        : closest);
                if (Vector3.Distance(closestPoi.transform.position, HordeBounds.center) <
                    Vector3.Distance(baseCamp, HordeBounds.center))
                {
                    StationAtRpc(closestPoi);
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

            populationCooldown = 15.0f;
            LastInCombat = Time.time;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void TeleportHordeRPC(Vector3 target)
        {
            Boids.TeleportHorde(target, GetBounds());
        }

        public void AttackPoi(PoiController poi)
        {
            Debug.Log("Attacking POI");
            if (CurrentCombatController)
                // Leave current combat
                CurrentCombatController.EventRetreatDesiredRpc(this);

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
            if (TotalHealth < 10 * populationController.GetState().HealthPerRat) return;

            // We're already fighting that horde!
            if (CurrentCombatController && CurrentCombatController.HordeInCombat(target)) return;

            if (CurrentCombatController)
            {
                Debug.Log($"HORDE {Object.Id}: Retreating from combat in order to start new one!");
                // Leave current combat
                CurrentCombatController.EventRetreatDesiredRpc(this);
            }


            if (StationedAt)
            {
                StationedAt.UnStationHordeRpc(this);
                StationedAt = null;
            }

            TargetPoi = null;

            targetLocation.Teleport(target.HordeBounds.center);
            _targetHorde = target;
            AddSpeechBubbleRpc(EmoteType.Attack);
            if (player.IsLocal) GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.CombatStarted, 1);
        }

        private IEnumerator ApplyStrategy(CombatOptions action)
        {
            var oldAlive = (uint)AliveRats;
            var oldEnemyAlive = (uint)CurrentCombatController!.GetEnemyRatCount(player);
            var poiCount = 0;
            var poiMult = 1.0f;
            switch (action)
            {
                case CombatOptions.FrontalAssault:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult * 1.2f);
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult * 1.2f);
                    break;
                case CombatOptions.ShockAndAwe:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult * 1.5f);
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult * 1.5f);
                    populationController.GetComponent<AbilityController>().abilityHaste += 10;

                    yield return new WaitForSeconds(10f);

                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult / 1.5f);
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult / 1.5f);
                    populationController.GetComponent<AbilityController>().abilityHaste -= 10;
                    break;
                case CombatOptions.Envelopment:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult * (1 + oldAlive / 1000f));
                    break;
                case CombatOptions.Fortify:
                    var colliders = Physics2D.OverlapCircleAll(GetBounds().center, 20f);
                    foreach (var col in colliders)
                    {
                        var poi = col.GetComponentInParent<PoiController>();
                        if (poi) poiCount++;
                    }

                    poiMult = poiCount == 0 ? 1f : poiCount * 1.3f;
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult * poiMult);
                    break;
                case CombatOptions.Hedgehog:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult * 0.8f);
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult * 0.8f);
                    isHedgehogged = true;
                    break;
                case CombatOptions.AllRound:
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult /
                                                                   (1f + 0.2f * Mathf.Log10(1f + oldEnemyAlive)));
                    break;
            }

            while (InCombat) yield return null;
            switch (action)
            {
                case CombatOptions.FrontalAssault:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult / 1.2f);
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult / 1.2f);
                    break;
                case CombatOptions.Envelopment:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult / (1 + oldAlive / 1000f));
                    break;
                case CombatOptions.Hedgehog:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult / 0.8f);
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult / 0.8f);
                    isHedgehogged = false;
                    break;
                case CombatOptions.Fortify:
                    populationController.SetDamageMultRpc(GetPopulationState().DamageMult / poiMult);
                    break;
                case CombatOptions.AllRound:
                    populationController.SetDamageReductionMultRpc(GetPopulationState().DamageReductionMult *
                                                                   (1f + 0.2f * Mathf.Log10(1f + oldEnemyAlive)));
                    break;
            }
        }

        public void SetCombatStrategy(string strategy)
        {
            _combatStrategy = strategy;
        }

        public string GetCombatStrategy()
        {
            return _combatStrategy;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventWonCombatRpc(HordeState[] hordes)
        {
            Debug.Log($"We ({Object.Id}) won combat!");

            if (player.IsLocal) GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.BattleWon, 1);

            var state = GetEvolutionState();
            var newMutations = new WeightedList<ActiveMutation>();
            foreach (var hordeState in hordes)
            {
                if (player.Hordes.Any(horde => horde.Id.Object == hordeState.Horde)) continue;
                state.PassiveEvolutions["attack"][1] = Math.Max(hordeState.Damage * 0.8,
                    state.PassiveEvolutions["attack"][1]);
                state.PassiveEvolutions["health"][1] = Math.Max(hordeState.HealthPerRat * 0.8,
                    state.PassiveEvolutions["health"][1]);
                state.PassiveEvolutions["defense"][1] = Math.Max(hordeState.DamageReduction * 0.8,
                    state.PassiveEvolutions["defense"][1]);
                foreach (var mut in hordeState.UnlockedMutations)
                {
                    // If horde has already unlocked the mutation, continue
                    if (_evolutionManager.UnlockedMutationNames.Contains(mut)) continue;
                    var mutation = state.AcquiredMutations.ToList().Find(x => x.Type == mut);

                    newMutations.Add(mutation, 1);
                }
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
            populationCooldown = 20.0f;
            LastInCombat = Time.time;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventJoinedCombatRpc(CombatController combat)
        {
            CurrentCombatController = combat;
            if (player.IsLocal) GameManager.Instance.PlaySfx(SoundEffectType.BattleStart);
        }

        public PopulationState GetPopulationState()
        {
            return populationController.GetState();
        }

        public void SetPopulationState(PopulationState newState)
        {
            populationController.SetState(newState);
        }

        public void SetPopulationInit(int initialPopulation)
        {
            populationController.initialPopulation = initialPopulation;
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
            Debug.Log($"HORDE {Object.Id} of {player.Username}: Joining boids to combat {combat.Id}");

            if (combat == null) throw new Exception("Tried to add boids to non-existent combat");
            Boids.JoinCombat(combat.boids, this);
            _combatText.SetActive(true);
        }

        /// <summary>
        ///     Despawns the current horde
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DestroyHordeRpc()
        {
            if (player.Hordes.Count == 1 && player.Type == PlayerType.Bot)
            {
                player.DestroyBotRpc();
                return;
            }

            player.Hordes.Remove(this);
            GameManager.Instance.UIManager.AbilityBars.Remove(this);
            if (GetEvolutionState().AcquiredEffects.Contains("unlock_gods_mistake"))
                foreach (var horde in player.Hordes)
                {
                    horde.GetComponent<EvolutionManager>().PointsAvailable++;
                    horde.AddSpeechBubbleRpc(EmoteType.Evolution);
                }

            Debug.Log($"HORDE {Object.Id}: Despawned self");
            Runner.Despawn(Object);
        }

        /// <summary>
        ///     Sent to *all* machines so they can update their local boid sims
        /// </summary>
        /// <param name="combat"></param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RetrieveBoidsFromCombatRpc(CombatController combat)
        {
            Debug.Log($"HORDE of {player.Username}: Retrieving boids from combat");
            Boids.GetBoidsBack(combat, this);
            _combatText.SetActive(false);
        }

        public void CombatDespawned()
        {
            if (!HasStateAuthority) throw new Exception("Tried to call combat despawned but not state authority");

            CurrentCombatController = null;
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
            Boids.SplitBoids(numBoids, numToSplit, other.Boids);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void CreateApparitionRPC(HordeController other, int numToClone)
        {
            Boids.CreateApparition(other.Boids, numToClone);
        }
    }
}