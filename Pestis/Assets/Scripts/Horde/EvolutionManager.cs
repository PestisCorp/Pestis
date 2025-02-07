using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Horde
{
    /// <summary>
    /// Responsible for managing evolution of a Horde. Stores state and calculates potential mutations.
    /// </summary>
    
    public class EvolutionManager : NetworkBehaviour
    {
        public HordeController hordeController;
        // Key : [Chance of acquisition, Effect on stats, Acquired or not]
        private Dictionary<string, double[]> _passiveEvolutions = new ()
        {
            {"Speed", new double[]{0.001, 1.05, 0}},
            {"Attack", new double[]{0.001, 1.05, 0}},
            {"Defense", new double[]{0.001, 1.05, 0}},
            {"Size", new double[]{0.001, 1.05, 0}},
        };
        private readonly Random _random = new Random();
    
        private void EvolutionaryEvent()
        {
            foreach (var ele in _passiveEvolutions)
            {
                double r = _random.NextDouble();
                string mutation = ele.Key;
                if (r < _passiveEvolutions[mutation][0])
                {
                    _passiveEvolutions[mutation][0] *= 1.1;
                    _passiveEvolutions[mutation][1] *= 1.1;
                    _passiveEvolutions[mutation][2] = 1;
                    return;
                }
            }
        }
        
        public override void FixedUpdateNetwork()
        {
            EvolutionaryEvent();
        }
    }
}
