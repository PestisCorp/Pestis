using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public ParticleSystem combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public Horde.CombatController combatController;


    private void OnEnable()
    {
        combatController.OnCombatStarted += combatStartFX;
        combatController.OnCombatEnded += combatEndFX;
    }

    private void OnDisable()
    {
        combatController.OnCombatStarted -= combatStartFX;
        combatController.OnCombatEnded -= combatEndFX;
    }
    public void combatStartFX()
    {
        audioSource.Stop();
        audioSource.clip = startCombatSound;
        combatVFX.Play();
        audioSource.Play();
    }

    public void combatEndFX()
    {
        audioSource.Stop();
        audioSource.clip = endCombatSound;
        combatVFX.Stop();
        audioSource.Play();
    }
}
