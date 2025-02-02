using Horde;
using JetBrains.Annotations;
using Players;
using UnityEngine;
using UnityEngine.UIElements;

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
    public GameObject resourceStats;
    
    // References to the resource text fields
    public TMPro.TextMeshProUGUI cheeseTotalText;
    public TMPro.TextMeshProUGUI cheeseRateText;
    public TMPro.TextMeshProUGUI popTotalText;
    public TMPro.TextMeshProUGUI hordeTotalText;
    
    private bool displayResourceInfo;
    

    // Start is called before the first frame update
    void Start()
    {
        // Ensure appropriate canvases are set to default at the start of the game
        ResetUI();
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
        if (toolbar != null) toolbar.SetActive(false);
        if (resourceStats != null) resourceStats.SetActive(false);
        displayResourceInfo = false;
    }
    
    // Update is called once per frame
    void Update()
    {
        //Only display resources if they player hasn't opted to show info
        if (localPlayer != null && !displayResourceInfo )
        {
            // Update the cheese text fields
            // Display total cheese up to 2 decimal places
            if (cheeseTotalText != null)
                cheeseTotalText.text = localPlayer?.player.CurrentCheese.ToString("F2");
            
            // Display cheese increment rate with a + sign and to 2 decimal places
            if (cheeseRateText != null)
                cheeseRateText.text = "+" + localPlayer?.player.CheeseIncrementRate.ToString("F2");

            // Update total pop text field
            if (popTotalText != null)
                popTotalText.text = "0";
            
            // Update total horde text field
            if (hordeTotalText != null)
                hordeTotalText.text = "0";
        }
    }

    // Function to reset all referenced canvases to their default states to prevent UI clutter
    // Not including mutation Pop Up as this is not controlled by button presses
    // Not including toolbar as this is controlled by the player selecting a horde
    public void ResetUI()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (attackPanel != null) attackPanel.SetActive(false);
        
        // Ignoring the state of the tool bar, ensuring the default buttons are visible
        GameObject[] toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (GameObject obj in toolbarButtons)
        {
            obj.GetComponent<UnityEngine.UI.Image>().enabled = true;

        }
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
    
    // Function to enable resource stats display
    public void EnableResourceStats()
    {
        if (resourceStats != null) resourceStats.SetActive(true);
    }
    
    // Function to disable resource stats display
    public void DisableResourceStats()
    {
        if (resourceStats != null) resourceStats.SetActive(false);
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
    
    // Function to toggle resource info display boolean
    public void ToggleResourceInfoDisplay()
    {
        displayResourceInfo = !displayResourceInfo;
    }
    
    // Function to change resource text fields to display info about what they show
    public void ResourceInfoDisplay()
    {
        if (displayResourceInfo) 
        {
            cheeseTotalText.text = "Total Cheese";
            cheeseRateText.text = "Cheese Increment Rate";
            popTotalText.text = "Total Population";
            hordeTotalText.text = "Total Hordes";
        }
    }
    
    // Function to toggle toolbar to display info or buttons by toggling the buttons tagged "UI_button_action"
    // Allowing the hidden button_info's to be seen instead
    public void ToolbarInfoDisplay()
    {
        // Inactive game objects can't be found so instead just disable their image component
        GameObject[] toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (GameObject obj in toolbarButtons)
        {
            obj.GetComponent<UnityEngine.UI.Image>().enabled = !obj.GetComponent<UnityEngine.UI.Image>().enabled;
            
        }
    }
}
    
    
