using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeTile : IsometricRuleTile
{
    public int foodLevel, HumanLevel, climate = 0;
    public virtual void FeatureGeneration()
    {
        // Empty method
    }
}
