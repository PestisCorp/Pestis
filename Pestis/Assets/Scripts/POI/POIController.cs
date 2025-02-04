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
            if (Application.isPlaying && Object.LastReceiveTick)
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
Controller: {(ControlledBy ? ControlledBy.Object.StateAuthority : "None")}
Stationed: {string.Join("\n    ", StationedHordes.Select(x => x.Object.Id))}
");
                HandleUtility.Repaint();
            }
        }
#endif

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void ChangeControllerRpc(Player player)
        {
            Debug.Log(
                $"Changing POI Controller from {(ControlledBy ? ControlledBy.Object.Id : "None")} to {player.Object.Id}");
            if (ControlledBy)
                // Remove Cheese benefits from previous controller
                ControlledBy.DecrementCheeseIncrementRateRpc(_cheesePerTick);

            ControlledBy = player;

            // Add Cheese benefits to new controller
            ControlledBy.IncrementCheeseIncrementRateRpc(_cheesePerTick);
            StationedHordes.Clear();
        }

        /// <summary>
        ///     Called by horde which wants to be stationed at a specific POI.
        /// </summary>
        /// <param name="horde"></param>
        /// <param name="rpcInfo">Automatically populated by Photon Fusion with details of caller.</param>
        /// <exception cref="Exception"></exception>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void StationHordeRpc(HordeController horde, RpcInfo rpcInfo = default)
        {
            Debug.Log($"Adding horde {horde.Object.Id} to myself (POI): {Object.Id}");
            if (horde.Object.StateAuthority != rpcInfo.Source)
                throw new Exception("Only the controlling player can station a horde!");

            if (horde.Player != ControlledBy)
                throw new Exception("Tried to station horde at POI not controlled by player.");

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