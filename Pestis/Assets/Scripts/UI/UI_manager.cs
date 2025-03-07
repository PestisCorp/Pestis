using System.Collections;
using System.Collections.Generic;
using Horde;
using JetBrains.Annotations;
using Players;
using TMPro;
using UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;

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
    public GameObject splitPanel;
    public GameObject abilityToolbar;
    public GameObject fearAndMorale;
    public GameObject objectives;

    // References to the resource text fields
    public TextMeshProUGUI cheeseTotalText;
    public TextMeshProUGUI cheeseRateText;
    public TextMeshProUGUI popTotalText;

    public TextMeshProUGUI hordeTotalText;

    // References to the buttons that need some added function
    // Button type wouldn't show in inspector so using GameObject instead
    public GameObject moveButton;
    public GameObject moveButtonInfo;
    
    
    
    public bool moveFunctionality;

    // References to notification system objects
    public GameObject notification;
    public float displayTime = 3f;
    public float fadeDuration = 1f;
    private readonly Queue<(string, Color)> messages = new();
    private bool _messageActive;
    private Image _notificationBackground;
    private TMP_Text _notificationText;
    
    private readonly Queue<(ActiveMutation, ActiveMutation, ActiveMutation, EvolutionManager, HordeController)> _mutationQueue = new();
    
    private bool displayResourceInfo;

    // Called by EvolutionManager every time a new mutation is acquired


    // Start is called before the first frame update
    private void Start()
    {
        // Ensure appropriate canvases are set to default at the start of the game
        ResetUI();
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
        if (toolbar != null) toolbar.SetActive(false);
        if (abilityToolbar != null) abilityToolbar.SetActive(false);
        if (resourceStats != null) resourceStats.SetActive(false);
        displayResourceInfo = false;
        moveFunctionality = false;

        _notificationText = notification.GetComponentInChildren<TMP_Text>();
        _notificationBackground = notification.GetComponentInChildren<Image>();

        foreach (var button in abilityToolbar.GetComponentsInChildren<Button>())
        {
            button.enabled = false;
            button.GetComponent<Image>().enabled = false;
            var childrenWithTag = GetComponentInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
            foreach (var child in childrenWithTag)
            {
                child.GetComponent<Image>().enabled = false;
            }
        }
        
        
        
    }

    private void FixedUpdate()
    {
        //Only display resources if they player hasn't opted to show info
        if (localPlayer != null && !displayResourceInfo)
        {
            // Update the cheese text fields
            // Display total cheese up to 2 decimal places
            if (cheeseTotalText != null)
                cheeseTotalText.text = localPlayer?.player.CurrentCheese.ToString("F2");

            // Display cheese increment rate with a + sign and to 2 decimal places
            if (cheeseRateText != null)
            {
                var cheeseRate = localPlayer!.player.CheesePerSecond;
                cheeseRateText.text = cheeseRate >= 0 ? "+" + cheeseRate.ToString("F2") : cheeseRate.ToString("F2");
            }

            // Update total pop text field
            if (popTotalText != null)
                popTotalText.text = "0";

            // Update total horde text field
            if (hordeTotalText != null)
                hordeTotalText.text = "0";
        }
        if (attackPanel.activeSelf) AttackPanelRefresh();
    }

    // Function to reset all referenced canvases to their default states to prevent UI clutter
    // Not including mutation Pop Up as this is not controlled by button presses
    // Not including toolbar as this is controlled by the player selecting a horde
    public void ResetUI()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
        if (attackPanel != null) attackPanel.SetActive(false);
        if (splitPanel != null) splitPanel.SetActive(false);

        // Ignoring the state of the tool bar, ensuring the default buttons are visible
        var toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (var obj in toolbarButtons) obj.GetComponent<Image>().enabled = true;

        //Change colour to normal
        var colour = Color.white;
        moveButton.GetComponent<Image>().color = colour;
        moveButtonInfo.GetComponent<Image>().color = new Color(colour.r * 0.75f, colour.g * 0.75f, colour.b * 0.75f, 1);
    }

    // Function to enable info panel
    // and update the text fields
    public void InfoPanelEnable()
    {
        ResetUI();
        if (infoPanel != null) infoPanel.SetActive(true);

        // Find all GameObjects with the tag "UI_stats_text"
        var taggedObjects = GameObject.FindGameObjectsWithTag("UI_stats_text");

        // Loop through and find the one specific to the info panel with name "Info_own_stats"
        foreach (var obj in taggedObjects)
            if (obj.name == "Info_own_stats")
            {
                var horde = GetSelectedHorde();
                UpdateStats(obj, horde);
            }

        // Find all GameObjects with the tag "UI_mutations_text"
        var taggedObjects2 = GameObject.FindGameObjectsWithTag("UI_mutations_text");

        // Loop through and find the one specific to the info panel with name "Info_mutations"
        foreach (var obj in taggedObjects2)
            if (obj.name == "Info_mutations")
            {
                var horde = GetSelectedHorde();
                UpdateMutations(obj, horde);
            }
    }

    // Function to disable info panel
    public void InfoPanelDisable()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    //Pure toggle caused issue with reset UI order so changed to use disable and enable functions
    public void InfoPanelToggle()
    {
        if (infoPanel.activeSelf)
            InfoPanelDisable();
        else
            InfoPanelEnable();
    }

    // Function to enable attack panel
    public void AttackPanelEnable()
    {
        ResetUI();
        var fightButton = attackPanel.GetComponentInChildren<Button>();
        fightButton.onClick.RemoveAllListeners();
        if (attackPanel != null) attackPanel.SetActive(true);

        // Find all GameObjects with the tag "UI_stats_text"
        var uiStatsTextObjects = GameObject.FindGameObjectsWithTag("UI_stats_text");

        // Loop through and find the one specific to the attack panel with name "Attack_own_stats"
        var friendlyHorde = GetSelectedHorde();
        var enemyHorde = GetSelectedEnemyHorde();
        foreach (var obj in uiStatsTextObjects)
            if (obj.name == "Attack_own_stats")
            {
                UpdateStats(obj, friendlyHorde);
            }
            else if (obj.name == "Attack_enemy_stats")
            {
                UpdateStats(obj, enemyHorde);
            }

        // Find all GameObjects with the tag "Attack_slider_text"
        var attackSliderObjects = GameObject.FindGameObjectsWithTag("Attack_slider_text");

        // Loop through and find the one specific to the attack panel with name "Attack_own_stats"
        foreach (var obj in attackSliderObjects)
            if (obj.name == "Text_max_pop")
            {
                var horde = GetSelectedHorde();
                UpdateSliderMaxPop(obj, horde);
            }

        var toggles = attackPanel.GetComponentsInChildren<Toggle>();
        string combatOption = "";
        foreach (var toggle in toggles)
        {
            var toggleText = toggle.GetComponentInChildren<TextMeshProUGUI>().text.Trim('\n');
            var optionInfo = toggle.GetComponent<CombatOptionInfo>();
            if (toggle.isOn) combatOption = toggleText;
            switch (toggleText)
            {
                case "Frontal Assault":
                    optionInfo.optionText = "Consistently high damage per second, lower armor.";
                    break;
                case "Shock and Awe":
                    optionInfo.optionText = "Massively buff damage. Large decrease in armor. Return to normal stats after 10 seconds. Lower ability cooldown.";
                    break;
                case "Envelopment":
                    optionInfo.optionText = "Damage linearly scales with horde size.";
                    break;
                case "Fortify":
                    optionInfo.optionText = "Gain large armor bonuses when near POIs you own.";
                    break;
                case "Hedgehog":
                    optionInfo.optionText = "Buff armor, reduce damage. Reflect a small amount of damage received.";
                    break;
                case "All Round":
                    optionInfo.optionText = "Armor scales with number of enemies in combat.";
                    break;
            }
        }
        if (combatOption != "") fightButton.onClick.AddListener(delegate {friendlyHorde.AttackHorde(enemyHorde, combatOption);});
        
    }

    public void AttackPanelRefresh()
    {
        AttackPanelDisable();
        AttackPanelEnable();
    }

    // Function to disable attack panel
    public void AttackPanelDisable()
    {
        if (attackPanel != null) attackPanel.SetActive(false);
    }

    public void AttackPanelToggle()
    {
        if (attackPanel.activeSelf)
            AttackPanelDisable();
        else
            AttackPanelEnable();
    }

    // Function to disable horde split panel
    public void SplitPanelDisable()
    {
        if (splitPanel != null) splitPanel.SetActive(false);
    }

    // Function to enable horde split panel
    public void SplitPanelEnable()
    {
        ResetUI();
        if (splitPanel != null) splitPanel.SetActive(true);
    }

    public void SplitPanelToggle()
    {
        if (splitPanel.activeSelf)
            SplitPanelDisable();
        else
            SplitPanelEnable();
    }

    // Function to enable mutation pop-up
    public void MutationPopUpEnable()
    {
        if (mutationPopUp != null) mutationPopUp.SetActive(true);
    }

    // Function to disable mutation pop-up
    public void MutationPopUpDisable()
    {
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
    }

    // Function to enable toolbar
    public void ToolbarEnable()
    {
        ResetUI();
        if (toolbar != null) toolbar.SetActive(true);
    }

    // Function to disable toolbar
    public void ToolbarDisable()
    {
        ResetUI();
        if (toolbar != null) toolbar.SetActive(false);
    }

    // Function to enable resource stats display
    public void ResourceStatsEnable()
    {
        if (resourceStats != null) resourceStats.SetActive(true);
    }

    // Function to disable resource stats display
    public void ResourceStatsDisable()
    {
        if (resourceStats != null) resourceStats.SetActive(false);
    }

    private void ObjectiveChecklistEnable()
    {
        ResetUI();
        if (objectives != null) objectives.SetActive(true);
    }

    private void ObjectiveChecklistDisable()
    {
        ResetUI();
        if (objectives != null) objectives.SetActive(false);
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
        return inputHandler?.GetComponent<InputHandler>()?.LocalPlayer?.selectedEnemyHorde;
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
            var hordeState = horde.GetPopulationState();
            var population = horde.AliveRats.ToString();
            var attack = horde.GetPopulationState().Damage;
            var defense = hordeState.DamageReduction;
            var avgSize = "XX";
            var avgWeight = "XX";

            var stats = "Population: " + population + "\n" +
                        "Attack: " + attack + "\n" +
                        "Defense: " + defense + "\n" +
                        "Avg. Size: " + avgSize + "cm\n" +
                        "Avg. Weight: " + avgWeight + "Kg";
            statsText.GetComponentInChildren<TextMeshProUGUI>().text = stats;
        }
        else
        {
            statsText.GetComponentInChildren<TextMeshProUGUI>().text = "No Horde Selected";
        }
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
            mutationText.GetComponentInChildren<TextMeshProUGUI>().text = "No Genome found";
        else mutationText.GetComponentInChildren<TextMeshProUGUI>().text = "No Horde Selected";
    }

    // Function to update the max population text of the attack horde size slider
    // Both the text field and the slider max value will be updated
    private void UpdateSliderMaxPop(GameObject maxPopText, HordeController horde)
    {
        //Get the slider with tag "Attack_slider"
        var slider = GameObject.FindGameObjectWithTag("Attack_slider");

        if (horde != null)
        {
            var population = horde.AliveRats;

            //Change the max value of the slider to the population of the horde
            slider.GetComponent<Slider>().maxValue = population;

            maxPopText.GetComponentInChildren<TextMeshProUGUI>().text = population.ToString();
        }
        else
        {
            //Set default value to 100 if no horde is selected
            slider.GetComponent<Slider>().maxValue = 100;
            maxPopText.GetComponentInChildren<TextMeshProUGUI>().text = "100";
        }

        //Set the value of the slider to the half of the max value (rounding down for odd numbers)
        slider.GetComponent<Slider>().value = slider.GetComponent<Slider>().maxValue / 2;
        UpdateSelectedPopulation();
    }


    // Function to update the currently selected population
    public void UpdateSelectedPopulation()
    {
        //Get the slider with tag "Attack_slider"
        var slider = GameObject.FindGameObjectWithTag("Attack_slider");

        //Get the current value of the slider
        var selectedPopulation = (int)slider.GetComponent<Slider>().value;

        //Find all GameObjects with the tag "Attack_slider_text"
        var attackSliderObjects = GameObject.FindGameObjectsWithTag("Attack_slider_text");

        //Loop through and find the one specific to the attack panel with name "Attack_selected_pop"
        foreach (var obj in attackSliderObjects)
            if (obj.name == "Text_selected_pop")
                obj.GetComponentInChildren<TextMeshProUGUI>().text = selectedPopulation.ToString();
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
        var toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (var obj in toolbarButtons) obj.GetComponent<Image>().enabled = !obj.GetComponent<Image>().enabled;
    }


    // Toggles if move function is active
    public void MoveButtonFunction()
    {
        moveFunctionality = !moveFunctionality;
        ResetUI();

        if (moveFunctionality)
        {
            //Change colour to 75% darker
            var colour = moveButton.GetComponent<Image>().color;
            moveButton.GetComponent<Image>().color = new Color(colour.r * 0.75f, colour.g * 0.75f, colour.b * 0.75f, 1);
            colour = moveButtonInfo.GetComponent<Image>().color;
            moveButtonInfo.GetComponent<Image>().color =
                new Color(colour.r * 0.75f, colour.g * 0.75f, colour.b * 0.75f, 1);
        }
    }

    public void RareMutationPopup((ActiveMutation, ActiveMutation, ActiveMutation) mutations, EvolutionManager evolutionManager, HordeController horde) 
    {
        _mutationQueue.Enqueue((mutations.Item1, mutations.Item2, mutations.Item3, evolutionManager, horde));
        if (_mutationQueue.Count != 0) StartCoroutine(ShowMutationPopUp());
    }

    private IEnumerator ShowMutationPopUp()
    {
        if (_mutationQueue.Count == 0) yield break;
        while (mutationPopUp.activeSelf)
        {
            yield return null;
        }
        MutationPopUpEnable();
        var mutation = _mutationQueue.Dequeue();
        Panner panner = FindFirstObjectByType<Panner>();
        panner.target.x = mutation.Item5.GetBounds().center.x;
        panner.target.y = mutation.Item5.GetBounds().center.y;
        panner.target.z = -1;
        panner.shouldPan = true;
        var buttons = mutationPopUp.GetComponentsInChildren<Button>();
        
        buttons[0].GetComponentInChildren<TMP_Text>().text = mutation.Item1.MutationName;
        buttons[0].GetComponent<Tooltip>().tooltipText = mutation.Item1.Tooltip;
        buttons[0].onClick.RemoveAllListeners();
        buttons[0].onClick.AddListener(delegate {mutation.Item4.ApplyActiveEffects(mutation.Item1);});
        buttons[0].onClick.AddListener(delegate {Destroy(buttons[0].GetComponent<Tooltip>().tooltipInstance);});
        
        buttons[1].GetComponentInChildren<TMP_Text>().text = mutation.Item2.MutationName;
        buttons[1].GetComponent<Tooltip>().tooltipText = mutation.Item2.Tooltip;
        buttons[1].onClick.RemoveAllListeners();
        buttons[1].onClick.AddListener(delegate {mutation.Item4.ApplyActiveEffects(mutation.Item2);});
        buttons[1].onClick.AddListener(delegate {Destroy(buttons[1].GetComponent<Tooltip>().tooltipInstance);});
        
        buttons[2].GetComponentInChildren<TMP_Text>().text = mutation.Item3.MutationName;
        buttons[2].GetComponent<Tooltip>().tooltipText = mutation.Item3.Tooltip;
        buttons[2].onClick.RemoveAllListeners();
        buttons[2].onClick.AddListener(delegate {mutation.Item4.ApplyActiveEffects(mutation.Item3);});
        buttons[2].onClick.AddListener(delegate {Destroy(buttons[2].GetComponent<Tooltip>().tooltipInstance);});
        
    }
    
    public void AbilityToolbarEnable()
    {
        ResetUI();
        if (abilityToolbar != null) abilityToolbar.SetActive(true);
    }
    
    public void AbilityToolbarDisable()
    {
        ResetUI();
        foreach (var button in abilityToolbar.GetComponentsInChildren<Button>())
        {
            button.enabled = false;
            button.onClick.RemoveAllListeners();
            button.GetComponent<Image>().enabled = false;
            button.GetComponentInChildren<TextMeshProUGUI>().text = ""; 

            
            var childrenWithTag = GetComponentInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
            foreach (var child in childrenWithTag)
            {
                child.GetComponent<Image>().enabled = false; 
            }
            
            var tooltip = button.GetComponent<Tooltip>();
            if (tooltip != null)
            {
                tooltip.tooltipText = "";
                tooltip.enabled = false;
            }
        }
        if (abilityToolbar != null) abilityToolbar.SetActive(false);
    }

    public T[] GetComponentInChildrenWithTag<T, TP>(TP parent, string tagToFind) where T : Component where TP : Component
    {
        List<T> componentsInChildren = new List<T>();
        foreach (T obj in parent.GetComponentsInChildren<T>()) 
        {
            if (obj.CompareTag(tagToFind))
            {
                componentsInChildren.Add(obj);
            }
        }
        return componentsInChildren.ToArray();
    }
    
    public void RegisterAbility((string, string) mutation, AbilityController abilityController)
    {
        foreach (var button in abilityToolbar.GetComponentsInChildren<Button>(true))
        {
            if (button.enabled) continue;
            button.enabled = true;
            button.onClick.RemoveAllListeners();
            button.GetComponent<Image>().enabled = true;
            button.GetComponentInChildren<TextMeshProUGUI>().text = mutation.Item1;
            var childrenWithTag = GetComponentInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
            foreach (var child in childrenWithTag)
            {
                child.GetComponent<Image>().enabled = true;
            }
            switch (mutation.Item1)
            {
                case "Pestis":
                    button.onClick.AddListener(delegate {abilityController.UsePestis(button);});
                    button.GetComponent<Tooltip>().tooltipText = mutation.Item2;
                    break;
            }
            break;
        }

    }
    
    public void AddNotification(string message, Color hordeColor)
    {
        messages.Enqueue((message, hordeColor));
        if (!_messageActive) StartCoroutine(ShowNextMessage());
    }

    // Removes a notification after 5 seconds, during which it fades out
    private IEnumerator ShowNextMessage()
    {
        if (messages.Count == 0) yield break;
        var message = messages.Dequeue();
        _messageActive = true;
        notification.SetActive(true);
        _notificationText.color = message.Item2;
        {
            var colour = _notificationBackground.color;
            colour.a = 1.0f;
            _notificationBackground.color = colour;
        }

        _notificationText.text = message.Item1;

        yield return new WaitForSeconds(displayTime);
        var elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            _notificationText.alpha = 1 - elapsedTime / fadeDuration;
            var colour = _notificationBackground.color;
            colour.a = 1 - elapsedTime / fadeDuration;
            _notificationBackground.color = colour;
            yield return null;
        }
        
        if (messages.Count > 0)
            StartCoroutine(ShowNextMessage());
        else
            _messageActive = false;
    }
}