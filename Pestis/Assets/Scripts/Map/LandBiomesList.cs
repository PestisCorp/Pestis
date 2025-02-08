using UnityEngine;
using UnityEngine.Tilemaps;

public class LandBiomesList : MonoBehaviour
{
    [HideInInspector] public TileBase[] landTiles;

    public TileBase[] GetList()
    {
        return landTiles;
    }
}