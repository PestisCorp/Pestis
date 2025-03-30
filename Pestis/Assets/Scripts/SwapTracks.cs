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
    public float fadeDuration = 1f;
    public float secondsBeforeCheckTrackSwap = 10f;
    private Type currentTrack;
    private bool isFading = false;
    void Start()
    {
        GameObject tilemapObject = GameObject.FindGameObjectWithTag("tilemap");
        if (tilemapObject != null)
        {
            this.tilemap = tilemapObject.GetComponent<Tilemap>();
            currentTrack = typeof(GrassTile);
            audioSource.clip = tracks[0];
            audioSource.Play();
            InvokeRepeating(nameof(CheckTrackSwap), 0f, secondsBeforeCheckTrackSwap);
            
        }
        else
        {
            Debug.LogError("Tilemap not found in the scene!");
        }
    }
    void CheckTrackSwap()
    {
        if (InputHandler.Instance.LocalPlayer.selectedHorde == null || isFading)
            return;

        Vector3Int pos = tilemap.WorldToCell(InputHandler.Instance.LocalPlayer.selectedHorde.GetCenter());

        TileBase tileAtPos = tilemap.GetTile(pos);
        Debug.Log(tileAtPos.ToString());
        if (tileAtPos != null)
        {
            System.Type currentTile = tileAtPos.GetType();

            if (currentTile != null && currentTile != currentTrack)
            {
                currentTrack = currentTile;
                AudioClip newClip = GetClipForTile(currentTile);

                Debug.Log(newClip);
                if (newClip != null && newClip != audioSource.clip)
                {
                    StartCoroutine(SwapTrackCoroutine(newClip));
                }
            }
        }
    }

    private IEnumerator SwapTrackCoroutine(AudioClip newClip)
    {
        isFading = true;
        yield return StartCoroutine(FadeOut(audioSource, fadeDuration));
        audioSource.clip = newClip;
        yield return StartCoroutine(FadeIn(audioSource, fadeDuration));
        isFading = false;
    }

    private AudioClip GetClipForTile(System.Type tileType)
    {
        if (tileType == typeof(GrassTile)) return tracks[2];
        if (tileType == typeof(TundraTile)) return tracks[0];
        if (tileType == typeof(DesertTile)) return tracks[1];
        if (tileType == typeof(StoneTile)) return tracks[3];

        return null;
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
