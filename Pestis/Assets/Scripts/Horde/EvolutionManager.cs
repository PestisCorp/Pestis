using Fusion;
using System.Collections.Generic;
using System.Globalization;
using UI;
using Random = System.Random;

namespace Horde
{
    /// <summary>
    /// Responsible for managing evolution of a Horde. Stores state and calculates potential mutations.
    /// </summary>
    
    public class EvolutionManager : NetworkBehaviour
    {
        private PopulationController _populationController;
        // Key : [Chance of acquisition, Effect on stats, Acquired or not]
        private Dictionary<string, float[]> _passiveEvolutions = new Dictionary<string, float[]>();
        private float _multiplier = 1.1f;
        private readonly Random _random = new Random();
        
        
        private void UpdateRatStats(string mutation)
        {
            float mutEffect = _passiveEvolutions[mutation][1];
            string text = ("Your " + mutation.ToLower() + " has improved by " +
                                         (mutEffect * 100).ToString(CultureInfo.CurrentCulture) + "%.");
            FindFirstObjectByType<UI_Manager>().AddNotification(text);
            switch (mutation)
            {
                case "Attack":
                    _populationController.SetDamage(mutEffect);
                    break;
                case "Health":
                    _populationController.SetHealthPerRat(mutEffect);
                    break;
                case "DamageReduction":
                    _populationController.SetDamageReduction(mutEffect);
                    break;
            }
        }
        
        private void EvolutionaryEvent()
        {
            double r = _random.NextDouble();
            if (r < 0.0005)
            {
                _multiplier *= _multiplier;
            }
            foreach (var ele in _passiveEvolutions)
            {
                r = _random.NextDouble();
                string mutation = ele.Key;
                float p = _passiveEvolutions[mutation][0];
                float mutEffect = _passiveEvolutions[mutation][1];
                if ((r < p) && (mutEffect < 3.0f))
                {
                    _passiveEvolutions[mutation][0] = p * _multiplier;
                    _passiveEvolutions[mutation][1] = mutEffect * _multiplier;
                    _passiveEvolutions[mutation][2] = 1;
                    UpdateRatStats(mutation);
                }
            }
            
        }

        public override void Spawned()
        {
            _populationController = GetComponent<PopulationController>();
            _passiveEvolutions["Attack"] = new []{0.001f, _populationController.GetState().Damage, 0.0f};
            _passiveEvolutions["Health"] = new []{0.001f, _populationController.GetState().HealthPerRat, 0.0f};
            _passiveEvolutions["Defense"] = new []{ 0.005f, _populationController.GetState().DamageReduction, 0.0f};
        }
        public override void FixedUpdateNetwork()
        {
            EvolutionaryEvent();
        }
    }
}
