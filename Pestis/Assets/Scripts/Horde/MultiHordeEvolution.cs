using System.Collections.Generic;
using UnityEngine;
using Horde;
public class MultiHordeEvolution : MonoBehaviour
{
    public List<EvolutionTile> MultiHordeEvolutions = new List<EvolutionTile>();
    public PopulationState applyAllTiles(PopulationState state)
    {
        PopulationState populationState = new PopulationState();
        
            //multiplicative effects;
        float AttackMult = 1;
        float HealthMult = 1;
        float SpeedMult = 1;
        float DefenseMult = 1;
        //additive effects
        float Attack = 0;
        float Health = 0;
        float Speed = 0;
        float Defense = 0;

        for (int i = 0; i < MultiHordeEvolutions.Count ; i++)
        {
            AttackMult *= MultiHordeEvolutions[i].AttackMult;
            HealthMult *= MultiHordeEvolutions[i].HealthMult;
            SpeedMult *= MultiHordeEvolutions[i].SpeedMult;
            DefenseMult *= MultiHordeEvolutions[i].DefenseMult;
            Attack += MultiHordeEvolutions[i].Attack;
            Health += MultiHordeEvolutions[i].Health;
            Speed += MultiHordeEvolutions[i].Speed;
            Defense += MultiHordeEvolutions[i].Defense;
        }
        populationState.HealthPerRat = (state.HealthPerRat + Health) * HealthMult;
        populationState.HealthPerRat = (state.HealthPerRat + Attack) * AttackMult;
        //populationState.Speed = (state.HealthPerRat + Speed) * SpeedMult; 
        populationState.HealthPerRat = (state.HealthPerRat + Defense) * DefenseMult;
        populationState.BirthRate = state.BirthRate;
        populationState.DeathRate = state.DeathRate;
        return populationState;
    }

}
