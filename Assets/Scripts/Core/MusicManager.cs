using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    public enum MusicState { Exploracao, Perseguicao, TerraFirme, Batalha, Menu }

    [Header("Listas de Músicas")]
    public List<AudioClip> musicasExploracao;
    public List<AudioClip> musicasPerseguicao;
    public List<AudioClip> musicasTerraFirme;
    public List<AudioClip> musicasBatalha;
    public List<AudioClip> musicasStart;

    [Header("Som Ambiente")]
    public AudioClip somDoMar;
    [Range(0f, 1f)] public float volumeAmbiente = 0.4f;

    [Header("Intervalo Sem Música")]
    public float intervaloMinimo = 5f;
    public float intervaloMaximo = 20f;

    [Header("Configuração")]
    public float fadeDuration = 1f;

    private AudioSource sourcMusica;
    private AudioSource sourceAmbiente;
    private MusicState estadoAtual;

    private Dictionary<MusicState, List<AudioClip>> playlists;
    private Dictionary<MusicState, List<int>> indicesRestantes;

    private Coroutine fadeMusicaCoroutine;
    private Coroutine fadeAmbienteCoroutine;
    private Coroutine aguardarProximaCoroutine;
    private bool emIntervalo = false;

    private bool musicaMutada = false;


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sourcMusica = gameObject.AddComponent<AudioSource>();
        sourcMusica.loop = false;

        sourceAmbiente = gameObject.AddComponent<AudioSource>();
        sourceAmbiente.loop = true;
        sourceAmbiente.clip = somDoMar;
        sourceAmbiente.volume = 0f;

        playlists = new Dictionary<MusicState, List<AudioClip>>
        {
            { MusicState.Exploracao,  musicasExploracao  },
            { MusicState.Perseguicao, musicasPerseguicao },
            { MusicState.TerraFirme,  musicasTerraFirme  },
            { MusicState.Batalha,     musicasBatalha     },
            { MusicState.Menu,        musicasStart       }
        };

        indicesRestantes = new Dictionary<MusicState, List<int>>();
        foreach (var estado in playlists.Keys)
            ResetarIndices(estado);
    }

    void Start()
    {
        estadoAtual = ResolverEstado();
        AtualizarAmbiente(estadoAtual);
        TocarProxima(estadoAtual);
    }

    void Update()
    {
        MusicState novoEstado = ResolverEstado();

        if (novoEstado != estadoAtual)
        {
            estadoAtual = novoEstado;
            AtualizarAmbiente(estadoAtual);
            TrocarMusica(estadoAtual);
        }

        if (!sourcMusica.isPlaying && !emIntervalo && !musicaMutada) 
        {
            if (aguardarProximaCoroutine != null) StopCoroutine(aguardarProximaCoroutine);
            aguardarProximaCoroutine = StartCoroutine(AguardarETocarProxima(estadoAtual));
        }
    }

    private MusicState ResolverEstado()
    {
        if (!GameState.isGameStarted)  return MusicState.Menu;
        if (GameState.IsInBattle)      return MusicState.Batalha;
        if (GameState.IsBeingChased)   return MusicState.Perseguicao;
        if (!GameState.IsOnWater)      return MusicState.TerraFirme;
        return MusicState.Exploracao;
    }

    private void AtualizarAmbiente(MusicState estado)
    {
        bool deveTocar = estado != MusicState.Batalha;

        if (deveTocar && !sourceAmbiente.isPlaying)
        {
            sourceAmbiente.Play();
            if (fadeAmbienteCoroutine != null) StopCoroutine(fadeAmbienteCoroutine);
            fadeAmbienteCoroutine = StartCoroutine(FadeAmbiente(volumeAmbiente));
        }
        else if (!deveTocar && sourceAmbiente.isPlaying)
        {
            if (fadeAmbienteCoroutine != null) StopCoroutine(fadeAmbienteCoroutine);
            fadeAmbienteCoroutine = StartCoroutine(FadeAmbiente(0f, pararAoTerminar: true));
        }
    }

    private IEnumerator FadeAmbiente(float volumeAlvo, bool pararAoTerminar = false)
    {
        float volumeInicial = sourceAmbiente.volume;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            sourceAmbiente.volume = Mathf.Lerp(volumeInicial, volumeAlvo, elapsed / fadeDuration);
            yield return null;
        }
        sourceAmbiente.volume = volumeAlvo;
        if (pararAoTerminar) sourceAmbiente.Stop();
    }

   private void TrocarMusica(MusicState novoEstado)
    {
        emIntervalo = false;
        if (aguardarProximaCoroutine != null) StopCoroutine(aguardarProximaCoroutine);
        if (fadeMusicaCoroutine != null) StopCoroutine(fadeMusicaCoroutine);
        fadeMusicaCoroutine = StartCoroutine(FadeParaNovoEstado(novoEstado));
    }

    private IEnumerator FadeParaNovoEstado(MusicState novoEstado)
    {
        // Fade out só se estiver tocando
        if (sourcMusica.isPlaying)
        {
            float volumeInicial = sourcMusica.volume;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                sourcMusica.volume = Mathf.Lerp(volumeInicial, 0f, elapsed / fadeDuration);
                yield return null;
            }
            sourcMusica.Stop();
        }

        sourcMusica.volume = 0f;
        TocarProxima(novoEstado);

        // Fade in
        float elapsedIn = 0f;
        while (elapsedIn < fadeDuration)
        {
            elapsedIn += Time.deltaTime;
            sourcMusica.volume = Mathf.Lerp(0f, 1f, elapsedIn / fadeDuration);
            yield return null;
        }
        sourcMusica.volume = 1f;
    }
    private IEnumerator AguardarETocarProxima(MusicState estado)
    {
        emIntervalo = true;
        float espera = Random.Range(intervaloMinimo, intervaloMaximo);
        yield return new WaitForSeconds(espera);
        emIntervalo = false;

        if (estadoAtual == estado)
            TocarProxima(estado);
    }


    private void TocarProxima(MusicState estado)
    {
        List<AudioClip> lista = playlists[estado];
        if (lista == null || lista.Count == 0) return;

        if (indicesRestantes[estado].Count == 0)
            ResetarIndices(estado);

        int sorteio = Random.Range(0, indicesRestantes[estado].Count);
        int idx = indicesRestantes[estado][sorteio];
        indicesRestantes[estado].RemoveAt(sorteio);

        sourcMusica.clip = lista[idx];
        sourcMusica.volume = 1f;
        sourcMusica.Play();
    }

    private void ResetarIndices(MusicState estado)
    {
        List<int> indices = new();
        for (int i = 0; i < playlists[estado].Count; i++)
            indices.Add(i);
        indicesRestantes[estado] = indices;
    }

    public IEnumerator FadeOutMusica()
    {
        musicaMutada = true;
        if (aguardarProximaCoroutine != null) StopCoroutine(aguardarProximaCoroutine);
        emIntervalo = false;

        float volumeInicial = sourcMusica.volume;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            sourcMusica.volume = Mathf.Lerp(volumeInicial, 0f, elapsed / fadeDuration);
            yield return null;
        }
        sourcMusica.Stop();
        sourcMusica.volume = 0f;
    }

    public void RetomarMusica()
    {
        musicaMutada = false;
        TrocarMusica(estadoAtual);
    }
}