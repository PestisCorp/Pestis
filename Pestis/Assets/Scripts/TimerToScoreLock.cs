using UnityEngine;

public class TimerToScoreLock : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public TMPro.TMP_Text timerText;
    public TMPro.TMP_Text scoreText;
    public void UpdateScore(ulong ScoreToDisplay)
    {

        scoreText.text = ScoreToDisplay.ToString();
    }

    public void UpdateTimer(int timeToDisplay)
    {

        int minutes = Mathf.FloorToInt(timeToDisplay / 60);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}