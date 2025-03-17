
using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/StoneTile")]
public class StoneTile : BiomeTile
{
    public override void biomeEffect(Horde.PopulationController populationController, Horde.HordeController horde)
    {
        base.biomeEffect(populationController, horde);
        GenericEffect(populationController, horde, populationController.GetState().StoneResistance);
    }
}
