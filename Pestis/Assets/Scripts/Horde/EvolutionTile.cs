using Horde;
using System;
using UnityEngine;

[CreateAssetMenu]
[Serializable]
public class EvolutionTile : ScriptableObject
{
    public Sprite card;
    public string description;
    //multiplicative effects;
    public float AttackMult;
    public float HealthMult;
    public float SpeedMult;
    public float DefenseMult;
    //additive effects
    public float Attack;
    public float Health;
    public float Speed;
    public float Defense;
}
