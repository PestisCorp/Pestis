using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/TundraTile")]
public class TundraTile : BiomeTile
{

    public override void biomeEffect(Horde.PopulationController populationController)
    {
        base.biomeEffect(populationController);
        GenericEffect(populationController, populationController.GetState().TundraResistance);
    }
}
