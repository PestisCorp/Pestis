using System.Linq;
using Fusion;
using Horde;
using UnityEngine;

namespace Human
{
    public class HumanCombatController : NetworkBehaviour
    {
        // Reference to your PatrolController (which spawns the humans)
        [SerializeField] private PatrolController patrolController;

        // Each human's base health
        [SerializeField] private float healthPerHuman = 5f;

        // How much damage each human deals per second
        [SerializeField] private float damagePerHuman = 0.5f;

        // We store total health as a Networked field so that all players see the same value
        [Networked] public float CurrentHumanHealth { get; set; }

        // Optionally track how much damage rats do to humans
        [SerializeField] private float ratDPS = 0.5f;

        private void Awake()
        {
            if (!patrolController)
                patrolController = GetComponent<PatrolController>();
        }

        public override void Spawned()
        {
            base.Spawned();

            // Initialize health once on the server
            if (Object.HasStateAuthority)
            {
                RecalculateHumanHealth();
            }
        }

        public void RecalculateHumanHealth()
        {
            // For example: total health = number of humans × health per human
            if (patrolController)
                CurrentHumanHealth = patrolController.HumanCount * healthPerHuman;
        }

        // Example if you want to reduce the human count => you can recompute total health
        public void RemoveHumans(int numberKilled)
        {
            if (!Object.HasStateAuthority) return;
            patrolController.UpdateHumanCountRpc(patrolController.HumanCount - numberKilled);
            RecalculateHumanHealth();
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return; // Only the server updates combat


            // If no humans left, nothing to do
            if (CurrentHumanHealth <= 0 || patrolController.HumanCount <= 0) return;

            // Check for nearby rat hordes
            var rats = FindObjectsOfType<HordeController>()
                .Where(horde =>
                {
                    // e.g. distance to your POI/humans
                    float dist = Vector2.Distance(
                        horde.GetBounds().center,
                        patrolController.transform.position
                    );
                    return dist < 10f; // or whatever "in combat" range you want
                })
                .ToArray();

            if (rats.Length == 0) return; // No rats in range => no combat

            // Humans fight each rat horde in range:
            float dt = Runner.DeltaTime; // Photon Fusion time step
            float humansDPS = damagePerHuman * patrolController.HumanCount; // total DPS from humans

            foreach (var ratHorde in rats)
            {
                // Subtract HP from the rats
                ratHorde.DealDamageRpc(humansDPS * dt);

                // Subtract HP from the human
                CurrentHumanHealth -= ratDPS * dt;

                // If humans die
                if (CurrentHumanHealth <= 0)
                {
                    CurrentHumanHealth = 0;
                    // You might set patrolController.HumanCount = 0, 
                    // or call a method to kill them off visually:
                    patrolController.UpdateHumanCountRpc(0);
                    Debug.Log("All humans died defending the POI!");
                    return;
                }
            }
        }
    }
}