using UnityEngine;

/// <summary>
/// Gerenciador global de efeitos sonoros pontuais (Singleton).
/// Lida com a execução de sons curtos como interface, consumo de itens, 
/// contratação de NPCs e as fanfarras de finalização de combate.
/// </summary>
public class SFXManager : MonoBehaviour
{
    #region Padrão Singleton
    public static SFXManager Instance { get; private set; }
    #endregion

    #region Clipes de Áudio
    [Header("Batalha")]
    public AudioClip sfxVitoria;
    public AudioClip sfxDerrota;

    [Header("Inventário")]
    public AudioClip sfxItemConsumido;

    [Header("Recrutamento")]
    public AudioClip sfxNPCContratado;
    #endregion

    #region Componentes
    private AudioSource audioSource;
    #endregion

    #region Inicialização
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
    }
    #endregion

    #region Reprodução de Efeitos
    public void TocarVitoria()  => StartCoroutine(FadeETocar(sfxVitoria));
    public void TocarDerrota()  => StartCoroutine(FadeETocar(sfxDerrota));
    public void TocarItem()     => audioSource.PlayOneShot(sfxItemConsumido);
    public void TocarContrato() => audioSource.PlayOneShot(sfxNPCContratado);
    #endregion

    #region Corrotinas
    /// <summary>
    /// Silencia suavemente a música ambiente do MusicManager e, em seguida, toca um som importante.
    /// Utilizado principalmente para dar destaque sonoro aos resultados de fim de batalha.
    /// </summary>
    /// <param name="clip">O clipe de áudio (vitória/derrota) a ser executado.</param>
    private System.Collections.IEnumerator FadeETocar(AudioClip clip)
    {
        if (MusicManager.Instance != null)
            yield return StartCoroutine(MusicManager.Instance.FadeOutMusica());

        audioSource.PlayOneShot(clip);
    }
    #endregion
}