using System;
using System.Collections;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using POI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;

namespace Players
{
    public enum PlayerType
    {
        Human,
        Bot
    }

    public class Player : NetworkBehaviour
    {
        public delegate void OnBeforeSpawned(NetworkRunner runner, NetworkObject obj);

        /// <summary>
        ///     Human or Bot?
        /// </summary>
        public PlayerType Type;

        public GameObject hordePrefab;

        [SerializeField] private float cheeseConsumptionRate = 0.001f; // k value

        [CanBeNull] private BotPlayer _botPlayer;

        [CanBeNull] private HumanPlayer _humanPlayer;

        private TickTimer _leaderboardUpdateTimer;

        public bool IsLocal => Type != PlayerType.Bot && HasStateAuthority;

        [Networked] [Capacity(32)] public NetworkLinkedList<HordeController> Hordes { get; } = default;

        [Networked] [Capacity(64)] public NetworkLinkedList<POIController> ControlledPOIs { get; } = default;

        [Networked] public string Username { get; private set; }


        // Cheese Management
        [Networked] public float CurrentCheese { get; private set; }

        [Networked] public float CheeseIncrementRate { get; private set; } = 0.03f;

        public float CheesePerSecond => CheeseIncrementRate / Runner.DeltaTime;

        [Networked] public float FixedCheeseGain { get; private set; } = 0.03f;

        private void OnDestroy()
        {
            GameManager.Instance.Players.Remove(this);
        }

        public int GetHordeCount()
        {
            return Hordes.Count;
        }


        public override void Spawned()
        {
            if (Type == PlayerType.Human)
            {
                _humanPlayer = this.AddComponent<HumanPlayer>();
                _humanPlayer!.player = this;

                if (HasStateAuthority)
                    FindAnyObjectByType<Grid>().GetComponent<InputHandler>().LocalPlayer = _humanPlayer;

                Username = $"Player {Object.StateAuthority.PlayerId}";
            }
            else
            {
                _botPlayer = this.AddComponent<BotPlayer>();
                _botPlayer!.player = this;

                Username = $"Bot {Object.Id.Raw}";
            }

            GameManager.Instance.Players.Add(this);

            if (HasStateAuthority) StartCoroutine(JoinStats());
        }

        // Manage Cheese
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AddCheeseRpc(float amount)
        {
            CurrentCheese += amount;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RemoveCheeseRpc(float amount)
        {
            CurrentCheese = Mathf.Max(0, CurrentCheese - amount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetCheeseIncrementRateRpc(float rate)
        {
            FixedCheeseGain = rate;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void IncrementCheeseIncrementRateRpc(float amount)
        {
            Debug.Log($"PLAYER: Increasing cheese rate by {amount}");
            FixedCheeseGain += amount;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DecrementCheeseIncrementRateRpc(float amount)
        {
            FixedCheeseGain -= amount;
        }

        private IEnumerator JoinStats()
        {
#if UNITY_EDITOR
            var uri = "http://localhost:8081/api/join";
#else
            string uri = "https://pestis.murraygrov.es/api/join";
#endif
            var jsonObj = new JoinRequest
            {
                id = Object.Id.Raw,
                username = Username
            };
            var json = JsonUtility.ToJson(jsonObj);

            var uwr = UnityWebRequest.Put(uri, json);
            uwr.method = "POST";
            uwr.SetRequestHeader("Content-Type", "application/json");
            yield return uwr.SendWebRequest();
        }

        private IEnumerator UpdateStats()
        {
#if UNITY_EDITOR
            var uri = "http://localhost:8081/api/update";
#else
            string uri = "https://pestis.murraygrov.es/api/update";
#endif
            var jsonObj = new StatsUpdate
            {
                tick = (ulong)Runner.Tick.Raw,
                fps = GameManager.Instance.currentFps,
                player = new PlayerUpdate
                {
                    id = Object.Id.Raw,
                    username = Username,
                    hordes = Hordes.Select(horde => new HordeUpdate
                    {
                        id = horde.Object.Id.Raw,
                        rats = (ulong)horde.AliveRats
                    }).ToArray(),
                    pois = ControlledPOIs.Select(poi => new POIUpdate
                    {
                        id = poi.Object.Id.Raw
                    }).ToArray(),
                    score = 0
                }
            };
            var json = JsonUtility.ToJson(jsonObj);

            var uwr = UnityWebRequest.Put(uri, json);
            uwr.method = "POST";
            uwr.SetRequestHeader("Content-Type", "application/json");
            yield return uwr.SendWebRequest();
        }

        public override void FixedUpdateNetwork()
        {
            if (Hordes.Count == 0) throw new Exception("Player has no hordes!");

            if (_leaderboardUpdateTimer.ExpiredOrNotRunning(Runner))
            {
                _leaderboardUpdateTimer = TickTimer.CreateFromSeconds(Runner, 60);
                StartCoroutine(UpdateStats());
            }


            // Consume cheese
            var totalRatsCount = Hordes.Select(horde => horde.AliveRats).Sum();

            // Cheese consumption formula
            var cheeseConsumed = cheeseConsumptionRate * totalRatsCount;

            // Cheese gain per game tick
            CheeseIncrementRate = FixedCheeseGain - cheeseConsumed;

            // handle boundary values
            if (CheeseIncrementRate < 0.005f && CheeseIncrementRate >= 0.00)
                CheeseIncrementRate = 0.00f;
            else if (CheeseIncrementRate > -0.005f && CheeseIncrementRate < 0.00) CheeseIncrementRate = 0.00f;

            // Prevent negative cheese values
            CurrentCheese = Mathf.Max(0, CurrentCheese + CheeseIncrementRate);
        }


        public ref HumanPlayer GetHumanPlayer()
        {
            if (Type == PlayerType.Human) return ref _humanPlayer;

            throw new NullReferenceException("Tried to get human player from a bot Player");
        }

        public void SplitHorde(HordeController toSplit, float splitPercentage)
        {
            if (!HasStateAuthority) throw new Exception("Only State Authority can split a horde");

            var newRats = (int)(toSplit.TotalHealth * splitPercentage / toSplit.GetPopulationState().HealthPerRat);

            var totalHealth = toSplit.TotalHealth;
            var populationState = toSplit.GetPopulationState();
            var evolutionaryState = toSplit.GetEvolutionState();
            var newHorde = Runner.Spawn(hordePrefab, Vector3.zero,
                    Quaternion.identity,
                    null, (runner, NO) =>
                    {
                        NO.transform.parent = transform;
                        // Ensure new horde spawns in at current location
                        NO.transform.position = toSplit.GetBounds().center;
                        var horde = NO.GetComponent<HordeController>();
                        horde.TotalHealth = totalHealth * splitPercentage;
                        horde.SetPopulationState(populationState);
                        horde.SetPopulationInit(newRats);
                    })
                .GetComponent<HordeController>();
            toSplit.SplitBoidsRpc(newHorde, newRats, toSplit.AliveRats);
            toSplit.TotalHealth = totalHealth * (1.0f - splitPercentage);
            newHorde.SetEvolutionaryState(evolutionaryState.DeepCopy());

            // Move two hordes slightly apart
            newHorde.Move(toSplit.targetLocation.transform.position - toSplit.GetBounds().extents);
            toSplit.Move(toSplit.targetLocation.transform.position + toSplit.GetBounds().extents);
            // Ensure genetics are transferred
            if (Type == PlayerType.Human) _humanPlayer?.SelectHorde(newHorde);

            Debug.Log(
                $"Split Horde {Object.Id}, creating new Horde {newHorde.Object.Id} with {splitPercentage}x health");
        }

        [Serializable]
        private struct JoinRequest
        {
            public ulong id;
            public string username;
        }

        [Serializable]
        private struct HordeUpdate
        {
            public ulong rats;
            public ulong id;
        }

        [Serializable]
        private struct POIUpdate
        {
            public ulong id;
        }

        [Serializable]
        private struct PlayerUpdate
        {
            public ulong id;
            public string username;
            public HordeUpdate[] hordes;
            public POIUpdate[] pois;
            public ulong score;
        }

        [Serializable]
        private struct StatsUpdate
        {
            public ulong tick;
            public float fps;
            public PlayerUpdate player;
        }
    }
}