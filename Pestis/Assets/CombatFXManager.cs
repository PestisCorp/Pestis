using Horde;
using System.Collections;
using System.Linq;
using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public ParticleSystem combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public HordeController HordeController;
    Bounds GetIntersection(Bounds a, Bounds b)
    {
        Vector3 min = Vector3.Max(a.min, b.min); // Maximum of the min corners
        Vector3 max = Vector3.Min(a.max, b.max); // Minimum of the max corners

        if (min.x > max.x || min.y > max.y || min.z > max.z)
        {
            // No valid intersection
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        return new Bounds(center, size);
    }
    void UpdateParticleBounds(Bounds bounds)
    {
        // Configure the Shape Module
        var shape = combatVFX.shape;
        combatVFX.transform.position = bounds.center;
        shape.scale = bounds.size;
        float radius = bounds.extents.magnitude + 1f;
        combatVFX.emissionRate =  (radius * radius) * 1.7f ;
    }

    Bounds getHordeBounds()
    {
        var hordelist = HordeController.CurrentCombatController.GetHordes().Where(h => h != HordeController).ToList();
        Bounds SumBound = new Bounds(hordelist[0].GetBounds().center, hordelist[0].GetBounds().size);
        foreach (HordeController horde in hordelist)
        {
            SumBound.Encapsulate(horde.GetBounds());
        }
        return SumBound;
    }

    void Update()
    {
        if (!HordeController.CurrentCombatController.HordeIsVoluntary(HordeController))
        {
            UpdateParticleBounds(GetIntersection(getHordeBounds(), HordeController.GetBounds()));

        }
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
}
