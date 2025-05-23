using System;
using Fusion;
using Horde;
using POI;
using Unity.Profiling;
using UnityEngine;

namespace Human
{
    public class PatrolController : NetworkBehaviour
    {
        private static readonly ProfilerMarker s_UpdateHumanCount = new("RPCPatrol.UpdateHumanCount");
        private static readonly ProfilerMarker s_DealDamage = new("RPCPopulation.DealDamage");

        private static readonly ProfilerMarker s_Attack = new("RPCPatrol.Attack");
        [SerializeField] private PoiController poi; // POI reference (van)

        // Each human's base health
        [SerializeField] private float healthPerHuman = 5f;

        // How much damage each human deals per second
        [SerializeField] private float damagePerHuman = 0.5f;

        // Optionally track how much damage rats do to humans
        [SerializeField] private float ratDPS = 0.5f;

        public int startingHumanCount;

        private Transform _poiCenter;

        private HordeController enemyHorde;

        public int HumanCount => (int)(CurrentHumanHealth / healthPerHuman); // Networked human count

        // We store total health as a Networked field so that all players see the same value
        [Networked]
        [OnChangedRender(nameof(AdjustHumanCount))]
        public float CurrentHumanHealth { get; set; }

        public override void Spawned()
        {
            if (poi != null)
                _poiCenter = poi.transform;
            else
                return;

            if (HasStateAuthority) CurrentHumanHealth = startingHumanCount * healthPerHuman;
        }

        private void AdjustHumanCount()
        {
            GameManager.Instance.BoidPois[poi.boidPoisIndex].NumBoids = Convert.ToUInt32(HumanCount);
        }

        // Can be used when dynamically changing human count is needed (e.g., UI buttons)
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UpdateHumanCountRpc(int newCount)
        {
            s_UpdateHumanCount.Begin();
            CurrentHumanHealth = newCount * healthPerHuman; // Updates across all clients
            s_UpdateHumanCount.End();
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void DealDamageRpc(float damage)
        {
            s_DealDamage.Begin();
            CurrentHumanHealth -= damage;
            CurrentHumanHealth = Mathf.Max(0, CurrentHumanHealth);
            s_DealDamage.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AttackRpc(HordeController attacker)
        {
            s_Attack.Begin();
            enemyHorde = attacker;
            s_Attack.End();
        }

        public float GetCurrentHumanHealth()
        {
            return CurrentHumanHealth;
        }

        public override void FixedUpdateNetwork()
        {
            if (enemyHorde)
            {
                if (HumanCount <= 0)
                {
                    // This could technically break if somehow the authority for the patrol controller is different to the one for the POI
                    // But they should both be controlled by the master client
                    poi.ChangeControllerRpc(enemyHorde.player);
                    enemyHorde.StationAtRpc(poi);
                    enemyHorde = null;
                    return;
                }

                if (enemyHorde.AliveRats < 7)
                {
                    enemyHorde.RetreatRpc();
                    enemyHorde = null;
                }
                else
                {
                    enemyHorde.DealDamageRpc(damagePerHuman * HumanCount);
                }
            }
        }
    }
}