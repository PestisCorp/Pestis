using UnityEngine;

public class UI_Manager : MonoBehaviour
{
    // References to the canvas elements
    public GameObject infoPanel;
    public GameObject mutationPopUp;

    // Start is called before the first frame update
    void Start()
    {
        // Ensure appropriate canvases are set to default at the start of the game
        ResetUI();
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
    }
    
    // Function to reset all referenced canvases to their default states to prevent UI clutter
    // Not including mutation Pop Up as this is not controlled by button presses
    public void ResetUI()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    // Function to enable info panel
    public void EnableInfoPanel()
    {
        ResetUI();
        if (infoPanel != null) infoPanel.SetActive(true);
    }

    // Function to disable info panel
    public void DisableInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    // Function to enable mutation pop-up
    public void EnableMutationPopUp()
    {
        if (mutationPopUp != null) mutationPopUp.SetActive(true);
    }

    // Function to disable mutation pop-up
    public void DisableMutationPopUp()
    {
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
    }
}
