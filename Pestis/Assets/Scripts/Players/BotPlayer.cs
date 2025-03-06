using System;
using System.Collections.Generic;
using System.Linq;
using Horde;
using MathNet.Numerics.Statistics;
using MoreLinq.Extensions;
using POI;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Players
{
    /// <summary>
    ///     Controls a single AI player, responsible for deciding what actions to take
    /// </summary>
    public class BotPlayer : MonoBehaviour
    {
        public Player player;

        /// <summary>
        ///     Squared distance below which a horde will be considered a problem and either attacked or moved away from
        /// </summary>
        public float territorialDistance = 100;

        private float _timeSinceLastUpdate;

        /// <summary>
        ///     Multiplier to current `aggressionUncapped`, makes a horde more likely to take offensive action at all times
        /// </summary>
        public float BaseAggression { get; private set; } = 1.0f;

        /// <summary>
        ///     Arbitrary float, starts at 0.0, and increases over time - increasing desire to take offensive action. Reset to zero
        ///     when offensive action taken.
        ///     away
        /// </summary>
        public float AggressionUncapped { get; private set; }

        public float AggressionRange => 400.0f * AggressionUncapped * BaseAggression;

        /// <summary>
        ///     True if this instance is the one that should handle the bot
        /// </summary>
        public bool IsRunningOnThisMachine => player.HasStateAuthority;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            BaseAggression = Random.Range(0.5f, 1.5f);
            territorialDistance = Random.Range(2.0f, 20.0f);
        }

        private void FixedUpdate()
        {
            // Only State Authority for the Player should process bot logic
            if (!IsRunningOnThisMachine) return;
            // Only run logic update every second
            _timeSinceLastUpdate += Time.deltaTime;
            if (_timeSinceLastUpdate < 1.0f) return;
            _timeSinceLastUpdate = 0.0f;

            var allHordes =
                FindObjectsByType<HordeController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();

            var allPoi =
                FindObjectsByType<POIController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();

            var medianHordeSize = allHordes.Select(horde => horde.TotalHealth).Median();

            // Check possible actions for each horde, exiting when a horde takes an action
            foreach (var myHorde in player.Hordes)
            {
                // Don't take any actions if we're already in combat or attacking a POI
                if (myHorde.InCombat || myHorde.TargetPoi) continue;

                var hordesByDistance = allHordes.Select(horde =>
                        new Tuple<HordeController, float>(horde,
                            (horde.GetBounds().center - myHorde.GetBounds().center).sqrMagnitude))
                    .OrderBy(horde => horde.Item2).ToList();

                // Remove the closest horde as it's ourselves!
                hordesByDistance.RemoveAt(0);

                var closestHorde = hordesByDistance.First().Item1;
                var closestHordeDistance = hordesByDistance.First().Item2;

                var distFromHordeEdgeToClosestHorde = closestHordeDistance -
                                                      closestHorde.GetBounds()
                                                          .extents.sqrMagnitude -
                                                      myHorde.GetBounds().extents.sqrMagnitude;

                // DEFENSIVE ACTIONS

                // If nearest horde is too close, either attack or run away
                if (distFromHordeEdgeToClosestHorde < territorialDistance)
                {
                    var closestHordeIsFriendly = closestHorde.Player == myHorde.Player;
                    var attack = !closestHordeIsFriendly;
                    if (!closestHordeIsFriendly) // If we can attack this horde, let's calculate a desirability
                    {
                        var desirability = CalcCombatDesirability(myHorde, closestHorde);
                        attack = Random.Range(0.0f, 1.0f) < desirability;
                    }

                    if (attack)
                    {
                        Debug.Log($"Horde {myHorde.Id} too close to other horde, attacking!");
                        myHorde.AttackHorde(closestHorde, "");
                        return;
                    }

                    Debug.Log($"Horde {myHorde.Id} too close to other horde, moving away!");

                    Vector2 pushDirection = (myHorde.GetBounds().center - closestHorde.GetBounds().center).normalized;
                    // Go 10 tiles in the opposite direction to the nearest horde.
                    var newLocation = (Vector2)myHorde.GetBounds().center + pushDirection * 10.0f;
                    myHorde.Move(newLocation);
                    // Return because we only want to take one action each second
                    return;
                }

                // MANAGEMENT ACTIONS

                if (myHorde.TotalHealth > 3 * medianHordeSize &&
                    myHorde.TotalHealth / myHorde.GetPopulationState().HealthPerRat > 10)
                {
                    player.SplitHorde(myHorde, 0.5f);
                    Debug.Log("BOT: Split Horde");
                    return;
                }

                // If we're the sole defender of a POI, don't make any offensive actions
                if (myHorde.StationedAt && myHorde.StationedAt.StationedHordes.Count == 1) return;

                // OFFENSIVE ACTIONS

                // OFFENSIVE ACTIONS - POI TARGETING
                Dictionary<POIController, float> poiDesirabilities = new();
                foreach (var poi in allPoi)
                {
                    if (poi.ControlledBy == player) continue;

                    // Skip POI if too far away
                    var sqrDistance = (poi.transform.position - myHorde.GetBounds().center).sqrMagnitude;
                    if (sqrDistance > AggressionRange) continue;

                    var desirability = 1.0f;
                    foreach (var enemy in poi.StationedHordes)
                        desirability *= CalcCombatDesirability(myHorde, enemy);

                    desirability *= 1.0f - sqrDistance / AggressionRange;

                    poiDesirabilities.Add(poi, desirability);
                }

                if (poiDesirabilities.Count != 0)
                {
                    var mostDesirable = poiDesirabilities.Maxima(kvp => kvp.Value).First();

                    if (Random.Range(0.0f, 1.0f) < mostDesirable.Value)
                    {
                        myHorde.AttackPoi(mostDesirable.Key);
                        AggressionUncapped = 0.0f;
                        return;
                    }
                }

                // OFFENSIVE ACTIONS - HORDE TARGETING

                Dictionary<HordeController, float> hordeDesirabilities = new();
                foreach (var horde in allHordes)
                {
                    if (horde.Player == player) return;

                    // Skip Horde if too far away
                    var sqrDistance = (horde.transform.position - myHorde.GetBounds().center).sqrMagnitude;
                    if (sqrDistance > AggressionRange) continue;

                    var desirability = CalcCombatDesirability(myHorde, horde);

                    desirability *= 1.0f - sqrDistance / AggressionRange;

                    hordeDesirabilities.Add(horde, desirability);
                }

                if (hordeDesirabilities.Count != 0)
                {
                    var mostDesirable = hordeDesirabilities.Maxima(kvp => kvp.Value).First();

                    if (Random.Range(0.0f, 1.0f) < mostDesirable.Value)
                    {
                        Debug.Log("Offensive attack");
                        myHorde.AttackHorde(mostDesirable.Key, "");
                        AggressionUncapped = 0.0f;
                        return;
                    }
                }
            }

            // We took no actions, increase aggression
            AggressionUncapped += 0.01f;
        }

#if UNITY_EDITOR
        public void OnDrawGizmosSelected()
        {
            foreach (var horde in player.Hordes)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(horde.GetBounds().center,
                    Mathf.Sqrt(territorialDistance) + horde.GetBounds().extents.magnitude);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(horde.GetBounds().center, Mathf.Sqrt(AggressionRange));
            }

            HandleUtility.Repaint();
        }
#endif

        /// <summary>
        /// </summary>
        /// <param name="myHorde">The horde that I'll be using to attack</param>
        /// <param name="enemyHorde">The enemy horde I'll be attacking</param>
        /// <returns>Float between 0 and 1, where 0 means we don't want to attack, and 1 means we do </returns>
        private float CalcCombatDesirability(HordeController myHorde, HordeController enemyHorde)
        {
            // Base desirability is 50/50
            var desirability = 0.5f;
            if (myHorde.TotalHealth > enemyHorde.TotalHealth)
                desirability += (myHorde.TotalHealth - enemyHorde.TotalHealth) / enemyHorde.TotalHealth *
                                (myHorde.GetPopulationState().Damage / enemyHorde.GetPopulationState().Damage);

            else
                desirability -= (enemyHorde.TotalHealth - myHorde.TotalHealth) / myHorde.TotalHealth *
                                (enemyHorde.GetPopulationState().Damage / myHorde.GetPopulationState().Damage);

            // If less than 60 seconds since the horde has been in combat,
            // reduce desirability by a nice curve that heavily discourages early re-engagement, but reduces effects closer to 60
            if (Time.time - myHorde.lastInCombat < 60)
                desirability += Mathf.Cos((Time.time - myHorde.lastInCombat) / 20.0f + 3.14f) / 3.0f - 0.35f;

            return Mathf.Clamp(desirability, 0.0f, 1.0f);
        }
    }
}