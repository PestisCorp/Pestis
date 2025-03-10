using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using Human;
using Players;
using POI;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horde
{
    public class AbilityController : NetworkBehaviour
    {
        private HordeController _hordeController;
        private PopulationController _populationController;
        public int abilityHaste = 0;
        public bool feared = false;
        public bool forceCooldownRefresh = false;
        
        public void UsePestis(Button calledBy)
        {
            if (feared)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("You are feared and cannot use any abilities!", Color.red);
                return;
            }
            
            HashSet<HordeController> affectedHordes = new HashSet<HordeController>();
            var players = GameManager.Instance.Players;
            foreach (var player in players)
            {
                foreach (var horde in player.Hordes)
                {
                    if (horde.GetHashCode() == _hordeController.GetHashCode()) continue;
                    var dist = ((Vector2)horde.GetBounds().center - (Vector2)_hordeController.GetBounds().center).sqrMagnitude;
                    if (dist < 20f)
                    {
                        affectedHordes.Add(horde);
                        var populationController = horde.GetComponent<PopulationController>();
                        float damageReductionMult = horde.GetPopulationState().DamageReductionMult * 1.3f;
                        populationController.SetDamageReductionMult(damageReductionMult);
                        StartCoroutine(RemovePestisAfterDelayRat(populationController));
                    }
                }
            }
            
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(_hordeController.GetBounds().center, 20f);
            HashSet<HumanController> affectedHumans = new HashSet<HumanController>();
            foreach (var col in hitColliders)
            {
                HumanController affectedHuman = col.GetComponentInParent<HumanController>();
                if (affectedHuman && affectedHumans.Add(affectedHuman))
                {
                    affectedHuman.SetRadius(affectedHuman.GetRadius() + 10.0f);
                    StartCoroutine(RemovePestisAfterDelayHuman(affectedHuman));
                }
            }
            if (affectedHordes.Count == 0 && affectedHumans.Count == 0)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("No enemies nearby!", Color.red);
                return;
            }
            _hordeController.TotalHealth = (int)Math.Ceiling(_hordeController.AliveRats * _populationController.GetState().HealthPerRat * 0.7);
            StartCoroutine(Cooldown(60, calledBy, "Pestis"));
        }
        
        IEnumerator RemovePestisAfterDelayHuman(HumanController affectedHuman)
        {
            yield return new WaitForSeconds(30f);
            if (affectedHuman)
                affectedHuman.SetRadius(affectedHuman.GetRadius() - 10.0f);
        }
        
        IEnumerator RemovePestisAfterDelayRat(PopulationController affectedEnemy)
        {
            yield return new WaitForSeconds(30f);
            if (affectedEnemy)
                affectedEnemy.SetDamageReductionMult(affectedEnemy.GetState().DamageReductionMult / 1.3f);
        }

        public void UseSewerDwellers(Button calledBy)
        {
            POIController travelFrom = null;
            foreach (var poi in _hordeController.Player.ControlledPOIs)
            {
                if (!poi.gameObject.name.Contains("City")) continue;
                if (!poi.Collider.bounds.Contains(_hordeController.GetBounds().center)) continue;
                travelFrom = poi;
                break;
            }
            
            if (travelFrom == null)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("You are not near a city that you control!", Color.red);
                return;
            }
            
            POIController travelTo = null;
            foreach (var poi in _hordeController.Player.ControlledPOIs)
            {
                if (!poi.gameObject.name.Contains("City")) continue;
                if (poi.GetHashCode() == travelFrom.GetHashCode()) continue;
                if (travelTo == null)
                {
                    travelTo = poi;
                }
                else
                {
                    var dist = (travelFrom.Collider.bounds.center - travelTo.Collider.bounds.center).sqrMagnitude;
                    var newDist = (travelFrom.Collider.bounds.center - poi.Collider.bounds.center).sqrMagnitude;
                    if (newDist < dist)
                    {
                        travelTo = poi;
                    }
                }
            }

            if (travelTo == null)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("You do not control any other cities!", Color.red);
                return;
            }
            
            _hordeController.targetLocation.Teleport(travelTo.Collider.bounds.center);
            _hordeController.TeleportHordeRPC(travelTo.Collider.bounds.center);
            
        }
        
        IEnumerator Cooldown(int duration, Button calledBy, string abilityName)
        {
            calledBy.onClick.RemoveAllListeners();
            calledBy.onClick.AddListener(delegate {FindFirstObjectByType<UI_Manager>().AddNotification($"{abilityName} is on cooldown!", Color.red);});
            var elapsedTime = 0.0f;
            CooldownBar cooldownBar = calledBy.GetComponentInChildren<CooldownBar>();
            cooldownBar.current = 100;
            while (elapsedTime < duration - abilityHaste)
            {
                elapsedTime += Time.deltaTime;
                cooldownBar.current = 100 - (int)(elapsedTime / duration * 100);
                yield return null;
            }
            calledBy.onClick.RemoveAllListeners();
            switch (abilityName)
            {
                case "Pestis":
                    calledBy.onClick.AddListener(delegate {UsePestis(calledBy);});
                    break;
            }
        }
        
        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
        }
    }
}