using Fusion;
using Horde;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Players
{
    public class HumanPlayer : MonoBehaviour
    {
        [CanBeNull] public HordeController selectedHorde = null;
        // Whether this HumanPlayer is the one being controlled by the player on this machine.
        public bool IsLocal = false;

        public Player player;

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
            if (selectedHorde && selectedHorde != horde)
            {
                selectedHorde.UnHighlight();
            }

            if (selectedHorde != horde)
            {
                selectedHorde = horde;
                selectedHorde?.Highlight();
            }
        }

        public void DeselectHorde()
        {
            selectedHorde?.UnHighlight();
            selectedHorde = null;
        }

        public void MoveHorde(Vector2 target)
        {
            selectedHorde?.Move(target);
        }
    }
}
