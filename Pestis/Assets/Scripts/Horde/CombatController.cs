using System;
using System.Collections.Generic;
using Fusion;
using Players;
using UnityEngine;

namespace Horde
{
    /// <summary>
    /// https://doc.photonengine.com/fusion/current/manual/fusion-types/network-collections#usage-in-inetworkstructs
    /// </summary>
    public struct CombatParticipant : INetworkStruct
    {
        public Player Player;
        
        [Networked, Capacity(5)] public NetworkLinkedList<NetworkBehaviourId> Hordes => default;

        public CombatParticipant(Player player, HordeController hordeController)
        {
            Player = player;
            Hordes.Add(hordeController);
        }

        public CombatParticipant(Player player)
        {
            Player = player;
        }

        public void AddHorde(HordeController horde)
        {
            Hordes.Add(horde);
        }
    }
    
    public class CombatController: NetworkBehaviour
    {
        public const int MaxParticipants = 6;
        
        [Networked]
        private Player InitiatingPlayer { get; set; }

        /// <summary>
        /// Stores the involved players and a list of their HordeControllers (as NetworkBehaviourId so must be converted before use)
        /// </summary>
        [Networked, Capacity(MaxParticipants)] public NetworkDictionary<Player, CombatParticipant> Participators {get; }
        

        public void AddHorde(HordeController horde)
        {
            if (Participators.Count == 0)
            {
                InitiatingPlayer = horde.Player;
            }
            
            if (!Participators.TryGet(horde.Player, out CombatParticipant participant))
            {
                if (InitiatingPlayer != horde.Player)
                {
                    horde.Player.EnterCombatRpc(this);
                }
                Participators.Add(horde.Player, new CombatParticipant(horde.Player, horde));
            }
            else
            {
                // Operates on local copy
                participant.AddHorde(horde);
                // Update stored copy
                Participators.Set(horde.Player, participant);
            }
        }
    }
}