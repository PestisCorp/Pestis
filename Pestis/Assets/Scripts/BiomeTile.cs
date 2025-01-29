using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeTile : TileBase
{
    public int foodLevel, HumanLevel, climate = 0;
    public Sprite tile;
    
    public virtual void FeatureGeneration()
    {
        // Empty method
    }
}
