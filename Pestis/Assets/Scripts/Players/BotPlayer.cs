using System;
using System.Collections.Generic;
using System.Linq;
using Horde;
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
        public float territorialDistance = 0;

        private float _timeSinceLastUpdate;

        /// <summary>
        ///     Multiplier to current `aggressionUncapped`, makes a horde more likely to take offensive action at all times
        /// </summary>
        public float BaseAggression { get; private set; } = 0.0f;

        /// <summary>
        ///     Arbitrary float, starts at 0.0, and increases over time - increasing desire to take offensive action. Reset to zero
        ///     when offensive action taken.
        ///     away
        /// </summary>
        public float AggressionUncapped { get; private set; }

        public float AggressionRange => 0.0f * AggressionUncapped * BaseAggression;

        /// <summary>
        ///     True if this instance is the one that should handle the bot
        /// </summary>
        public bool IsRunningOnThisMachine => player.HasStateAuthority;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
            //BaseAggression = Random.Range(1f, 10f);
        }

        private void FixedUpdate()
        {
            // Only State Authority for the Player should process bot logic
            if (!IsRunningOnThisMachine) return;
            // Only run logic update every second
            _timeSinceLastUpdate += Time.deltaTime;
            if (_timeSinceLastUpdate < 1.0f) return;
            _timeSinceLastUpdate = 0.0f;

            var allHordes = GameManager.Instance.AllHordes;

            var allPoi = GameManager.Instance.pois;


            // Check possible actions for each horde, exiting when a horde takes an action
            foreach (var myHorde in player.Hordes)
            {
                // Don't take any actions if we're already in combat or attacking a POI
                if (myHorde.InCombat || myHorde.TargetPoi) continue;
                
                List<Tuple<HordeController, float>> hordesByDistance = new();
                foreach (var horde in allHordes)
                {
                    float calc = (horde.HordeBounds.center - myHorde.HordeBounds.center).sqrMagnitude;
                    if (calc < AggressionRange)
                    {
                        Tuple<HordeController, float> temp = new(horde, calc);
                        hordesByDistance.Add(temp);
                        //Debug.Log($"{horde} distance = {calc}");
                    }
                }
                hordesByDistance = hordesByDistance.OrderBy(t => t.Item2).ToList();
                
                // var temp = allHordes.Select(horde => new Tuple<HordeController, float>(horde, calc));
                // var temp2 = temp.Where(tuple => tuple.Item2 < AggressionRange);
                // var temp3 = temp2.OrderBy(horde => horde.Item2);
                // var hordesByDistance = temp3.ToArray();
                
                
                if (hordesByDistance.Count <= 1) continue;
                
                // First closest horde is us, so get second
                var closestHorde = hordesByDistance[1].Item1;
                var closestHordeDistance = hordesByDistance[1].Item2;

                var distFromHordeEdgeToClosestHorde = closestHordeDistance -
                                                      closestHorde.GetBounds()
                                                          .extents.sqrMagnitude -
                                                      myHorde.GetBounds().extents.sqrMagnitude;

                // DEFENSIVE ACTIONS
                // If nearest horde is too close, either attack or run away
                if (distFromHordeEdgeToClosestHorde < territorialDistance)
                {
                    var closestHordeIsFriendly = closestHorde.player == myHorde.player;
                    var attack = !closestHordeIsFriendly;
                    if (!closestHordeIsFriendly) // If we can attack this horde, let's calculate a desirability
                    {
                        var desirability = CalcCombatDesirability(myHorde, closestHorde);
                        attack = Random.Range(0.0f, 1.0f) < desirability;
                        attack = false;
                    }
                    
                    if (attack)
                    {
                        myHorde.AttackHorde(closestHorde);
                        return;
                    }

                    Vector2 pushDirection = (myHorde.GetBounds().center - closestHorde.GetBounds().center).normalized;
                    // Go 10 tiles in the opposite direction to the nearest horde.
                    var newLocation = (Vector2)myHorde.GetBounds().center + pushDirection * 10.0f;
                    myHorde.Move(newLocation);
                    // Return because we only want to take one action each second
                    return;
                }

                // MANAGEMENT ACTIONS

                if (myHorde.TotalHealth > 3 * GameManager.Instance.meanHordeHealth &&
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
                Dictionary<PoiController, float> poiDesirabilities = new();
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
                    desirability = 0;
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
                foreach (var kvp in hordesByDistance)
                {
                    if (kvp.Item1.player == player) return;

                    // Skip Horde if too far away
                    var sqrDistance = kvp.Item2;
                    if (sqrDistance > AggressionRange) continue;

                    var desirability = CalcCombatDesirability(myHorde, kvp.Item1);

                    desirability *= 1.0f - sqrDistance / AggressionRange;
                    desirability = 0;
                    hordeDesirabilities.Add(kvp.Item1, desirability);
                }

                if (hordeDesirabilities.Count != 0)
                {
                    var mostDesirable = hordeDesirabilities.Maxima(kvp => kvp.Value).First();

                    if (Random.Range(0.0f, 1.0f) < mostDesirable.Value)
                    {
                        Debug.Log("Offensive attack");
                        myHorde.AttackHorde(mostDesirable.Key);
                        AggressionUncapped = 0.0f;
                        return;
                    }
                }
            }

            // We took no actions, increase aggression
            AggressionUncapped += 0.0f;
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
            if (myHorde.LastInCombat.HasValue && Time.time - myHorde.LastInCombat < 60)
            {
                //desirability += Mathf.Cos((Time.time - myHorde.lastInCombat) / 20.0f + 3.14f) / 3.0f - 0.35f;
                UnityEngine.Debug.Log("Fought too recently");
                return 0;
            }
            
            return Mathf.Clamp(desirability, 0.0f, 0.0f);
        }
    }
}