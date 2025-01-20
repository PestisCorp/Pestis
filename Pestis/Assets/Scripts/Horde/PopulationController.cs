// Population manager. Update birth and death rates here

using System;

namespace Horde
{
    public class PopulationController
    {
        private const int InitialPopulation = 5;
        private double _birthRate;
        private double _deathRate;

        private float _healthPerRat;
        
        private HordeController _hordeController;
        private Random _random;

        public PopulationController(double birthRate, double deathRate, HordeController hordeController)
        {
            _birthRate = birthRate;
            _deathRate = deathRate;

            _healthPerRat = 5.0f;
            
            _hordeController = hordeController;
            _random = new Random();
        }
        
        // Check for birth or death events
        public void PopulationEvent()
        {
            double rMax = 1;
            
            double r = _random.NextDouble() * rMax; // Pick which event should happen
            // A birth event occurs here
            if (r < _birthRate || _hordeController.AliveRats < InitialPopulation)
            {
                _hordeController.AliveRats++;
            }
            // Death event occurs here
            if ((_birthRate <= r) && (r < (_birthRate + _deathRate)))
            {
                _hordeController.AliveRats--;
            }
        }

        public float GetHealthPerRat()
        {
            return _healthPerRat;
        }
        
    }
}