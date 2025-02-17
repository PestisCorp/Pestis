using System;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using Players;
using UnityEditor;
using UnityEngine;

namespace POI
{
    public class POIController : NetworkBehaviour
    {
        private readonly float _cheesePerTick = 0.3f;

        public float CheesePerSecond => _cheesePerTick / Runner.DeltaTime;

        public Collider2D Collider { get; private set; }

        [Networked] public Player ControlledBy { get; private set; }

        [Networked] [Capacity(4)] public NetworkLinkedList<HordeController> StationedHordes { get; } = default;

        [Networked] [CanBeNull] public CombatController Combat { get; private set; }

#if UNITY_EDITOR
        [DrawGizmo(GizmoType.Selected ^ GizmoType.NonSelected)]
        public void OnDrawGizmos()
        {
            if (Application.isPlaying && Object)
            {
                var centeredStyle = GUI.skin.GetStyle("Label");
                centeredStyle.alignment = TextAnchor.MiddleCenter;
                centeredStyle.normal.background = Texture2D.whiteTexture;
                centeredStyle.normal.textColor = Color.black;
                centeredStyle.fontStyle = FontStyle.Bold;

                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(Collider.bounds.center, Collider.bounds.size);
                Handles.Label(Collider.bounds.center, $@"{Object.Id}
Combat: {(Combat ? Combat.Object.Id : "None")}
Controller: {(ControlledBy ? ControlledBy.Username : "None")}
Stationed: {string.Join("\n    ", StationedHordes.Select(x => x.Object.Id))}
");
                HandleUtility.Repaint();
            }
        }
#endif

        public void ChangeController(Player player)
        {
            Debug.Log(
                $"Changing POI Controller from {(ControlledBy ? ControlledBy.Object.Id : "None")} to {player.Object.Id}");
            if (ControlledBy)
            {
                // Remove Cheese benefits from previous controller
                ControlledBy.DecrementCheeseIncrementRateRpc(_cheesePerTick);
                // Remove this POI from previous controller
                ControlledBy.ControlledPOIs.Remove(this);
            }
            // Give control to new controller
            ControlledBy = player;
            player.ControlledPOIs.Add(this);
            // Add Cheese benefits to new controller
            ControlledBy.IncrementCheeseIncrementRateRpc(_cheesePerTick);
            StationedHordes.Clear();
        }

        private void StationHorde(HordeController horde)
        {
            Debug.Log($"Adding horde {horde.Object.Id} to myself (POI): {Object.Id}");
            StationedHordes.Add(horde);
        }

        /// <summary>
        ///     Called by horde which wants to stop being stationed at a specific POI.
        /// </summary>
        /// <param name="horde"></param>
        /// <param name="rpcInfo">Automatically populated by Photon Fusion with details of caller.</param>
        /// <exception cref="Exception"></exception>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void UnStationHordeRpc(HordeController horde, RpcInfo rpcInfo = default)
        {
            if (horde.Object.StateAuthority != rpcInfo.Source)
                throw new Exception("Only the controlling player can unstation a horde!");

            StationedHordes.Remove(horde);
        }

        public override void Spawned()
        {
            Collider = GetComponent<Collider2D>();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void AttackRpc(HordeController horde)
        {
            if (ControlledBy == horde.Player)
            {
                StationHorde(horde);
                return;
            }

            // No need to start combat, just hand over control
            if (StationedHordes.Count == 0)
            {
                ChangeController(horde.Player);
                StationHorde(horde);
                return;
            }

            Debug.Log("POI being attacked");
            if (!Combat)
            {
                Debug.Log("Changing POI Combat Controller");
                Combat = horde.GetComponent<CombatController>();
                if (!Combat) throw new NullReferenceException("Failed to get Combat Controller from horde.");
                Combat!.SetFightingOverRpc(this);
                foreach (var defender in StationedHordes) Combat.AddHordeRpc(defender, false);
            }

            Combat.AddHordeRpc(horde, true);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void EventCombatOverRpc()
        {
            Debug.Log("POI Combat over");
            Combat = null;
        }
    }
}