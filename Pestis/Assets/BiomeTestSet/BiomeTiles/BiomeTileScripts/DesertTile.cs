using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/DesertTile")]
public class DesertTile : BiomeTile
{
    public override void biomeEffect(Horde.PopulationController populationController)
    {
        base.biomeEffect(populationController);
        GenericEffect(populationController, populationController.GetState().DesertResistance);
    }
}
