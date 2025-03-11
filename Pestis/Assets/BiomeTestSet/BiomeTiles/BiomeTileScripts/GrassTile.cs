using UnityEngine;



[CreateAssetMenu(menuName = "Tiles/GrassTile")]
public class GrassTile : BiomeTile
{
    public override void biomeEffect(Horde.PopulationController populationController)
    {
        base.biomeEffect(populationController);
        GenericEffect(populationController, populationController.GetState().GrassResistance);
    }
}
