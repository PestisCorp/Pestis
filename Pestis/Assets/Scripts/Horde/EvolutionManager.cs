using System;
using Fusion;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using File = System.IO.File;
using Random = System.Random;
using KaimiraGames;
using Objectives;
using UnityEngine.Tilemaps;

// TODO: Change AcquiredMutations to a HashSet.


namespace Horde
{
    /// <summary>
    /// Responsible for managing evolution of a Horde. Stores state and calculates potential mutations.
    /// </summary>
    public struct ActiveMutation : IEquatable<ActiveMutation>
    {
        public string MutationName { get; set; }
        public string MutationTag { get; set; }
        public int MutationWeight { get; set; }
        public string[] Effects { get; set; }
        public bool IsAbility { get; set; }
        public string Tooltip { get; set; }

        public bool Equals(ActiveMutation other)
        {
            return MutationName == other.MutationName && MutationTag == other.MutationTag && MutationWeight == other.MutationWeight && Equals(Effects, other.Effects) && IsAbility == other.IsAbility && Tooltip == other.Tooltip;
        }

        public override bool Equals(object obj)
        {
            return obj is ActiveMutation other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MutationName, MutationTag, MutationWeight, Effects, IsAbility, Tooltip);
        }
        
        
        
    }

    public struct EvolutionaryState : IEquatable<EvolutionaryState>
    {
        // "Evolutionary effect" : [Chance of acquisition, Effect on stats, Maximum effect]
        public Dictionary<string, double[]> PassiveEvolutions;
        public WeightedList<ActiveMutation> ActiveMutations;
        public HashSet<ActiveMutation> AcquiredMutations;
        public List<(string, string)> AcquiredAbilities;
        public Dictionary<string, int> TagCounts;
        public HashSet<string> AcquiredEffects;

        public bool Equals(EvolutionaryState other)
        {
            return Equals(PassiveEvolutions, other.PassiveEvolutions) && Equals(ActiveMutations, other.ActiveMutations) && Equals(AcquiredMutations, other.AcquiredMutations) && Equals(AcquiredAbilities, other.AcquiredAbilities) && Equals(TagCounts, other.TagCounts);
        }

        public override bool Equals(object obj)
        {
            return obj is EvolutionaryState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PassiveEvolutions, ActiveMutations, AcquiredMutations, AcquiredAbilities, TagCounts);
        }
        
        public EvolutionaryState DeepCopy()
        {
            EvolutionaryState copy = new EvolutionaryState
            {
                PassiveEvolutions = this.PassiveEvolutions.ToDictionary(entry => entry.Key, entry => (double[])entry.Value.Clone()), 
                ActiveMutations = new WeightedList<ActiveMutation>(), 
                AcquiredAbilities = new List<(string, string)>(AcquiredAbilities), 
                AcquiredMutations = new HashSet<ActiveMutation>(AcquiredMutations), 
                AcquiredEffects = new HashSet<string>(AcquiredEffects), 
                TagCounts = new Dictionary<string, int>(TagCounts) 
            };
            
            foreach (var mutation in this.ActiveMutations)
            {
                copy.ActiveMutations.Add(mutation, mutation.MutationWeight);
            }

            return copy;
        }
        
    }
    
    public class EvolutionManager : NetworkBehaviour
    {
        private PopulationController _populationController;
        private HordeController _hordeController;
        
        private EvolutionaryState _evolutionaryState;
        private const double PredispositionStrength = 1.01;
        private Color _hordeColor;
        private readonly Random _random = new();
        private Timer _mutationClock;
        private Timer _rareMutationClock;
        
        
        // Set the rat stats in the Population Controller
        // Shows notification of mutation
        private void UpdateRatStats(string mutation)
        {
            _hordeColor = _hordeController.GetHordeColor();
            double mutEffect = _evolutionaryState.PassiveEvolutions[mutation][1];
            string text = ("A horde's " + mutation + " has improved by " +
                                         (Math.Round(_evolutionaryState.PassiveEvolutions["evolution strength"][1] * 100 - 100, 2)).ToString(CultureInfo.CurrentCulture) + "%.");
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
            foreach (var ele in _evolutionaryState.PassiveEvolutions)
            {
                double r = _random.NextDouble();
                string mutation = ele.Key;
                double p = _evolutionaryState.PassiveEvolutions[mutation][0];
                double mutEffect = _evolutionaryState.PassiveEvolutions[mutation][1];
                if ((r < p) && (_evolutionaryState.PassiveEvolutions[mutation][2] > mutEffect))
                {
                    _evolutionaryState.PassiveEvolutions[mutation][0] = p * PredispositionStrength;
                    if (mutation is "rare mutation rate" or "evolution rate")
                    {
                        _evolutionaryState.PassiveEvolutions[mutation][1] =
                            Math.Max(mutEffect / _evolutionaryState.PassiveEvolutions[mutation][1], _evolutionaryState.PassiveEvolutions[mutation][2]);
                    }
                    else
                    {
                        _evolutionaryState.PassiveEvolutions[mutation][1] = Math.Min(mutEffect * _evolutionaryState.PassiveEvolutions["evolution strength"][1], _evolutionaryState.PassiveEvolutions[mutation][2]);
                    }
                    UpdateRatStats(mutation);
                }
            }
        }

        private void RareEvolutionaryEvent()
        {
            _rareMutationClock.Reset();
            if (_evolutionaryState.ActiveMutations.Count < 3) 
            {
                FindFirstObjectByType<UI_Manager>().AddNotification("This horde has acquired the maximum number of mutations." , _hordeColor);
                return;
            }
            var firstMut = _evolutionaryState.ActiveMutations.Next();
            var secondMut = _evolutionaryState.ActiveMutations.Next();
            var thirdMut = _evolutionaryState.ActiveMutations.Next();

            while (firstMut.MutationName == secondMut.MutationName ||  secondMut.MutationName == thirdMut.MutationName || thirdMut.MutationName == firstMut.MutationName)
            {
                secondMut = _evolutionaryState.ActiveMutations.Next();
                thirdMut = _evolutionaryState.ActiveMutations.Next();
            }
            
            FindFirstObjectByType<UI_Manager>().RareMutationPopup((firstMut, secondMut, thirdMut), this, _hordeController);
            _rareMutationClock.Start();
        }

        public void ApplyActiveEffects(ActiveMutation mutation)
        {
            FindFirstObjectByType<UI_Manager>().MutationPopUpDisable();
            _evolutionaryState.ActiveMutations.Remove(mutation);
            _evolutionaryState.AcquiredMutations.Add(mutation);
            foreach (var effect in mutation.Effects)
            {
                _evolutionaryState.AcquiredEffects.Add(effect);
                if (effect == "unlock_necrosis")
                {
                    _populationController.SetDamageReductionMult(_populationController.GetState().DamageReductionMult * 1.2f);
                }
            }
            
            if (_evolutionaryState.AcquiredEffects.Contains("unlock_fester") && mutation.MutationTag == "disease")
            {
                
                PopulationState newState = new PopulationState()
                {
                    BirthRate = _populationController.GetState().BirthRate * 1.1,
                    Damage = _populationController.GetState().Damage * 1.1f,
                    DamageMult = _populationController.GetState().DamageMult * 1.1f,
                    DamageReduction = _populationController.GetState().DamageReduction * 1.1f,
                    DamageReductionMult = _populationController.GetState().DamageReductionMult * 1.1f,
                    DeathRate = _populationController.GetState().DeathRate * 1.1,
                    HealthPerRat = _populationController.GetState().HealthPerRat * 1.1f,
                    SepticMult = _populationController.GetState().SepticMult * 1.1f
                };
                _populationController.SetState(newState);
            }

            _evolutionaryState.TagCounts[mutation.MutationTag] =
                _evolutionaryState.TagCounts.ContainsKey(mutation.MutationTag) ? _evolutionaryState.TagCounts[mutation.MutationTag]++ : 0;
            if (mutation.IsAbility)
            {
                _evolutionaryState.AcquiredAbilities.Add((mutation.MutationName, mutation.Tooltip));
            }

            if (mutation.MutationName.Contains("swim"))
            {
                GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.SwimmingUnlocked, 1);
            }
        }
        
        private void CalculateActiveWeights()
        {
            for (int i = 0; i < _evolutionaryState.ActiveMutations.Count; i++)
            {
                int count = 0;
                if (_evolutionaryState.TagCounts.TryGetValue(_evolutionaryState.ActiveMutations[i].MutationTag, out var tagCount))
                {
                    count = tagCount + 1;
                }
                _evolutionaryState.ActiveMutations.SetWeight(_evolutionaryState.ActiveMutations[i], count);
                //var biome = BiomeClass.GetBiomeAtPosition(tilemap.WorldToCell(_hordeController.GetBounds().center)).template.name;
            }
        }

        private void CreatePassiveEvolutions()
        {
            // Initialise all the passive mutations
            _mutationClock.Start();
            _evolutionaryState.PassiveEvolutions["attack"] = new []{0.03, _populationController.GetState().Damage, 2.0};
            _evolutionaryState.PassiveEvolutions["health"] = new []{0.03, _populationController.GetState().HealthPerRat, 20.0};
            _evolutionaryState.PassiveEvolutions["defense"] = new []{ 0.03, _populationController.GetState().DamageReduction, 2.5};
            _evolutionaryState.PassiveEvolutions["evolution rate"] = new []{ 0.01, 2, 0.5};
            _evolutionaryState.PassiveEvolutions["evolution strength"] = new []{ 0.01, 1.02, 1.3};
            _evolutionaryState.PassiveEvolutions["birth rate"] = new[]{ 0.01, _populationController.GetState().BirthRate, 0.1};
            //_evolutionaryState.PassiveEvolutions["resource consumption"] = new []{ 0.0005, _hordeController.Player.CheeseIncrementRate };
            // Need to change the default values for rate, and strength of evolutions to referring to values in PC.State (for horde split reasons)
            _evolutionaryState.PassiveEvolutions["rare mutation rate"] = new []{ 0.01, 30, 20};
        }

        private void CreateActiveEvolutions()
        {
            _rareMutationClock.Start();
            string json = File.ReadAllText("Assets/json/active_mutations.json");
            var activeMutations = JsonConvert.DeserializeObject<List<ActiveMutation>>(json);
            foreach (var mut in activeMutations)
            {
                _evolutionaryState.ActiveMutations.Add(mut, mut.MutationWeight);
            }
        }

        public EvolutionaryState GetEvolutionaryState()
        {
            return _evolutionaryState;
        }

        public void SetEvolutionaryState(EvolutionaryState newState)
        {
            _evolutionaryState = newState;
        }
        
        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _populationController = GetComponent<PopulationController>();
            _evolutionaryState = new EvolutionaryState()
            {
                PassiveEvolutions = new Dictionary<string, double[]>(),
                ActiveMutations = new WeightedList<ActiveMutation>(),
                AcquiredAbilities = new List<(string, string)>(),
                AcquiredMutations = new HashSet<ActiveMutation>(),
                AcquiredEffects = new HashSet<string>(),
                TagCounts = new Dictionary<string, int>()
            };
            CreatePassiveEvolutions();
            CreateActiveEvolutions();
        }
        
        public override void FixedUpdateNetwork()
        {
            if (_hordeController.InCombat) return;
            if (_mutationClock.ElapsedInSeconds > _evolutionaryState.PassiveEvolutions["evolution rate"][1])
            {
                EvolutionaryEvent();
                _mutationClock.Restart();
            }

            if ((_rareMutationClock.ElapsedInSeconds >
                 _evolutionaryState.PassiveEvolutions["rare mutation rate"][1]) &&
                (_hordeController.Player.Type == 0))
            {
                CalculateActiveWeights();
                RareEvolutionaryEvent();
            }
        }
    }
}
