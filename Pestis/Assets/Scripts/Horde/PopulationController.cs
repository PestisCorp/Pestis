// Population manager. Update birth and death rates here

using Fusion;
using UnityEngine;
using Random = System.Random;

namespace Horde
{
    public struct PopulationState : INetworkStruct
    {
        internal double BirthRate;
        internal double DeathRate;
        internal float HealthPerRat;

        /// <summary>
        ///     How much damage the horde does to other hordes per tick in combat
        /// </summary>
        internal float Damage;
    }

    public class PopulationController : NetworkBehaviour
    {
        private const int InitialPopulation = 5;
        private const int PopMax = 1000;
        public HordeController hordeController;

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private Random _random;

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();

        private static float ResourceWeightGrowth(int population, int resources)
        {
            return (float)resources / (resources + population);
        }

        private static float ResourceWeightDecline(int population, int resources)
        {
            return (float)population / (resources + population);
        }

        private float Alpha(float b, int resources, int population, float weight)
        {
            return b * ResourceWeightGrowth(population, resources) * weight;
        }

        private float Beta(float d, int resources, int population, float weight)
        {
            return d * ResourceWeightDecline(population, resources) * weight;
        }
        
        public override void Spawned()
        {
            _random = new Random();

            State.BirthRate = 0.0015;
            State.DeathRate = 0.0009;
            State.HealthPerRat = 5.0f;
            State.Damage = 0.5f;

            hordeController.TotalHealth = InitialPopulation * State.HealthPerRat;
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            double rMax = 1;

            var r = _random.NextDouble() * rMax; // Pick which event should happen
            // A birth event occurs here
            if (r < State.BirthRate) hordeController.TotalHealth += State.HealthPerRat;
            // Death event occurs here
            if (State.BirthRate <= r && r < State.BirthRate + State.DeathRate &&
                hordeController.TotalHealth > _highestHealth * 0.2f)
                hordeController.TotalHealth -= State.HealthPerRat;
        }

        // Only executed on State Authority
        public override void FixedUpdateNetwork()
        {
            PopulationEvent();
            _highestHealth = Mathf.Max(hordeController.TotalHealth, _highestHealth);
        }

        public PopulationState GetState()
        {
            return State;
        }
    }
}