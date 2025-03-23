using Horde;
using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public ParticleSystem combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public HordeController HordeController;

    private void Update()
    {
        if (HordeController.CurrentCombatController &&
            HordeController.CurrentCombatController.HordeInCombat(HordeController) &&
            !HordeController.CurrentCombatController.HordeIsVoluntary(HordeController))
            UpdateParticleBounds(GetIntersection(getHordeBounds(), HordeController.GetBounds()));
    }

    private void OnEnable()
    {
        combatVFX.Play();
        audioSource.Stop();
        audioSource.clip = startCombatSound;
        audioSource.Play();
    }

    private void OnDisable()
    {
        combatVFX.Stop();
        audioSource.Stop();
        audioSource.clip = endCombatSound;
        audioSource.Play();
    }

    private Bounds GetIntersection(Bounds a, Bounds b)
    {
        var min = Vector3.Max(a.min, b.min); // Maximum of the min corners
        var max = Vector3.Min(a.max, b.max); // Minimum of the max corners

        if (min.x > max.x || min.y > max.y || min.z > max.z)
            // No valid intersection
            return new Bounds(Vector3.zero, Vector3.zero);

        var center = (min + max) * 0.5f;
        var size = max - min;

        return new Bounds(center, size);
    }

    private void UpdateParticleBounds(Bounds bounds)
    {
        // Configure the Shape Module
        var shape = combatVFX.shape;
        combatVFX.transform.position = bounds.center;
        shape.scale = bounds.size;
        var radius = bounds.extents.magnitude + 1f;
        combatVFX.emissionRate = radius * radius * 1.7f;
    }

    private Bounds getHordeBounds()
    {
        return HordeController.CurrentCombatController.Bounds;
    }
}