using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Networking
{
    public struct PlayerSlot : INetworkStruct, IEquatable<PlayerSlot>
    {
        /// Always set to the player who either controls the bot taking this slot, or takes up the slot themselves
        public PlayerRef PlayerRef;

        public NetworkBool IsBot;

        /// Network ID of Player's NetworkObject
        public NetworkId PlayerId;

        public bool Equals(PlayerSlot other)
        {
            return PlayerRef.Equals(other.PlayerRef) && IsBot.Equals(other.IsBot) && PlayerId.Equals(other.PlayerId);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerRef, IsBot, PlayerId);
        }
    }

    public class SessionManager : NetworkBehaviour
    {
        private const int TARGET_PLAYERS = 10;
        public static SessionManager Instance;

        public GameObject botPrefab;
        public GameObject playerPrefab;

        /// <summary>
        ///     Each element corresponds to one bot,
        /// </summary>
        [Networked]
        [Capacity(TARGET_PLAYERS)]
        private NetworkArray<PlayerSlot> Players => default;

        public void Start()
        {
            Debug.Log("Started session manager");
        }

        /// <summary>
        ///     Called by Fusion when a player leaves
        /// </summary>
        /// <param name="player"></param>
        public void PlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // Get all bots that belong to the player that left
            var botsNeedingStealing = Players.Select((slot, index) => new KeyValuePair<int, PlayerSlot>(index, slot))
                .Where(kvp => kvp.Value.PlayerRef == player).ToList();
            // Get all clients still in game
            var clients = Players.Select(slot => slot.PlayerRef).Distinct().Where(p => p != player).ToArray();
            // Loop over each client, assigning one bot at a time
            for (var i = 0; botsNeedingStealing.Count != 0; i = (i + 1) % clients.Length)
            {
                if (botsNeedingStealing.Last().Value.IsBot)
                {
                    // If the current client is Us, steal the bot
                    if (clients[i] == Runner.LocalPlayer) StealBot(botsNeedingStealing.Last().Key);
                }
                // Replace left player with bot
                else if (clients[i] == Runner.LocalPlayer)
                {
                    var spawnIndex = Players.ToList().IndexOf(botsNeedingStealing.Last().Value);
                    var spawnPoint = CalcSpawnPositions()[spawnIndex];
                    var bot = Runner.Spawn(botPrefab, spawnPoint, Quaternion.identity);
                    var slot = new PlayerSlot
                    {
                        IsBot = true,
                        PlayerRef = Runner.LocalPlayer,
                        PlayerId = bot.Id
                    };
                    Players.Set(i, slot);
                }

                botsNeedingStealing.RemoveAt(botsNeedingStealing.Count - 1);
            }
        }

        /// Convert polar coordinates into Cartesian coordinates.
        private void PolarToCartesian(float r, float theta,
            out float x, out float y)
        {
            x = (float)(r * Math.Cos(theta));
            y = (float)(r * Math.Sin(theta));
        }

        private List<Vector2> CalcSpawnPositions()
        {
            const float a = 1.4f;
            var dtheta = (float)(160.0f * Math.PI / 180.0f); // 20 degrees.
            List<Vector2> spawnPositions = new();
            for (var theta = dtheta * 5;; theta += dtheta)
            {
                // Calculate r.
                var r = a * theta;

                dtheta *= 0.95f;

                // Convert to Cartesian coordinates.
                float x, y;
                PolarToCartesian(r, theta, out x, out y);

                var point = new Vector2(x, y);

                var tilePos = GameManager.Instance.terrainMap.WorldToCell(point);
                var tile = GameManager.Instance.terrainMap.GetTile(tilePos);

                // Don't spawn on water
                if (tile == GameManager.Instance.map.water) continue;

                spawnPositions.Add(point);

                if (spawnPositions.Count == TARGET_PLAYERS) break;
            }

            return spawnPositions;
        }

        private int FindNewIndex()
        {
            var presentNumbers = new List<int>();

            // Extract the indices of present numbers
            for (var i = 0; i < Players.Length; i++)
                if (!Players[i].IsBot)
                    presentNumbers.Add(i);

            // If no numbers exist, return 50 as the first placement
            if (presentNumbers.Count == 0)
            {
                Debug.Log("No existing players");
                return TARGET_PLAYERS / 2;
            }


            // Consider the implicit boundaries 0 and 100
            presentNumbers.Insert(0, 0);
            presentNumbers.Add(TARGET_PLAYERS);

            var maxGap = 0;
            var bestIndex = -1;

            // Find the largest gap
            for (var i = 0; i < presentNumbers.Count - 1; i++)
            {
                var start = presentNumbers[i];
                var end = presentNumbers[i + 1];
                var gap = end - start;

                if (gap > maxGap)
                {
                    maxGap = gap;
                    bestIndex = start + gap / 2; // Midpoint of the gap
                }
            }

            if (bestIndex == -1)
            {
                Debug.LogError(
                    "Something went terribly wrong picking a spawn index for the new player. Failing over to spawning player in a random position.");
                bestIndex = Random.Range(0, TARGET_PLAYERS);
            }

            return bestIndex;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AddPlayerRpc(int playerSpawnIndex, NetworkId playerID, RpcInfo rpcInfo = default)
        {
            var player = new PlayerSlot
            {
                PlayerRef = rpcInfo.Source,
                IsBot = false,
                PlayerId = playerID
            };
            Players.Set(playerSpawnIndex, player);
        }

        private void SetSlotOwnerRpc(int playerIndex, RpcInfo info = default)
        {
            var slot = Players[playerIndex];
            slot.PlayerRef = info.Source;
            Players.Set(playerIndex, slot);
        }

        private void StealBot(int playerIndex)
        {
            if (Runner.TryFindObject(Players[playerIndex].PlayerId, out var botObj))
            {
                botObj.RequestStateAuthority();
                var hordes = botObj.GetComponentsInChildren<NetworkObject>();
                foreach (var horde in hordes) horde.RequestStateAuthority();
                SetSlotOwnerRpc(playerIndex);
                Debug.Log($"Stole bot {botObj.Id} at player index {playerIndex}");
            }
            else
            {
                Debug.LogWarning("Failed to get bot object in order to steal it");
            }
        }

        private void StealBots()
        {
            var numBots = Players.Count(slot => slot.IsBot);
            var numClients = Players.Length - numBots + 1; // Plus one for ourselves
            var botsPerClient = numBots / numClients;

            var clientRefs = Players.Select(slot => slot.PlayerRef).Distinct().Where(x => x != Runner.LocalPlayer)
                .ToArray();

            var botsStolen = 0;
            for (var i = 0; botsStolen < botsPerClient; i = (i + 1) % clientRefs.Length)
            {
                var index = Array.FindIndex(Players.ToArray(), slot => slot.PlayerRef == clientRefs[i] && slot.IsBot);
                StealBot(index);
                botsStolen++;
            }
        }

        public override void Spawned()
        {
            Instance = this;

            var spawnPositions = CalcSpawnPositions();

            var spawnIndex = FindNewIndex();

            Debug.Log($"Our spawn index is {spawnIndex}");

            var player = Runner.Spawn(playerPrefab, spawnPositions[spawnIndex]);

            Camera.main.transform.position =
                new Vector3(spawnPositions[spawnIndex].x, spawnPositions[spawnIndex].y, -1.0f);

            AddPlayerRpc(spawnIndex, player.Id);

            if (!HasStateAuthority)
            {
                // Despawn the bot whose place we're taking
                Runner.TryFindObject(Players[spawnIndex].PlayerId, out var botObj);
                var bot = botObj.GetComponent<Player>();
                bot.DestroyBotRpc();

                StealBots();
                return;
            }

            // Spawn necessary bots
            for (var i = 0; i < TARGET_PLAYERS; i++)
            {
                if (i == spawnIndex) continue;

                Debug.Log($"Spawning bot {i}");
                var bot = Runner.Spawn(botPrefab, spawnPositions[i], Quaternion.identity);
                var slot = new PlayerSlot
                {
                    IsBot = true,
                    PlayerRef = Runner.LocalPlayer,
                    PlayerId = bot.Id
                };
                Players.Set(i, slot);
            }
        }
    }
}