// Population manager. Update birth and death rates here

using System;
using System.Linq;
using Fusion;
using KaimiraGames;
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
        private const int PopMax = 1000;
        private const int resources = 100;
        private const int MaxPopGrowth = 1;
        public HordeController hordeController;

        /// <summary>
        ///     Stores the highest health this horde has achieved. Used to stop the player losing too much progress.
        /// </summary>
        private float _highestHealth;

        private Random _random;

        [Networked] private ref PopulationState State => ref MakeRef<PopulationState>();

        private double ResourceWeightGrowth()
        {
            return (double)resources / (resources + hordeController.AliveRats);
        }

        private double ResourceWeightDecline()
        {
            return (double)hordeController.AliveRats / (resources + hordeController.AliveRats);
        }

        private double Alpha(double weight)
        {
            return State.BirthRate * ResourceWeightGrowth() * weight;
        }

        private double Beta(double weight)
        {
            return State.DeathRate * ResourceWeightDecline() * weight;
        }
        
        static void NormalizeRows(double[,] matrix, int n)
        {
            for (int i = 0; i < n; i++)
            {
                double rowSum = 0;
                for (int j = 0; j < n; j++)
                    rowSum += matrix[i, j];

                if (rowSum > 0) 
                    
                {
                    for (int j = 0; j < n; j++)
                        matrix[i, j] /= rowSum; 
                }
            }
        }
        
        private double[,] GenerateTransitionMatrix(int[] weights)
        {
            var transitionMatrix = new double[PopMax, PopMax];
            int wMin = weights.Min();
            int wMax = weights.Max();
            for (int i = 0; i < PopMax; i++)
            {
                double delta = 0;
                for (int j = 0; j < MaxPopGrowth; j++)
                {
                    float w = (wMin == wMax) ? 1 : (float)(weights[j] - wMin) / (wMax - wMin);
                    if (i + j <= PopMax)
                    {
                        double alphaj = Alpha(w);
                        transitionMatrix[i, i + j] = alphaj;
                        delta += alphaj;
                    }

                    if (i - j >= 1)
                    {
                        double betaj = Beta(w);
                        transitionMatrix[i, i - j] = betaj;
                        delta += betaj;
                    }
                }

                transitionMatrix[i, i] = Math.Max(1 - delta, 0.0);
                
            }
            NormalizeRows(transitionMatrix, PopMax);
            return transitionMatrix;
        }
        
        public override void Spawned()
        {
            State.BirthRate = 0.0015;
            State.DeathRate = 0.0009;
            State.HealthPerRat = 5.0f;
            State.Damage = 0.5f;

            hordeController.TotalHealth = InitialPopulation * State.HealthPerRat;
        }

        // Check for birth or death events
        private void PopulationEvent()
        {
            int[] weights = Enumerable.Range(1, MaxPopGrowth).Reverse().ToArray();
            var transitionMatrix = GenerateTransitionMatrix(weights);
            double[] probabilities = new double[transitionMatrix.GetLength(1)];
            for (int j = 0; j < transitionMatrix.GetLength(1); j++)
            {
                probabilities[j] = transitionMatrix[hordeController.AliveRats, j];
            }

            WeightedList<int> stateDist = new();
            for (int i = 0; i < PopMax; i++)
            {
                stateDist.Add(i, (int)(probabilities[i] * 10000));
            }
            hordeController.TotalHealth = stateDist.Next() * State.HealthPerRat;
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