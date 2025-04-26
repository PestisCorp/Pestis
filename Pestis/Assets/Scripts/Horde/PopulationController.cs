// Population manager. Update birth and death rates here

using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Networking;
using Unity.Profiling;
using UnityEngine;
using Random = System.Random;

namespace Horde
{
    public struct PopulationState : INetworkStruct
    {
        // GROUP 1 - 30 bits

        private PopulationGroup1 _group1 { get; set; }

        // 0 -> 1, 0.001 accuracy, 1000 vals, 10 bits
        internal double BirthRate
        {
            get => _group1.BirthRate;
            set
            {
                var temp = _group1;
                temp.BirthRate = value;
                _group1 = temp;
            }
        }

        internal double DeathRate
        {
            get => _group1.BirthRate;
            set
            {
                var temp = _group1;
                temp.DeathRate = value;
                _group1 = temp;
            }
        }

        // 10 -> 20, 0.01 accuracy, 1000 vals, 10 bits
        internal float HealthPerRat
        {
            get => _group1.HealthPerRat + 10;
            set
            {
                var temp = _group1;
                temp.HealthPerRat = value - 10;
                _group1 = temp;
            }
        }

        // GROUP 2 - 32 bits
        private PopulationGroup2 _group2 { get; set; }

        // 0 -> 1.5 clamp, 0.01 accuracy, 150 vals, 8 bits
        internal float TundraResistance
        {
            get => _group2.Tundra;
            set
            {
                var temp = _group2;
                temp.Tundra = Mathf.Clamp(value, 0, 1.5f);
                _group2 = temp;
            }
        }

        internal float DesertResistance
        {
            get => _group2.Desert;
            set
            {
                var temp = _group2;
                temp.Desert = Mathf.Clamp(value, 0, 1.5f);
                _group2 = temp;
            }
        }

        internal float GrassResistance
        {
            get => _group2.Grass;
            set
            {
                var temp = _group2;
                temp.Grass = Mathf.Clamp(value, 0, 1.5f);
                _group2 = temp;
            }
        }

        internal float StoneResistance
        {
            get => _group2.Stone;
            set
            {
                var temp = _group2;
                temp.Stone = Mathf.Clamp(value, 0, 1.5f);
                _group2 = temp;
            }
        }

        // GROUP 3 - 14 bits
        private PopulationGroup3 _group3 { get; set; }

        /// <summary>
        ///     How much damage the horde does to other hordes per tick in combat
        /// </summary>
        /// 0 -> 1.2, 0.01 accuracy, 120 vals, 7 bits
        internal float Damage
        {
            get => _group3.Damage;
            set
            {
                var temp = _group3;
                temp.Damage = Mathf.Clamp(value, 0, 1.2f);
                _group3 = temp;
            }
        }

        // 0.4 -> 1.0, 0.01 accuracy, 80 vals, 7 bits
        internal float DamageReduction
        {
            get => _group3.DamageReduction;
            set
            {
                var temp = _group3;
                temp.DamageReduction = Mathf.Clamp(value, 0, 1.2f);
                _group3 = temp;
            }
        }

        // GROUP 4 - 24 bits
        private PopulationGroup4 _group4 { get; set; }

        // Multipliers applied to damage original state
        // 0 -> 2 clamped, 0.01 accuracy, 200 vals, 8 bits
        internal float DamageMult
        {
            get => _group4.DamageMult;
            set
            {
                var temp = _group4;
                temp.DamageMult = Mathf.Clamp(value, 0, 2);
                _group4 = temp;
            }
        }

        internal float DamageReductionMult
        {
            get => _group4.DamageReductionMult;
            set
            {
                var temp = _group4;
                temp.DamageReductionMult = Mathf.Clamp(value, 0, 2);
                _group4 = temp;
            }
        }

        internal float SepticMult
        {
            get => _group4.SepticMult;
            set
            {
                var temp = _group4;
                temp.SepticMult = Mathf.Clamp(value, 0, 2);
                _group4 = temp;
            }
        }
    }

    public class PopulationController : NetworkBehaviour
    {
        // Soft population limit, should be extremely difficult for the player to grow beyond this, but still possible
        private const int PopMax = 1000;

        // The maximum change in a population per network tick
        private const int MaxPopGrowth = 3;

        private static readonly ProfilerMarker s_SetDamage = new("RPCPopulation.SetDamage");

        private static readonly ProfilerMarker s_SetHealth = new("RPCPopulation.SetHealth");

        private static readonly ProfilerMarker s_SetDamageReduction = new("RPCPopulation.SetDamageReduction");

        private static readonly ProfilerMarker s_SetDamageReductionMult = new("RPCPopulation.SetDamageReductionMult");

        private static readonly ProfilerMarker s_SetBirthRate = new("RPCPopulation.SetBirthRate");

        private static readonly ProfilerMarker s_SetDamageMult = new("RPCPopulation.SetDamageMult");

        private static readonly ProfilerMarker s_SetSepticMult = new("RPCPopulation.SetSepticMult");
        public int initialPopulation = 5;
        public bool isAgriculturalist;

        private readonly Random _random = new();

        // Weights are reversed because weight decreases with distance from n
        // e.g. the probability of going from population n to n + 5 should be
        // smaller than going from n to n + 1, so the weight applied is smaller
        // for the former transition
        private readonly int[] _weights = Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray();
        private BiomeEffects _biomeEffects;

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private HordeController _hordeController;

        private Timer _populationClock;

        private int _populationPeak;

        private List<double[]> _transitionMatrix;

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();


        // Weight used in probability of population growth
        // W > 1 if R > P
        // W = 1 if R == P
        // 0.3 < W < 1 if R < P
        private double ResourceWeightGrowth()
        {
            var currentBiome = "";
            if (_biomeEffects.currentBiome) currentBiome = _biomeEffects.currentBiome.name;
            var resistance = currentBiome switch
            {
                "GrassTile" => State.GrassResistance,
                "TundraTile" => State.TundraResistance,
                "StoneTile" => State.StoneResistance,
                "DesertTile" => State.DesertResistance,
                _ => 1f
            };
            var w = 2.5 * resistance;
            if (isAgriculturalist) w *= 1.2;
            return Math.Max(1 + w *
                (1.0 - Math.Exp(-(_hordeController.player.CurrentCheese / (uint)_hordeController.AliveRats - 1))), 0.3);
        }


        // Weight used in probability of population decline
        // W = 1 if R >= P
        // W > 1 if R < P
        private double ResourceWeightDecline()
        {
            if (_hordeController.player.CurrentCheese >= (uint)_hordeController.AliveRats) return 1.0;
            return Math.Exp(2 - _hordeController.player.CurrentCheese / (uint)_hordeController.AliveRats);
        }

        // Calculate probability of population growth
        // Tapers off as population approaches PopMax
        private double Alpha(double weight, int population)
        {
            return State.BirthRate * weight * ((double)PopMax / (population + PopMax));
        }

        // Calculate probability of population decline
        private double Beta(double weight, int population)
        {
            return State.DeathRate * weight;
        }

        // Normalize rows of a matrix to be between 0 and 1.0
        private void NormalizeRows(List<double[]> matrix)
        {
            for (var i = 0; i < _populationPeak; i++)
            {
                var ratio = 1.0 / matrix[i].Sum();
                matrix[i] = matrix[i].Select(o => o * ratio).ToArray();
            }
        }

        // Generate an InitialPopulation x (MaxPopGrowth * 2) + 1 transition matrix
        // Uses a list type so that rows can be added when a new populationPeak is reached
        // The entry X[n][n] is the probability of the population staying the same
        // The entry X[n][n+k] is the probability of the population growing by an amount k
        // The entry X[n][n-k] is the probability of the population declining by an amount k
        private List<double[]> GenerateTransitionMatrix()
        {
            _populationPeak = initialPopulation;
            var transitionMatrix = new List<double[]>(_populationPeak);
            var wMin = _weights.Min();
            var wMax = _weights.Max();
            for (var i = 0; i < _populationPeak; i++)
            {
                // Delta is the probability of staying at the same population
                double delta = 0;
                var row = new double[MaxPopGrowth * 2 + 1];
                for (var j = 0; j < MaxPopGrowth; j++)
                {
                    // Normalise w using MinMax rescaling
                    var w = wMin == wMax ? 1 : (double)(_weights[j] - wMin) / (wMax - wMin);

                    var alpha = Alpha(w, i + 1);
                    row[MaxPopGrowth + j + 1] = alpha;
                    delta += alpha;

                    var beta = Beta(w, i + 1);
                    row[MaxPopGrowth - j - 1] = beta;
                    delta += beta;
                }

                row[MaxPopGrowth] = Math.Max(1 - delta, 0);
                transitionMatrix.Add(row);
            }

            NormalizeRows(transitionMatrix);
            return transitionMatrix;
        }

        // Update the transition matrix when a new population peak is achieved
        // Logic is mostly similar to GenerateTransitionMatrix, except that you are
        // operating on a single row here
        private void UpdateTransitionMatrix(int population)
        {
            var row = new double[MaxPopGrowth * 2 + 1];
            double delta = 0;
            var wMin = _weights.Min();
            var wMax = _weights.Max();
            for (var i = 0; i < MaxPopGrowth; i++)
            {
                var w = wMin == wMax ? 1 : (double)(_weights[i] - wMin) / (wMax - wMin);

                var alpha = Alpha(w, population);
                row[MaxPopGrowth + i + 1] = alpha;
                delta += alpha;

                var beta = Beta(w, population);
                row[MaxPopGrowth - i - 1] = beta;
                delta += beta;
            }

            row[MaxPopGrowth] = Math.Max(1 - delta, 0);
            // Normalize row
            var ratio = 1.0 / row.Sum();
            row = row.Select(o => o * ratio).ToArray();
            _transitionMatrix.Add(row);
        }

        public override void Spawned()
        {
            _hordeController = GetComponent<HordeController>();
            _biomeEffects = _hordeController.GetComponent<BiomeEffects>();
            State.BirthRate = 0.1;
            State.DeathRate = 0.05;

            State.HealthPerRat = 10.0f;
            State.Damage = 0.5f;
            State.DamageReduction = 1.0f;
            State.GrassResistance = 1.0f;
            State.DesertResistance = 1.0f;
            State.TundraResistance = 1.0f;
            State.StoneResistance = 1.0f;

            State.DamageMult = 1.0f;
            State.DamageReductionMult = 1.0f;
            State.SepticMult = 1.0f;

            _hordeController.AliveRats = new IntPositive((uint)initialPopulation);

            _populationClock.Start();

            _transitionMatrix = GenerateTransitionMatrix();
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            return;
            _populationClock.Restart();
            if (_hordeController.AliveRats > _populationPeak)
            {
                for (var i = _populationPeak + 1; i <= _hordeController.AliveRats; i++) UpdateTransitionMatrix(i);
                _populationPeak = _hordeController.AliveRats;
            }

            // Lookup the relevant probabilities for the current population
            var probabilities = (double[])_transitionMatrix[_hordeController.AliveRats - 1].Clone();
            var growthWeight = ResourceWeightGrowth();
            var declineWeight = ResourceWeightDecline();
            for (var i = 0; i < MaxPopGrowth * 2 + 1; i++)
            {
                if (MaxPopGrowth < i) probabilities[i] *= growthWeight;

                if (MaxPopGrowth > i) probabilities[i] *= declineWeight;
            }

            var ratio = 1.0 / probabilities.Sum();
            probabilities = probabilities.Select(o => o * ratio).ToArray();
            // Use a CDF for doing a weighted sample of the transition states
            double cumulative = 0.0f;
            var cdf = new List<double>(MaxPopGrowth * 2 + 1);
            for (var i = 0; i < MaxPopGrowth * 2 + 1; i++)
            {
                cumulative += probabilities[i];
                cdf.Add(cumulative);
            }

            // Randomly pick a number and do a binary search for it in CDF, returns the closest match
            var r = _random.NextDouble() * cumulative;
            var nextState = cdf.BinarySearch(r);
            if (nextState < 0) nextState = ~nextState;
            _hordeController.AliveRats = new IntPositive((uint)(nextState - MaxPopGrowth + _hordeController.AliveRats));
        }

        // Only executed on State Authority
        public override void FixedUpdateNetwork()
        {
            // Suspend population simulation during combat or retreat to avoid interference
            if (!_hordeController.InCombat && _hordeController.populationCooldown == 0 &&
                _populationClock.ElapsedInMilliseconds > 50 && !_hordeController.isApparition) PopulationEvent();
            _highestHealth = Mathf.Max(_hordeController.TotalHealth, _highestHealth);
            if (!_hordeController.InCombat)
                _hordeController.TotalHealth =
                    Mathf.Max(_hordeController.TotalHealth, initialPopulation * State.HealthPerRat);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageRpc(float damage)
        {
            s_SetDamage.Begin();
            State.Damage = damage;
            s_SetDamage.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetHealthPerRatRpc(float healthPerRat)
        {
            s_SetHealth.Begin();
            State.HealthPerRat = healthPerRat;
            s_SetHealth.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageReductionRpc(float damageReduction)
        {
            s_SetDamageReduction.Begin();
            State.DamageReduction = 1.0f / damageReduction;
            s_SetDamageReduction.End();
        }


        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageReductionMultRpc(float damageReductionMult)
        {
            s_SetDamageReductionMult.Begin();
            State.DamageReductionMult = damageReductionMult;
            s_SetDamageReductionMult.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetBirthRateRpc(double birthRate)
        {
            s_SetBirthRate.Begin();
            State.BirthRate = birthRate;
            s_SetBirthRate.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageMultRpc(float damageMult)
        {
            s_SetDamageMult.Begin();
            State.DamageMult = damageMult;
            s_SetDamageMult.End();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetSepticMultRpc(float damageMult)
        {
            s_SetSepticMult.Begin();
            State.DamageMult = damageMult;
            s_SetSepticMult.End();
        }

        public PopulationState GetState()
        {
            return State;
        }

        public void SetState(PopulationState newState)
        {
            State = newState;
        }


        //I don't understand how population stuff works well enough to impliment this
        public void speedMult(float mult)
        {
            //multiplies speed once, should not repeatedly multiply if called multiple times
        }
    }
}