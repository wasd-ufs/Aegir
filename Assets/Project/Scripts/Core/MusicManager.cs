using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Máquina de estados musical e gerenciador de trilha sonora do jogo (Singleton).
/// Controla as playlists de acordo com o GameState atual (Exploração, Batalha, Perseguição, etc.),
/// gerenciando intervalos dinâmicos entre músicas e som ambiente do mar.
/// </summary>
public class MusicManager : MonoBehaviour
{
    #region Singleton e Enumerações
    public static MusicManager Instance { get; private set; }

    public enum MusicState { Exploration, Chase, Land, Battle, Menu }
    #endregion

    #region Listas de Reprodutibilidade (Playlists)
    [Header("Listas de Músicas")]
    [SerializeField] private List<AudioClip> _explorationMusicList;
    [SerializeField] private List<AudioClip> _chaseMusicList;
    [SerializeField] private List<AudioClip> _landMusicList;
    [SerializeField] private List<AudioClip> _battleMusicList;
    [SerializeField] private List<AudioClip> _menuMusicList;
    #endregion

    #region Configurações Dinâmicas e Ambiente
    
    [Header("Configurações das Músicas")]
    [SerializeField][Range(0f, 1f)] private float _musicVolume;
    
    [Header("Som Ambiente")]
    [SerializeField] private AudioClip _seaSound;
    [SerializeField][Range(0f, 1f)] private float _ambientVolume = 0.4f;

    [Header("Intervalo Sem Música")]
    [SerializeField] private float _minInterval = 5f;
    [SerializeField] private float _maxInterval = 20f;

    [Header("Configuração")]
    [SerializeField] private float _fadeDuration = 1f;
    #endregion

    #region Estado Interno e Componentes
    private AudioSource _musicSource;
    private AudioSource _ambientSource;
    private MusicState _currentState;

    // Listas mapeadas para evitar repetições contínuas da mesma faixa
    private Dictionary<MusicState, List<AudioClip>> _playlistsDictionary;
    private Dictionary<MusicState, List<int>> _remainingIndexesDictionary;

    private Coroutine _musicFadeCoroutine;
    private Coroutine _ambientFadeCoroutine;
    private Coroutine _waitNextCoroutine;
    private bool _isInInterval = false;
    private bool _isMusicMuted = false;
    #endregion

    #region Inicialização e Ciclo de Vida
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // Inicialização de fontes de áudio dinâmicas
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = false;

        _ambientSource = gameObject.AddComponent<AudioSource>();
        _ambientSource.loop = true;
        _ambientSource.clip = _seaSound;
        _ambientSource.volume = 0f;

        // Mapeia listas do inspector para dicionários a fim de facilitar os sorteios em modo "shuffle"
        _playlistsDictionary = new Dictionary<MusicState, List<AudioClip>>
        {
            { MusicState.Exploration,  _explorationMusicList  },
            { MusicState.Chase, _chaseMusicList },
            { MusicState.Land,  _landMusicList  },
            { MusicState.Battle,     _battleMusicList     },
            { MusicState.Menu,        _menuMusicList       }
        };

        _remainingIndexesDictionary = new Dictionary<MusicState, List<int>>();
        foreach (var state in _playlistsDictionary.Keys)
            ResetIndexes(state);
    }

    void Start()
    {
        _currentState = ResolveState();
        UpdateAmbient(_currentState);
        PlayNext(_currentState);
    }

    void Update()
    {
        MusicState newState = ResolveState();

        // Se a situação global do jogo mudou, atualiza toda a rádio e os sons ambientes
        if (newState != _currentState)
        {
            _currentState = newState;
            UpdateAmbient(_currentState);
            ChangeMusic(_currentState);
        }

        // Caso uma música termine e não haja silêncio programado ativado, agenda a próxima trilha
        if (!_musicSource.isPlaying && !_isInInterval && !_isMusicMuted) 
        {
            if (_waitNextCoroutine != null) StopCoroutine(_waitNextCoroutine);
            _waitNextCoroutine = StartCoroutine(WaitAndPlayNext(_currentState));
        }
    }
    #endregion

    #region Lógica de Estado e Seleção Musical
    /// <summary>
    /// Consulta o GameState para determinar qual é o clima musical necessário no momento.
    /// Prioriza sempre situações de urgência como a Batalha e a Perseguição.
    /// </summary>
    private MusicState ResolveState()
    {
        if (!GameState.IsGameStarted)  return MusicState.Menu;
        if (GameState.IsInBattle)      return MusicState.Battle;
        if (GameState.IsBeingChased)   return MusicState.Chase;
        if (!GameState.IsOnWater)      return MusicState.Land;
        return MusicState.Exploration;
    }

    /// <summary>
    /// Escolhe a próxima música garantindo que ela não toque novamente até que toda
    /// a playlist deste estado específico tenha sido percorrida (Sistema de "Saco" parecido com Tetris).
    /// </summary>
    private void PlayNext(MusicState state)
    {
        List<AudioClip> musicList = _playlistsDictionary[state];
        if (musicList == null || musicList.Count == 0) return;

        if (_remainingIndexesDictionary[state].Count == 0)
            ResetIndexes(state);

        int randomIndex = Random.Range(0, _remainingIndexesDictionary[state].Count);
        int index = _remainingIndexesDictionary[state][randomIndex];
        _remainingIndexesDictionary[state].RemoveAt(randomIndex);

        _musicSource.clip = musicList[index];
        _musicSource.volume = _musicVolume;
        _musicSource.Play();
    }

    private void ResetIndexes(MusicState state)
    {
        List<int> indexes = new();
        for (int i = 0; i < _playlistsDictionary[state].Count; i++)
            indexes.Add(i);
        _remainingIndexesDictionary[state] = indexes;
    }
    #endregion

    #region Gerenciamento de Fades (Transições Suaves)
    private void UpdateAmbient(MusicState state)
    {
        bool shouldPlay = state != MusicState.Battle;

        if (shouldPlay && !_ambientSource.isPlaying)
        {
            _ambientSource.Play();
            if (_ambientFadeCoroutine != null) StopCoroutine(_ambientFadeCoroutine);
            _ambientFadeCoroutine = StartCoroutine(FadeAmbient(_ambientVolume));
        }
        else if (!shouldPlay && _ambientSource.isPlaying)
        {
            if (_ambientFadeCoroutine != null) StopCoroutine(_ambientFadeCoroutine);
            _ambientFadeCoroutine = StartCoroutine(FadeAmbient(0f, true));
        }
    }

    private IEnumerator FadeAmbient(float targetVolume, bool stopAfter = false)
    {
        float initialVolume = _ambientSource.volume;
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _ambientSource.volume = Mathf.Lerp(initialVolume, targetVolume, elapsed / _fadeDuration);
            yield return null;
        }
        _ambientSource.volume = targetVolume;
        if (stopAfter) _ambientSource.Stop();
    }

    private void ChangeMusic(MusicState newState)
    {
        _isInInterval = false;
        if (_waitNextCoroutine != null) StopCoroutine(_waitNextCoroutine);
        if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
        _musicFadeCoroutine = StartCoroutine(FadeToNewState(newState));
    }

    private IEnumerator FadeToNewState(MusicState newState)
    {
        // Fade out só se estiver tocando
        if (_musicSource.isPlaying)
        {
            float initialVolume = _musicSource.volume;
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                _musicSource.volume = Mathf.Lerp(initialVolume, 0f, elapsed / _fadeDuration);
                yield return null;
            }
            _musicSource.Stop();
        }

        _musicSource.volume = 0f;
        PlayNext(newState);

        // Fade in da nova música
        float elapsedIn = 0f;
        while (elapsedIn < _fadeDuration)
        {
            elapsedIn += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(0f, _musicVolume, elapsedIn / _fadeDuration);
            yield return null;
        }
        _musicSource.volume = _musicVolume;
    }

    private IEnumerator WaitAndPlayNext(MusicState state)
    {
        _isInInterval = true;
        float waitTime = Random.Range(_minInterval, _maxInterval);
        yield return new WaitForSeconds(waitTime);
        _isInInterval = false;

        if (_currentState == state)
            PlayNext(state);
    }

    public IEnumerator FadeOutMusic()
    {
        _isMusicMuted = true;
        if (_waitNextCoroutine != null) StopCoroutine(_waitNextCoroutine);
        _isInInterval = false;

        float initialVolume = _musicSource.volume;
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(initialVolume, 0f, elapsed / _fadeDuration);
            yield return null;
        }
        _musicSource.Stop();
        _musicSource.volume = 0f;
    }

    public void ResumeMusic()
    {
        _isMusicMuted = false;
        ChangeMusic(_currentState);
    }
    #endregion
}