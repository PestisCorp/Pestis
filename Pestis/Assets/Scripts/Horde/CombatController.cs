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

        public CombatBoids boids;


        /// <summary>
        ///     Lock that must be acquired to use `Participators` to prevent races
        /// </summary>
        private readonly Mutex _participatorsLock = new();

        private bool _initiated;

        [Networked]
        [Capacity(MAX_PARTICIPANTS)]
        private NetworkLinkedList<NetworkBehaviourId> AllParticipants => default;

        /// <summary>
        ///     Bounds of all *actively* participating hordes i.e. hordes which are dealing damage due to proximity.
        /// </summary>
        public Bounds bounds { private set; get; }

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
            if (Participators.Count == 0 || !_initiated) return;

            bounds = boids.GetBounds();

            List<HordeController> hordesToRemove = new();
            List<Player> playersToRemove = new();
            _participatorsLock.WaitOne(-1);
            foreach (var kvp in Participators)
            {
                var aliveHordes = 0;
                foreach (var hordeID in kvp.Value.Hordes)
                {
                    Runner.TryFindBehaviour(hordeID, out HordeController horde);
                    if (horde.TotalHealth > 0)
                        aliveHordes++;
                    else
                        hordesToRemove.Add(horde);
                }

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
                {
                    Debug.Log($"COMBAT: Removing {horde.Object.Id} from combat");
                    horde.RemoveBoidsFromCombatRpc(this);

                    if (horde.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredEffects
                        .Contains("unlock_septic_bite")) horde.GetComponent<PopulationController>().SetSepticMult(1.0f);
                    if (horde.Player.Hordes.Count == 1)
                    {
                        // Tell horde to run away to nearest friendly POI
                        var icon = Resources.Load<Sprite>("UI_design/Emotes/combat_loss_emote");
                        horde.AddSpeechBubble(icon);
                        horde.moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 0;
                        horde.RetreatRpc();
                    }
                    else
                        horde.DestroyHordeRpc();
                }

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

                    horde.RemoveBoidsFromCombatRpc(this);
                    if (horde.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredEffects
                        .Contains("unlock_septic_bite")) horde.GetComponent<PopulationController>().SetSepticMult(1.0f);
                    var icon = Resources.Load<Sprite>("UI_design/Emotes/victory_emote");
                    horde.AddSpeechBubble(icon);
                    horde.EventWonCombatRpc(AllParticipants.ToArray());
                    horde.moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 0;
                }

                if (FightingOver)
                {
                    FightingOver.EventCombatOverRpc();
                    AllParticipants.Clear();
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
                        var icon = Resources.Load<Sprite>("UI_design/Emotes/defend_emote");
                        horde.AddSpeechBubble(icon);
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
            {
                horde.RemoveBoidsFromCombatRpc(this);
                // If last horde of that player
                if (horde.Player.Hordes.Count == 1)
                {
                    // Tell horde to run away to nearest friendly POI
                    var icon = Resources.Load<Sprite>("UI_design/Emotes/combat_loss_emote");
                    horde.AddSpeechBubble(icon);
                    horde.RetreatRpc();
                }
                else
                {
                    Debug.Log("COMBAT: Killing horde");
                    horde.DestroyHordeRpc();
                }
            }


            void Despawn()
            {
                Debug.Log("COMBAT CONTROLLER: Despawning now");
                Runner.Despawn(Object);
            }

            Debug.Log("COMBAT CONTROLLER: Despawning in 10 secs");
            Invoke(nameof(Despawn), 10);
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
                AllParticipants.Add(horde.Id);
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

            if (!voluntary)
            {
                horde.EventAttackedRpc(this);
                // Immediately transfer defending horde to combat boids sim
                // Other hordes will then get transferred when they intersect the combat boids
                horde.AddBoidsToCombatRpc(this);
            }

            if (Participators.Count > 1) _initiated = true;
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

        public override void Spawned()
        {
            boids.Start();
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
    }
}