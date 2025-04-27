using UnityEngine;

// Garante que este GameObject sempre terá um componente AudioSource
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    // --- Singleton Instance ---
    // Permite acesso fácil de qualquer lugar: SoundManager.Instance.Metodo()
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("AudioSource principal para efeitos sonoros (SFX) não posicionais.")]
    public AudioSource sfxSource; // Usaremos este para os sons da UI

    // Você pode adicionar mais AudioSources aqui para música, vozes, etc., se necessário
    // public AudioSource musicSource;

    [Header("Notification Sounds")]
    [Tooltip("Som tocado quando uma notificação aparece.")]
    public AudioClip notificationAppearSound;

    [Tooltip("Som (opcional) tocado quando uma notificação desaparece.")]
    public AudioClip notificationDisappearSound;

    // --- Volumes (Opcional, para controle futuro) ---
    // [Range(0f, 1f)] public float masterVolume = 1.0f;
    // [Range(0f, 1f)] public float sfxVolume = 1.0f;
    // [Range(0f, 1f)] public float musicVolume = 1.0f;


    void Awake()
    {
        // --- Singleton Pattern Implementation ---
        if (Instance != null && Instance != this)
        {
            // Se já existe uma instância e não sou eu, destruo este GameObject.
            Debug.LogWarning("SoundManager: More than one instance found! Destroying duplicate.");
            Destroy(gameObject);
            return; // Impede a execução do resto do Awake
        }
        // Define esta como a instância única
        Instance = this;

        // (Opcional) Mantém o SoundManager vivo entre carregamentos de cena
        // DontDestroyOnLoad(gameObject);

        // --- Pega a referência ao AudioSource principal ---
        // Se você não atribuir no Inspector, ele tentará pegar o do próprio GameObject
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        // --- Configurações Iniciais (Opcional) ---
        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false; // Geralmente não queremos que SFX toque ao iniciar
            sfxSource.loop = false;        // SFX geralmente não são loops
            // Defina Spatial Blend para 0 para sons 2D (UI)
            sfxSource.spatialBlend = 0f;
        }
        else
        {
             Debug.LogError("SoundManager: SFX AudioSource not found or assigned!");
        }
    }

    // --- Métodos Públicos para Tocar Sons ---

    // Toca o som de aparecimento da notificação
    public void PlayNotificationAppearSound(float volumeScale = 1.0f)
    {
        PlaySound(notificationAppearSound, volumeScale);
    }

    // Toca o som de desaparecimento da notificação
    public void PlayNotificationDisappearSound(float volumeScale = 1.0f)
    {
        PlaySound(notificationDisappearSound, volumeScale);
    }

    // --- Método Genérico Privado para Tocar SFX ---
    // Ajuda a evitar repetição de código
    private void PlaySound(AudioClip clip, float volumeScale = 1.0f)
    {
        // Verifica se temos uma fonte e um clipe válidos
        if (sfxSource != null && clip != null)
        {
            // Calcula o volume final (poderia incluir masterVolume * sfxVolume aqui no futuro)
            float finalVolume = /* sfxVolume * */ volumeScale;

            // PlayOneShot é ideal para SFX, pois não interrompe outros sons na mesma fonte
            // e permite tocar o mesmo som rapidamente várias vezes se necessário.
            sfxSource.PlayOneShot(clip, finalVolume);
        }
        else
        {
            if(clip == null) Debug.LogWarning($"SoundManager: AudioClip is null. Cannot play sound.");
            // O erro da fonte já é logado no Awake
        }
    }

    // Você pode adicionar mais métodos públicos aqui para outros sons (botões, ações do jogo, etc.)
    // public void PlayButtonClickSound() { PlaySound(buttonClickClip); }
}