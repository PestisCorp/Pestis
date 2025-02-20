using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using Players;
using UnityEngine;

namespace Horde
{
    public class AbilityController : NetworkBehaviour
    {
        private HordeController _hordeController;
        private PopulationController _populationController;
        
        public void UsePestis()
        {
            _hordeController.TotalHealth = (int)Math.Ceiling(_hordeController.AliveRats * _populationController.GetState().HealthPerRat * 0.7);
            HashSet<PopulationController> affectedHordes = new HashSet<PopulationController>();
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(_hordeController.GetBounds().center, 5f);
            foreach (var col in hitColliders)
            {
                PopulationController affectedEnemy = col.GetComponentInParent<PopulationController>();
                if (affectedEnemy && affectedHordes.Add(affectedEnemy))
                {
                    affectedEnemy.SetDamageReductionMult(affectedEnemy.GetState().DamageReductionMult * 0.7f);
                    StartCoroutine(RemovePestisAfterDelay(affectedEnemy));
                }
            }
        }

        public void RemovePestis(PopulationController affectedEnemy)
        {
            affectedEnemy.SetDamageReductionMult(affectedEnemy.GetState().DamageReductionMult / 0.7f);
        }
        
        IEnumerator RemovePestisAfterDelay(PopulationController affectedEnemy)
        {
            yield return new WaitForSeconds(30f);
            RemovePestis(affectedEnemy);
        }

        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
        }
    }
}