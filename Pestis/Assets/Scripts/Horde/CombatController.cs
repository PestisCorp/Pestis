using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fusion;
using JetBrains.Annotations;
using Players;
using POI;
using UnityEditor;
using UnityEngine;

namespace Horde
{
    /// <summary>
    ///     https://doc.photonengine.com/fusion/current/manual/fusion-types/network-collections#usage-in-inetworkstructs
    /// </summary>
    public struct CombatParticipant : INetworkStruct
    {
        public Player Player;

        [Networked] [Capacity(5)] public NetworkLinkedList<NetworkBehaviourId> Hordes => default;


        /// <summary>
        ///     Whether each horde chose to be in combat, or was attacked.
        /// </summary>
        [Networked]
        [Capacity(5)]
        public NetworkDictionary<NetworkBehaviourId, bool> Voluntary => default;

        /// <summary>
        ///     The health each horde had when it joined the battle. It will retreat if below 20%.
        /// </summary>
        [Networked]
        [Capacity(5)]
        public NetworkDictionary<NetworkBehaviourId, float> HordeStartingHealth => default;

        public CombatParticipant(Player player, HordeController hordeController, bool voluntary)
        {
            Player = player;
            Hordes.Add(hordeController);
            Voluntary.Add(hordeController, voluntary);
            HordeStartingHealth.Add(hordeController, hordeController.TotalHealth);
        }

        public void AddHorde(HordeController horde, bool voluntary)
        {
            Hordes.Add(horde);
            Voluntary.Add(horde, voluntary);
            HordeStartingHealth.Add(horde, horde.TotalHealth);
        }

        public void RemoveHorde(HordeController horde)
        {
            Hordes.Remove(horde);
            Voluntary.Remove(horde);
            HordeStartingHealth.Remove(horde);
        }
    }

    public class CombatController : NetworkBehaviour
    {
        public const int MAX_PARTICIPANTS = 6;

        /// <summary>
        ///     Lock that must be acquired to use `Participators` to prevent races
        /// </summary>
        private readonly Mutex _participatorsLock = new();

        [Networked] private Player InitiatingPlayer { get; set; }

        /// <summary>
        ///     Stores the involved players and a list of their HordeControllers (as NetworkBehaviourId so must be converted before
        ///     use)
        /// </summary>
        [Networked]
        [Capacity(MAX_PARTICIPANTS)]
        private NetworkDictionary<Player, CombatParticipant> Participators { get; }

        /// <summary>
        ///     The POI the fight is over (winner gains control).
        ///     May be null if the fight isn't over a POI.
        /// </summary>
        [Networked]
        [CanBeNull]
        public POIController FightingOver { get; private set; }

#if UNITY_EDITOR
        [DrawGizmo(GizmoType.Selected ^ GizmoType.NonSelected)]
        public void OnDrawGizmos()
        {
            if (!Object || !InitiatingPlayer) return;

            var text = $@"Initiator: {InitiatingPlayer}
POI: {FightingOver}
";

            _participatorsLock.WaitOne();
            var b = new Bounds();
            foreach (var kvp in Participators)
            {
                text += $"\n{kvp.Key}:";
                foreach (var hordeID in kvp.Value.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    if (b.size == new Vector3()) b.center = horde.GetBounds().center;

                    b.Encapsulate(horde.GetBounds());
                    text += $"\n  {hordeID}";
                }
            }

            _participatorsLock.ReleaseMutex();

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(b.center, b.size);
            Handles.Label(new Vector3(b.center.x - b.extents.x, b.center.y + b.extents.y), text);
        }
#endif

        public override void FixedUpdateNetwork()
        {
            if (Participators.Count == 0) return;

            List<HordeController> hordesToRemove = new();
            List<Player> playersToRemove = new();
            _participatorsLock.WaitOne(-1);
            foreach (var kvp in Participators)
            {
                var aliveHordes = 0;
                foreach (var hordeID in kvp.Value.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    var minimumHealth = kvp.Value.HordeStartingHealth.Get(hordeID) * 0.2f;
                    // If horde is above 20% of it's starting health
                    if (horde.TotalHealth > minimumHealth)
                        aliveHordes++;
                    else
                        hordesToRemove.Add(horde);
                }

                // If player has no hordes above 20% health participating
                if (aliveHordes == 0) playersToRemove.Add(kvp.Key);
            }

            foreach (var horde in hordesToRemove)
            {
                var copy = Participators.Get(horde.Player);
                copy.RemoveHorde(horde);
                Participators.Set(horde.Player, copy);
            }

            foreach (var player in playersToRemove)
            {
                Debug.Log($"Removing {player.Object.Id} from participators");
                Participators.Remove(player);
            }

            _participatorsLock.ReleaseMutex();


            // Combat still going
            if (Participators.Count > 1)
            {
                // It's safe to call the RPCs now
                foreach (var horde in hordesToRemove)
                    // Tell horde to run away to nearest friendly POI
                    horde.RetreatRpc();
                return;
            }

            // If there's only one person left in combat they are the winner! Otherwise we tied
            if (Participators.Count == 1)
            {
                var winner = Participators.First().Key;
                Debug.Log($"Combat is over! Winner is {winner.Object.StateAuthority}");
                var winnerParticipant = Participators.First().Value;

                // Tell each winning horde that they won.
                foreach (var hordeID in winnerParticipant.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    horde.EventWonCombatRpc();
                }

                if (FightingOver)
                {
                    FightingOver.EventCombatOverRpc();
                    Debug.Log($"COMBAT: Current Controller {FightingOver.ControlledBy.Id}, winner is {winner.Id}");
                }

                // If the fight was over a POI, hand over control.
                if (FightingOver && winner != FightingOver.ControlledBy)
                {
                    Debug.Log($"Transferring POI Ownership to {winner.Object.StateAuthority}");
                    FightingOver.ChangeController(winner);
                    foreach (var hordeID in winnerParticipant.Hordes)
                    {
                        Runner.TryFindBehaviour(hordeID, out HordeController horde);
                        horde.targetLocation.Teleport(FightingOver.transform.position);
                        horde.StationAtRpc(FightingOver);
                    }
                }
                else if (FightingOver && winner == FightingOver.ControlledBy)
                {
                    Debug.Log("POI successfully defended");
                }

                _participatorsLock.WaitOne();
                Participators.Remove(winner);
                _participatorsLock.ReleaseMutex();
            }

            // Clear Combat Controller
            InitiatingPlayer = null;
            FightingOver = null;

            _participatorsLock.WaitOne();
            Participators.Clear();
            _participatorsLock.ReleaseMutex();

            // It's safe to call the RPCs now
            foreach (var horde in hordesToRemove)
                // Tell horde to run away to nearest friendly POI
                horde.RetreatRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AddHordeRpc(HordeController horde, bool voluntary)
        {
            _participatorsLock.WaitOne();
            if (Participators.Count == 0) InitiatingPlayer = horde.Player;

            if (!Participators.TryGet(horde.Player, out var participant))
            {
                Debug.Log("COMBAT: Adding player");
                Participators.Add(horde.Player, new CombatParticipant(horde.Player, horde, voluntary));
            }
            else
            {
                if (participant.Hordes.Contains(horde)) return;

                // Operates on local copy
                participant.AddHorde(horde, voluntary);
                // Update stored copy
                Participators.Set(horde.Player, participant);
            }

            _participatorsLock.ReleaseMutex();

            if (!voluntary) horde.EventAttackedRpc(this);
        }

        public HordeController GetNearestEnemy(HordeController me)
        {
            Vector2 myCenter = me.GetBounds().center;

            HordeController bestTarget = null;
            var closestDistance = Mathf.Infinity;

            _participatorsLock.WaitOne();
            try
            {
                foreach (var kvp in Participators.AsEnumerable().ToArray())
                {
                    // Only look at enemies
                    if (kvp.Key == me.Player) continue;

                    foreach (var hordeID in kvp.Value.Hordes)
                    {
                        Runner.TryFindBehaviour(hordeID, out HordeController horde);
                        var dist = ((Vector2)horde.GetBounds().center - myCenter).sqrMagnitude;

                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            bestTarget = horde;
                        }
                    }
                }
            }
            catch
            {
                Debug.LogError("Failed to iterate over participators");
            }


            _participatorsLock.ReleaseMutex();

            return bestTarget;
        }

        public bool HordeIsVoluntary(HordeController horde)
        {
            var participant = Participators.Get(horde.Player);
            return participant.Voluntary.Get(horde);
        }

        public bool HordeInCombat(HordeController horde)
        {
            if (!Participators.TryGet(horde.Player, out var participant)) return false;
            return participant.Hordes.Contains(horde);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetFightingOverRpc(POIController poi)
        {
            Debug.Log("COMBAT: Setting FightingOver");
            FightingOver = poi;
        }

        /// <summary>
        ///     Called by a horde when it wants to leave this combat.
        /// </summary>
        /// <param name="horde"></param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventRetreatRpc(HordeController horde)
        {
            Debug.Log($"Horde retreating from combat: {horde.Object.Id}");
            _participatorsLock.WaitOne();
            var copy = Participators.Get(horde.Player);
            copy.RemoveHorde(horde);
            Participators.Set(horde.Player, copy);

            // Remove player from participators if that was the only horde it had in combat
            if (!copy.Hordes.Any()) Participators.Remove(horde.Player);
            _participatorsLock.ReleaseMutex();
        }
    }
}