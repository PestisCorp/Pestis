
using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/StoneTile")]
public class StoneTile : BiomeTile
{
    public override void biomeEffect(Horde.PopulationController populationController)
    {
        base.biomeEffect(populationController);
        GenericEffect(populationController, populationController.GetState().StoneResistance);
    }
}
