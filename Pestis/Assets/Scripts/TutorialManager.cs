using UnityEngine;
using UnityEngine.Serialization;

public class TutorialManager : MonoBehaviour
{
    public GameObject welcomeCanvas;
    private GameObject next;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Display_welcome_slide();
    }

    public void CloseTutorial()
    {
        welcomeCanvas.SetActive(false);
        UI_Manager.Instance.UnhighlightUiElement(welcomeCanvas);
    }

    public void nextSlide()
    {
        if (!next)
        {
            CloseTutorial();
        }
    }
    
    // Display slides
    
    // Display welcome slide
    void Display_welcome_slide()
    {
        welcomeCanvas.SetActive(true);
        UI_Manager.Instance.HighlightUiElement(welcomeCanvas);
    }
    
    
    // Display fight slide
    void Display_fight_slide()
    {
        //turn on relevant canvas
        //display relevant UI in the tutorial positions
        // move UI after (diff function)
    }
    // Display conquer slide
    
    // Display evolve slide
    
    // Display dominate slide
    
    // Display basic controls
}