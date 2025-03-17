using UnityEngine;
using UnityEngine.Serialization;

public class TutorialManager : MonoBehaviour
{
    public GameObject welcomeCanvas;
    public GameObject fightCanvas;
    public GameObject conquerCanvas;
    public GameObject evolveCanvas;
    public GameObject dominateCanvas;
    private GameObject current;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OpenWelcome();
    }

    void OpenWelcome()
    {
        current = welcomeCanvas;
        welcomeCanvas.SetActive(true);
        UI_Manager.Instance.HighlightUiElement(welcomeCanvas);
    }

    public void NextWelcome()
    {
        welcomeCanvas.SetActive(false);
        UI_Manager.Instance.UnhighlightUiElement(welcomeCanvas);
        OpenFight();
    }

    void OpenFight()
    {
        current = fightCanvas;
        fightCanvas.SetActive(true);
        UI_Manager.Instance.HighlightUiElement(fightCanvas);
    }

    public void NextFight()
    {
        CloseTutorial();
    }

    public void CloseTutorial()
    {
        current.SetActive(false);
        UI_Manager.Instance.UnhighlightUiElement(current);
        UI_Manager.Instance.EnableStartMenu();
    }
}