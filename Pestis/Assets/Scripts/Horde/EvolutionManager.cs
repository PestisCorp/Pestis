using System;
using Fusion;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Windows;
using File = System.IO.File;
using Random = System.Random;
using KaimiraGames;

// TODO: Retain mutations on horde split.
// TODO: Rather than work with values from State, just store multiplier. Change setters.


namespace Horde
{
    /// <summary>
    /// Responsible for managing evolution of a Horde. Stores state and calculates potential mutations.
    /// </summary>
    public struct ActiveMutation
    {
        public int MutationID { get; set; }
        public string MutationName { get; set; }
        public string MutationTag { get; set; }
        public int MutationWeight { get; set; }
        public string[] Effects { get; set; }
    }
    
    
    public class EvolutionManager : NetworkBehaviour
    {
        private PopulationController _populationController;
        private HordeController _hordeController;
        // "Evolutionary effect" : [Chance of acquisition, Effect on stats, Maximum effect]
        private Dictionary<string, double[]> _passiveEvolutions = new Dictionary<string, double[]>();
        private WeightedList<ActiveMutation> _activeMutations = new WeightedList<ActiveMutation>();
        public Dictionary<string, ActiveMutation> AcquiredMutations = new Dictionary<string, ActiveMutation>();
        private const double PredispositionStrength = 1.01;
        private Color _hordeColor;
        private readonly Random _random = new Random();
        private Timer _mutationClock;
        private Timer _rareMutationClock;
        
        // Set the rat stats in the Population Controller
        // Shows notification of mutation
        private void UpdateRatStats(string mutation)
        {
            _hordeColor = _hordeController.GetHordeColor();
            double mutEffect = _passiveEvolutions[mutation][1];
            string text = ("A horde's " + mutation + " has improved by " +
                                         (Math.Round(_passiveEvolutions["evolution strength"][1] * 100 - 100, 2)).ToString(CultureInfo.CurrentCulture) + "%.");
            if (_hordeController.Player.Type == 0)
            {
                FindFirstObjectByType<UI_Manager>().AddNotification(text, _hordeColor);
            }
            switch (mutation)
            {
                case "attack":
                    _populationController.SetDamage((float)mutEffect);
                    break;
                case "health":
                    _populationController.SetHealthPerRat((float)mutEffect);
                    break;
                case "defense":
                    _populationController.SetDamageReduction((float)mutEffect);
                    break;
                case "birth rate":
                    _populationController.SetBirthRate(mutEffect);
                    break;
                //case "resource consumption":
                   //hordeController.Player.
                   //break;
            }
        }
        
        // Check if a mutation is will be acquired this tick
        private void EvolutionaryEvent()
        {
            foreach (var ele in _passiveEvolutions)
            {
                double r = _random.NextDouble();
                string mutation = ele.Key;
                double p = _passiveEvolutions[mutation][0];
                double mutEffect = _passiveEvolutions[mutation][1];
                if ((r < p) && (_passiveEvolutions[mutation][2] > mutEffect))
                {
                    _passiveEvolutions[mutation][0] = p * PredispositionStrength;
                    if ((mutation == "rare mutation rate") || (mutation == "evolution rate"))
                    {
                        _passiveEvolutions[mutation][1] =
                            Math.Max(mutEffect / _passiveEvolutions[mutation][1], _passiveEvolutions[mutation][2]);
                    }
                    else
                    {
                        _passiveEvolutions[mutation][1] = Math.Min(mutEffect * _passiveEvolutions["evolution strength"][1], _passiveEvolutions[mutation][2]);
                    }
                    UpdateRatStats(mutation);
                }
            }
        }

        private void RareEvolutionaryEvent()
        {
            _rareMutationClock.Reset();
            var firstMut = _activeMutations.Next();
            //_activeMutations.Remove(firstMut);
            
            var secondMut = _activeMutations.Next();
            //_activeMutations.Remove(secondMut);
            
            var thirdMut = _activeMutations.Next();
            //_activeMutations.Remove(thirdMut);
            
            FindFirstObjectByType<UI_Manager>().RareMutationPopup((firstMut, secondMut, thirdMut), this);
        }

        public void ApplyActiveEffects(ActiveMutation mutation)
        {
            FindFirstObjectByType<UI_Manager>().MutationPopUpDisable();
            foreach (var effect in mutation.Effects)
            {
                AcquiredMutations[effect] = mutation;
            }
        }
        
        private void CalculateActiveWeights()
        {
        }

        private void CreatePassiveEvolutions()
        {
            // Initialise all the passive mutations
            
            _passiveEvolutions["attack"] = new []{0.05, _populationController.GetState().Damage, 2.0};
            _passiveEvolutions["health"] = new []{0.05, _populationController.GetState().HealthPerRat, 20.0};
            _passiveEvolutions["defense"] = new []{ 0.05, _populationController.GetState().DamageReduction, 2.5};
            _passiveEvolutions["evolution rate"] = new []{ 0.025, 2, 0.5};
            _passiveEvolutions["evolution strength"] = new []{ 0.03, 1.02, 1.3};
            _passiveEvolutions["birth rate"] = new[]{ 0.02, _populationController.GetState().BirthRate, 0.1};
            //_passiveEvolutions["resource consumption"] = new []{ 0.0005, _hordeController.Player.CheeseIncrementRate };
            // Need to change the default values for rate, and strength of evolutions to referring to values in PC.State (for horde split reasons)
            _passiveEvolutions["rare mutation rate"] = new []{ 0.025, 30, 20};
        }

        private void CreateActiveEvolutions()
        {
            string json = File.ReadAllText("Assets/json/active_mutations.json");
            var activeMutations = JsonConvert.DeserializeObject<List<ActiveMutation>>(json);
            foreach (var mut in activeMutations)
            {
                _activeMutations.Add(mut, mut.MutationWeight);
            }
        }
        public override void Spawned()
        {
            _mutationClock.Start();
            _rareMutationClock.Start();
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
            CreatePassiveEvolutions();
            CreateActiveEvolutions();
        }
        public override void FixedUpdateNetwork()
        {
            if (_mutationClock.ElapsedInSeconds > _passiveEvolutions["evolution rate"][1])
            {
                EvolutionaryEvent();
                _mutationClock.Restart();
            }
            if (_rareMutationClock.ElapsedInSeconds > _passiveEvolutions["rare mutation rate"][1])  
            {
                CalculateActiveWeights();
                RareEvolutionaryEvent();
            }
        }
    }
}
