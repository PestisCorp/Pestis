using Fusion;
using Horde;
using JetBrains.Annotations;
using UnityEngine;

namespace Players
{
    public class HumanPlayer : MonoBehaviour
    {
        [CanBeNull] public HordeController selectedHorde;

        // Whether this HumanPlayer is the one being controlled by the player on this machine.
        public bool IsLocal;

        public Player player;

        private UI_Manager UI_manager;

        private void Awake()
        {
            UI_manager = FindAnyObjectByType<UI_Manager>();
        }

        private void Start()
        {
            IsLocal = GetComponent<NetworkObject>().HasStateAuthority;
            if (IsLocal)
            {
                FindAnyObjectByType<InputHandler>().LocalPlayer = this;
                UI_manager.localPlayer = this;

                //Enable resource stats upon loading in
                UI_manager.ResourceStatsEnable();
            }
        }

        public void SelectHorde(HordeController horde)
        {
            if (selectedHorde && selectedHorde != horde)
            {
                selectedHorde.UnHighlight();
                UI_manager.ToolbarDisable();
            }

            if (selectedHorde != horde)
            {
                selectedHorde = horde;
                selectedHorde?.Highlight();
                if (selectedHorde.Player.IsLocal)
                    UI_manager.ToolbarEnable();
            }
        }

        public void DeselectHorde()
        {
            selectedHorde?.UnHighlight();
            selectedHorde = null;
            UI_manager.ToolbarDisable();
        }

        public void MoveHorde(Vector2 target)
        {
            selectedHorde?.Move(target);
        }
    }
}