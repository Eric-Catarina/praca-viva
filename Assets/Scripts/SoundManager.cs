using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("Fonte de áudio para efeitos sonoros (SFX)")]
    public AudioSource sfxSource;
    [Tooltip("Fonte de áudio para música de fundo (opcional)")]
    public AudioSource musicSource;

    [Header("SFX Clips")]
    public AudioClip notificationAppearSound;
    public AudioClip notificationDisappearSound;
    public AudioClip trashCollectedSound;
    public AudioClip gameWinSound;
    public AudioClip gameLoseSound;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                Debug.LogError("SoundManager: SFX AudioSource not found or assigned, and couldn't get one from this GameObject!", this);
            }
        }

        if (musicSource != null)
        {
            musicSource.loop = true;
        }
    }


    public void PlayNotificationAppearSound(float volumeScale = 1.0f)
    {
        PlaySound(notificationAppearSound, volumeScale);
    }

    public void PlayNotificationDisappearSound(float volumeScale = 1.0f)
    {
        PlaySound(notificationDisappearSound, volumeScale);
    }

    public void PlayTrashCollectedSound(float volumeScale = 1.0f)
    {
        PlaySound(trashCollectedSound, volumeScale);
    }

     public void PlayGameWinSound(float volumeScale = 1.0f)
    {
        PlaySound(gameWinSound, volumeScale);
    }

    public void PlayGameLoseSound(float volumeScale = 1.0f)
    {
        PlaySound(gameLoseSound, volumeScale);
    }

    public void PlayMusic(AudioClip musicClip, float volumeScale = 1.0f)
    {
        if (musicSource != null && musicClip != null)
        {
            musicSource.clip = musicClip;
            musicSource.volume = volumeScale;
            musicSource.Play();
        }
        else
        {
            if(musicSource == null) Debug.LogWarning("SoundManager: Music AudioSource is not assigned.");
            if(musicClip == null) Debug.LogWarning("SoundManager: Music AudioClip is null.");
        }
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }


    private void PlaySound(AudioClip clip, float volumeScale = 1.0f)
    {
        if (sfxSource != null && clip != null)
        {
            float finalVolume = volumeScale;

            sfxSource.PlayOneShot(clip, finalVolume);
        }
        else
        {
            if(clip == null) Debug.LogWarning($"SoundManager: AudioClip is null. Cannot play sound.");
        }
    }

}