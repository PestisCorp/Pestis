using System.Collections;
using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public ParticleSystem combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public Horde.HordeController HordeController;
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

    void UpdateParticleBounds(Bounds bounds)
    {

        // Configure the Shape Module
        var shape = combatVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.scale = bounds.size;
        shape.position = bounds.center - transform.position; // Align the box with the object's position
    }

    private void FixedUpdate()
    {
        UpdateParticleBounds(HordeController.GetBounds());
    }
    private void OnEnable()
    {
        audioSource.Stop();
        audioSource.clip = startCombatSound;
        audioSource.Play();

    }

    private void OnDisable()
    {
        audioSource.Stop();
        audioSource.clip = endCombatSound;
        audioSource.Play();
    }
}
