using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Horde;
using JetBrains.Annotations;
using Players;
using TMPro;
using UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Color = UnityEngine.Color;
using Object = UnityEngine.Object;

public class UI_Manager : MonoBehaviour
{
    public static UI_Manager Instance;
    
    // References to the game managers and objects to view in UI
    public GameObject inputHandler;
    [CanBeNull] public HumanPlayer localPlayer;

    // References to the canvas elements
    public GameObject infoPanel;
    public GameObject attackPanel;
    public GameObject mutationPopUp;
    public GameObject mutationViewer;
    public GameObject actionPanel;
    public Transform contentParent;
    public GameObject resourceStats;
    public GameObject splitPanel;
    public GameObject abilityToolbar;
    public GameObject fearAndMorale;
    public GameObject objectives;
    public TimerToScoreLock timer;
    public GameObject darkScreen;
    public GameObject textPrefab;
    public GameObject startMenu;
    
    // References to the resource text fields
    public TextMeshProUGUI cheeseTotalText;
    public TextMeshProUGUI cheeseRateText;
    public TextMeshProUGUI popTotalText;

    public TextMeshProUGUI hordeTotalText;
    
    

    // References to notification system objects
    public GameObject notification;
    public float displayTime = 3f;
    public float fadeDuration = 1f;
    private readonly Queue<(string, Color)> messages = new();
    private bool _messageActive;
    private Image _notificationBackground;
    private TMP_Text _notificationText;
    
    private bool displayResourceInfo;

    // Called by EvolutionManager every time a new mutation is acquired


    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    private void Start()
    {
        // Ensure appropriate canvases are set to default at the start of the game
        ResetUI();
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
        if (resourceStats != null) resourceStats.SetActive(false);
        if (objectives != null) objectives.SetActive(false);
        if (startMenu) objectives.SetActive(false);
        if (darkScreen != null)
        {
            darkScreen.GetComponent<Canvas>().enabled = false;
            darkScreen.SetActive(false);
        }
        
        displayResourceInfo = false;


        _notificationText = notification.GetComponentInChildren<TMP_Text>();
        _notificationBackground = notification.GetComponentInChildren<Image>();

        foreach (var button in abilityToolbar.GetComponentsInChildren<Button>())
        {
            button.enabled = false;
            button.GetComponent<Image>().enabled = false;
            button.GetComponent<Tooltip>().enabled = false;
            var childrenWithTag = GetComponentsInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
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

            if(localPlayer.player.Score != null)
            {
                timer.UpdateScore(localPlayer.player.Score);
            }

            if (localPlayer.player.Timer != null)
            {
                timer.UpdateTimer(localPlayer.player.Timer);
            }
        }

        if (infoPanel.activeSelf)
        {
            var taggedObjects = GameObject.FindGameObjectsWithTag("UI_stats_text");
            foreach (var obj in taggedObjects)
                if (obj.name == "Info_own_stats")
                {
                    var horde = GetSelectedHorde();
                    UpdateStats(obj, horde);
                }
        }

        if (actionPanel.activeSelf)
        {
            var toggles = attackPanel.GetComponentsInChildren<Toggle>();
            foreach (var toggle in toggles)
            {
                var toggleText = toggle.GetComponentInChildren<TextMeshProUGUI>().text.Trim('\n');
                if (toggle.isOn) GetSelectedHorde().SetCombatStrategy(toggleText);
            }
        }
    }

    // Function to reset all referenced canvases to their default states to prevent UI clutter
    // Not including mutation Pop Up as this is not controlled by button presses
    // Not including toolbar as this is controlled by the player selecting a horde
    public void ResetUI()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
        
        if (splitPanel != null)
        {
            splitPanel.SetActive(false);
        }

        if (mutationPopUp != null)
        {
            mutationPopUp.SetActive(false);
            mutationViewer.SetActive(false);
        }

        if (actionPanel != null)
        {
            ActionPanelDisable();
        }
        
        // Ignoring the state of the tool bar, ensuring the default buttons are visible
        var toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (var obj in toolbarButtons) obj.GetComponent<Image>().enabled = true;
        
    }

    public void ActionPanelEnable()
    {
        ResetUI();
        var horde = GetSelectedHorde();
        foreach (var mut in GetSelectedHorde().GetEvolutionState().AcquiredAbilities)
        {
            RegisterAbility(mut, horde.GetComponent<AbilityController>());
        }
        SplitPanelEnable();
        AbilityToolbarEnable();
        var toggles = attackPanel.GetComponentsInChildren<Toggle>();
        foreach (var toggle in toggles)
        {
            var toggleText = toggle.GetComponentInChildren<TextMeshProUGUI>().text.Trim('\n');
            if (toggle.isOn) GetSelectedHorde().SetCombatStrategy(toggleText);
            var tooltip = toggle.GetComponent<Tooltip>();
            switch (toggleText)
            {
                case "Frontal Assault":
                    tooltip.tooltipText = "Consistently high damage per second, lower armor.";
                    break;
                case "Shock And Awe":
                    tooltip.tooltipText = "Massively buff damage. Large decrease in armor. Lower ability cooldown.";
                    break;
                case "Envelopment":
                    tooltip.tooltipText = "Damage linearly scales with horde size.";
                    break;
               case "Fortify":
                    tooltip.tooltipText = "Gain large armor bonuses when near POIs you own.";
                    break;
                case "Hedgehog":
                    tooltip.tooltipText = "Buff armor, reduce damage. Reflect a small amount of damage received.";
                    break;
                case "All Round":
                    tooltip.tooltipText = "Armor scales with number of enemies in combat.";
                    break;
            }
        }
        if (actionPanel != null) actionPanel.SetActive(true);
    }

    public void ActionPanelDisable()
    {
        if (actionPanel != null) actionPanel.SetActive(false);
        AbilityToolbarDisable();
        SplitPanelDisable();
    }
    
    // Function to enable info panel
    // and update the text fields
    public void InfoPanelEnable()
    {
        ResetUI();
        if (infoPanel != null) 
            infoPanel.SetActive(true);

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

        var button = GameObject.FindGameObjectWithTag("viewer");
        button.GetComponent<Button>().onClick.RemoveAllListeners();
        button.GetComponent<Button>().onClick.AddListener(delegate
        {
            Camera.main.GetComponent<Panner>().PanTo(GetSelectedHorde());
        });
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
    

    // Function to disable horde split panel
    public void SplitPanelDisable()
    {
        if (splitPanel != null) splitPanel.SetActive(false);
    }

    // Function to enable horde split panel
    public void SplitPanelEnable()
    {
        if (splitPanel != null) splitPanel.SetActive(true);
    }

    public void SplitPanelToggle()
    {
        if (splitPanel.activeSelf)
            SplitPanelDisable();
        else
            SplitPanelEnable();
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

    public void ObjectiveChecklistEnable()
    {
        if (objectives != null) objectives.SetActive(true);
    }

    public void ObjectiveChecklistDisable()
    {
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
            var health = horde.TotalHealth;
            var birthRate = horde.GetPopulationState().BirthRate;

            var stats = "Population: " + population + "\n" +
                        "Attack: " + attack + "\n" +
                        "Defense: " + defense + "\n" +
                        "Health: " + health + "\n" +
                        "Birth Rate: " + birthRate;
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



    
    
    // Function to enable mutation pop-up
    public void MutationPopUpEnable()
    {
        if (mutationPopUp.activeSelf)
        {
            mutationPopUp.SetActive(false);
            return;
        }

        if (mutationViewer.activeSelf)
        {
            mutationViewer.SetActive(false);
            return;
        }
        MutationPopUpDisable();
        var horde = GetSelectedHorde();
        var evolutionManager = horde.GetComponent<EvolutionManager>();
        if (evolutionManager.PointsAvailable == 0 && mutationViewer.activeSelf == false)
        {
            mutationViewer.SetActive(true);
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }
            foreach (var mutation in evolutionManager.GetEvolutionaryState().AcquiredMutations)
            {
                GameObject textBox = Instantiate(textPrefab, contentParent);
                textBox.GetComponent<Tooltip>().tooltipText = mutation.Tooltip;
                var mutationType = GetComponentsInChildrenWithTag<Image, GameObject>(textBox, "mutation_type")[0];
                mutationType.sprite = Resources.Load<Sprite>(mutation.IsAbility ? "UI_design/Mutations/active_mutation" : "UI_design/Mutations/passive_mutation");
                var mutationUse = GetComponentsInChildrenWithTag<Image, GameObject>(textBox, "mutation_use")[0];
                var path = "UI_design/Mutations/" + mutation.MutationUse + "_mutation";
                mutationUse.sprite = Resources.Load<Sprite>(path);
                textBox.GetComponentInChildren<TMP_Text>().text = mutation.MutationName;
            }
        }
        if (evolutionManager.PointsAvailable > 0 && mutationPopUp.activeSelf == false)
        {
            if (mutationPopUp != null) mutationPopUp.SetActive(true);
            GameObject.FindGameObjectWithTag("mutation_points").GetComponent<TextMeshProUGUI>().text = evolutionManager.PointsAvailable.ToString() + "pts";
            var mutations = GetSelectedHorde().GetComponent<EvolutionManager>().RareEvolutionaryEvent();
            var buttons = GetComponentsInChildrenWithTag<Button, GameObject>(mutationPopUp, "mutation_option");
            buttons[0].GetComponentInChildren<TMP_Text>().text = mutations.Item1.MutationName;
            buttons[0].GetComponent<Tooltip>().tooltipText = mutations.Item1.Tooltip;
            GetComponentsInChildrenWithTag<Image, Button>(buttons[0], "mutation_type")[0].sprite =
                mutations.Item1.IsAbility
                    ? Resources.Load<Sprite>("UI_design/Mutations/active_mutation")
                    : Resources.Load<Sprite>("UI_design/Mutations/passive_mutation");
            GetComponentsInChildrenWithTag<Image, Button>(buttons[0], "mutation_use")[0].sprite = 
                Resources.Load<Sprite>("UI_design/Mutations/" + mutations.Item1.MutationUse + "_mutation");
            buttons[0].onClick.RemoveAllListeners();
            buttons[0].onClick.AddListener(delegate {evolutionManager.ApplyActiveEffects(mutations.Item1);});
            buttons[0].onClick.AddListener(delegate {Destroy(buttons[0].GetComponent<Tooltip>().tooltipInstance);});
        
            buttons[1].GetComponentInChildren<TMP_Text>().text = mutations.Item2.MutationName;
            buttons[1].GetComponent<Tooltip>().tooltipText = mutations.Item2.Tooltip;
            GetComponentsInChildrenWithTag<Image, Button>(buttons[1], "mutation_type")[0].sprite =
                mutations.Item2.IsAbility
                    ? Resources.Load<Sprite>("UI_design/Mutations/active_mutation")
                    : Resources.Load<Sprite>("UI_design/Mutations/passive_mutation");
            GetComponentsInChildrenWithTag<Image, Button>(buttons[1], "mutation_use")[0].sprite = 
                Resources.Load<Sprite>("UI_design/Mutations/" + mutations.Item2.MutationUse + "_mutation");
            buttons[1].onClick.RemoveAllListeners();
            buttons[1].onClick.AddListener(delegate {evolutionManager.ApplyActiveEffects(mutations.Item2);});
            buttons[1].onClick.AddListener(delegate {Destroy(buttons[1].GetComponent<Tooltip>().tooltipInstance);});
        
            buttons[2].GetComponentInChildren<TMP_Text>().text = mutations.Item3.MutationName;
            buttons[2].GetComponent<Tooltip>().tooltipText = mutations.Item3.Tooltip;
            GetComponentsInChildrenWithTag<Image, Button>(buttons[2], "mutation_type")[0].sprite =
                mutations.Item3.IsAbility
                    ? Resources.Load<Sprite>("UI_design/Mutations/active_mutation")
                    : Resources.Load<Sprite>("UI_design/Mutations/passive_mutation");
            GetComponentsInChildrenWithTag<Image, Button>(buttons[2], "mutation_use")[0].sprite = 
                Resources.Load<Sprite>("UI_design/Mutations/" + mutations.Item3.MutationUse + "_mutation");
            buttons[2].onClick.RemoveAllListeners();
            buttons[2].onClick.AddListener(delegate {evolutionManager.ApplyActiveEffects(mutations.Item3);});
            buttons[2].onClick.AddListener(delegate {Destroy(buttons[2].GetComponent<Tooltip>().tooltipInstance);});
        }
    }

    // Function to disable mutation pop-up
    public void MutationPopUpDisable()
    {
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
        if (mutationViewer != null) mutationViewer.SetActive(false);
    }
    
    
    public void AbilityToolbarEnable()
    {
        if (abilityToolbar != null) abilityToolbar.SetActive(true);
    }
    
    public void AbilityToolbarDisable()
    {
        foreach (var button in abilityToolbar.GetComponentsInChildren<Button>())
        {
            button.enabled = false;
            button.onClick.RemoveAllListeners();
            button.GetComponent<Image>().enabled = false;
            button.GetComponentInChildren<TextMeshProUGUI>().text = ""; 

            
            var childrenWithTag = GetComponentsInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
            foreach (var child in childrenWithTag)
            {
                child.GetComponent<Image>().enabled = false; 
            }
            
            var tooltip = button.GetComponent<Tooltip>();
            if (tooltip)
            {
                tooltip.tooltipText = "";
                tooltip.enabled = false;
            }
        }
        if (abilityToolbar != null) abilityToolbar.SetActive(false);
    }

    private T[] GetComponentsInChildrenWithTag<T, TP>(TP parent, string tagToFind) where T : Component where TP : Object
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
    
    private void RegisterAbility((string, string) mutation, AbilityController abilityController)
    {
        foreach (var button in abilityToolbar.GetComponentsInChildren<Button>(true))
        {
            if (button.enabled) continue;
            button.enabled = true;
            button.onClick.RemoveAllListeners();
            button.GetComponent<Image>().enabled = true;
            button.GetComponentInChildren<TextMeshProUGUI>().text = mutation.Item1;
            var childrenWithTag = GetComponentsInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
            foreach (var child in childrenWithTag)
            {
                child.GetComponent<Image>().enabled = true;
            }
            switch (mutation.Item1)
            {
                case "Pestis":
                    button.onClick.AddListener(delegate {abilityController.UsePestis(button);});
                    break;
                case "Sewer Dwellers":
                    button.onClick.AddListener(delegate {abilityController.UseSewerDwellers(button);});
                    break;
                case "Poltergeist":
                    button.onClick.AddListener(delegate {abilityController.UsePoltergeist(button);});
                    break;
                case "Apparition":
                    button.onClick.AddListener(delegate {abilityController.UseApparition(button);});
                    break;
            }
            var tooltip = button.GetComponent<Tooltip>();
            if (tooltip)
            {
                tooltip.enabled = true;
            }
            tooltip.tooltipText = mutation.Item2;
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

    public void HighlightUiElement(GameObject uiToHighlight)
    {
        uiToHighlight.GetComponent<Canvas>().sortingOrder = 2;
        darkScreen.SetActive(true);
        darkScreen.GetComponent<Canvas>().enabled = true;
    }
    
    public void UnhighlightUiElement(GameObject uiToUnhighlight)
    {
        darkScreen.GetComponent<Canvas>().enabled = false;
        darkScreen.SetActive(false);
        uiToUnhighlight.GetComponent<Canvas>().sortingOrder = 0;
    }

    public void EnableStartMenu()
    {
        startMenu.SetActive(true);
    }
}
