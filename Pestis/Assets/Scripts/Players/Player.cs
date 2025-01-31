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
        /// <summary>
        ///     Human or Bot?
        /// </summary>
        public PlayerType Type;

        [CanBeNull] private BotPlayer _botPlayer;

        [CanBeNull] private HumanPlayer _humanPlayer;

        [Networked] [Capacity(32)] private NetworkLinkedList<HordeController> Hordes { get; } = default;

        /// <summary>
        ///     Can only be in one combat instance at a time.
        /// </summary>
        [Networked]
        [CanBeNull]
        private CombatController CurrentCombatController { get; set; }

        public bool InCombat => CurrentCombatController;

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
            }
            else
            {
                _botPlayer = this.AddComponent<BotPlayer>();
                _botPlayer!.player = this;
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

        /// <summary>
        ///     Adds a horde to the combat we are in.
        ///     This can add our hordes or enemy hordes.
        /// </summary>
        /// <param name="horde"></param>
        public void JoinHordeToCombat(HordeController horde)
        {
            if (!CurrentCombatController) CurrentCombatController = GetComponent<CombatController>();

            CurrentCombatController!.AddHorde(horde, horde.Player == this);
        }

        /// <summary>
        ///     Tell the player to enter combat.
        ///     Called by the combat initiator.
        /// </summary>
        /// <param name="combat"></param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EnterCombatRpc(CombatController combat)
        {
            if (CurrentCombatController) throw new Exception("Already in combat!");

            CurrentCombatController = combat;
        }

        public ref HumanPlayer GetHumanPlayer()
        {
            if (Type == PlayerType.Human) return ref _humanPlayer;

            throw new NullReferenceException("Tried to get human player from a bot Player");
        }


        public CombatController GetCombatController()
        {
            return CurrentCombatController;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void LeaveCombatRpc()
        {
            Debug.Log("Leaving combat!");
            CurrentCombatController = null;
            Debug.Log($"Hordes: {Hordes}");
            foreach (var horde in Hordes) horde.HordeBeingDamaged = null;
        }
    }
}