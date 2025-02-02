using Horde;
using JetBrains.Annotations;
using Players;
using UnityEngine;

public class UI_Manager : MonoBehaviour
{
    // References to the game managers and objects to view in UI
    public GameObject inputHandler;
    [CanBeNull] public HumanPlayer localPlayer;

    // References to the canvas elements
    public GameObject infoPanel;
    public GameObject attackPanel;
    public GameObject mutationPopUp;
    public GameObject toolbar;

    // Start is called before the first frame update
    void Start()
    {
        // Ensure appropriate canvases are set to default at the start of the game
        ResetUI();
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
        if (toolbar != null) toolbar.SetActive(false);
    }
    
    // Update is called once per frame

    // Function to reset all referenced canvases to their default states to prevent UI clutter
    // Not including mutation Pop Up as this is not controlled by button presses
    public void ResetUI()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (attackPanel != null) attackPanel.SetActive(false);
    }

    // Function to enable info panel
    // and update the text fields
    public void EnableInfoPanel()
    {
        ResetUI();
        if (infoPanel != null) infoPanel.SetActive(true);

        // Find all GameObjects with the tag "UI_stats_text"
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("UI_stats_text");

        // Loop through and find the one specific to the info panel with name "Info_own_stats"
        foreach (GameObject obj in taggedObjects)
        {
            if (obj.name == "Info_own_stats")
            {
                HordeController horde = GetSelectedHorde();
                UpdateStats(obj, horde);
            }
        }

        // Find all GameObjects with the tag "UI_mutations_text"
        GameObject[] taggedObjects2 = GameObject.FindGameObjectsWithTag("UI_mutations_text");

        // Loop through and find the one specific to the info panel with name "Info_mutations"
        foreach (GameObject obj in taggedObjects2)
        {
            if (obj.name == "Info_mutations")
            {
                HordeController horde = GetSelectedHorde();
                UpdateMutations(obj, horde);
            }
        }
    }

    // Function to disable info panel
    public void DisableInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    // Function to enable attack panel
    public void EnableAttackPanel()
    {
        ResetUI();
        if (attackPanel != null) attackPanel.SetActive(true);

        // Find all GameObjects with the tag "UI_stats_text"
        GameObject[] uiStatsTextObjects = GameObject.FindGameObjectsWithTag("UI_stats_text");

        // Loop through and find the one specific to the attack panel with name "Attack_own_stats"
        foreach (GameObject obj in uiStatsTextObjects)
        {
            if (obj.name == "Attack_own_stats")
            {
                HordeController horde = GetSelectedHorde();
                UpdateStats(obj, horde);
            }
            else if (obj.name == "Attack_enemy_stats")
            {
                HordeController horde = GetSelectedEnemyHorde();
                UpdateStats(obj, horde);
            }
        }
        
        // Find all GameObjects with the tag "Attack_slider_text
        GameObject[] attackSliderObjects = GameObject.FindGameObjectsWithTag("Attack_slider_text");

        // Loop through and find the one specific to the attack panel with name "Attack_own_stats"
        foreach (GameObject obj in attackSliderObjects)
        {
            if (obj.name == "Text_max_pop")
            {
                HordeController horde = GetSelectedHorde();
                UpdateSliderMaxPop(obj, horde);
            }
        }
    }

    // Function to disable attack panel
    public void DisableAttackPanel()
    {
        if (attackPanel != null) attackPanel.SetActive(false);
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

    // Function to enable toolbar
    public void EnableToolbar()
    {
        ResetUI();
        if (toolbar != null) toolbar.SetActive(true);
    }

    // Function to disable toolbar
    public void DisableToolbar()
    {
        ResetUI();
        if (toolbar != null) toolbar.SetActive(false);
    }

    //To display the correct UI the LocalPlayer will be monitored and when they select a horde the toolbar will be displayed
    //The selected horde game object will also be acquired so the horde statistics can be retrieved and displayed

    // Function to retrieve the selected horde and return it
    private HordeController GetSelectedHorde()
    {
        return inputHandler?.GetComponent<InputHandler>()?.LocalPlayer?.selectedHorde;
    }

    // Function to retrieve the selected enemy horde and return it
    private HordeController GetSelectedEnemyHorde()
    {
        return null;
    }

    // Function to update the stats text field of the passed game object
    // Using the template:
    // • Population: XX
    // • Attack: XX
    // • Defense: XX
    // • Avg. Size: XXcm
    // • Avg. Weight: XXKg
    private void UpdateStats(GameObject statsText, HordeController horde)
    {
        if (horde != null)
        {
            // Create string variables for the stats, with XX as default if no value is present
            string population = horde.AliveRats.ToString();
            string attack = "XX";
            string defense = "XX";
            string avgSize = "XX";
            string avgWeight = "XX";

            string stats = "Population: " + population + "\n" +
                           "Attack: " + attack + "\n" +
                           "Defense: " + defense + "\n" +
                           "Avg. Size: " + avgSize + "cm\n" +
                           "Avg. Weight: " + avgWeight + "Kg";
            statsText.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = stats;
        }
        else statsText.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "No Horde Selected";
    }

    // Function to update the mutation text field of the info panel
    // Using the template:
    //Genome XX: 
    //   Mutation XX - Description
    //   Mutation XX - Description
    // TODO: Implement this function
    private void UpdateMutations(GameObject mutationText, HordeController horde)
    {
        if (horde != null)
        {
            mutationText.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "No Genome found";
        }
        else mutationText.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "No Horde Selected";
    }

    // Function to update the max population text of the attack horde size slider
    // Both the text field and the slider max value will be updated
    private void UpdateSliderMaxPop(GameObject maxPopText, HordeController horde)
    {
        //Get the slider with tag "Attack_slider"
        GameObject slider = GameObject.FindGameObjectWithTag("Attack_slider");
        
        if (horde != null)
        {
            int population = horde.AliveRats;
            
            //Change the max value of the slider to the population of the horde
            slider.GetComponent<UnityEngine.UI.Slider>().maxValue = population;
            
            maxPopText.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = population.ToString();
        }
        else
        {
            //Set default value to 100 if no horde is selected
            slider.GetComponent<UnityEngine.UI.Slider>().maxValue = 100;
            maxPopText.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "100";
        }
        
        //Set the value of the slider to the half of the max value (rounding down for odd numbers)
        slider.GetComponent<UnityEngine.UI.Slider>().value = slider.GetComponent<UnityEngine.UI.Slider>().maxValue / 2;
        UpdateSelectedPopulation();

    }
    
    
    // Function to update the currently selected population
    public void UpdateSelectedPopulation()
    {
        
        //Get the slider with tag "Attack_slider"
        GameObject slider = GameObject.FindGameObjectWithTag("Attack_slider");
        
        //Get the current value of the slider
        int selectedPopulation = (int)slider.GetComponent<UnityEngine.UI.Slider>().value;
        
        //Find all GameObjects with the tag "Attack_slider_text"
        GameObject[] attackSliderObjects = GameObject.FindGameObjectsWithTag("Attack_slider_text");
        
        //Loop through and find the one specific to the attack panel with name "Attack_selected_pop"
        foreach (GameObject obj in attackSliderObjects)
        {
            if (obj.name == "Text_selected_pop")
            {
                obj.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = selectedPopulation.ToString();
            }
        }
    }
}
    
    
