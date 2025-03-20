using System;
using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    private const int numBots = 99;
    private const int spawnSeed = 312;
    private static readonly Vector2 spawnCenter = new(0, 0);

    public GameObject PlayerPrefab;
    public GameObject BotPrefab;

    public void PlayerJoined(PlayerRef player)
    {
    }

    // Convert polar coordinates into Cartesian coordinates.
    private void PolarToCartesian(float r, float theta,
        out float x, out float y)
    {
        x = (float)(r * Math.Cos(theta));
        y = (float)(r * Math.Sin(theta));
    }
}