using Fusion;
using Horde;
using JetBrains.Annotations;
using UnityEngine;

namespace Players
{
    public class HumanPlayer : MonoBehaviour
    {
        [CanBeNull] public HordeController selectedHorde;
        [CanBeNull] public HordeController selectedEnemyHorde;

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
            }
        }

        public void SelectHorde(HordeController horde)
        {
            
            if (horde.Player.IsLocal)
            {
                if (horde.isApparition) return;
                if (selectedHorde && selectedHorde != horde)
                {
                    selectedHorde.UnHighlight();
                    selectedHorde.moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 0;
                    
                }
                if (selectedHorde != horde)
                {
                    selectedHorde = horde;
                    selectedHorde?.Highlight();
                    if (selectedHorde.Player.IsLocal)
                    {
                        foreach (var mut in selectedHorde.GetComponent<EvolutionManager>().GetEvolutionaryState().AcquiredAbilities)
                        {
                            UI_manager.RegisterAbility(mut, selectedHorde.GetComponent<AbilityController>());
                        }

                        if (selectedHorde.InCombat) selectedHorde.moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 1;
                        UI_manager.InfoPanelToggle();
                        UI_manager.AbilityToolbarEnable();
                    }
                     
                }
            }
            else
            {
                if (selectedEnemyHorde && selectedEnemyHorde != horde)
                {
                    selectedEnemyHorde.UnHighlight();
                }

                if (selectedEnemyHorde != horde)
                {
                    selectedEnemyHorde = horde;
                    selectedEnemyHorde?.Highlight();
                }
            }
        }

        public void DeselectHorde()
        {
            selectedHorde?.UnHighlight();
            selectedEnemyHorde?.UnHighlight();
            if (selectedHorde) selectedHorde.moraleAndFearInstance.GetComponent<CanvasGroup>().alpha = 0;
            selectedHorde = null;
            selectedEnemyHorde = null;
            UI_manager.InfoPanelToggle();
            UI_manager.AbilityToolbarDisable();
        }

        public void MoveHorde(Vector2 target)
        {
            selectedHorde?.Move(target);
        }
    }
}