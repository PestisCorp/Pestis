using Horde;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BiomeEffects : MonoBehaviour
{
    public HordeController horde;
    public Tilemap tilemap;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Find the tilemap dynamically in the scene
        GameObject tilemapObject = GameObject.FindGameObjectWithTag("tilemap");
        if (tilemapObject != null)
        {
            this.tilemap = tilemapObject.GetComponent<Tilemap>();
        }
        else
        {
            Debug.LogError("Tilemap not found in the scene!");
        }
    }

    void DetectTileBeneath()
    {
        Vector3Int playerCell = tilemap.WorldToCell(transform.position);
        TileBase tileBelow = tilemap.GetTile(playerCell);

        if (tileBelow is BiomeTile biome)
        {
            biome.biomeEffect(null);
        }
    }
    // Update is called once per frame
    void Update()
    {
        DetectTileBeneath();
    }
}
