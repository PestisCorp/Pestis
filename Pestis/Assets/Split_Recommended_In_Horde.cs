using Objectives;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using ColorUtility = UnityEngine.ColorUtility;

public class Split_Recommended_In_Horde : MonoBehaviour
{
    public GameObject splitHordeUI;
    public Horde.HordeController horde;
    public Button splitHordeButton;
    public Image image;
    public TMPro.TMP_Text buttonTMPText;
    public int splitRecommended = 500;
    private void Start()
    {
        splitHordeUI.SetActive(false);
        splitHordeButton.onClick.AddListener(Split_Recommended.SplitHordeHalf);
        if (horde.Player.IsLocal)
        {
            StartCoroutine(WaitTilPopulationGreaterThanRecommended());
        }
    }




    IEnumerator WaitTilPopulationGreaterThanRecommended()
    {
        while (true)
        {
            // Wait until the horde population exceeds the recommended amount
            yield return new WaitUntil(() => horde.AliveRats > splitRecommended);

            // Show the split UI
            splitHordeUI.SetActive(true);
            for (int i = 0; i < 10;)
            {
                if (horde.AliveRats < splitRecommended) { splitHordeUI.SetActive(false); break; }
                if (ColorUtility.TryParseHtmlString("#0A2046", out Color newColor))
                {
                    buttonTMPText.color = newColor; // Apply hex color to TextMeshPro text

                }

                if (ColorUtility.TryParseHtmlString("#FF5F5F", out Color newColor2))
                {
                    image.color = newColor2; // Apply hex color to TextMeshPro text
                }
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
        horde.Player.SplitHorde(horde, 0.5f);
        GameManager.Instance.ObjectiveManager.AddProgress(ObjectiveTrigger.HordeSplit, 1);
        splitHordeUI.SetActive(false);

    }
}
