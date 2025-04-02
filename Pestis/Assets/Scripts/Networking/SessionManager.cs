using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Players;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Networking
{
    [Serializable]
    internal struct RoomConfig
    {
        [JsonProperty("players_per_room")] internal int PlayersPerRoom;

        [JsonProperty("max_bots_per_client")] internal int MaxBotsPerClient;
    }

    [Serializable]
    internal struct RoomResponse
    {
        [JsonProperty("name")] internal string Name;

        [JsonProperty("config")] internal RoomConfig Config;
    }

    public struct PlayerSlot : INetworkStruct, IEquatable<PlayerSlot>
    {
        /// Always set to the player who either controls the bot taking this slot, or takes up the slot themselves
        public PlayerRef PlayerRef;

        public NetworkBool IsBot;

        /// Network ID of Player's NetworkObject
        public NetworkId PlayerId;

        /// <summary>
        ///     Whether the slot is in use or empty
        /// </summary>
        public NetworkBool InUse;

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
        private RoomResponse _room;

        public static SessionManager Instance;

        public GameObject botPrefab;
        public GameObject playerPrefab;

        /// <summary>
        ///     Each element corresponds to one bot,
        /// </summary>
        [Networked]
        [Capacity(100)]
        private NetworkArray<PlayerSlot> Players => default;

        [SerializeField] private TMP_Text roomNameText;

#if UNITY_EDITOR
        private const string APIEndpoint = "http://localhost:8081/api";
#else
        //private const string APIEndpoint = "https://pestis.murraygrov.es/api";
// TODO - Remove
        private const string APIEndpoint = "http://localhost:8081/api";
#endif

        public async void Start()
        {
            Instance = this;
            try
            {
                var req = UnityWebRequest.Get($"{APIEndpoint}/room");
                req.timeout = 5;
                await req.SendWebRequest();

                _room = JsonConvert.DeserializeObject<RoomResponse>(req.downloadHandler.text);
                Debug.Log("Got room info");
            }
            catch (Exception e)
            {
                _room = new RoomResponse
                {
                    Name = "Fallback Room",
                    Config = new RoomConfig
                    {
                        PlayersPerRoom = 50,
                        MaxBotsPerClient = 25
                    }
                };
                UnityEngine.Debug.LogWarning($"Failed to get room config: {e}");
            }

            roomNameText.text = _room.Name;
        }

        public void JoinGame(NetworkRunner runner)
        {
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = _room.Name
            };
            var scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));
            args.Scene = scene;
            runner.StartGame(args);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void BotLeftRpc(NetworkId bot)
        {
            for (var i = 0; i < Players.Length; i++)
                if (Players[i].PlayerId == bot)
                {
                    var temp = Players[i];
                    temp.InUse = false;
                    Players.Set(i, temp);
                }
        }

        /// <summary>
        ///     Called by Fusion when a player leaves
        /// </summary>
        /// <param name="runner">The current runner</param>
        /// <param name="player"></param>
        public void PlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // Get all bots that belong to the player that left
            var botsNeedingStealing = Players.Select((slot, index) => new KeyValuePair<int, PlayerSlot>(index, slot))
                .Where(kvp => kvp.Value.PlayerRef == player && kvp.Value.InUse).ToList();
            // Get all clients still in game
            var clients = Players.Where(slot => slot.InUse).Select(slot => slot.PlayerRef).Distinct()
                .Where(p => p != player).ToArray();
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

            var botsOnThisClient =
                Players.Count(slot => slot.InUse && slot.PlayerRef == Runner.LocalPlayer && slot.IsBot);

            var numBotsToRemove = botsOnThisClient - _room.Config.MaxBotsPerClient;
            if (numBotsToRemove <= 0) return;

            var ourBots = Players.Select((slot, i) => new KeyValuePair<int, PlayerSlot>(i, slot)).Where(kvp =>
                kvp.Value.InUse && kvp.Value.PlayerRef == Runner.LocalPlayer && kvp.Value.IsBot).ToArray();
            for (; numBotsToRemove > 0; numBotsToRemove--)
            {
                MarkSlotUnusedRpc(ourBots[numBotsToRemove].Key);
                if (Runner.TryFindObject(ourBots[numBotsToRemove].Value.PlayerId, out var botObj))
                    botObj.GetComponent<Player>().DestroyBotRpc();
            }
        }

        /// Convert polar coordinates into Cartesian coordinates.
        private static void PolarToCartesian(float r, float theta,
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
                PolarToCartesian(r, theta, out var x, out var y);

                var point = new Vector2(x, y);

                var tilePos = GameManager.Instance.terrainMap.WorldToCell(point);
                var tile = GameManager.Instance.terrainMap.GetTile(tilePos);

                // Don't spawn on water
                if (tile == GameManager.Instance.map.water) continue;

                spawnPositions.Add(point);

                if (spawnPositions.Count == _room.Config.PlayersPerRoom) break;
            }

            return spawnPositions;
        }

        private int FindNewIndex()
        {
            var presentNumbers = new List<int>();

            // Extract the indices of present numbers
            for (var i = 0; i < Players.Length; i++)
                if (!Players[i].IsBot && Players[i].InUse)
                    presentNumbers.Add(i);

            // If no numbers exist, return 50 as the first placement
            if (presentNumbers.Count == 0)
            {
                Debug.Log("No existing players");
                return _room.Config.PlayersPerRoom / 2;
            }


            // Consider the implicit boundaries 0 and 100
            presentNumbers.Insert(0, 0);
            presentNumbers.Add(Math.Min(_room.Config.PlayersPerRoom, Players.Count(slot => slot.InUse)));

            var maxGap = 0;
            var bestIndex = -1;

            // Find the largest gap
            for (var i = 0; i < presentNumbers.Count - 1; i++)
            {
                var start = presentNumbers[i];
                var end = presentNumbers[i + 1];
                var gap = end - start;

                if (gap <= maxGap) continue;

                maxGap = gap;
                bestIndex = start + gap / 2; // Midpoint of the gap
            }

            if (bestIndex != -1) return bestIndex;

            Debug.LogError(
                "Something went terribly wrong picking a spawn index for the new player. Failing over to spawning player in a random position.");
            bestIndex = Random.Range(0, _room.Config.PlayersPerRoom);

            return bestIndex;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void AddPlayerRpc(int playerSpawnIndex, NetworkId playerID, bool isBot, RpcInfo rpcInfo = default)
        {
            var player = new PlayerSlot
            {
                PlayerRef = rpcInfo.Source,
                IsBot = isBot,
                PlayerId = playerID,
                InUse = true
            };
            Players.Set(playerSpawnIndex, player);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void SetSlotOwnerRpc(int playerIndex, RpcInfo info = default)
        {
            var slot = Players[playerIndex];
            slot.PlayerRef = info.Source;
            Players.Set(playerIndex, slot);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void MarkSlotUnusedRpc(int playerIndex)
        {
            var slot = Players[playerIndex];
            slot.InUse = false;
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

        /// <summary>
        ///     Steal bots from other clients to spread bot load out
        /// </summary>
        /// <param name="numSpawned">The number of bots the client has spawned in itself</param>
        private void StealBots(int numSpawned)
        {
            var numBots = Players.Count(slot => slot.IsBot && slot.InUse && slot.PlayerRef != Runner.LocalPlayer) +
                          numSpawned;

            var botsByClient = Players.Select((slot, i) => new KeyValuePair<int, PlayerSlot>(i, slot)).Where(slot =>
                    slot.Value.InUse && slot.Value.IsBot && slot.Value.PlayerRef != Runner.LocalPlayer)
                .GroupBy(kvp => kvp.Value.PlayerRef).Select(group => group.ToList()).ToArray();

            // +1 to include self
            var botsPerClient = numBots / (botsByClient.Length + 1);

            // Bots this client already owns
            var botsStolen = numSpawned;
            for (var i = 0; botsStolen < botsPerClient; i = (i + 1) % botsByClient.Length)
            {
                StealBot(botsByClient[i].Last().Key);
                botsByClient[i].RemoveAt(botsByClient[i].Count - 1);
                botsStolen++;
            }
        }

        public override void Spawned()
        {
            Instance = this;

            StartCoroutine(WatchForCommands());

            var spawnPositions = CalcSpawnPositions();

            var spawnIndex = FindNewIndex();

            Debug.Log($"Our spawn index is {spawnIndex}");

            var player = Runner.Spawn(playerPrefab, spawnPositions[spawnIndex]);

            Camera.main!.transform.position =
                new Vector3(spawnPositions[spawnIndex].x, spawnPositions[spawnIndex].y, -1.0f);

            AddPlayerRpc(spawnIndex, player.Id, false);

            if (!HasStateAuthority)
            {
                // Despawn the bot whose place we're taking
                Runner.TryFindObject(Players[spawnIndex].PlayerId, out var botObj);
                var bot = botObj.GetComponent<Player>();
                bot.DestroyBotRpc();


                var currentPlayers = Players.Count(slot => slot.InUse);
                var neededPlayers = _room.Config.PlayersPerRoom - currentPlayers;
                var botsToSpawn = Math.Min(neededPlayers, _room.Config.MaxBotsPerClient);

                // Find empty slots to spawn the bots in
                for (var i = 0; botsToSpawn != 0; i++)
                {
                    if (Players[i].InUse) continue;

                    var newBot = Runner.Spawn(botPrefab, spawnPositions[i], Quaternion.identity);

                    AddPlayerRpc(i, newBot.Id, true);
                    botsToSpawn--;
                }

                StealBots(Math.Min(neededPlayers, _room.Config.MaxBotsPerClient));

                return;
            }

            // Spawn necessary bots
            for (var i = 0; i < _room.Config.MaxBotsPerClient; i++)
            {
                if (i == spawnIndex) continue;

                Debug.Log($"Spawning bot {i}");
                var bot = Runner.Spawn(botPrefab, spawnPositions[i], Quaternion.identity);
                var slot = new PlayerSlot
                {
                    IsBot = true,
                    PlayerRef = Runner.LocalPlayer,
                    PlayerId = bot.Id,
                    InUse = true
                };
                Players.Set(i, slot);
            }
        }

        private int _lastReceivedCommandNonce = -1;

        private struct GetCommandsRequest
        {
            [JsonProperty("room")] internal string Room;
            [JsonProperty("last_received_nonce")] internal int LastReceivedNonce;
        }

        private enum CommandType
        {
            Restart
        }

        private struct Command
        {
            [JsonConverter(typeof(StringEnumConverter))] [JsonProperty("command_type")]
            internal CommandType CommandType;

            [JsonProperty("nonce")] internal int Nonce;
        }

        private IEnumerator WatchForCommands()
        {
            while (true)
            {
                var body = new GetCommandsRequest
                {
                    Room = _room.Name,
                    LastReceivedNonce = _lastReceivedCommandNonce
                };

                var uwr = UnityWebRequest.Put($"{APIEndpoint}/commands", JsonConvert.SerializeObject(body));
                uwr.method = "GET";
                uwr.SetRequestHeader("Content-Type", "application/json");
                yield return uwr.SendWebRequest();

                var alreadyRestarted = false;

                var commands = JsonConvert.DeserializeObject<List<Command>>(uwr.downloadHandler.text);
                if (commands == null)
                {
                    yield return new WaitForSecondsRealtime(30);
                    continue;
                }

                foreach (var command in commands)
                {
                    Debug.Log(
                        $"SESSION MANAGER: Received new command {command.CommandType} with nonce {command.Nonce}");
                    _lastReceivedCommandNonce = command.Nonce;
                    switch (command.CommandType)
                    {
                        case CommandType.Restart when !alreadyRestarted:
                            alreadyRestarted = true;
                            TimerToScoreLock.reset(UI_Manager.Instance.destroy);
                            break;

                        case CommandType.Restart:
                            Debug.Log("Received command to restart, but already restarting");
                            break;

                        default:
                            Debug.Log($"Received unknown command: {command.CommandType}");
                            break;
                    }
                }

                // Wait 30 seconds before checking for new commands
                yield return new WaitForSecondsRealtime(30);
            }
            // ReSharper disable once IteratorNeverReturns
        }
    }
}