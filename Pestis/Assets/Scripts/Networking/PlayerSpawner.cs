using Fusion;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    public GameObject PlayerPrefab;
    public GameObject BotPrefab;

    public void PlayerJoined(PlayerRef player)
    {
        if (player == Runner.LocalPlayer)
        {
            Runner.Spawn(PlayerPrefab, new Vector2(Random.Range(-10.0f, 10.0f), Random.Range(-5.0f, 5.0f)),
                Quaternion.identity);


            Runner.Spawn(BotPrefab, new Vector2(Random.Range(-10.0f, 10.0f), Random.Range(-5.0f, 5.0f)),
                Quaternion.identity);


            Runner.Spawn(BotPrefab, new Vector2(Random.Range(-10.0f, 10.0f), Random.Range(-5.0f, 5.0f)),
                Quaternion.identity);

            Runner.Spawn(BotPrefab, new Vector2(Random.Range(-10.0f, 10.0f), Random.Range(-5.0f, 5.0f)),
                Quaternion.identity);

            Runner.Spawn(BotPrefab, new Vector2(Random.Range(-10.0f, 10.0f), Random.Range(-5.0f, 5.0f)),
                Quaternion.identity);
        }
    }
}