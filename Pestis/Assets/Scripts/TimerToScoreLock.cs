using Fusion;
using Players;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Unity.Collections.Unicode;

public class TimerToScoreLock : MonoBehaviour
{
    public TMPro.TMP_Text timerText;
    public TMPro.TMP_Text scoreText;
    public Button resetButton;
    public GameObject resetButtonUI;
    public GameObject prefab;
    public void UpdateScore(ulong ScoreToDisplay)
    {
        scoreText.text = ScoreToDisplay.ToString() + " pts";
    }
    public void UpdateTimer(int timeToDisplay)
    {

        int minutes = Mathf.FloorToInt(timeToDisplay / 60);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public static void reset(GameObject[] destroy)
    {
        foreach (GameObject go in destroy) {GameObject.Destroy(go);}
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    }


}
