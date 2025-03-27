// Population manager. Update birth and death rates here

using System;
using System.Collections.Generic;
using System.Linq;
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
        internal float TundraResistance;
        internal float DesertResistance;
        internal float GrassResistance;
        internal float StoneResistance;

        /// <summary>
        ///     How much damage the horde does to other hordes per tick in combat
        /// </summary>
        internal float Damage;

        internal float DamageReduction;

        // Multipliers applied to damage original state
        internal float DamageMult;
        internal float DamageReductionMult;
        internal float SepticMult;
    }

    public class PopulationController : NetworkBehaviour
    {
        // Soft population limit, should be extremely difficult for the player to grow beyond this, but still possible
        private const int PopMax = 1000;

        // The maximum change in a population per network tick
        private const int MaxPopGrowth = 3;

        private readonly Random _random = new();

        // Weights are reversed because weight decreases with distance from n
        // e.g. the probability of going from population n to n + 5 should be
        // smaller than going from n to n + 1, so the weight applied is smaller
        // for the former transition
        private readonly int[] _weights = Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray();

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private HordeController _hordeController;

        private Timer _populationClock;

        private int _populationPeak;

        private List<double[]> _transitionMatrix;
        public int initialPopulation = 5;
        public bool isAgriculturalist;

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();


        // Weight used in probability of population growth
        // W > 1 if R > P
        // W = 1 if R == P
        // W < 1 if R < P
        private double ResourceWeightGrowth()
        {
            var w = 2.5;
            if (isAgriculturalist) w *= 1.2;
            return 1 + w *
                (1.0 - Math.Exp(-(_hordeController.player.CurrentCheese / (uint)_hordeController.AliveRats - 1)));
        }


        // Weight used in probability of population decline
        // W = 1 if R >= P
        // W > 1 if R < P
        private double ResourceWeightDecline()
        {
            if (_hordeController.player.CurrentCheese >= (uint)_hordeController.AliveRats) return 1.0;
            return Math.Exp(1.0 - _hordeController.player.CurrentCheese / (uint)_hordeController.AliveRats);
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
            return State.DeathRate * weight * ((double)PopMax / (population + PopMax));
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

                var beta = Beta(w, _populationPeak);
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
            State.BirthRate = 0.01;
            State.DeathRate = 0.005;

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

            _hordeController.TotalHealth = initialPopulation * State.HealthPerRat;

            _populationClock.Start();

            _transitionMatrix = GenerateTransitionMatrix();
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            _populationClock.Restart();
            if (_hordeController.AliveRats > _populationPeak)
            {
                for (var i = _populationPeak + 1; i <= _hordeController.AliveRats; i++) UpdateTransitionMatrix(i);
                _populationPeak = _hordeController.AliveRats;
            }

            // Lookup the relevant probabilities for the current population
            var probabilities = _transitionMatrix[_hordeController.AliveRats - 1];
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
            _hordeController.TotalHealth = (nextState - MaxPopGrowth + _hordeController.AliveRats) * State.HealthPerRat;
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
            State.Damage = damage;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetHealthPerRatRpc(float healthPerRat)
        {
            State.HealthPerRat = healthPerRat;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageReductionRpc(float damageReduction)
        {
            State.DamageReduction = 1.0f / damageReduction;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageReductionMultRpc(float damageReductionMult)
        {
            State.DamageReductionMult = damageReductionMult;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetBirthRateRpc(double birthRate)
        {
            State.BirthRate = birthRate;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetDamageMultRpc(float damageMult)
        {
            State.DamageMult = damageMult;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void SetSepticMultRpc(float damageMult)
        {
            State.DamageMult = damageMult;
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