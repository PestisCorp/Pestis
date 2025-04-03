using System.Collections;
using Horde;
using Objectives;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ColorUtility = UnityEngine.ColorUtility;

public class Split_Recommended_In_Horde : MonoBehaviour
{
    public GameObject splitHordeUI;
    public HordeController horde;
    public Button splitHordeButton;
    public Image image;
    public TMP_Text buttonTMPText;
    public int splitRecommended = 300;

    private void Start()
    {
        if (!horde.player.IsLocal) return;
        splitHordeUI.SetActive(false);
        splitHordeButton.onClick.AddListener(SplitHordeHalf);

        if (!horde.isApparition) StartCoroutine(WaitTilPopulationGreaterThanRecommended());
    }


    private IEnumerator WaitTilPopulationGreaterThanRecommended()
    {
        while (true)
        {
            
            // Wait until the horde population exceeds the recommended amount
            yield return new WaitUntil(() => horde.AliveRats > splitRecommended);

            // Show the split UI
            splitHordeUI.SetActive(true);
            for (var i = 0; i < 20; i++)
            {
                if (horde.AliveRats < splitRecommended)
                {
                    splitHordeUI.SetActive(false);
                    break;
                }

                if (ColorUtility.TryParseHtmlString("#0A2046", out var newColor))
                    buttonTMPText.color = newColor; // Apply hex color to TextMeshPro text

                if (ColorUtility.TryParseHtmlString("#FF5F5F", out var newColor2))
                    image.color = newColor2; // Apply hex color to TextMeshPro text
                yield return new WaitForSeconds(0.5f);
                buttonTMPText.color = Color.black;
                image.color = Color.white;
                yield return new WaitForSeconds(0.5f);
            }

            splitHordeUI.SetActive(false);

            // Wait until the horde population drops below the recommended amount
            yield return new WaitForSeconds(10f);
            yield return new WaitUntil(() => horde.AliveRats <= splitRecommended);
        }
    }

    public void SplitHordeHalf()
    {
        if (horde.AliveRats < 10) return;
        horde.player.SplitHorde(horde, 0.5f);
        GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.HordeSplit, 1);
        splitHordeUI.SetActive(false);
    }
}