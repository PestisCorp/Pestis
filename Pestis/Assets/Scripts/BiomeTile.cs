using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu]
public class BiomeTile : TileBase
{
    public int foodLevel, HumanLevel, climate = 0;
    public Sprite tile;


    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = this.tile;
        tileData.color = Color.white;
        tileData.flags = TileFlags.LockTransform;
        tileData.colliderType = Tile.ColliderType.None;
    }

    public virtual void FeatureGeneration()
    {
        // Empty method
    }
}
