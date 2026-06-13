using Unity.Hierarchy;
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
    [SerializeField] private AudioClip _victorySfx;
    [SerializeField] private AudioClip _defeatSfx;

    [Header("Inventário")]
    [SerializeField] private AudioClip _itemConsumedSfx;

    [Header("Recrutamento")]
    [SerializeField] private AudioClip _npcHiredSfx;

    [Header("Configurações")]
    [SerializeField][Range(0f, 1f)] private float _sfxVolume;
    #endregion

    #region Componentes
    private AudioSource _audioSource;
    #endregion

    #region Inicialização
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.volume = _sfxVolume;
        _audioSource.loop = false;
    }
    #endregion

    #region Reprodução de Efeitos
    public void PlayVictory()  => StartCoroutine(FadeAndPlay(_victorySfx));
    public void PlayDefeat()   => StartCoroutine(FadeAndPlay(_defeatSfx));
    public void PlayItem()     => _audioSource.PlayOneShot(_itemConsumedSfx);
    public void PlayContract() => _audioSource.PlayOneShot(_npcHiredSfx);
    #endregion

    #region Corrotinas
    /// <summary>
    /// Silencia suavemente a música ambiente do MusicManager e, em seguida, toca um som importante.
    /// Utilizado principalmente para dar destaque sonoro aos resultados de fim de batalha.
    /// </summary>
    /// <param name="clip">O clipe de áudio (vitória/derrota) a ser executado.</param>
    private System.Collections.IEnumerator FadeAndPlay(AudioClip clip)
    {
        if (MusicManager.Instance != null)
            yield return StartCoroutine(MusicManager.Instance.FadeOutMusic());

        _audioSource.PlayOneShot(clip, _sfxVolume);
    }
    #endregion
}