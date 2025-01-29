using UnityEngine;
using UnityEngine.Tilemaps;

public class LandBiomesList : MonoBehaviour
{
    [HideInInspector] public TileBase[] landTiles = new TileBase[] { };

    public TileBase[] GetList()
    {
        return landTiles;
    }
}