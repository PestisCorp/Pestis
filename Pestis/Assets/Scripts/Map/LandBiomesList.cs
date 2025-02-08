using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    public class LandBiomesList : MonoBehaviour
    {
        [HideInInspector] public TileBase[] landTiles;

        public TileBase[] GetList()
        {
            return landTiles;
        }
    }
}