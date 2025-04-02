using Horde;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BiomeEffects : MonoBehaviour
{
    public PopulationController pop;
    public HordeController horde;
    public Tilemap tilemap;
    public BiomeTile currentBiome;

    void Start()
    {
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
        Vector3Int playerCell = tilemap.WorldToCell(horde.GetBounds().center);
        TileBase tileBelow = tilemap.GetTile(playerCell);

        if (tileBelow is BiomeTile biome)
        {
            biome.biomeEffect(pop, horde);
            currentBiome = biome;
        }
    }
    // Update is called once per frame
    void FixedUpdate()
    {
        DetectTileBeneath();
    }
}
