using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/TundraTile")]
public class TundraTile : BiomeTile
{

    public override void biomeEffect(Horde.PopulationController populationController, Horde.HordeController horde)
    {
        base.biomeEffect(populationController, horde);
        GenericEffect(populationController, horde, populationController.GetState().TundraResistance);
    }
}
