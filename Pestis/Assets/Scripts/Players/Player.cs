using System;
using System.Collections.Generic;
using Fusion;
using Horde;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

namespace Players
{
    public enum PlayerType
    {
        Human,
        Bot
    }
    
    public class Player: NetworkBehaviour
    {
        /// <summary>
        /// Human or Bot?
        /// </summary>
        public PlayerType Type;

        [CanBeNull] private HumanPlayer _humanPlayer;
        [CanBeNull] private BotPlayer _botPlayer;

        [Capacity(32)]
        private NetworkArray<HordeController> Hordes { get; set; }

        /// <summary>
        /// Can only be in one combat instance at a time.
        /// </summary>
        [Networked]
        [CanBeNull] private CombatController CurrentCombatController { get; set; }
        
        
        public override void Spawned()
        {
            if (Type == PlayerType.Human)
            {
                _humanPlayer = this.AddComponent<HumanPlayer>();
                _humanPlayer!.player = this;

                if (HasStateAuthority)
                {
                    FindAnyObjectByType<Grid>().GetComponent<InputHandler>().LocalPlayer = _humanPlayer;
                }
            }
            else
            {
                _botPlayer = this.AddComponent<BotPlayer>();
                _botPlayer!.player = this;
            }
        }

        /// <summary>
        /// Adds a horde to the combat we started, starting one if it doesn't exist.
        /// This can add our hordes or enemy hordes.
        /// </summary>
        /// <param name="horde"></param>
        public void JoinHordeToCombat(HordeController horde) 
        {
            if (CurrentCombatController == null)
            {
                CurrentCombatController = GetComponent<CombatController>();
            }
            
            CurrentCombatController!.AddHorde(horde);
        }

        /// <summary>
        /// Tell the player to enter combat.
        /// Called by the combat initiator.
        /// </summary>
        /// <param name="combat"></param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EnterCombatRpc(CombatController combat)
        {
            if (CurrentCombatController != null)
            {
                throw new Exception("Already in combat!");
            }
            else
            {
                CurrentCombatController = combat;
            }
        }

        public ref HumanPlayer GetHumanPlayer()
        {
            if (Type == PlayerType.Human)
            {
                return ref _humanPlayer;
            }
            else
            {
                throw new NullReferenceException("Tried to get human player from a bot Player");
            }
        }

        public bool InCombat()
        {
            return CurrentCombatController != null;
        }
    }
    
}