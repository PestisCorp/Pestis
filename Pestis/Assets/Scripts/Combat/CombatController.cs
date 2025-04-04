using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using Players;
using POI;
using Unity.Profiling;
using UnityEngine;
using Bounds = Networking.Bounds;

namespace Combat
{
    public enum CombatState
    {
        NotStarted,
        InProgress,
        Finished
    }

    internal enum LeaveReason
    {
        /// <summary>
        ///     Horde was forced to retreat by the combat controller due to low health
        /// </summary>
        ForceRetreat,

        /// <summary>
        ///     Horde told the combat controller it wanted to leave
        /// </summary>
        VoluntaryRetreat,

        /// <summary>
        ///     Horde ran out of health, and was eligible to be killed instead of retreating
        /// </summary>
        Died,

        /// <summary>
        ///     The player controlling the horde left the game
        /// </summary>
        LeftGame,

        /// <summary>
        ///     The horde won combat
        /// </summary>
        WonCombat
    }

    /// <summary>
    ///     https://doc.photonengine.com/fusion/current/manual/fusion-types/network-collections#usage-in-inetworkstructs
    /// </summary>
    [Serializable]
    public struct CombatParticipant : INetworkStruct
    {
        [Networked] [Capacity(5)] public NetworkLinkedList<NetworkBehaviourId> Hordes => default;

        public CombatParticipant(HordeController hordeController)
        {
            Hordes.Add(hordeController.Id);
        }

        public void AddHorde(HordeController horde)
        {
            Hordes.Add(horde.Id);
        }

        public void RemoveHorde(HordeController horde)
        {
            Hordes.Remove(horde.Id);
        }
    }

    public class CombatController : NetworkBehaviour
    {
        public const int MaxParticipants = 6;

        private static readonly ProfilerMarker s_PlayerLeft = new("RPCCombat.PlayerLeft");

        private static readonly ProfilerMarker s_AddHorde = new("RPCCombat.AddHorde");

        private static readonly ProfilerMarker s_RemoveHordeBoids = new("RPCCombat.RemoveHordeBoids");

        private static readonly ProfilerMarker s_EventRetreatDesired = new("RPCCombat.EventRetreatDesired");

        public CombatBoids boids;

        [SerializeField] private CombatFXManager fxManager;

        [Networked]
        [OnChangedRender(nameof(StateChanged))]
        public CombatState state { get; set; }

        public int NumParticipators => Participators.Count;

        /// <summary>
        ///     Bounds of all *actively* participating hordes i.e. hordes which are dealing damage due to proximity.
        /// </summary>
        [Networked]
        private Bounds BoundsNetworked { set; get; }

        public UnityEngine.Bounds Bounds
        {
            private set => BoundsNetworked = value;

            get => BoundsNetworked;
        }

        /// <summary>
        ///     Stores the involved players and a list of their HordeControllers (as NetworkBehaviourId so must be converted before
        ///     use)
        /// </summary>
        [Networked]
        [Capacity(MaxParticipants)]
        private NetworkDictionary<Player, CombatParticipant> Participators => default;

        /// <summary>
        ///     The POI the fight is over (winner gains control).
        ///     May be null if the fight isn't over a POI.
        /// </summary>
        [Networked]
        [CanBeNull]
        public PoiController FightingOver { get; private set; }

        private void FixedUpdate()
        {
            if (Object.StateAuthority.IsNone)
            {
                TimeToDie();
                Destroy(this);
            }
        }

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            if (!Object) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        }
#endif

        private void StateChanged()
        {
            if (state != CombatState.InProgress)
                fxManager.enabled = false;
            else
                fxManager.enabled = true;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void PlayerLeftRpc(string username, RpcInfo rpcInfo = default)
        {
            s_PlayerLeft.Begin();
            s_PlayerLeft.End();
            Debug.Log($"COMBAT: Player left during combat: {username}");

            if (Participators.All(kvp => kvp.Key.Username != username)) return;

            var participant = Participators.First(kvp => kvp.Key.Username == username);
            var hordes = participant.Value.Hordes.ToArray();
            foreach (var hordeId in hordes)
            {
                if (!Runner.TryFindBehaviour<HordeController>(hordeId, out var horde))
                    throw new NullReferenceException("Couldn't find horde controller to remove it");

                RemoveHorde(horde, LeaveReason.LeftGame);
            }
        }

        /// <summary>
        ///     Remove local hordes from combat if the combat controller left game
        /// </summary>
        /// <param name="runner"></param>
        /// <param name="hasState"></param>
        /// <exception cref="NullReferenceException"></exception>
        public void TimeToDie()
        {
            Debug.Log("COMBAT: Despawned");

            var localClients = Participators.Where(kvp => kvp.Key.HasStateAuthority);
            foreach (var kvp in localClients)
            foreach (var hordeId in kvp.Value.Hordes)
            {
                if (!Runner.TryFindBehaviour<HordeController>(hordeId, out var horde))
                    throw new NullReferenceException("Failed to get local horde to nullify combat");

                horde.RetrieveBoidsFromCombatRpc(this);
                horde.CombatDespawned();
                Debug.Log("COMBAT: Removed local horde from combat because the controller left");
            }
        }

        public void SetFightingOver(PoiController poi)
        {
            if (!HasStateAuthority) throw new Exception("COMBAT: Tried to set fighting over but not authority");
            FightingOver = poi;
        }

        public override void FixedUpdateNetwork()
        {
            if (state != CombatState.InProgress) return;

            Bounds = boids.bounds;

            List<HordeController> hordesToRemove = new();
            foreach (var kvp in Participators)
            foreach (var hordeID in kvp.Value.Hordes)
            {
                Runner.TryFindBehaviour(hordeID, out HordeController horde);
                if (horde.TotalHealth < 5 * horde.GetPopulationState().HealthPerRat)
                    hordesToRemove.Add(horde);
            }

            foreach (var horde in hordesToRemove)
                RemoveHorde(horde,
                    horde.player.Hordes.Count == 1 && horde.player.Type == PlayerType.Human
                        ? LeaveReason.ForceRetreat
                        : LeaveReason.Died);

            if (Participators.Count > 1) return;

            state = CombatState.Finished;

            if (FightingOver) FightingOver.EventCombatOverRpc();

            // If only one player left, they won.
            if (Participators.Count == 1)
            {
                var winner = Participators.First().Key;
                var winnerParticipant = Participators.First().Value;
                Debug.Log($"COMBAT: Winner is {winner.Username}");

                // Tell each winning horde that they won.
                foreach (var hordeID in winnerParticipant.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    RemoveHorde(horde, LeaveReason.WonCombat);
                }

                // If the fight was over a POI, hand over control.
                if (FightingOver && winner != FightingOver.ControlledBy)
                {
                    Debug.Log($"Transferring POI Ownership to {winner.Object.StateAuthority}");
                    FightingOver.ChangeControllerRpc(winner);

                    foreach (var hordeID in winnerParticipant.Hordes)
                    {
                        Runner.TryFindBehaviour(hordeID, out HordeController horde);
                        horde.targetLocation.Teleport(FightingOver.transform.position);
                        horde.StationAtRpc(FightingOver);
                        horde.AddSpeechBubbleRpc(EmoteType.Defend);
                    }
                }
            }
            else
            {
                Debug.Log($"Combat over with {Participators.Count} players");
            }

            Debug.Log("COMBAT CONTROLLER: Despawning in 10 secs");
            Invoke(nameof(Despawn), 10);
        }

        private void Despawn()
        {
            Debug.Log("COMBAT CONTROLLER: Despawning now");
            Runner.Despawn(Object);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AddHordeRpc(HordeController horde)
        {
            s_AddHorde.Begin();
            s_AddHorde.End();

            if (state == CombatState.Finished) throw new Exception("COMBAT: Tried to join finished combat.");

            // Player not in combat
            if (!Participators.TryGet(horde.player, out var participant))
            {
                Debug.Log($"COMBAT: Adding player {horde.player.Username}");
                Participators.Add(horde.player, new CombatParticipant(horde));
            }
            else // Player in combat, just add horde
            {
                if (participant.Hordes.Contains(horde)) return;

                // Operates on local copy
                participant.AddHorde(horde);
                // Update stored copy
                Participators.Set(horde.player, participant);
            }


            // Notify horde authority it has been added to combat
            horde.EventJoinedCombatRpc(this);
            // Tell all clients to add their boids to this controller
            horde.AddBoidsToCombatRpc(this);

            if (Participators.Count > 1 && state == CombatState.NotStarted) state = CombatState.InProgress;
        }

        public bool HordeInCombat(HordeController horde)
        {
            if (!Participators.TryGet(horde.player, out var participant)) return false;
            return participant.Hordes.Contains(horde);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RemoveHordeBoidsRpc(NetworkBehaviourId horde)
        {
            s_RemoveHordeBoids.Begin();
            Debug.Log("COMBAT: Removing boids");
            boids.RemoveBoids(horde);
            s_RemoveHordeBoids.End();
        }

        private void RemoveHorde(HordeController horde, LeaveReason reason)
        {
            // If this is the last horde of that player, just remove the player
            if (Participators.Get(horde.player).Hordes.Count == 1)
            {
                Participators.Remove(horde.player);
            }
            else
            {
                var copy = Participators.Get(horde.player);
                copy.RemoveHorde(horde);
                Participators.Set(horde.player, copy);
            }

            Debug.Log($"COMBAT: Removing horde {horde.Id} for {reason}");

            switch (reason)
            {
                // If horde is still alive after this, it needs to get its boids back
                case LeaveReason.ForceRetreat or LeaveReason.VoluntaryRetreat:
                    horde.RetrieveBoidsFromCombatRpc(this);
                    horde.AddSpeechBubbleRpc(EmoteType.CombatLoss);
                    horde.RetreatRpc();


                    if (horde.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredEffects
                        .Contains("unlock_septic_bite"))
                        horde.GetComponent<PopulationController>().SetSepticMultRpc(1.0f);
                    break;
                case LeaveReason.Died:
                    RemoveHordeBoidsRpc(horde.Id);
                    horde.DestroyHordeRpc();
                    break;
                case LeaveReason.LeftGame:
                    RemoveHordeBoidsRpc(horde.Id);
                    horde.DestroyHordeRpc();
                    break;

                case LeaveReason.WonCombat:
                    horde.RetrieveBoidsFromCombatRpc(this);
                    horde.AddSpeechBubbleRpc(EmoteType.Victory);

                    if (horde.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredEffects
                        .Contains("unlock_septic_bite"))
                        horde.GetComponent<PopulationController>().SetSepticMultRpc(1.0f);
                    horde.EventWonCombatRpc();
                    break;
            }
        }


        /// <summary>
        ///     Called by a horde when it wants to leave this combat.
        /// </summary>
        /// <param name="horde"></param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventRetreatDesiredRpc(HordeController horde)
        {
            s_EventRetreatDesired.Begin();
            Debug.Log($"COMBAT: Horde wants to retreat from combat: {horde.Object.Id}");
            RemoveHorde(horde, LeaveReason.VoluntaryRetreat);
            s_EventRetreatDesired.End();
        }

        public override void Spawned()
        {
            if (HasStateAuthority) boids.local = true;
            boids.Runner = Runner;
            boids.Start();
            fxManager.enabled = true;
        }

        public List<HordeController> GetHordes()
        {
            List<HordeController> list = new();
            foreach (var participant in Participators)
            foreach (var hordeID in participant.Value.Hordes)
            {
                Runner.TryFindBehaviour(hordeID, out HordeController horde);
                list.Add(horde);
            }

            return list;
        }

        /// <summary>
        ///     Get sum count of all enemy rats in combat
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public int GetEnemyRatCount(Player player)
        {
            return Participators.Where(kvp => kvp.Key != player).SelectMany(kvp => kvp.Value.Hordes).Sum(hordeID =>
            {
                var found = Runner.TryFindBehaviour(hordeID, out HordeController horde);
                if (horde is null)
                    throw new NullReferenceException(
                        "Combat participant contained horde that couldn't be found by network runner");
                return horde.AliveRats;
            });
        }

        /// <summary>
        ///     Whether this combat is involving the local human player
        /// </summary>
        /// <returns></returns>
        public bool InvolvesLocalPlayer()
        {
            return Participators.Any(kvp => kvp.Key.IsLocal);
        }
    }
}