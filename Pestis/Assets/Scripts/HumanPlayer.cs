using Fusion;
using Horde;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

public class HumanPlayer : MonoBehaviour
{
    [FormerlySerializedAs("_selectedHorde")] [CanBeNull] public HordeController SelectedHorde = null;
    // Whether this HumanPlayer is the one being controlled by the player on this machine.
    public bool IsLocal = false;

    void Start()
    {
        IsLocal = GetComponent<NetworkObject>().HasStateAuthority;
        if (IsLocal)
        {
            GameObject.FindAnyObjectByType<InputHandler>().LocalPlayer = this;
        }
    }
    
    public void SelectHorde(HordeController horde)
    {
        if (SelectedHorde && SelectedHorde != horde)
        {
            SelectedHorde.UnHighlight();
        }

        if (SelectedHorde != horde)
        {
            SelectedHorde = horde;
            SelectedHorde?.Highlight();
        }
    }

    public void DeselectHorde()
    {
        SelectedHorde?.UnHighlight();
        SelectedHorde = null;
    }

    public void MoveHorde(Vector2 target)
    {
        SelectedHorde?.Move(target);
    }
}
