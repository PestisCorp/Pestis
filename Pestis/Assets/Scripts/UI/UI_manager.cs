using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Horde;
using JetBrains.Annotations;
using Players;
using TMPro;
using UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Color = UnityEngine.Color;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;
using Timer = Fusion.Timer;
using Toggle = UnityEngine.UI.Toggle;


public class UI_Manager : MonoBehaviour
{
    public static UI_Manager Instance;

    // References to the game managers and objects to view in UI
    public GameObject inputHandler;
    [CanBeNull] public HumanPlayer localPlayer;

    public GameObject[] destroy;
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
    public GameObject objectives;
    public TimerToScoreLock timer;
    public GameObject darkScreen;
    public GameObject textPrefab;
    public GameObject startMenu;
    public GameObject tutorialButton;
    public GameObject hordesListPanel;
    public Transform hordesListContentParent;
    public GameObject hordeButtonPrefab;
    public GameObject mugshotImage;
    public GameObject mutationButtonOne;
    public GameObject mutationButtonTwo;
    public GameObject mutationButtonThree;
    public GameObject noPointsWarning;

    public Transform abilityPanel;

    // References to the resource text fields
    public TextMeshProUGUI cheeseRateText;
    [SerializeField] private Image cheeseRateBackground;

    // References to notification system objects
    public GameObject notification;
    public float displayTime = 3f;
    public float fadeDuration = 1f;

    public readonly Dictionary<HordeController, GameObject> AbilityBars = new();
    private readonly Queue<(string, Color)> messages = new();
    private bool _messageActive;
    private Image _notificationBackground;
    private TMP_Text _notificationText;

    private Timer _refreshClock;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    private void Start()
    {
        //if (timer.resetButton != null) { timer.resetButton.onClick.AddListener(() => TimerToScoreLock.reset( runner, InputHandler.Instance.LocalPlayer.player.Username)); }
        if (timer.resetButton != null) { timer.resetButton.onClick.AddListener(() => TimerToScoreLock.reset(destroy)); }
        // Ensure appropriate canvases are set to default at the start of the game
        ResetUI();
        if (mutationPopUp) mutationPopUp.SetActive(false);
        if (resourceStats) resourceStats.SetActive(false);
        if (objectives) objectives.SetActive(false);
        if (startMenu) objectives.SetActive(false);
        if (tutorialButton) tutorialButton.SetActive(false);
        if (timer.parent) timer.parent.SetActive(false);
        if (hordesListPanel) hordesListPanel.SetActive(false);
        if (timer.parent) timer.parent.SetActive(false);
        _notificationText = notification.GetComponentInChildren<TMP_Text>();
        _notificationBackground = notification.GetComponentInChildren<Image>();
    }

    private void FixedUpdate()
    {
        if (localPlayer)
        {
            // Display cheese increment rate with a + sign and to 2 decimal places
            if (cheeseRateText)
            {
                var cheeseRate = localPlayer!.player.CheesePerSecond;

                switch (cheeseRate)
                {
                    case <= 0 when localPlayer.player.CurrentCheese >
                                   localPlayer.player.aliveRats * localPlayer.player.cheeseConsumptionRate * 2:
                        cheeseRateText.text = "> Eating reserves <\n" +
                                              "> Capture bases <";
                        cheeseRateBackground.color = new Color(0.9f, 0.6f, 0.0f);
                        break;
                    case > 0:
                        cheeseRateText.text = "> Stockpiling <\n" +
                                              "> Rats in heat <";
                        cheeseRateBackground.color = new Color(0, 0.65f, 0);
                        break;
                    default:
                        cheeseRateText.text = "> Rats starving <\n" +
                                              "> Capture bases! <";
                        cheeseRateBackground.color = new Color(0.65f, 0.0f, 0.0f);
                        break;
                }
            }

            timer.UpdateScore(localPlayer.player.Score);

            timer.UpdateTimer(localPlayer.player.Timer);
        }

        if (_refreshClock.ElapsedInSeconds > 3)
        {
            HordesListRefresh();
            var taggedObjects = GameObject.FindGameObjectsWithTag("UI_stats_text");
            if (infoPanel.activeSelf)
                foreach (var obj in taggedObjects)
                    if (obj.name == "Info_own_stats")
                    {
                        var horde = GetSelectedHorde();
                        UpdateStats(obj, horde);
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
    }

    //automatically shows reset once timer is 0

    public IEnumerator showReset()
    {
        timer.resetButtonUI.SetActive(false);
        Debug.Log("set false");

        //yield return new WaitUntil(() => localPlayer != null && localPlayer.player != null);
        Debug.Log("Player assigned");
        yield return new WaitUntil(() => InputHandler.Instance.LocalPlayer && InputHandler.Instance.LocalPlayer.player.TimeUp);
        Debug.Log("set true");
        timer.resetButtonUI.SetActive(true);
        yield return null;
    }

    // Function to reset all referenced canvases to their default states to prevent UI clutter
    // Not including mutation Pop Up as this is not controlled by button presses
    // Not including toolbar as this is controlled by the player selecting a horde
    public void ResetUI()
    {
        if (infoPanel) infoPanel.SetActive(false);

        if (splitPanel) splitPanel.SetActive(false);

        if (mutationPopUp) MutationPopUpDisable();

        if (mutationViewer) MutationViewerDisable();

        if (actionPanel) ActionPanelDisable();
       

        // Ignoring the state of the tool bar, ensuring the default buttons are visible
        var toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (var obj in toolbarButtons) obj.GetComponent<Image>().enabled = true;
    }

    public void HordesListRefresh()
    {
        foreach (Transform child in hordesListContentParent) Destroy(child.gameObject);
        foreach (var horde in localPlayer!.player.Hordes)
        {
            if (horde.isApparition) continue;
            var hordeButton = Instantiate(hordeButtonPrefab, hordesListContentParent.transform);
            var textBoxes = hordeButton.GetComponentsInChildren<TextMeshProUGUI>();
            textBoxes[0].text = horde.AliveRats.ToString();
            textBoxes[1].text = horde.GetComponent<EvolutionManager>().PointsAvailable.ToString() + " pts";
            var button = hordeButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(delegate { Camera.main.GetComponent<Panner>().PanTo(horde); });
            button.onClick.AddListener(delegate { localPlayer.SelectHorde(horde); });
            var image = GetComponentsInChildrenWithTag<Image, GameObject>(hordeButton, "rat_img")[0];
            image.sprite = horde.Boids.GetSpriteFromMat();
        }

        _refreshClock.Restart();
    }

    public void ActionPanelEnable()
    {
        ResetUI();
        var horde = GetSelectedHorde();
        foreach (var mut in GetSelectedHorde().GetEvolutionState().AcquiredAbilities)
            RegisterAbility(mut, horde.GetComponent<AbilityController>());
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

        if (actionPanel) actionPanel.SetActive(true);
    }

    public void ActionPanelDisable()
    {
        if (actionPanel) actionPanel.SetActive(false);
        if (AbilityBars.Count > 0) AbilityToolbarDisable();
        SplitPanelDisable();
    }

    // Function to enable info panel
    // and update the text fields
    public void InfoPanelEnable()
    {
        ResetUI();
        if (infoPanel)
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

        mugshotImage.GetComponent<Image>().sprite = GetSelectedHorde().Boids.GetSpriteFromMat();

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
        if (infoPanel) infoPanel.SetActive(false);
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
        if (splitPanel) splitPanel.SetActive(false);
    }

    // Function to enable horde split panel
    public void SplitPanelEnable()
    {
        if (splitPanel) splitPanel.SetActive(true);
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
    
    // Function to update the stats text field of the passed game object
    // Using the template:
    // • Population: XX
    // • Attack: XX
    // • Defense: XX
    // • Avg. Size: XXcm
    // • Avg. Weight: XXKg
    private void UpdateStats(GameObject statsText, HordeController horde)
    {
        if (horde)
        {
            // Create string variables for the stats, with XX as default if no value is present
            var hordeState = horde.GetPopulationState();
            var population = horde.AliveRats.ToString();
            var attack = horde.GetPopulationState().Damage.ToString("F2");
            var defense = (1 / hordeState.DamageReduction).ToString("F2");
            var health = horde.TotalHealth.ToString("N0");

            var stats = population + "\n" +
                        attack + "\n" +
                        defense + "\n" +
                        health;
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

    // Function to toggle toolbar to display info or buttons by toggling the buttons tagged "UI_button_action"
    // Allowing the hidden button_info's to be seen instead
    public void ToolbarInfoDisplay()
    {
        // Inactive game objects can't be found so instead just disable their image component
        var toolbarButtons = GameObject.FindGameObjectsWithTag("UI_button_action");
        foreach (var obj in toolbarButtons) obj.GetComponent<Image>().enabled = !obj.GetComponent<Image>().enabled;
    }

    public void MutationViewerEnable()
    {
        ResetUI();
        mutationViewer.SetActive(true);
        var horde = GetSelectedHorde();
        var evolutionManager = horde.GetComponent<EvolutionManager>();
        mutationViewer.SetActive(true);
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        foreach (var mutation in evolutionManager.GetEvolutionaryState().AcquiredMutations)
        {
            var textBox = Instantiate(textPrefab, contentParent);
            textBox.GetComponent<Tooltip>().tooltipText = mutation.Tooltip;
            var mutationType = GetComponentsInChildrenWithTag<Image, GameObject>(textBox, "mutation_type")[0];
            mutationType.sprite = Resources.Load<Sprite>(mutation.IsAbility
                ? "UI_design/Mutations/active_mutation"
                : "UI_design/Mutations/passive_mutation");
            var mutationUse = GetComponentsInChildrenWithTag<Image, GameObject>(textBox, "mutation_use")[0];
            var path = "UI_design/Mutations/" + mutation.MutationUse + "_mutation";
            mutationUse.sprite = Resources.Load<Sprite>(path);
            textBox.GetComponentInChildren<TMP_Text>().text = mutation.MutationName;
        }
    }

    // Function to enable mutation pop-up
    public void MutationPopUpEnable()
    {
        ResetUI();
        if (mutationPopUp != null) mutationPopUp.SetActive(true);
        var evolutionManager = GetSelectedHorde().GetComponent<EvolutionManager>();
        GameObject.FindGameObjectWithTag("mutation_points").GetComponent<TextMeshProUGUI>().text =
            evolutionManager.PointsAvailable + "pts";
        var buttons = new[] { mutationButtonOne, mutationButtonTwo, mutationButtonThree };
        if (evolutionManager.PointsAvailable == 0)
        {
            buttons[0].SetActive(false);
            buttons[1].SetActive(false);
            buttons[2].SetActive(false);
            noPointsWarning.SetActive(true);
        }
        else
        {
            buttons[0].SetActive(true);
            buttons[1].SetActive(true);
            buttons[2].SetActive(true);
            if (GameObject.FindGameObjectWithTag("no_points"))
                GameObject.FindGameObjectWithTag("no_points").SetActive(false);
            var mutations = GetSelectedHorde().GetComponent<EvolutionManager>().RareEvolutionaryEvent();
            buttons[0].GetComponentInChildren<TMP_Text>().text = mutations.Item1.MutationName;
            buttons[0].GetComponent<Tooltip>().tooltipText = mutations.Item1.Tooltip;
            GetComponentsInChildrenWithTag<Image, Button>(buttons[0].GetComponent<Button>(), "mutation_type")[0]
                    .sprite =
                mutations.Item1.IsAbility
                    ? Resources.Load<Sprite>("UI_design/Mutations/active_mutation")
                    : Resources.Load<Sprite>("UI_design/Mutations/passive_mutation");
            GetComponentsInChildrenWithTag<Image, Button>(buttons[0].GetComponent<Button>(), "mutation_use")[0].sprite =
                Resources.Load<Sprite>("UI_design/Mutations/" + mutations.Item1.MutationUse + "_mutation");
            buttons[0].GetComponent<Button>().onClick.RemoveAllListeners();
            buttons[0].GetComponent<Button>().onClick.AddListener(delegate
            {
                evolutionManager.ApplyActiveEffects(mutations.Item1);
            });
            buttons[0].GetComponent<Button>().onClick.AddListener(delegate
            {
                Destroy(buttons[0].GetComponent<Tooltip>().tooltipInstance);
            });

            buttons[1].GetComponentInChildren<TMP_Text>().text = mutations.Item2.MutationName;
            buttons[1].GetComponent<Tooltip>().tooltipText = mutations.Item2.Tooltip;
            GetComponentsInChildrenWithTag<Image, Button>(buttons[1].GetComponent<Button>(), "mutation_type")[0]
                .sprite = mutations.Item2.IsAbility
                ? Resources.Load<Sprite>("UI_design/Mutations/active_mutation")
                : Resources.Load<Sprite>("UI_design/Mutations/passive_mutation");
            GetComponentsInChildrenWithTag<Image, Button>(buttons[1].GetComponent<Button>(), "mutation_use")[0].sprite =
                Resources.Load<Sprite>("UI_design/Mutations/" + mutations.Item2.MutationUse + "_mutation");
            buttons[1].GetComponent<Button>().onClick.RemoveAllListeners();
            buttons[1].GetComponent<Button>().onClick.AddListener(delegate
            {
                evolutionManager.ApplyActiveEffects(mutations.Item2);
            });
            buttons[1].GetComponent<Button>().onClick.AddListener(delegate
            {
                Destroy(buttons[1].GetComponent<Tooltip>().tooltipInstance);
            });

            buttons[2].GetComponentInChildren<TMP_Text>().text = mutations.Item3.MutationName;
            buttons[2].GetComponent<Tooltip>().tooltipText = mutations.Item3.Tooltip;
            GetComponentsInChildrenWithTag<Image, Button>(buttons[2].GetComponent<Button>(), "mutation_type")[0]
                .sprite = mutations.Item3.IsAbility
                ? Resources.Load<Sprite>("UI_design/Mutations/active_mutation")
                : Resources.Load<Sprite>("UI_design/Mutations/passive_mutation");
            GetComponentsInChildrenWithTag<Image, Button>(buttons[2].GetComponent<Button>(), "mutation_use")[0].sprite =
                Resources.Load<Sprite>("UI_design/Mutations/" + mutations.Item3.MutationUse + "_mutation");
            buttons[2].GetComponent<Button>().onClick.RemoveAllListeners();
            buttons[2].GetComponent<Button>().onClick.AddListener(delegate
            {
                evolutionManager.ApplyActiveEffects(mutations.Item3);
            });
            buttons[2].GetComponent<Button>().onClick.AddListener(delegate
            {
                Destroy(buttons[2].GetComponent<Tooltip>().tooltipInstance);
            });
        }
    }

    // Function to disable mutation pop-up
    public void MutationPopUpDisable()
    {
        if (mutationPopUp != null) mutationPopUp.SetActive(false);
    }

    public void MutationViewerDisable()
    {
        if (mutationViewer) mutationViewer.SetActive(false);
    }


    public void AbilityToolbarEnable()
    {
        var horde = GetSelectedHorde();
        if (AbilityBars[horde]) AbilityBars[horde].SetActive(true);
    }

    public void AbilityToolbarDisable()
    {
        foreach (var abilityBar in AbilityBars.Values)
        {
            foreach (var button in abilityBar.GetComponentsInChildren<Button>())
            {
                button.enabled = false;
                button.onClick.RemoveAllListeners();
                button.GetComponent<Image>().enabled = false;
                button.GetComponentInChildren<TextMeshProUGUI>().text = "";


                var childrenWithTag = GetComponentsInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
                foreach (var child in childrenWithTag) child.GetComponent<Image>().enabled = false;

                var tooltip = button.GetComponent<Tooltip>();
                if (tooltip)
                {
                    tooltip.tooltipText = "";
                    tooltip.enabled = false;
                }
            }

            if (abilityBar) abilityBar.SetActive(false);
        }
    }

    public T[] GetComponentsInChildrenWithTag<T, TP>(TP parent, string tagToFind) where T : Component where TP : Object
    {
        var componentsInChildren = new List<T>();
        foreach (var obj in parent.GetComponentsInChildren<T>())
            if (obj.CompareTag(tagToFind))
                componentsInChildren.Add(obj);

        return componentsInChildren.ToArray();
    }

    private void RegisterAbility((string, string) mutation, AbilityController abilityController)
    {
        var horde = GetSelectedHorde();
        var abilityBar = AbilityBars[horde];
        foreach (var button in abilityBar.GetComponentsInChildren<Button>(true))
        {
            if (button.enabled) continue;
            button.enabled = true;
            button.onClick.RemoveAllListeners();
            var btnImage = button.GetComponent<Image>();
            btnImage.enabled = true;
            var childrenWithTag = GetComponentsInChildrenWithTag<Image, Button>(button, "UI_cooldown_bar");
            foreach (var child in childrenWithTag) child.GetComponent<Image>().enabled = true;
            Enum.TryParse(mutation.Item1.Replace(" ", ""), out Abilities ability);
            switch (ability)
            {
                case Abilities.Pestis:
                    button.onClick.AddListener(delegate { abilityController.UsePestis(button); });
                    btnImage.sprite = Resources.Load<Sprite>("UI_design/Mutations/pestis_btn");
                    break;
                case Abilities.SewerDwellers:
                    button.onClick.AddListener(delegate { abilityController.UseSewerDwellers(button); });
                    btnImage.sprite = Resources.Load<Sprite>("UI_design/Mutations/sewer_dwellers_btn");
                    break;
                case Abilities.Poltergeist:
                    button.onClick.AddListener(delegate { abilityController.UsePoltergeist(button); });
                    btnImage.sprite = Resources.Load<Sprite>("UI_design/Mutations/poltergeist_btn");
                    break;
                case Abilities.Apparition:
                    button.onClick.AddListener(delegate { abilityController.UseApparition(button); });
                    btnImage.sprite = Resources.Load<Sprite>("UI_design/Mutations/apparition_btn");
                    break;
                case Abilities.MAD:
                    button.onClick.AddListener(delegate { abilityController.UseMAD(button); });
                    btnImage.sprite = Resources.Load<Sprite>("UI_design/Mutations/MAD_btn");
                    break;
                case Abilities.Corpsebloom:
                    button.onClick.AddListener(delegate { abilityController.UseCorpseBloom(button); });
                    btnImage.sprite = Resources.Load<Sprite>("UI_design/Mutations/corpsebloom_btn");
                    break;
            }

            var tooltip = button.GetComponent<Tooltip>();
            if (tooltip) tooltip.enabled = true;
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

    public void DisableTutorialButton()
    {
        tutorialButton.SetActive(false);
    }

    public void EnableTutorialButton()
    {
        tutorialButton.SetActive(true);
    }

    public void DisableStartMenu()
    {
        startMenu.SetActive(false);
    }
}