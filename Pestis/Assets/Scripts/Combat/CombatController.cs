using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using Players;
using POI;
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

        /// <summary>
        ///     The health each horde had when it joined the battle. It will retreat if below 20%.
        /// </summary>
        [Networked]
        [Capacity(5)]
        public NetworkArray<float> HordeStartingHealth => default;


        public CombatParticipant(HordeController hordeController)
        {
            Hordes.Add(hordeController.Id);
            var index = Hordes.IndexOf(hordeController.Id);
            HordeStartingHealth.Set(index, hordeController.TotalHealth);
        }

        public void AddHorde(HordeController horde)
        {
            Hordes.Add(horde.Id);
            var index = Hordes.IndexOf(horde.Id);
            HordeStartingHealth.Set(index, horde.TotalHealth);
        }

        public void RemoveHorde(HordeController horde)
        {
            Hordes.Remove(horde.Id);
        }
    }

    public class CombatController : NetworkBehaviour
    {
        public const int MaxParticipants = 6;

        public CombatBoids boids;

        [SerializeField] private CombatFXManager fxManager;

        public CombatState state;

        [Networked]
        [Capacity(MaxParticipants)]
        private NetworkLinkedList<NetworkBehaviourId> AllParticipants => default;

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

#if UNITY_EDITOR
        public void OnDrawGizmos()
        {
            if (!Object) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        }
#endif

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
                RemoveHorde(horde, horde.player.Hordes.Count == 1 ? LeaveReason.ForceRetreat : LeaveReason.Died);

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
                    FightingOver.ChangeController(winner);

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
            if (state == CombatState.Finished) throw new Exception("COMBAT: Tried to join finished combat.");

            // Player not in combat
            if (!Participators.TryGet(horde.player, out var participant))
            {
                Debug.Log($"COMBAT: Adding player {horde.player.Username}");
                Participators.Add(horde.player, new CombatParticipant(horde));
                AllParticipants.Add(horde.Id);
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
        private void RemoveHordeBoidsRpc(HordeController horde)
        {
            boids.RemoveBoids(horde);
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

            if (horde.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredEffects
                .Contains("unlock_septic_bite")) horde.GetComponent<PopulationController>().SetSepticMultRpc(1.0f);

            Debug.Log($"COMBAT: Removing horde {horde.Id} for {reason}");

            switch (reason)
            {
                // If horde is still alive after this, it needs to get its boids back
                case LeaveReason.ForceRetreat or LeaveReason.VoluntaryRetreat:
                    horde.RetrieveBoidsFromCombatRpc(this);
                    horde.AddSpeechBubbleRpc(EmoteType.CombatLoss);
                    horde.RetreatRpc();
                    break;
                case LeaveReason.Died:
                    horde.DestroyHordeRpc();
                    RemoveHordeBoidsRpc(horde);
                    break;
                case LeaveReason.LeftGame:
                    RemoveHordeBoidsRpc(horde);
                    break;

                case LeaveReason.WonCombat:
                    horde.RetrieveBoidsFromCombatRpc(this);
                    horde.AddSpeechBubbleRpc(EmoteType.Victory);
                    if (horde.GetEvolutionState().AcquiredEffects.Contains("unlock_war_hawk"))
                        horde.GetComponent<AbilityController>().RefreshCooldownsRpc();
                    horde.EventWonCombatRpc(AllParticipants.ToArray());
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
            Debug.Log($"COMBAT: Horde wants to retreat from combat: {horde.Object.Id}");
            RemoveHorde(horde, LeaveReason.VoluntaryRetreat);
        }

        public override void Spawned()
        {
            if (HasStateAuthority) boids.local = true;
            boids.Start();
            fxManager.gameObject.SetActive(true);
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
                Runner.TryFindBehaviour(hordeID, out HordeController horde);
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