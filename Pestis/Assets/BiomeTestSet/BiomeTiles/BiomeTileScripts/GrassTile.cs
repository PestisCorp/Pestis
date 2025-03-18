using UnityEngine;



[CreateAssetMenu(menuName = "Tiles/GrassTile")]
public class GrassTile : BiomeTile
{
    public override void biomeEffect(Horde.PopulationController populationController, Horde.HordeController horde)
    {
        base.biomeEffect(populationController, horde);
        GenericEffect(populationController, horde, populationController.GetState().GrassResistance);
    }
}
