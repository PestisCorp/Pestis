using Fusion;
using Players;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    public GameObject PlayerPrefab;

    public void PlayerJoined(PlayerRef player)
    {
        if (player == Runner.LocalPlayer)
        {
            Runner.Spawn(PlayerPrefab, new Vector2(Random.Range(-10.0f, 10.0f), Random.Range(-5.0f, 5.0f)), Quaternion.identity);
        }
    }
}
