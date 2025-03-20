using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public GameObject tutorialCanvas;
    public Sprite welcomeImage;
    public Sprite fightImage;
    public Sprite conquerImage;
    public Sprite evolveImage;
    public Sprite dominateImage;
    public Sprite controlsImage;
    private Image current;
    private Sprite[] slides;
    private int slidesIndex = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slides =  new Sprite[] {welcomeImage, fightImage, conquerImage, evolveImage, dominateImage, controlsImage};
        GameObject imageObj = tutorialCanvas.transform.Find("image").gameObject;
        current = imageObj.GetComponent<Image>();
        StartTutorial();
    }

    public void StartTutorial()
    {
        UI_Manager.Instance.DisableTutorialButton();
        UI_Manager.Instance.DisableStartMenu();
        slidesIndex = 0;
        tutorialCanvas.SetActive(true);
        UI_Manager.Instance.HighlightUiElement(tutorialCanvas);
        DisplayTutorial();
    }

    void DisplayTutorial()
    {
        if (slides[slidesIndex] ) current.sprite = slides[slidesIndex];
        else CloseTutorial();
    }

    public void NextSlide()
    {
        slidesIndex++;
        if(slidesIndex > (slides.Length - 1)) CloseTutorial();
        else DisplayTutorial();
    }

    public void CloseTutorial()
    {
        tutorialCanvas.SetActive(false);
        UI_Manager.Instance.UnhighlightUiElement(tutorialCanvas);
        if(UI_Manager.Instance.localPlayer == null) UI_Manager.Instance.EnableStartMenu();
        UI_Manager.Instance.EnableTutorialButton();
    }
}