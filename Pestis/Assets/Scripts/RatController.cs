using System;
using UnityEngine;

/// <summary>
/// Component attached to each rat so that clicking a Rat selects the horde it belongs to
/// </summary>
public class RatController : MonoBehaviour
{
    private void OnMouseDown()
    {
        HordeController hordeController = GetComponentInParent<HordeController>();
        HumanPlayer player = hordeController.GetComponentInParent<HumanPlayer>();
        player.SelectHorde(hordeController);
    }
}
