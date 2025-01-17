using Fusion;
using JetBrains.Annotations;
using UnityEngine;

public class HumanPlayer : MonoBehaviour
{
    [CanBeNull] private HordeController _selectedHorde = null;
    // Whether this HumanPlayer is the one being controlled by the player on this machine.
    public bool IsLocal = false;

    void Start()
    {
        IsLocal = GetComponent<NetworkObject>().HasStateAuthority;
        if (IsLocal)
        {
            GameObject.FindAnyObjectByType<MapClickHandler>().LocalPlayer = this;
        }
    }
    
    public void SelectHorde(HordeController horde)
    {
        if (_selectedHorde && _selectedHorde != horde)
        {
            _selectedHorde.UnHighlight();
        }

        if (_selectedHorde != horde)
        {
            _selectedHorde = horde;
            _selectedHorde?.Highlight();
        }
    }

    public void DeselectHorde()
    {
        _selectedHorde?.UnHighlight();
        _selectedHorde = null;
    }

    public void MoveHorde(Vector2 target)
    {
        _selectedHorde?.Move(target);
    }
}
