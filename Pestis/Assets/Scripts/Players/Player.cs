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
            foreach (var horde in Hordes) horde.HordeBeingDamaged = null;
        }
    }
}