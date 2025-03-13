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
        StartCoroutine(TimerTilScoreLock(600));
    }

    public IEnumerator TimerTilScoreLock(int timeRemaining)
    {
        //waits for player to be assigned, assuming game loads after player is assigned
        while (player == null)
        {
            Debug.Log("Waiting for timerText to be assigned...");
            yield return null; // Wait until the next frame
        }

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
