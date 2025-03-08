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

    private IEnumerator PlaySFX(AudioClip sound, float delay)
    {

        while (!battleSoundMuteX)
        {
            yield return null; // Wait for the next frame until battleSoundMuteX is true
        }
        battleSoundMuteX = false;  

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
        audioSource.Stop();
        audioSource.clip = startCombatSound;
        audioSource.Play();

    }

    private void OnDestroy()
    {
        audioSource.Stop();
        audioSource.clip = endCombatSound;
        audioSource.Play();
    }
}
