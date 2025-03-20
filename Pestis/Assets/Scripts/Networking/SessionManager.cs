using System;
using System.Collections.Generic;
using Fusion;
using Players;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Networking
{
    public struct PlayerSlot : INetworkStruct
    {
        // Always set to the player who either controls the bot taking this slot, or takes up the slot themselves
        public PlayerRef PlayerRef;

        public NetworkBool IsBot;

        // Network ID of Player's NetworkObject
        public NetworkId PlayerId;
    }

    public class SessionManager : NetworkBehaviour
    {
        private const int TARGET_PLAYERS = 50;
        public static SessionManager Instance;

        public GameObject botPrefab;
        public GameObject playerPrefab;

        [Networked] [Capacity(TARGET_PLAYERS)] private NetworkArray<int> PlayerSpawnIndices => default;

        /// <summary>
        ///     Mapping of spawn index to bot that spawned there
        /// </summary>
        [Networked]
        [Capacity(TARGET_PLAYERS)]
        private NetworkArray<NetworkId> BotSpawnIndices => default;


        /// <summary>
        ///     Each element corresponds to one bot,
        /// </summary>
        [Networked]
        [Capacity(TARGET_PLAYERS)]
        private NetworkArray<PlayerSlot> Players => default;

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
                Runner.TryFindObject(BotSpawnIndices[spawnIndex], out var botObj);
                var bot = botObj.GetComponent<Player>();
                bot.DestroyBotRpc();
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
                    PlayerRef = Object.StateAuthority,
                    PlayerId = bot.Id
                };
                Players.Set(i, slot);
                BotSpawnIndices.Set(i, bot.Id);
            }
        }
    }
}