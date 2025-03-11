using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/DesertTile")]
public class DesertTile : BiomeTile
{
    public override void biomeEffect(Horde.PopulationController populationController, Horde.HordeController horde)
    {
        base.biomeEffect(populationController, horde);
        GenericEffect(populationController, horde, populationController.GetState().DesertResistance);
    }
}
