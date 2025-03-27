using System.Collections.Generic;
using System.Linq;
using Combat;
using UnityEngine;

public class CombatFXManager : MonoBehaviour
{
    public List<ParticleSystem> combatVFX;
    public AudioSource audioSource;
    public AudioClip startCombatSound;
    public AudioClip endCombatSound;
    public CombatController combatController;


    private async void FixedUpdate()
    {
        if (!combatController || combatController.state != CombatState.InProgress) return;

        var hordes = combatController.GetHordes();

        var neededVfx = hordes.Count - 1 - combatVFX.Count;
        if (neededVfx > 0)
        {
            var newVfx = await InstantiateAsync(combatVFX.First(), neededVfx);
            combatVFX.AddRange(newVfx);
        }

        for (var i = 0; i < combatVFX.Count; i++)
        {
            var vfx = combatVFX[i];
            var hordeOne = hordes[i];
            var hordeTwo = hordes[i + 1];

            var overlap = GetIntersection(hordeOne.GetBounds(), hordeTwo.GetBounds());

            vfx.transform.position = overlap.center;
            var shape = vfx.shape;
            shape.radius = overlap.extents.magnitude;
            var emissions = vfx.emission;
            emissions.rateOverDistanceMultiplier = overlap.extents.sqrMagnitude * 1.7f;
            if (vfx.isPaused) vfx.Play();
        }
    }

    private void OnEnable()
    {
        if (!combatController.InvolvesLocalPlayer()) return;
        audioSource.Stop();
        audioSource.clip = startCombatSound;
        audioSource.Play();
    }

    private void OnDisable()
    {
        if (!combatController.InvolvesLocalPlayer()) return;
        foreach (var vfx in combatVFX) vfx.Stop();
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
}