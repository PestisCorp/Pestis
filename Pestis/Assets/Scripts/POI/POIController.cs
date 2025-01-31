using System;
using Fusion;
using Horde;
using Players;
using UnityEngine;

namespace POI
{
    public class POIController : NetworkBehaviour
    {
        [Networked] public Player ControlledBy { get; private set; }

        [Networked] [Capacity(4)] public NetworkLinkedList<HordeController> StationedHordes { get; } = default;

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void ChangeControllerRpc(Player player)
        {
            ControlledBy = player;
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
    }
}