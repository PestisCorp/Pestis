using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using Networking;
using Objectives;
using POI;
using Unity.Profiling;
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

        private static readonly ProfilerMarker s_AddControlledPoiRpc = new("RPCPlayer.AddControlledPoiRpc");
        private static readonly ProfilerMarker s_RemovedControlledPoiRpc = new("RPCPlayer.RemovedControlledPoiRpc");
        private static readonly ProfilerMarker s_AddCheeseRpc = new("RPCPlayer.AddCheeseRpc");
        private static readonly ProfilerMarker s_RemoveCheeseRpc = new("RPCPlayer.RemoveCheeseRpc");
        private static readonly ProfilerMarker s_IncrementCheeseRate = new("RPCPlayer.IncrementCheeseRate");
        private static readonly ProfilerMarker s_DecrementCheeseRate = new("RPCPlayer.DecrementCheeseRate");

        /// <summary>
        ///     Human or Bot?
        /// </summary>
        public PlayerType Type;

        public GameObject hordePrefab;


        [SerializeField] public float cheeseConsumptionRate = 0.001f; // k value

        [DoNotSerialize] public double TotalDamageDealt;

        public int aliveRats;

        public string usernameOffline;

        [CanBeNull] private BotPlayer _botPlayer;

        [CanBeNull] private HumanPlayer _humanPlayer;

        private TickTimer _leaderboardUpdateTimer;


        private int framesSinceLostAuth;

        /// <summary>
        ///     Whether this player is the one being controlled by the player on this machine.
        /// </summary>
        public bool IsLocal => Type != PlayerType.Bot && HasStateAuthority;

        [Networked] [Capacity(32)] public NetworkLinkedList<HordeController> Hordes { get; } = default;

        [Networked] [Capacity(64)] public NetworkLinkedList<PoiController> ControlledPOIs { get; } = default;

        [Networked] public string Username { get; private set; }

        //time and score
        public bool TimeUp { get; private set; }
        public int Timer { get; private set; }
        public ulong Score { get; private set; }

        // Cheese Management
        [Networked] public PositiveFloatRes100 CurrentCheese { get; private set; }

        public float CheeseIncrementRate { get; private set; } = 0.03f;

        public float CheesePerSecond => CheeseIncrementRate / Runner.DeltaTime;

        [Networked] public FloatRes100 FixedCheeseGain { get; private set; } = 0.03f;

        private void FixedUpdate()
        {
            if (Object.StateAuthority.IsNone || !Runner.ActivePlayers.Contains(Object.StateAuthority))
            {
                framesSinceLostAuth++;
                if (framesSinceLostAuth > 20)
                {
                    foreach (var horde in Hordes)
                    {
                        Runner.RequestStateAuthority(horde.Object.Id);
                        Runner.Despawn(horde.Object);
                    }

                    Runner.RequestStateAuthority(Object);
                    Runner.Despawn(Object);
                }
            }
            else
            {
                framesSinceLostAuth = 0;
            }

            var newSum = 0;
            // LinQ/ForEach use enumerators which allocate memory
            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < Hordes.Count; i++) newSum += Hordes[i].AliveRats;
            aliveRats = newSum;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AddControlledPoiRpc(PoiController poi)
        {
            s_AddControlledPoiRpc.Begin();
            ControlledPOIs.Add(poi);
            GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.POICaptured, 1);
            GameManager.Instance.PlaySfx(SoundEffectType.POICapture);
            if (IsLocal)
                switch (poi._poiType)
                {
                    case POIType.City:
                        foreach (var horde in poi.StationedHordes)
                            horde.SetAliveRatsRpc((uint)((uint)horde.AliveRats * 1.1));
                        GameManager.Instance.UIManager.AddNotification("City captured. Population increased",
                            Color.black);
                        break;
                    case POIType.Lab:
                        foreach (var horde in poi.StationedHordes) horde.GetComponent<EvolutionManager>().AddPoints();

                        GameManager.Instance.UIManager.AddNotification("Lab captured. Mutation points acquired.",
                            Color.black);
                        break;
                    case POIType.Farm:
                        AddCheeseRpc(100);
                        GameManager.Instance.UIManager.AddNotification("Farm captured. Food package acquired.",
                            Color.black);
                        break;
                }

            s_AddControlledPoiRpc.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RemoveControlledPoiRpc(PoiController poi)
        {
            s_RemovedControlledPoiRpc.Begin();
            ControlledPOIs.Remove(poi);
            s_RemovedControlledPoiRpc.End();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Debug.Log($"Player {(hasState ? Username : "unknown")} left");

            foreach (var horde in Hordes) Destroy(horde);
            GameManager.Instance.Players.Remove(this);
            foreach (var poi in ControlledPOIs) poi.RemoveControllerRpc();

            if (Object)
            {
                if (Type == PlayerType.Bot) SessionManager.Instance.BotLeftRpc(Object.Id);
            }
            else
            {
                Debug.LogWarning("Player despawned, but we don't have access to their Network Object to do cleanup!");
            }
        }

        public int GetHordeCount()
        {
            return Hordes.Count;
        }

        public override void Spawned()
        {
            GameManager.Instance.Players.Add(this);

            if (!HasStateAuthority) return;

            if (Type == PlayerType.Human)
            {
                _humanPlayer = this.AddComponent<HumanPlayer>();
                _humanPlayer!.player = this;

                FindAnyObjectByType<Grid>().GetComponent<InputHandler>().LocalPlayer = _humanPlayer;
                if (GameManager.Instance.localUsername.Length != 0)
                    Username = GameManager.Instance.localUsername;
                else
                    Username = $"Player {Object.StateAuthority}";
                StartCoroutine(TimerTilScoreLock(600));
            }
            else
            {
                _botPlayer = this.AddComponent<BotPlayer>();
                _botPlayer!.player = this;


                Username = $"Bot {Object.Id.Raw}";
            }

            usernameOffline = Username;

            StartCoroutine(JoinStats());
            CurrentCheese = 50.0f;
        }

        // Manage Cheese
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AddCheeseRpc(float amount)
        {
            s_AddCheeseRpc.Begin();
            CurrentCheese += amount;
            s_AddCheeseRpc.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RemoveCheeseRpc(float amount)
        {
            s_RemoveCheeseRpc.Begin();
            CurrentCheese = Mathf.Max(0, CurrentCheese - amount);
            s_RemoveCheeseRpc.End();
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
            s_IncrementCheeseRate.Begin();
            FixedCheeseGain += amount;
            s_IncrementCheeseRate.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DecrementCheeseIncrementRateRpc(float amount)
        {
            s_DecrementCheeseRate.Begin();
            FixedCheeseGain -= amount;
            s_DecrementCheeseRate.End();
        }


        private IEnumerator TimerTilScoreLock(int timeRemaining)
        {
            while (timeRemaining > 0)
            {
                yield return new WaitForSeconds(1f);
                timeRemaining--;
                Timer = timeRemaining;
                CalculateScore();
            }

            TimeUp = true;
            Debug.Log("Times Up, final score " + Score);
            yield return null;
        }

        private IEnumerator JoinStats()
        {
            if (!IsLocal) yield break;

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
            if (!IsLocal) yield break;

            var uri = "https://pestis.murraygrov.es/api/update";

            var jsonObj = new StatsUpdate
            {
                tick = (ulong)Runner.Tick.Raw,
                fps = GameManager.Instance.currentFps,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                player = new PlayerUpdate
                {
                    id = Object.Id.Raw,
                    username = Username,
                    hordes = Hordes.Select(horde => new HordeUpdate
                    {
                        id = horde.Object.Id.Raw,
                        rats = horde.AliveRats
                    }).ToArray(),
                    pois = ControlledPOIs.Select(poi => new POIUpdate
                    {
                        id = poi.Object.Id.Raw
                    }).ToArray(),
                    score = CalculateScore(),
                    damage = Convert.ToUInt64(TotalDamageDealt)
                },
                room = SessionManager.Instance.Room.Name
            };
            var json = JsonUtility.ToJson(jsonObj);

            var uwr = UnityWebRequest.Put(uri, json);
            uwr.method = "POST";
            uwr.SetRequestHeader("Content-Type", "application/json");
            yield return uwr.SendWebRequest();
        }

        public override void FixedUpdateNetwork()
        {
            if (Hordes.Count == 0) throw new Exception($"Player {Username} has no hordes!");

            if (_leaderboardUpdateTimer.ExpiredOrNotRunning(Runner))
            {
                _leaderboardUpdateTimer = TickTimer.CreateFromSeconds(Runner, 60);
                StartCoroutine(UpdateStats());
            }

            // Consume cheese
            var totalRatsCount = Hordes.Select(horde => (int)horde.AliveRats).Sum();

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


        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DestroyBotRpc()
        {
            if (Type != PlayerType.Bot) throw new Exception("Tried to destroy human player remotely!");

            Debug.Log($"PLAYER {Username}: Destroyed bot (self)");
            Runner.Despawn(Object);
        }

        public void SplitHorde(HordeController toSplit, float splitPercentage)
        {
            if (!HasStateAuthority) throw new Exception("Only State Authority can split a horde");

            var newRats = (int)((uint)toSplit.AliveRats * splitPercentage);

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
                        horde.SetPopulationState(populationState);
                        horde.SetPopulationInit(newRats);
                        horde.AliveRats = new IntPositive((uint)newRats);
                    })
                .GetComponent<HordeController>();
            toSplit.SplitBoidsRpc(newHorde, newRats, toSplit.AliveRats);
            toSplit.AliveRats = new IntPositive(Convert.ToUInt32((uint)toSplit.AliveRats * (1.0f - splitPercentage)));
            newHorde.SetEvolutionaryState(evolutionaryState.DeepCopy());

            // Move two hordes slightly apart
            newHorde.Move(toSplit.targetLocation.transform.position - toSplit.GetBounds().extents);
            toSplit.Move(toSplit.targetLocation.transform.position + toSplit.GetBounds().extents);
            newHorde.populationCooldown += 5;
            // Ensure genetics are transferred
            if (Type == PlayerType.Human) _humanPlayer?.SelectHorde(newHorde);

            Debug.Log(
                $"Split Horde {Object.Id}, creating new Horde {newHorde.Object.Id} with {splitPercentage}x health");
        }

        public ulong CalculateScore()
        {
            if (TimeUp) return Score;

            ulong score = 0;

            score += (ulong)Hordes.Sum(horde => horde.AliveRats);
            score += (ulong)(100 * Hordes.Count);
            score += (ulong)(300 * ControlledPOIs.Count);

            HashSet<ActiveMutation> allMutations = new();
            HashSet<string> mutationTags = new();
            foreach (var horde in Hordes)
            {
                var mutations = horde.GetEvolutionState().AcquiredMutations;
                foreach (var mutation in mutations)
                {
                    allMutations.Add(mutation);
                    mutationTags.Add(mutation.MutationTag);
                }
            }

            score += (ulong)(300 * allMutations.Count);
            score += (ulong)(500 * mutationTags.Count);

            score += Convert.ToUInt64(TotalDamageDealt / 5.0);

            Score = score;
            return score;
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
            public ulong damage;
        }

        [Serializable]
        private struct StatsUpdate
        {
            public ulong tick;
            public long timestamp;
            public float fps;
            public PlayerUpdate player;
            public string room;
        }
    }
}