using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class SwapTracks : MonoBehaviour
{
    public AudioClip[] tracks;
    public AudioSource audioSource;
    public Tilemap tilemap;
    public float fadeDuration = 3f;
    public float secondsBeforeCheckTrackSwap = 8f;
    void Start()
    {
        GameObject tilemapObject = GameObject.FindGameObjectWithTag("tilemap");
        if (tilemapObject != null)
        {
            this.tilemap = tilemapObject.GetComponent<Tilemap>();
            StartCoroutine(SwapTrack());
        }
        else
        {
            Debug.LogError("Tilemap not found in the scene!");
        }
    }

    private IEnumerator SwapTrack()
    {
        Type currentTrack = typeof(GrassTile);
        audioSource.clip = tracks[0];
        audioSource.Play();

        while (true)
        {
            while (InputHandler.Instance.LocalPlayer.selectedHorde) yield return null;
            Vector3Int pos = new Vector3Int(
                (int)(InputHandler.Instance.LocalPlayer.selectedHorde.GetCenter().x),
                (int)(InputHandler.Instance.LocalPlayer.selectedHorde.GetCenter().y),
                0
            );

            System.Type currentTile = tilemap.GetTile(pos)?.GetType();

            if (currentTile != null && currentTile != currentTrack)
            {
                currentTrack = currentTile;

                AudioClip newClip = null;
                if (currentTile == typeof(GrassTile)) newClip = tracks[1];
                else if (currentTile == typeof(TundraTile)) newClip = tracks[0];
                else if (currentTile == typeof(DesertTile)) newClip = tracks[1];
                else if (currentTile == typeof(StoneTile)) newClip = tracks[0];

                if (newClip != null && newClip != audioSource.clip)
                {
                    yield return StartCoroutine(FadeOut(audioSource, fadeDuration));
                    audioSource.clip = newClip;
                    yield return StartCoroutine(FadeIn(audioSource, fadeDuration));
                }
                
            }
            yield return new WaitForSeconds(secondsBeforeCheckTrackSwap);

        }
        yield return null;
    }

    private IEnumerator FadeOut(AudioSource audio, float duration)
    {
        float startVolume = audio.volume;

        while (audio.volume > 0)
        {
            audio.volume -= startVolume * Time.deltaTime / duration;
            yield return null;
        }

        audio.Stop();
        audio.volume = startVolume; // Reset volume for next track
    }

    private IEnumerator FadeIn(AudioSource audio, float duration)
    {
        audio.Play();
        audio.volume = 0f;
        float targetVolume = 1.0f;

        while (audio.volume < targetVolume)
        {
            audio.volume += targetVolume * Time.deltaTime / duration;
            yield return null;
        }
    }
}
