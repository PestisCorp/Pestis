using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Biomes/GrassBiome")]
public class GrassBiome : BiomeClass
{
    public override void CellGeneration(Tilemap map, BiomeInstance biomeInstance) //generate biomes by seeding 1 biomeTile and then repeatedly "growing" it
    {
        BoundsInt bounds = map.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(pos);
            if (this.CompatableBiomeTiles.Contains(tile))
            {
                biomeInstance.setTile(map, pos, getRandomBiomeTile());
            }
        }
    }
}
