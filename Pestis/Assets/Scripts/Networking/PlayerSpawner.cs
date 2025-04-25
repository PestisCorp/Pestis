using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    private const int numBots = 0;
    private const int spawnSeed = 312;
    private static readonly Vector2 spawnCenter = new(0, 0);

    public GameObject PlayerPrefab;
    public GameObject BotPrefab;

    public void PlayerJoined(PlayerRef player)
    {
        if (player == Runner.LocalPlayer)
        {
            Debug.Log($"Spawning player {player.AsIndex}");
            const float A = 8;
            const float dtheta = (float)(40 * Math.PI / 180); // 20 degrees.
            List<Vector2> spawnPositions = new();
            for (var theta = dtheta * 4 + player.AsIndex * 5 * dtheta;; theta += dtheta)
            {
                // Calculate r.
                var r = A * theta;

                Debug.Log($"Theta: {theta}");
                Debug.Log($"R: {r}");
                // Convert to Cartesian coordinates.
                float x, y;
                PolarToCartesian(r, theta, out x, out y);

                // Center.
                x += spawnCenter.x;
                y += spawnCenter.y;

                var point = new Vector2(x, y);

                var tilePos = GameManager.Instance.terrainMap.WorldToCell(point);
                var tile = GameManager.Instance.terrainMap.GetTile(tilePos);

                // Don't spawn on water
                if (tile == GameManager.Instance.map.water) continue;

                spawnPositions.Add(point);

                if (spawnPositions.Count == numBots + 1) break;
            }

            Runner.Spawn(PlayerPrefab, spawnPositions[0],
                Quaternion.identity);

            for (var i = 1; i < numBots + 1; i++)
                Runner.Spawn(BotPrefab, spawnPositions[i],
                    Quaternion.identity);

            Camera.main.transform.position = new Vector3(spawnPositions[0].x, spawnPositions[0].y, -1.0f);
        }
    }

    // Convert polar coordinates into Cartesian coordinates.
    private void PolarToCartesian(float r, float theta,
        out float x, out float y)
    {
        x = (float)(r * Math.Cos(theta));
        y = (float)(r * Math.Sin(theta));
    }
}