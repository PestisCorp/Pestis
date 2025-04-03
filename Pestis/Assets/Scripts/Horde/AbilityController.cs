using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Human;
using Networking;
using POI;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horde
{
    public enum Abilities
    {
        Pestis,
        SewerDwellers,
        Poltergeist,
        Apparition,
        MAD,
        Corpsebloom
    }

    public class AbilityController : NetworkBehaviour
    {
        public int abilityHaste;
        public bool forceCooldownRefresh;
        private HordeController _hordeController;
        private PopulationController _populationController;

        public void UsePestis(Button calledBy)
        {
            var affectedHordes = new HashSet<HordeController>();
            var players = GameManager.Instance.Players;
            foreach (var player in players)
            foreach (var horde in player.Hordes)
            {
                if (_hordeController.player.Hordes.Contains(horde)) continue;
                var dist = Vector2.Distance(horde.GetBounds().center, _hordeController.GetBounds().center);
                if (dist < 20f)
                {
                    affectedHordes.Add(horde);
                    var populationController = horde.GetComponent<PopulationController>();
                    var damageReductionMult = horde.GetPopulationState().DamageReductionMult * 1.3f;
                    populationController.SetDamageReductionMultRpc(damageReductionMult);
                    StartCoroutine(RemovePestisAfterDelayRat(populationController));
                }
            }

            var hitColliders = Physics2D.OverlapCircleAll(_hordeController.GetBounds().center, 20f);
            var affectedHumans = new HashSet<HumanController>();
            foreach (var col in hitColliders)
            {
                var affectedHuman = col.GetComponentInParent<HumanController>();
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


            _hordeController.TotalHealth = (int)Math.Ceiling((int)_hordeController.AliveRats *
                                                             _populationController.GetState().HealthPerRat * 0.7);
            StartCoroutine(Cooldown(60, calledBy, Abilities.Pestis));
        }

        private IEnumerator RemovePestisAfterDelayHuman(HumanController affectedHuman)
        {
            yield return new WaitForSeconds(30f);
            if (affectedHuman)
                affectedHuman.SetRadius(affectedHuman.GetRadius() - 10.0f);
        }

        private IEnumerator RemovePestisAfterDelayRat(PopulationController affectedEnemy)
        {
            yield return new WaitForSeconds(30f);
            if (affectedEnemy)
                affectedEnemy.SetDamageReductionMultRpc(affectedEnemy.GetState().DamageReductionMult / 1.3f);
        }

        public void UseSewerDwellers(Button calledBy)
        {
            PoiController travelFrom = null;
            foreach (var poi in _hordeController.player.ControlledPOIs)
            {
                if (!poi.gameObject.name.Contains("City")) continue;
                if (Vector2.Distance(poi.Collider.bounds.center, _hordeController.GetBounds().center) > 50f) continue;
                if (!poi.Collider.bounds.Contains(_hordeController.GetBounds().center)) continue;
                travelFrom = poi;
                break;
            }

            if (!travelFrom)
            {
                FindFirstObjectByType<UI_Manager>()
                    .AddNotification("You are not near a city that you control!", Color.red);
                return;
            }

            PoiController travelTo = null;
            foreach (var poi in _hordeController.player.ControlledPOIs)
            {
                if (!poi.gameObject.name.Contains("City")) continue;
                if (poi.Id == travelFrom.Id) continue;
                if (!travelTo)
                {
                    travelTo = poi;
                }
                else
                {
                    var dist = (travelFrom.Collider.bounds.center - travelTo.Collider.bounds.center).sqrMagnitude;
                    var newDist = (travelFrom.Collider.bounds.center - poi.Collider.bounds.center).sqrMagnitude;
                    if (newDist < dist) travelTo = poi;
                }
            }

            if (!travelTo)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("You do not control any other cities!", Color.red);
                return;
            }

            _hordeController.targetLocation.Teleport(travelTo.Collider.bounds.center);
            _hordeController.TeleportHordeRPC(travelTo.Collider.bounds.center);
            Camera.main.GetComponent<Panner>().PanTo(_hordeController);
            StartCoroutine(Cooldown(60, calledBy, Abilities.SewerDwellers));
        }

        public void UsePoltergeist(Button calledBy)
        {
            _populationController.SetDamageReductionMultRpc(_populationController.GetState().DamageMult * 0.001f);
            StartCoroutine(Cooldown(90, calledBy, Abilities.Poltergeist));
            StartCoroutine(RemovePoltergeist());
        }

        private IEnumerator RemovePoltergeist()
        {
            yield return new WaitForSeconds(5f);
            _populationController.SetDamageReductionMultRpc(_populationController.GetState().DamageMult / 0.001f);
        }

        public void UseApparition(Button calledBy)
        {
            var populationState = _hordeController.GetPopulationState();
            var evolutionaryState = _hordeController.GetEvolutionState();
            var newHorde = Runner.Spawn(_hordeController.player.hordePrefab, Vector3.zero,
                    Quaternion.identity,
                    null, (runner, NO) =>
                    {
                        NO.transform.parent = _hordeController.player.transform;
                        // Ensure new horde spawns in at current location
                        NO.transform.position = _hordeController.GetBounds().center;
                        var horde = NO.GetComponent<HordeController>();
                        horde.AliveRats = new IntPositive(_hordeController.AliveRats);
                        horde.SetPopulationState(populationState);
                        horde.SetPopulationInit(_hordeController.AliveRats);
                    })
                .GetComponent<HordeController>();
            newHorde.isApparition = true;
            _hordeController.CreateApparitionRPC(newHorde, newHorde.AliveRats);
            newHorde.SetEvolutionaryState(evolutionaryState.DeepCopy());
            newHorde.Move(_hordeController.targetLocation.transform.position - _hordeController.GetBounds().extents);
            GameManager.Instance.UIManager.ActionPanelDisable();
            GameManager.Instance.UIManager.ActionPanelEnable();
            StartCoroutine(Cooldown(120, calledBy, Abilities.Apparition));
            StartCoroutine(RemoveApparation(newHorde));
        }

        private IEnumerator RemoveApparation(HordeController apparition)
        {
            yield return new WaitForSeconds(20f);
            while (apparition.InCombat) yield return null;
            if (apparition)
                apparition.DestroyHordeRpc();
        }

        public void UseMAD(Button calledBy)
        {
            var affectedHordes = new HashSet<HordeController>();
            var players = GameManager.Instance.Players;
            foreach (var player in players)
            foreach (var horde in player.Hordes)
            {
                if (horde.Id == _hordeController.Id) continue;
                var dist = Vector2.Distance(horde.GetBounds().center, _hordeController.GetBounds().center);
                if (!(dist < 30)) continue;
                affectedHordes.Add(horde);
                horde.TotalHealth =
                    (int)Math.Ceiling((int)horde.AliveRats * horde.GetPopulationState().HealthPerRat * 0.2);
            }

            if (affectedHordes.Count > 0)
            {
                _hordeController.TotalHealth = (int)Math.Ceiling((int)_hordeController.AliveRats *
                                                                 _populationController.GetState().HealthPerRat * 0.2);
                _hordeController.populationCooldown += 10;
            }
            else
            {
                GameManager.Instance.UIManager.AddNotification("You are not near any hordes!", Color.red);
            }

            StartCoroutine(Cooldown(180, calledBy, Abilities.MAD));
        }

        public void UseCorpseBloom(Button calledBy)
        {
            if (_hordeController.InCombat)
            {
                GameManager.Instance.UIManager.AddNotification("You cannot use Corpsebloom in combat!", Color.red);
                return;
            }

            if (_hordeController.player.Hordes.Count == 1)
            {
                GameManager.Instance.UIManager.AddNotification("Cannot use Corpsebloom with one horde left!",
                    Color.red);
                return;
            }
            Destroy(calledBy.GetComponent<Tooltip>().tooltipInstance);
            _hordeController.DestroyHordeRpc();
            GameManager.Instance.UIManager.ResetUI();
            foreach (var horde in _hordeController.player.Hordes)
                horde.GetComponent<PopulationController>().SetBirthRateRpc(horde.GetPopulationState().BirthRate * 1.5);
        }

        private IEnumerator Cooldown(int duration, Button calledBy, Abilities ability)
        {
            calledBy.onClick.RemoveAllListeners();
            calledBy.onClick.AddListener(delegate
            {
                FindFirstObjectByType<UI_Manager>().AddNotification(
                    $"{Enum.GetName(typeof(Abilities), ability)} is on cooldown!", Color.red);
            });
            var elapsedTime = 0.0f;
            var cooldownBar = calledBy.GetComponentInChildren<CooldownBar>();
            cooldownBar.current = 100;
            while (elapsedTime < duration - abilityHaste)
            {
                elapsedTime += Time.deltaTime;
                cooldownBar.current = 100 - (int)(elapsedTime / duration * 100);
                if (forceCooldownRefresh)
                {
                    cooldownBar.current = 0;
                    forceCooldownRefresh = false;
                    break;
                }

                yield return null;
            }

            calledBy.onClick.RemoveAllListeners();
            switch (ability)
            {
                case Abilities.Pestis:
                    calledBy.onClick.AddListener(delegate { UsePestis(calledBy); });
                    break;
                case Abilities.SewerDwellers:
                    calledBy.onClick.AddListener(delegate { UseSewerDwellers(calledBy); });
                    break;
                case Abilities.Poltergeist:
                    calledBy.onClick.AddListener(delegate { UsePoltergeist(calledBy); });
                    break;
                case Abilities.Apparition:
                    calledBy.onClick.AddListener(delegate { UseApparition(calledBy); });
                    break;
            }
        }

        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RefreshCooldownsRpc()
        {
            forceCooldownRefresh = true;
        }
    }
}