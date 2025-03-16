using Objectives;
using UnityEngine;

public class Split_Recommended : MonoBehaviour
{
    public GameObject splitHordeUI;
    public UnityEngine.UI.Button splitHordeButton;
    public int splitRecommended = 100;
    // Update is called once per frame
    void FixedUpdate()
    {
        splitHordeUI.SetActive(InputHandler.Instance.LocalPlayer?.selectedHorde?.AliveRats > splitRecommended);

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
