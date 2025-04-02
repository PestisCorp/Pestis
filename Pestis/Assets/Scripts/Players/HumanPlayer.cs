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
                // Enable objective checklist upon loading in
                UI_manager.ObjectiveChecklistEnable();
                UI_manager.timer.parent.SetActive(true);
                UI_manager.hordesListPanel.SetActive(true);
                UI_manager.HordesListRefresh();
                UI_manager.timer.parent.SetActive(true);
                StartCoroutine(UI_manager.showReset());
            }
        }

        public void SelectHorde(HordeController horde)
        {
            if (!horde.player.IsLocal) return;
            if (horde.isApparition) return;
            if (selectedHorde && selectedHorde != horde) selectedHorde.UnHighlight();
            if (selectedHorde == horde) return;
            selectedHorde = horde;
            selectedHorde?.Highlight();
            if (selectedHorde.player.IsLocal)
            {
                UI_manager.InfoPanelEnable();
            }
        }

        public void DeselectHorde()
        {
            selectedHorde?.UnHighlight();
            selectedHorde = null;
            UI_manager.InfoPanelDisable();
            UI_manager.MutationPopUpDisable();
            UI_manager.MutationViewerDisable();
            UI_manager.ActionPanelDisable();
        }

        public void MoveHorde(Vector2 target)
        {
            selectedHorde?.Move(target);
        }
    }
}