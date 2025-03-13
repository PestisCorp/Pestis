using Players;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TimerToScoreLock : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    bool ScoreLocked = false;
    public Text timerText;
    public Text scoreText;
    public Player player;
    void Start()
    {
        
    }

    public IEnumerator TimerTilScoreLock(int timeRemaining)
    {
        while (timeRemaining > 0)
        {
            UpdateTimer(timeRemaining);
            yield return new WaitForSeconds(1f);
            timeRemaining--;
        }

        ScoreLocked = true;
        yield return null;
    }

    private void FixedUpdate()
    {
        while (ScoreLocked == false)
        {
            scoreText.text = player.CalculateScore().ToString();
        }
    }
    private void UpdateTimer(int timeToDisplay)
    {
        int minutes = Mathf.FloorToInt(timeToDisplay / 60);
        int seconds = Mathf.FloorToInt(timeToDisplay % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
