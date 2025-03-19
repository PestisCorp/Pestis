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
    private Image current;
    private Sprite[] slides;
    private int slidesIndex = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        slides =  new Sprite[] {welcomeImage, fightImage, conquerImage, evolveImage, dominateImage};
        tutorialCanvas.SetActive(true);
        UI_Manager.Instance.HighlightUiElement(tutorialCanvas);
        GameObject imageObj = tutorialCanvas.transform.Find("image").gameObject;
        current = imageObj.GetComponent<Image>();
        displayTutorial();
    }

    void displayTutorial()
    {
        if (slides[slidesIndex] ) current.sprite = slides[slidesIndex];
        else CloseTutorial();
    }

    public void nextSlide()
    {
        slidesIndex++;
        if(slidesIndex > 4) CloseTutorial();
        displayTutorial();
    }

    public void CloseTutorial()
    {
        tutorialCanvas.SetActive(false);
        UI_Manager.Instance.UnhighlightUiElement(tutorialCanvas);
        UI_Manager.Instance.EnableStartMenu();
    }
}