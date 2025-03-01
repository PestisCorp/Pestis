using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public ParticleSystem combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public Horde.CombatController combatController;


    public void combatStartFX()
    {
        Debug.Log("Combat Start VFX");
        audioSource.Stop();
        audioSource.clip = startCombatSound;
        combatVFX.Play();
        audioSource.Play();
    }

    public void combatEndFX()
    {
        Debug.Log("Combat Start VFX");
        audioSource.Stop();
        audioSource.clip = endCombatSound;
        combatVFX.Stop();
        audioSource.Play();
    }


    private void OnEnable()
    {
        combatController.BattleParticipantHordeDecreased.AddListener(combatEndFX);
        combatController.BattleParticipantHordeIncreased.AddListener(combatStartFX);
        combatController.BattleParticipantHordeDecreased.AddListener(() => Debug.Log("BattleParticipant Horde Decreased!"));
        combatController.BattleParticipantHordeIncreased.AddListener(() => Debug.Log("BattleParticipant Horde Increased!"));
        combatController.BattleParticipantPlayerDecreased.AddListener(() => Debug.Log("BattleParticipant Player Decreased!"));
        combatController.BattleParticipantPlayerIncreased.AddListener(() => Debug.Log("BattleParticipant Player Increased!"));
    }
}
