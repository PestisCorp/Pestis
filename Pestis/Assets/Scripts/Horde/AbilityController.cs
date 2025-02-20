using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using Players;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horde
{
    public class AbilityController : NetworkBehaviour
    {
        private HordeController _hordeController;
        private PopulationController _populationController;
        
        public void UsePestis(Button calledBy)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(_hordeController.GetBounds().center, 5f);
            HashSet<PopulationController> affectedHordes = new HashSet<PopulationController>();
            affectedHordes.Add(_hordeController.GetComponent<PopulationController>());
            foreach (var col in hitColliders)
            {
                PopulationController affectedEnemy = col.GetComponentInParent<PopulationController>();
                if (affectedEnemy && affectedHordes.Add(affectedEnemy) )
                {
                    float damageReduction = affectedEnemy.GetState().DamageReduction * 1.3f;
                    affectedEnemy.SetDamageReduction(damageReduction);
                    StartCoroutine(RemovePestisAfterDelay(affectedEnemy));
                }
            }
            if (affectedHordes.Count == 1)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("No enemy hordes nearby!", Color.red);
                return;
            }
            _hordeController.TotalHealth = (int)Math.Ceiling(_hordeController.AliveRats * _populationController.GetState().HealthPerRat * 0.7);
            
            StartCoroutine(Cooldown(60, calledBy, "Pestis"));
        }
        
        
        
        public void RemovePestis(PopulationController affectedEnemy)
        {
            affectedEnemy.SetDamageReduction(affectedEnemy.GetState().DamageReduction / 1.3f);
        }
        
        IEnumerator RemovePestisAfterDelay(PopulationController affectedEnemy)
        {
            yield return new WaitForSeconds(30f);
            RemovePestis(affectedEnemy);
        }

        IEnumerator Cooldown(int duration, Button calledBy, string abilityName)
        {
            calledBy.onClick.RemoveAllListeners();
            calledBy.onClick.AddListener(delegate {FindFirstObjectByType<UI_Manager>().AddNotification($"{abilityName} is on cooldown!", Color.red);});
            var elapsedTime = 0.0f;
            CooldownBar cooldownBar = calledBy.GetComponentInChildren<CooldownBar>();
            cooldownBar.current = 100;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                cooldownBar.current = 100 - (int)(elapsedTime / duration * 100);
                yield return null;
            }
            calledBy.onClick.RemoveAllListeners();
            calledBy.onClick.AddListener(delegate {UsePestis(calledBy);});
        }
        
        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
        }
    }
}