using System;
using System.Linq;
using Horde;
using UnityEngine;

namespace Players
{
    /// <summary>
    ///     Controls a single AI player, responsible for deciding what actions to take
    /// </summary>
    public class BotPlayer : MonoBehaviour
    {
        public Player player;

        private float _timeSinceLastUpdate;

        /// <summary>
        ///     True if this instance is the one that should handle the bot
        /// </summary>
        public bool IsRunningOnThisMachine => player.HasStateAuthority;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Start()
        {
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

                // If less than 10 tiles between us and nearest horde
                if (distFromHordeEdgeToClosestHorde <
                    100) // 100 not 10 because we're using squared distance (as it's cheaper!)
                {
                    Debug.Log($"Horde {myHorde.Id} too close to other horde, moving away!");

                    Vector2 pushDirection = (myHorde.GetBounds().center - closestHorde.GetBounds().center).normalized;
                    // Go 10 tiles in the opposite direction to the nearest horde.
                    var newLocation = (Vector2)myHorde.GetBounds().center + pushDirection * 10.0f;
                    myHorde.Move(newLocation);
                    // Return because we only want to take one action each second
                    return;
                }
            }
        }
    }
}