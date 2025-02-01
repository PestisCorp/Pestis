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

        [Capacity(32)] private NetworkArray<HordeController> Hordes { get; set; }

        /// <summary>
        ///     Can only be in one combat instance at a time.
        /// </summary>
        [Networked]
        [CanBeNull]
        private CombatController CurrentCombatController { get; set; }

        // Cheese Management
        [Networked] public int CurrentCheese { get; private set; } = 0;

        [Networked] public int CheeseIncrementRate { get; private set; } = 1;


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
        }

        // Manage Cheese
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AddCheese(int amount)
        {
            CurrentCheese += amount;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RemoveCheese(int amount)
        {
            CurrentCheese = Mathf.Max(0, CurrentCheese - amount);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetCheeseIncrementRate(int rate)
        {
            CheeseIncrementRate = rate;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void IncrementCheeseIncrementRate(int amount)
        {
            CheeseIncrementRate += amount;
        }

        public void DecrementCheeseIncrementRate(int amount)
        {
            CheeseIncrementRate -= amount;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                // Add Cheese every tick based on the increment rate
                CurrentCheese += CheeseIncrementRate;
            }
        }

        /// <summary>
        ///     Adds a horde to the combat we are in.
        ///     This can add our hordes or enemy hordes.
        /// </summary>
        /// <param name="horde"></param>
        public void JoinHordeToCombat(HordeController horde)
        {
            if (CurrentCombatController == null) CurrentCombatController = GetComponent<CombatController>();

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
            if (CurrentCombatController != null) throw new Exception("Already in combat!");

            CurrentCombatController = combat;
        }

        public ref HumanPlayer GetHumanPlayer()
        {
            if (Type == PlayerType.Human) return ref _humanPlayer;

            throw new NullReferenceException("Tried to get human player from a bot Player");
        }

        public bool InCombat()
        {
            return CurrentCombatController;
        }

        public CombatController GetCombatController()
        {
            return CurrentCombatController;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void LeaveCombatRpc()
        {
            CurrentCombatController = null;
            foreach (var horde in Hordes) horde.HordeBeingDamaged = null;
        }
    }
}