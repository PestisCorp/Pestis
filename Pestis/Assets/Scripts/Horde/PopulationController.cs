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
        // The maximum change in a population per network tick
        private const int MaxPopGrowth = 1;
        public HordeController hordeController;

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private Random _random = new Random();

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
        private static void NormalizeRows(double[][] matrix)
        {
            for (int i = 0; i < PopMax; i++)
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
            var transitionMatrix = new double[PopMax][];
            int wMin = weights.Min();
            int wMax = weights.Max();
            for (int i = 0; i < PopMax; i++)
            {
                // Delta is the probability of staying at the same population
                double delta = 0;
                var row = new double[PopMax];
                for (int j = 0; j < MaxPopGrowth; j++)
                {
                    // Normalise w using MinMax rescaling
                    double w = (wMin == wMax) ? 1 : (double)(weights[j] - wMin) / (wMax - wMin);
                    if (i + j + 1 < PopMax)
                    {
                        double alpha = Alpha(w);
                        row[i + j + 1] = alpha;
                        delta += alpha;
                    }

                    if (i - j - 1 >= 0)
                    {
                        double beta = Beta(w);
                        row[i - j - 1] = beta;
                        delta += beta;
                    }
                }
                row[i] = Math.Max(1 - delta, 0);
                transitionMatrix[i] = row;
            }
            NormalizeRows(transitionMatrix);
            return transitionMatrix;
        }
        
        public override void Spawned()
        {
            State.BirthRate = 0.01;
            State.DeathRate = 0.005;
            State.HealthPerRat = 5.0f;
            State.Damage = 0.5f;
            hordeController.TotalHealth = InitialPopulation * State.HealthPerRat;
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            // Weights are reversed because weight decreases with distance from n
            // e.g. the probability of going from population n to n + 5 should be
            // smaller than going from n to n + 1, so the weight applied is smaller
            // for the former transition
            int[] weights = Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray();
            var transitionMatrix = GenerateTransitionMatrix(weights);
            double[] probabilities = transitionMatrix[hordeController.AliveRats - 1];
            // Use a CDF for doing a weighted sample of the transition states
            double cumulative = 0.0f;
            List<double> cdf = new List<double>(PopMax);
            for (int i = 0; i < PopMax; i++)
            {
                cumulative += probabilities[i];
                cdf.Add(cumulative);
            }
            double r = _random.NextDouble() * cumulative;
            int nextState = cdf.BinarySearch(r);
            if (nextState < 0) nextState = ~nextState;
            hordeController.TotalHealth = (nextState + 1) * State.HealthPerRat;
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