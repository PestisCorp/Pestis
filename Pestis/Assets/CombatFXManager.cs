using System.Collections;
using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public ParticleSystem combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public Horde.CombatController combatController;
    bool battleSoundMuteX = true; //helps to reduce the same battle start effect being repeatedly played
    bool startnoise = true;
    bool endnoise = true;
    private float currentTime = 0f;   // Current time for the timer
    public void combatStartFX()
    {
        if (startnoise)
        {
            startnoise = false;
            endnoise = false;
            StartCoroutine(PlaySFX(startCombatSound, 8f));
        }
    }

    public void combatEndFX()
    {
        if (endnoise)
        {
            endnoise = false;
            //checks first if combat has ended for 3 seconds before playing the sound
            StartCoroutine(PlaySFX(endCombatSound, 6f));
        }
    }


    private IEnumerator PlaySFX(AudioClip sound, float delay)
    {

        while (!battleSoundMuteX)
        {
            yield return null; // Wait for the next frame until battleSoundMuteX is true
        }
        battleSoundMuteX = false;  // Prevent sound from being played while waiting for delay

        // Start playing sound and VFX
        Debug.Log("playing sfx");
        audioSource.Stop();
        audioSource.clip = sound;
        audioSource.Play();

        // Wait for the specified delay
        yield return new WaitForSeconds(delay);

        // Allow sound to be played again
        battleSoundMuteX = true;
        startnoise = true;
        endnoise = true;
    }


    private void OnEnable()
    {
        combatController.BattleParticipantHordeDecreased.AddListener(combatEndFX);
        combatController.BattleParticipantHordeIncreased.AddListener(combatStartFX);
    }
}
