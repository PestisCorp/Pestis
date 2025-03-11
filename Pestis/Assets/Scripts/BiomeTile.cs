using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeTile : TileBase
{
    public int foodLevel, HumanLevel, climate = 0;
    public Sprite tile;
    public Color tilecolor = Color.white;
    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = this.tile;
        tileData.color = this.tilecolor;
        tileData.flags = TileFlags.LockTransform;
        tileData.colliderType = Tile.ColliderType.None;
    }
    public virtual void biomeEffect(Horde.PopulationController populationController)
    {
        Debug.Log(this.GetType());
    }
    internal BiomeInstance GetBiomeInstanceAtPosition(Vector3Int position, List<BiomeInstance> biomeInstances)
    {
        foreach (var biomeInstance in biomeInstances)
        {
            if (biomeInstance.tilePositions.Contains((Vector3Int)position))
            {
                return biomeInstance;
            }
        }
        return null;
    }


    internal List<(TileBase tile, Vector3Int position)> TilesInArea(int x, int y, int size, Tilemap map)
    {
        List<(TileBase tile, Vector3Int position)> tilesWithPositions = new List<(TileBase, Vector3Int)>();
        for (int dx = -size / 2; dx <= size / 2; dx++)
        {
            for (int dy = -size / 2; dy <= size / 2; dy++)
            {
                var checkPosition = new Vector3Int(x + dx, y + dy, 0); // Ensure z is 0 or correct value
                var tile = map.GetTile(checkPosition);
                tilesWithPositions.Add((tile, checkPosition)); // Store both tile and position
            }
        }
        return tilesWithPositions;
    }
    internal TileBase GetMostCommonTile(List<(TileBase tile, Vector3Int position)> tiles)
    {
        return tiles
            .Where(t => t.tile != null) // Ignore null tiles
            .GroupBy(t => t.tile)       // Group by the tile (not the whole tuple)
            .OrderByDescending(group => group.Count()) // Sort by count (highest first)
            .Select(group => group.Key)  // Select the tile (most common one)
            .FirstOrDefault();           // Return the most common tile (or null if empty)
    }
    public virtual void automata(Tilemap map, Vector3Int position, List<BiomeInstance> biomeInstances)
    {
    

        List < (TileBase tile, Vector3Int position)> SurroundingTiles = TilesInArea(position.x, position.y, 2, map);
        if (SurroundingTiles.All(tile => tile.tile != null && tile.GetType() == this.GetType())){return; }// if surrounded by same tile type, no growth occurs
        TileBase mostCommonTile = GetMostCommonTile(SurroundingTiles);
        BiomeInstance centreBiome = GetBiomeInstanceAtPosition(position, biomeInstances);
        if (mostCommonTile.GetType() != this.GetType() && centreBiome.template.CompatableBiomeTiles.Contains(mostCommonTile))
            {
            Vector3Int commonPosition = SurroundingTiles.FirstOrDefault(t => t.tile == mostCommonTile).position;
            BiomeInstance commmonBiome = GetBiomeInstanceAtPosition(commonPosition, biomeInstances);
            int commonTileCount =  SurroundingTiles.Count(tile => tile.tile != null && tile.GetType() == mostCommonTile.GetType());
            if (commonTileCount >= 6) { centreBiome.swapTile(map, position, commmonBiome.template.getRandomBiomeTile(), commmonBiome); }
            else if (Random.value < commmonBiome.template.strength) { centreBiome.swapTile(map, position, commmonBiome.template.getRandomBiomeTile(), commmonBiome); }
            else return;
        }

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
        Vector3Int nextPosition = position + directions[Random.Range(0, 4)];
        
        BiomeTile nextTile = map.GetTile(nextPosition) as BiomeTile;
        if (nextTile != null)
        {
            
            //nextTile.automata(map, nextPosition, biomeInstances);
        }

    }

    public virtual void FeatureGeneration()
    {
        // Empty method
    }
}
