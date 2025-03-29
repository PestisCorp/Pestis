using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fusion;
using KaimiraGames;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using Random = System.Random;


namespace Horde
{
    public enum ActiveMutationType
    {
        SepticBite,
        Pestis,
        SewerDwellers,
        Fester,
        Necrosis,
        Poltergeist,
        Haunt,
        Apparition,
        Mentat,
        Abraxas,
        Agriculturalist,
        PriceOfWar,
        WarHawk,
        AnarchoSyndicalist,
        MAD,
        Eyeless,
        BloodPocket,
        GodsMistake,
        Corpsebloom
    }

    /// <summary>
    ///     Responsible for managing evolution of a Horde. Stores state and calculates potential mutations.
    /// </summary>
    public struct ActiveMutation : IEquatable<ActiveMutation>
    {
        public ActiveMutationType Type { get; set; }
        public string MutationName { get; set; }
        public string MutationTag { get; set; }
        public int MutationWeight { get; set; }
        public string[] Effects { get; set; }
        public bool IsAbility { get; set; }
        public string MutationUse { get; set; }
        public string Tooltip { get; set; }

        public bool Equals(ActiveMutation other)
        {
            return MutationName == other.MutationName && MutationTag == other.MutationTag &&
                   MutationWeight == other.MutationWeight && Equals(Effects, other.Effects) &&
                   IsAbility == other.IsAbility && Tooltip == other.Tooltip;
        }

        public override bool Equals(object obj)
        {
            return obj is ActiveMutation other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MutationName, MutationTag, MutationWeight, Effects, IsAbility, MutationUse,
                Tooltip);
        }
    }

    public struct EvolutionaryState : IEquatable<EvolutionaryState>
    {
        public static Dictionary<string, ActiveMutationType> MutationNames = new()
        {
            { "Septic bite", ActiveMutationType.SepticBite },
            { "Pestis", ActiveMutationType.Pestis },
            { "Sewer Dwellers", ActiveMutationType.SewerDwellers },
            { "Fester", ActiveMutationType.Fester },
            { "Necrosis", ActiveMutationType.Necrosis },
            { "Poltergeist", ActiveMutationType.Poltergeist },
            { "Haunt", ActiveMutationType.Haunt },
            { "Apparition", ActiveMutationType.Apparition },
            { "Mentat", ActiveMutationType.Mentat },
            { "Abraxas", ActiveMutationType.Abraxas },
            { "Agriculturalist", ActiveMutationType.Agriculturalist },
            { "Price of War", ActiveMutationType.PriceOfWar },
            { "War Hawk", ActiveMutationType.WarHawk },
            { "Anarcho-syndicalist", ActiveMutationType.AnarchoSyndicalist },
            { "MAD", ActiveMutationType.MAD },
            { "Eyeless", ActiveMutationType.Eyeless },
            { "Blood pocket", ActiveMutationType.BloodPocket },
            { "God's Mistake", ActiveMutationType.GodsMistake },
            { "Corpsebloom", ActiveMutationType.Corpsebloom }
        };

        // "Evolutionary effect" : [Chance of acquisition, Effect on stats, Maximum effect]
        public Dictionary<string, double[]> PassiveEvolutions;
        public WeightedList<ActiveMutation> ActiveMutations;
        public HashSet<ActiveMutation> AcquiredMutations;
        public List<(string, string)> AcquiredAbilities;
        public Dictionary<string, int> TagCounts;
        public HashSet<string> AcquiredEffects;

        public bool Equals(EvolutionaryState other)
        {
            return Equals(PassiveEvolutions, other.PassiveEvolutions) &&
                   Equals(ActiveMutations, other.ActiveMutations) &&
                   Equals(AcquiredMutations, other.AcquiredMutations) &&
                   Equals(AcquiredAbilities, other.AcquiredAbilities) && Equals(TagCounts, other.TagCounts);
        }

        public override bool Equals(object obj)
        {
            return obj is EvolutionaryState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PassiveEvolutions, ActiveMutations, AcquiredMutations, AcquiredAbilities,
                TagCounts);
        }

        public EvolutionaryState DeepCopy()
        {
            var copy = new EvolutionaryState
            {
                PassiveEvolutions =
                    PassiveEvolutions.ToDictionary(entry => entry.Key, entry => (double[])entry.Value.Clone()),
                ActiveMutations = new WeightedList<ActiveMutation>(),
                AcquiredAbilities = new List<(string, string)>(AcquiredAbilities),
                AcquiredMutations = new HashSet<ActiveMutation>(AcquiredMutations),
                AcquiredEffects = new HashSet<string>(AcquiredEffects),
                TagCounts = new Dictionary<string, int>(TagCounts)
            };

            foreach (var mutation in ActiveMutations) copy.ActiveMutations.Add(mutation, mutation.MutationWeight);

            return copy;
        }
    }

    public class EvolutionManager : NetworkBehaviour
    {
        private const double PredispositionStrength = 1.005;
        public int PointsAvailable;
        private readonly Random _random = new();

        private EvolutionaryState _evolutionaryState;
        private HordeController _hordeController;
        private Timer _mutationClock;
        private PopulationController _populationController;
        private Timer _rareMutationClock;
        public NetworkLinkedList<ActiveMutationType> UnlockedMutationNames;


        // Set the rat stats in the Population Controller
        // Shows notification of mutation
        private void UpdateRatStats(string mutation)
        {
            var mutEffect = _evolutionaryState.PassiveEvolutions[mutation][1];
            var text = "A horde's " + mutation + " has improved by " +
                       Math.Round(_evolutionaryState.PassiveEvolutions["evolution strength"][1] * 100 - 100, 2)
                           .ToString(CultureInfo.CurrentCulture) + "%.";
            if (_hordeController.player.Type == 0) GameManager.Instance.UIManager.AddNotification(text, Color.black);
            switch (mutation)
            {
                case "attack":
                    _hordeController.AddSpeechBubbleRpc(EmoteType.DamageBuff);
                    _populationController.SetDamageRpc((float)mutEffect);
                    break;
                case "health":
                    _populationController.SetHealthPerRatRpc((float)mutEffect);
                    break;
                case "defense":
                    _hordeController.AddSpeechBubbleRpc(EmoteType.DamageReductionBuff);
                    _populationController.SetDamageReductionRpc((float)mutEffect);
                    break;
                case "birth rate":
                    _populationController.SetBirthRateRpc(mutEffect);
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
                var r = _random.NextDouble();
                var mutation = ele.Key;
                var p = _evolutionaryState.PassiveEvolutions[mutation][0];
                var mutEffect = _evolutionaryState.PassiveEvolutions[mutation][1];
                if (r < p && _evolutionaryState.PassiveEvolutions[mutation][2] > mutEffect)
                {
                    _evolutionaryState.PassiveEvolutions[mutation][0] = p * PredispositionStrength;
                    if (mutation is "rare mutation rate" or "evolution rate")
                        _evolutionaryState.PassiveEvolutions[mutation][1] =
                            Math.Max(mutEffect / _evolutionaryState.PassiveEvolutions[mutation][1],
                                _evolutionaryState.PassiveEvolutions[mutation][2]);
                    else
                        _evolutionaryState.PassiveEvolutions[mutation][1] = Math.Min(
                            mutEffect * _evolutionaryState.PassiveEvolutions["evolution strength"][1],
                            _evolutionaryState.PassiveEvolutions[mutation][2]);
                    UpdateRatStats(mutation);
                }
            }
        }

        public (ActiveMutation, ActiveMutation, ActiveMutation) RareEvolutionaryEvent()
        {
            CalculateActiveWeights();
            var firstMut = _evolutionaryState.ActiveMutations.Next();
            var secondMut = _evolutionaryState.ActiveMutations.Next();
            var thirdMut = _evolutionaryState.ActiveMutations.Next();

            while (firstMut.MutationName == secondMut.MutationName || secondMut.MutationName == thirdMut.MutationName ||
                   thirdMut.MutationName == firstMut.MutationName)
            {
                secondMut = _evolutionaryState.ActiveMutations.Next();
                thirdMut = _evolutionaryState.ActiveMutations.Next();
            }

            return (firstMut, secondMut, thirdMut);
        }

        public void ApplyActiveEffects(ActiveMutation mutation)
        {
            _evolutionaryState.ActiveMutations.Remove(mutation);
            _evolutionaryState.AcquiredMutations.Add(mutation);
            foreach (var effect in mutation.Effects)
            {
                _evolutionaryState.AcquiredEffects.Add(effect);
                if (effect == "unlock_necrosis")
                    _populationController.SetDamageReductionMultRpc(
                        _populationController.GetState().DamageReductionMult *
                        1.2f);
                if (effect == "unlock_farmer") _populationController.isAgriculturalist = true;
                if (effect == "unlock_syndicalism")
                {
                    var hordesControlled = _hordeController.player.Hordes.Count;
                    var newState = new PopulationState
                    {
                        BirthRate = _populationController.GetState().BirthRate * 1.01 * hordesControlled,
                        Damage = _populationController.GetState().Damage * 1.01f * hordesControlled,
                        DamageMult = _populationController.GetState().DamageMult,
                        DamageReduction = _populationController.GetState().DamageReduction * 1.01f * hordesControlled,
                        DamageReductionMult = _populationController.GetState().DamageReductionMult,
                        DeathRate = _populationController.GetState().DeathRate,
                        HealthPerRat = _populationController.GetState().HealthPerRat * 1.01f * hordesControlled,
                        SepticMult = _populationController.GetState().SepticMult
                    };
                    _populationController.SetState(newState);
                }
            }

            if ((_evolutionaryState.AcquiredEffects.Contains("unlock_fester") && mutation.MutationTag == "disease") ||
                (_evolutionaryState.AcquiredEffects.Contains("unlock_abraxas") && mutation.MutationTag == "psionic"))
            {
                var newState = new PopulationState
                {
                    BirthRate = _populationController.GetState().BirthRate * 1.1,
                    Damage = _populationController.GetState().Damage * 1.1f,
                    DamageMult = _populationController.GetState().DamageMult,
                    DamageReduction = _populationController.GetState().DamageReduction / 1.1f,
                    DamageReductionMult = _populationController.GetState().DamageReductionMult,
                    DeathRate = _populationController.GetState().DeathRate,
                    HealthPerRat = _populationController.GetState().HealthPerRat * 1.1f,
                    SepticMult = _populationController.GetState().SepticMult
                };
                _populationController.SetState(newState);
            }

            _evolutionaryState.TagCounts[mutation.MutationTag] =
                _evolutionaryState.TagCounts.ContainsKey(mutation.MutationTag)
                    ? _evolutionaryState.TagCounts[mutation.MutationTag]++
                    : 0;
            if (mutation.IsAbility) _evolutionaryState.AcquiredAbilities.Add((mutation.MutationName, mutation.Tooltip));

            if (_evolutionaryState.ActiveMutations.Count < 3)
            {
                GameManager.Instance.UIManager.AddNotification(
                    "This horde has acquired the maximum number of mutations.", Color.red);
                _rareMutationClock.Reset();
            }

            PointsAvailable--;
            if (GameManager.Instance.UIManager.mutationPopUp.activeSelf)
            {
                GameManager.Instance.UIManager.MutationPopUpDisable();
                GameManager.Instance.UIManager.MutationPopUpEnable();
            }
        }

        private void CalculateActiveWeights()
        {
            for (var i = 0; i < _evolutionaryState.ActiveMutations.Count; i++)
            {
                var count = 1;
                if (_evolutionaryState.TagCounts.TryGetValue(_evolutionaryState.ActiveMutations[i].MutationTag,
                        out var tagCount)) count = tagCount + 1;
                _evolutionaryState.ActiveMutations.SetWeight(_evolutionaryState.ActiveMutations[i],
                    count * _evolutionaryState.ActiveMutations[i].MutationWeight);
                //GameManager.Instance.terrainMap.GetTile(_hordeController.GetBounds().center);
                //var biome = BiomeClass.GetBiomeAtPosition(tilemap.WorldToCell(_hordeController.GetBounds().center)).template.name;
            }
        }

        private void CreatePassiveEvolutions()
        {
            // Initialise all the passive mutations
            _mutationClock.Start();
            _evolutionaryState.PassiveEvolutions["attack"] =
                new[] { 0.03, _populationController.GetState().Damage, 1.2 };
            _evolutionaryState.PassiveEvolutions["health"] =
                new[] { 0.03, _populationController.GetState().HealthPerRat, 20.0 };
            _evolutionaryState.PassiveEvolutions["defense"] =
                new[] { 0.03, _populationController.GetState().DamageReduction, 2.5 };
            _evolutionaryState.PassiveEvolutions["TundraResistance"] = new[]
                { 0.03, _populationController.GetState().TundraResistance, 2.5 };
            _evolutionaryState.PassiveEvolutions["StoneResistance"] = new[]
                { 0.03, _populationController.GetState().StoneResistance, 2.5 };
            _evolutionaryState.PassiveEvolutions["DesertResistance"] = new[]
                { 0.03, _populationController.GetState().DesertResistance, 2.5 };
            _evolutionaryState.PassiveEvolutions["GrassResistance"] = new[]
                { 0.03, _populationController.GetState().GrassResistance, 2.5 };

            _evolutionaryState.PassiveEvolutions["evolution rate"] = new[] { 0.01, 4, 0.5 };
            _evolutionaryState.PassiveEvolutions["evolution strength"] = new[] { 0.01, 1.02, 1.3 };

            _evolutionaryState.PassiveEvolutions["birth rate"] =
                new[] { 0.01, _populationController.GetState().BirthRate, 0.1 };
            //_evolutionaryState.PassiveEvolutions["resource consumption"] = new []{ 0.0005, _hordeController.Player.CheeseIncrementRate };
            // Need to change the default values for rate, and strength of evolutions to referring to values in PC.State (for horde split reasons)
            _evolutionaryState.PassiveEvolutions["rare mutation rate"] = new[] { 0.01, 40, 20 };
        }

        private void CreateActiveEvolutions()
        {
            _rareMutationClock.Start();
            var json = Resources.Load<TextAsset>("active_mutations");
            var activeMutations = JsonConvert.DeserializeObject<List<ActiveMutation>>(json.text);
            foreach (var mut in activeMutations) _evolutionaryState.ActiveMutations.Add(mut, mut.MutationWeight);
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
            _evolutionaryState = new EvolutionaryState
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
            if (_hordeController.InCombat || _hordeController.isApparition) return;
            if (_mutationClock.ElapsedInSeconds > _evolutionaryState.PassiveEvolutions["evolution rate"][1])
            {
                EvolutionaryEvent();
                _mutationClock.Restart();
            }

            if (!(_rareMutationClock.ElapsedInSeconds >
                  _evolutionaryState.PassiveEvolutions["rare mutation rate"][1]) ||
                _hordeController.player.Type != 0) return;
            PointsAvailable++;
            if (GameManager.Instance.UIManager.mutationPopUp.activeSelf &&
                _hordeController.player.GetHumanPlayer().selectedHorde!.Id == _hordeController.Id)
            {
                if (PointsAvailable == 1)
                {
                    GameManager.Instance.UIManager.MutationPopUpDisable();
                    GameManager.Instance.UIManager.MutationPopUpEnable();
                }
                else
                {
                    GameObject.FindGameObjectWithTag("mutation_points").GetComponent<TextMeshProUGUI>().text =
                        PointsAvailable + "pts";
                }
            }

            _hordeController.AddSpeechBubbleRpc(EmoteType.Evolution);
            _rareMutationClock.Restart();
        }
    }
}