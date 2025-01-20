// Population manager. Update birth and death rates here

using System;
using Fusion;

namespace Horde
{
    public struct PopulationState: INetworkStruct
    {
        internal double BirthRate;
        internal double DeathRate;
        internal float HealthPerRat;
    }
    
    public class PopulationController: NetworkBehaviour
    {
        private const int InitialPopulation = 5;

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();
        
        public  HordeController hordeController;
        private Random _random;

        public override void Spawned()
        {
            _random = new Random();

            State.BirthRate = 0.001;
            State.DeathRate = 0.0009;
            State.HealthPerRat = 5.0f;
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            double rMax = 1;
            
            double r = _random.NextDouble() * rMax; // Pick which event should happen
            // A birth event occurs here
            if (r < State.BirthRate || hordeController.AliveRats < InitialPopulation)
            {
                hordeController.TotalHealth += State.HealthPerRat;
            }
            // Death event occurs here
            if ((State.BirthRate <= r) && (r < (State.BirthRate + State.DeathRate)))
            {
                hordeController.TotalHealth -= State.HealthPerRat;
            }
        }
        
        // Only executed on State Authority
        public override void FixedUpdateNetwork()
        {
            PopulationEvent();
        }

        public PopulationState GetState()
        {
            return State;
        }
    }
}