using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using Human;
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
        public bool forceCooldownRefresh = false;
        
        public void UsePestis(Button calledBy)
        {
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(_hordeController.GetBounds().center, 20f);
            HashSet<PopulationController> affectedHordes = new HashSet<PopulationController>();
            HashSet<HumanController> affectedHumans = new HashSet<HumanController>();
            foreach (var col in hitColliders)
            {
                PopulationController affectedEnemy = col.GetComponentInParent<PopulationController>();
                if (affectedEnemy && affectedHordes.Add(affectedEnemy) && !affectedEnemy.GetComponent<HordeController>().Player.IsLocal)
                {
                    float damageReductionMult = affectedEnemy.GetState().DamageReductionMult * 1.3f;
                    affectedEnemy.SetDamageReductionMult(damageReductionMult);
                    StartCoroutine(RemovePestisAfterDelayRat(affectedEnemy));
                }
                HumanController affectedHuman = col.GetComponentInParent<HumanController>();
                if (affectedHuman && affectedHumans.Add(affectedHuman))
                {
                    affectedHuman.SetRadius(affectedHuman.GetRadius() + 10.0f);
                    StartCoroutine(RemovePestisAfterDelayHuman(affectedHuman));
                }
            }
            if (affectedHordes.Count == 0)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("No enemy hordes nearby!", Color.red);
                return;
            }
            _hordeController.TotalHealth = (int)Math.Ceiling(_hordeController.AliveRats * _populationController.GetState().HealthPerRat * 0.7);
            
            StartCoroutine(Cooldown(60, calledBy, "Pestis"));
        }
        
        IEnumerator RemovePestisAfterDelayHuman(HumanController affectedHuman)
        {
            yield return new WaitForSeconds(30f);
            affectedHuman.SetRadius(affectedHuman.GetRadius() - 10.0f);
        }
        
        IEnumerator RemovePestisAfterDelayRat(PopulationController affectedEnemy)
        {
            yield return new WaitForSeconds(30f);
            affectedEnemy.SetDamageReductionMult(affectedEnemy.GetState().DamageReductionMult / 1.3f);
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