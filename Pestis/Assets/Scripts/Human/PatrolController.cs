using System.Collections.Generic;
using Fusion;
using Horde;
using POI;
using UnityEngine;

namespace Human
{
    public class PatrolController : NetworkBehaviour
    {
        [SerializeField] private GameObject humanPrefab;
        [SerializeField] private POIController poi; // POI reference (van)

        // Each human's base health
        [SerializeField] private float healthPerHuman = 5f;

        // How much damage each human deals per second
        [SerializeField] private float damagePerHuman = 0.5f;

        // Optionally track how much damage rats do to humans
        [SerializeField] private float ratDPS = 0.5f;

        public int startingHumanCount;
        private readonly List<GameObject> _spawnedHumans = new();

        private Transform _poiCenter;

        private HordeController enemyHorde;

        public int HumanCount => (int)(CurrentHumanHealth / healthPerHuman); // Networked human count

        [Networked]
        [OnChangedRender(nameof(OnPatrolRadiusChanged))]
        public float PatrolRadius { get; set; } = 5.0f; // Networked patrol radius

        // We store total health as a Networked field so that all players see the same value
        [Networked] public float CurrentHumanHealth { get; set; }

        public override void Spawned()
        {
            if (poi != null)
                _poiCenter = poi.transform;
            else
                return;

            if (HasStateAuthority) CurrentHumanHealth = startingHumanCount * healthPerHuman;
        }


        private void OnPatrolRadiusChanged()
        {
            AdjustPatrolRadius();
        }

        private void AdjustPatrolRadius()
        {
            // Update the patrol radius for all humans
            foreach (var human in _spawnedHumans)
                if (human.TryGetComponent(out HumanController humanScript))
                    humanScript.UpdatePatrolRadius(PatrolRadius);
        }

        private void AdjustHumanCount()
        {
            var difference = HumanCount - _spawnedHumans.Count;

            if (difference > 0)
                // Spawn more humans
                for (var i = 0; i < difference; i++)
                {
                    var spawnPosition = _poiCenter.position + (Vector3)Random.insideUnitCircle * PatrolRadius;
                    spawnPosition.z = 0;
                    var human = Instantiate(humanPrefab, spawnPosition, Quaternion.identity);
                    _spawnedHumans.Add(human);

                    // Assign POI to HumanController
                    if (human.TryGetComponent(out HumanController humanScript))
                    {
                        humanScript.SetPOI(_poiCenter);
                        humanScript.UpdatePatrolRadius(PatrolRadius);
                    }
                }
            else if (difference < 0)
                // Remove excess humans
                for (var i = 0; i < -difference; i++)
                    if (_spawnedHumans.Count > 0)
                    {
                        var toRemove = _spawnedHumans[_spawnedHumans.Count - 1];
                        _spawnedHumans.RemoveAt(_spawnedHumans.Count - 1);
                        Destroy(toRemove);
                    }
        }

        // Can be used when dynamically changing human count is needed (e.g., UI buttons)
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UpdateHumanCountRpc(int newCount)
        {
            CurrentHumanHealth = newCount * healthPerHuman; // Updates across all clients
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void DealDamageRpc(float damage)
        {
            CurrentHumanHealth -= damage;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AttackRpc(HordeController attacker)
        {
            enemyHorde = attacker;
        }

        public override void FixedUpdateNetwork()
        {
            AdjustHumanCount();

            if (enemyHorde)
            {
                if (enemyHorde.AliveRats < 5)
                {
                    enemyHorde.RetreatRpc();
                    enemyHorde = null;
                }
                else
                {
                    enemyHorde.DealDamageRpc(damagePerHuman * HumanCount);
                }

                if (HumanCount == 0)
                {
                    // This could technically break if somehow the authority for the patrol controller is different to the one for the POI
                    // But they should both be controlled by the master client
                    poi.ChangeController(enemyHorde.Player);
                    enemyHorde.StationAtRpc(poi);
                    enemyHorde = null;
                }
            }
        }
    }
}