// Population manager. Update birth and death rates here

using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

namespace Horde
{
    public struct PopulationState : INetworkStruct
    {
        internal double BirthRate;
        internal double DeathRate;
        internal float HealthPerRat;
        internal int PopulationPeak;

        /// <summary>
        ///     How much damage the horde does to other hordes per tick in combat
        /// </summary>
        internal float Damage;
    }

    public class PopulationController : NetworkBehaviour
    {
        private const int InitialPopulation = 5;
        private const int PopMax = 10;
        private const int resources = 10;

        private double[][] _transitionMatrix;
        // The maximum change in a population per network tick
        private const int MaxPopGrowth = 1;
        public HordeController hordeController;

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private readonly Random _random = new Random();

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();
        
        
        // Weight used in probability of population growth
        private double ResourceWeightGrowth()
        {
            return (double)resources / (resources + hordeController.AliveRats);
        }
        
        // Weight used in probability of population decline
        private double ResourceWeightDecline()
        {
            return (double)hordeController.AliveRats / (resources + hordeController.AliveRats);
        }
        
        // Calculate probability of population growth
        private double Alpha(double weight)
        {
            return State.BirthRate * ResourceWeightGrowth() * weight;
        }
        
        // Calculate probability of population decline
        private double Beta(double weight)
        {
            return State.DeathRate * ResourceWeightDecline() * weight;
        }
        
        // Normalize rows of a matrix to be between 0 and 1.0
        private void NormalizeRows(double[][] matrix)
        {
            for (int i = 0; i < State.PopulationPeak; i++)
            {
                double ratio = 1.0 / matrix[i].Sum();
                matrix[i] = matrix[i].Select(o => o * ratio).ToArray();
            }
        }
        
        // Generate a PopMax x PopMax transition matrix
        // The entry X[n][n] is the probability of the population staying the same
        // The entry X[n][n+k] is the probability of the population growing by an amount k
        // The entry X[n][n-k] is the probability of the population declining by an amount k
        private double[][] GenerateTransitionMatrix(int[] weights)
        {
            var transitionMatrix = new double[State.PopulationPeak][];
            int wMin = weights.Min();
            int wMax = weights.Max();
            for (int i = 0; i < State.PopulationPeak; i++)
            {
                // Delta is the probability of staying at the same population
                double delta = 0;
                var row = new double[(MaxPopGrowth * 2) + 1];
                for (int j = 0; j < MaxPopGrowth; j++)
                {
                    // Normalise w using MinMax rescaling
                    double w = (wMin == wMax) ? 1 : (double)(weights[j] - wMin) / (wMax - wMin);
                    
                    double alpha = Alpha(w);
                    row[MaxPopGrowth + j + 1] = alpha;
                    delta += alpha;
                    
                    double beta = Beta(w);
                    row[MaxPopGrowth - j - 1] = beta;
                    delta += beta;
                    
                }
                row[MaxPopGrowth] = Math.Max(1 - delta, 0);
                transitionMatrix[i] = row;
            }
            NormalizeRows(transitionMatrix);
            return transitionMatrix;
        }
        
        
        private void UpdateTransitionMatrix(int[] weights)
        {
            var row = new double[(MaxPopGrowth * 2) + 1];
            double delta = 0;
            int wMin = weights.Min();
            int wMax = weights.Max();
            for (int i = 0; i < MaxPopGrowth; i++)
            {
                double w = (wMin == wMax) ? 1 : (double)(weights[i] - wMin) / (wMax - wMin);
                
                double alpha = Alpha(w);
                row[MaxPopGrowth + i + 1] = alpha;
                delta += alpha;
                
                double beta = Beta(w);
                row[MaxPopGrowth - i - 1] = beta;
                delta += beta;
                
            }
            row[MaxPopGrowth] = Math.Max(1 - delta, 0);
            double ratio = 1.0 / row.Sum();
            row = row.Select(o => o * ratio).ToArray();
            _transitionMatrix[State.PopulationPeak] = row;
        }
        
        public override void Spawned()
        {
            State.BirthRate = 0.01;
            State.DeathRate = 0.005;
            State.HealthPerRat = 5.0f;
            State.Damage = 0.5f;
            State.PopulationPeak = InitialPopulation;
            hordeController.TotalHealth = InitialPopulation * State.HealthPerRat;
            _transitionMatrix = GenerateTransitionMatrix(Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray());
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            // Weights are reversed because weight decreases with distance from n
            // e.g. the probability of going from population n to n + 5 should be
            // smaller than going from n to n + 1, so the weight applied is smaller
            // for the former transition
            int[] weights = Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray();
            double[] probabilities = _transitionMatrix[(MaxPopGrowth * 2) + 1];
            // Use a CDF for doing a weighted sample of the transition states
            double cumulative = 0.0f;
            List<double> cdf = new List<double>((MaxPopGrowth * 2) + 1);
            for (int i = 0; i < (MaxPopGrowth * 2) + 1; i++)
            {
                cumulative += probabilities[i];
                cdf.Add(cumulative);
            }
            double r = _random.NextDouble() * cumulative;
            int nextState = cdf.BinarySearch(r);
            if (nextState < 0) nextState = ~nextState;
            hordeController.TotalHealth = (nextState - MaxPopGrowth + hordeController.AliveRats) * State.HealthPerRat;
            if ((nextState + 1) > State.PopulationPeak)
            {
                State.PopulationPeak = nextState + 1;
                UpdateTransitionMatrix(weights);
            }
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