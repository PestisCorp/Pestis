using System;
using System.Collections.Generic;
using Fusion;
using Players;
using UnityEngine;

namespace Horde
{
    
    public class CombatController: NetworkBehaviour
    {
        public const int MaxParticipants = 6;
        
        [Networked]
        private Player InitiatingPlayer { get; set; }

        [Networked, Capacity(MaxParticipants)] private NetworkLinkedList<Player> Participators {get;}

        /// <summary>
        ///  Instantiate new combat controller
        /// </summary>
        /// <param name="parent">The Horde Controller instantiating it</param>
        /// <param name="otherParticipants">The other hordes that are participating in the combat</param>
        /// <returns>New combat controller</returns>
        public static CombatController Instantiate(Player parent, HashSet<Player> otherParticipants)
        {
            if (otherParticipants.Count > MaxParticipants - 1)
            {
                throw new Exception("Tried to start combat with more participants than allowed");
            }
            
            CombatController controller = parent.gameObject.AddComponent<CombatController>();
            controller.InitiatingPlayer = parent;
            
            controller.Participators.Add(parent);
            foreach (Player participant in otherParticipants)
            {
                controller.Participators.Add(participant);
            }
            return controller;
        }
    }
}