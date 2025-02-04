using System;
using Fusion;
using Horde;
using JetBrains.Annotations;
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

        [CanBeNull] private BotPlayer _botPlayer;

        [CanBeNull] private HumanPlayer _humanPlayer;

        public bool IsLocal => Type != PlayerType.Bot && HasStateAuthority;

        [Networked] [Capacity(32)] private NetworkLinkedList<HordeController> Hordes { get; } = default;

        [Networked] public string Username { get; private set; }


        // Cheese Management
        [Networked] public float CurrentCheese { get; private set; }

        [Networked] public float CheeseIncrementRate { get; private set; } = 0.03f;


        public override void Spawned()
        {
            if (Type == PlayerType.Human)
            {
                _humanPlayer = this.AddComponent<HumanPlayer>();
                _humanPlayer!.player = this;

                if (HasStateAuthority)
                    FindAnyObjectByType<Grid>().GetComponent<InputHandler>().LocalPlayer = _humanPlayer;

                Username = $"{Object.StateAuthority}";
            }
            else
            {
                _botPlayer = this.AddComponent<BotPlayer>();
                _botPlayer!.player = this;

                Username = $"Bot {Object.Id}";
            }

            foreach (var horde in GetComponentsInChildren<HordeController>()) Hordes.Add(horde);
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
            CheeseIncrementRate = rate;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void IncrementCheeseIncrementRateRpc(float amount)
        {
            CheeseIncrementRate += amount;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void DecrementCheeseIncrementRateRpc(float amount)
        {
            CheeseIncrementRate -= amount;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
                // Add Cheese every tick based on the increment rate
                CurrentCheese += CheeseIncrementRate;
        }


        public ref HumanPlayer GetHumanPlayer()
        {
            if (Type == PlayerType.Human) return ref _humanPlayer;

            throw new NullReferenceException("Tried to get human player from a bot Player");
        }

        public void SplitHorde(HordeController toSplit)
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
                    })
                .GetComponent<HordeController>();
            newHorde.TotalHealth = totalHealth / 2.0f;
            toSplit.TotalHealth = totalHealth / 2.0f;
            // Ensure new horde rats try to move to correct location
            newHorde.Move(toSplit.targetLocation.transform.position);
            // Ensure genetics are transferred
            newHorde.SetPopulationState(populationState);

            Debug.Log($"Split Horde {Object.Id}, creating new Horde {newHorde.Object.Id}");
        }
    }
}