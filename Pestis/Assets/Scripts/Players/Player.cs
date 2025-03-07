using System;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using POI;
using Unity.VisualScripting;
using UnityEngine;

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

            foreach (var horde in GetComponentsInChildren<HordeController>()) Hordes.Add(horde);

            GameManager.Instance.Players.Add(this);
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

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
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
        }


        public ref HumanPlayer GetHumanPlayer()
        {
            if (Type == PlayerType.Human) return ref _humanPlayer;

            throw new NullReferenceException("Tried to get human player from a bot Player");
        }

        public void SplitHorde(HordeController toSplit, float splitPercentage)
        {
            if (!HasStateAuthority) throw new Exception("Only State Authority can split a horde");


            var totalHealth = toSplit.TotalHealth;
            var populationState = toSplit.GetPopulationState();
            var newHorde = Runner.Spawn(hordePrefab, Vector3.zero,
                    Quaternion.identity,
                    null, (runner, NO) =>
                    {
                        NO.transform.parent = transform;
                        // Ensure new horde spawns in at current location
                        NO.transform.position = toSplit.GetBounds().center;
                        var horde = NO.GetComponent<HordeController>();
                        horde.TotalHealth = totalHealth * splitPercentage;
                    })
                .GetComponent<HordeController>();
            toSplit.SplitBoidsRpc(newHorde, toSplit.AliveRats / 2, toSplit.AliveRats);
            toSplit.TotalHealth = totalHealth * (1.0f - splitPercentage);
            newHorde.SetPopulationState(populationState);

            // Move two hordes slightly apart
            newHorde.Move(toSplit.targetLocation.transform.position - toSplit.GetBounds().extents);
            toSplit.Move(toSplit.targetLocation.transform.position + toSplit.GetBounds().extents);
            // Ensure genetics are transferred
            if (Type == PlayerType.Human) _humanPlayer?.SelectHorde(newHorde);

            Debug.Log(
                $"Split Horde {Object.Id}, creating new Horde {newHorde.Object.Id} with {splitPercentage}x health");
        }
    }
}