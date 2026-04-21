using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Batalha")]
    public AudioClip sfxVitoria;
    public AudioClip sfxDerrota;

    [Header("Inventário")]
    public AudioClip sfxItemConsumido;

    [Header("Recrutamento")]
    public AudioClip sfxNPCContratado;

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;
    }

    public void TocarVitoria()  => StartCoroutine(FadeETocar(sfxVitoria));
    public void TocarDerrota()  => StartCoroutine(FadeETocar(sfxDerrota));
    public void TocarItem()     => audioSource.PlayOneShot(sfxItemConsumido);
    public void TocarContrato() => audioSource.PlayOneShot(sfxNPCContratado);

    private System.Collections.IEnumerator FadeETocar(AudioClip clip)
    {
        if (MusicManager.Instance != null)
            yield return StartCoroutine(MusicManager.Instance.FadeOutMusica());

        audioSource.PlayOneShot(clip);
    }
}