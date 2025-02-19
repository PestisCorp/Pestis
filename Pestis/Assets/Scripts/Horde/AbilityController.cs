using System;
using ExitGames.Client.Photon.StructWrapping;
using Fusion;
using Players;
using UnityEngine;

namespace Horde
{
    public class AbilityController : NetworkBehaviour
    {
        private HordeController _hordeController;
        private PopulationController _populationController;
        
        public void UsePestis()
        {
            _hordeController.TotalHealth = (int)Math.Ceiling(_hordeController.AliveRats * _populationController.GetState().HealthPerRat * 0.7);
            Collider[] hitColliders = Physics.OverlapSphere(_hordeController.GetCenter(), 5.0f);
        }

        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
        }
    }
}