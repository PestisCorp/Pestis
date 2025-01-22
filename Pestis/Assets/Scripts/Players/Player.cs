using System;
using System.Collections.Generic;
using Fusion;
using Horde;
using JetBrains.Annotations;
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
        public NetworkString<_16> UniqueIdentifier;

        [CanBeNull] private HumanPlayer _humanPlayer;
        [CanBeNull] private BotPlayer _botPlayer;

        [Capacity(32)]
        private NetworkArray<HordeController> _hordes { get; set; }

        public void Awake()
        {
            UniqueIdentifier = new NetworkString<_16>();
        }
        
        public override void Spawned()
        {
            Random rand = new Random();
            String id = "";
            for (byte i = 0; i < 15; i++)
            {
                id += Convert.ToChar(rand.Next(33, 126));
            }

            UniqueIdentifier = id;

            if (Type == PlayerType.Human)
            {
                _humanPlayer = gameObject.AddComponent<HumanPlayer>();
            }
            else
            {
                _botPlayer = gameObject.AddComponent<BotPlayer>();
            }
        }
    }
    
}