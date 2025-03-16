using Objectives;
using UnityEngine;
using UnityEngine.UI;

public class Split_Recommended : MonoBehaviour
{
    public GameObject splitHordeUI;
    public Button splitHordeButton;
    public Image image;
    public TMPro.TMP_Text buttonTMPText;
    public int splitRecommended = 500;
    // Update is called once per frame
    void FixedUpdate()
    {
        if (InputHandler.Instance.LocalPlayer?.selectedHorde?.AliveRats > splitRecommended)
        {
            splitRecommendedText();
            splitHordeUI.SetActive(true);
        }
        else if (InputHandler.Instance.LocalPlayer?.selectedHorde?.AliveRats > 10)
        {
            splitPossibleText();
            splitHordeUI.SetActive(true);
        }
        else splitHordeUI.SetActive(false);

    }

    private void splitPossibleText()
    {
        buttonTMPText.text = "Split Possible";
        buttonTMPText.color =Color.black;
        image.color = Color.white;

    }
    private void splitRecommendedText()
    {
        buttonTMPText.text = "Split Recommended";
        if (ColorUtility.TryParseHtmlString("#0A2046", out Color newColor))
        {
            buttonTMPText.color = newColor; // Apply hex color to TextMeshPro text

        }

        if (ColorUtility.TryParseHtmlString("#FF5F5F", out Color newColor2))
        {
            image.color = newColor2; // Apply hex color to TextMeshPro text
        }
    }
    private void Start()
    {
        splitHordeButton.onClick.AddListener(SplitHordeHalf);
    }
    public void SplitHordeHalf()
    {
        if (InputHandler.Instance.LocalPlayer!.selectedHorde!.AliveRats < 10) return;
        var horde = InputHandler.Instance.LocalPlayer?.selectedHorde;
        horde?.Player.SplitHorde(horde, 0.5f);
        if (horde)
        {
            GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.HordeSplit, 1);
        }
    }
}
