using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Transição estilo Game Boy com dois modos sorteados aleatoriamente a cada chamada.
/// </summary>
public class GameBoyTransition : MonoBehaviour
{
    #region Referências e Configurações
    [Header("Referências")]
    [Tooltip("Panel que cobre a tela toda — serve de container para as barras")]
    [SerializeField] private RectTransform _transitionContainer;

    [Header("Configuração das Barras")]
    [Range(2, 30)]
    [SerializeField] private int _barCount = 8;

    [SerializeField] private Color _barColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Header("Timing")]
    [SerializeField] private float _closeDuration = 0.5f;
    [SerializeField] private float _openDuration  = 0.5f;

    [Tooltip("Pausa com a tela completamente fechada")]
    [SerializeField] private float _holdDuration  = 0.15f;

    [Tooltip("Atraso escalonado entre cada barra")]
    [Range(0f, 0.1f)]
    [SerializeField] private float _staggerDelay  = 0.03f;

    [Header("Curva de Animação")]
    [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Eventos e Sons")]
    [Tooltip("Chamado quando a tela está totalmente coberta")]
    [SerializeField] private UnityEvent _onMidpoint;

    [Tooltip("Chamado quando a transição termina por completo")]
    [SerializeField] private UnityEvent _onComplete;

    [SerializeField] private AudioClip _openingSound;
    
    private AudioSource _audioSource;

    private enum TransitionMode
    {
        Encounter,
        Venetian
    }

    private RectTransform[] _leftBarsArray;
    private RectTransform[] _rightBarsArray;
    private RectTransform[] _fullBarsArray;

    private float _screenWidth;
    private float _screenHeight;
    private float _barHeight;

    private bool _isTransitioning = false;
    #endregion

    #region Inicialização
    private void Awake()
    {
        if (_transitionContainer == null)
        {
            Debug.LogError("[GameBoyTransition] Atribua o transitionContainer no Inspector!");
            return;
        }

        _transitionContainer.gameObject.SetActive(false);

        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
    }
    #endregion

    #region API Pública e Corrotinas
    public void StartTransition(Action onMidpointCallback = null, Action onCompleteCallback = null)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("[GameBoyTransition] Transição já em andamento!");
            return;
        }

        TransitionMode mode = (UnityEngine.Random.value < 0.5f)
            ? TransitionMode.Encounter
            : TransitionMode.Venetian;

        StartCoroutine(RunTransition(mode, onMidpointCallback, onCompleteCallback));
    }

    public void StartTransition() => StartTransition(null, null);

    private IEnumerator RunTransition(TransitionMode mode, Action onMidpointCallback, Action onCompleteCallback)
    {
        _isTransitioning = true;

        MeasureScreen();

        if (_openingSound != null)
            _audioSource.PlayOneShot(_openingSound);

        _transitionContainer.gameObject.SetActive(true);

        if (mode == TransitionMode.Encounter)
        {
            BuildEncounterBars();
            yield return AnimateEncounter(closing: true);
        }
        else
        {
            BuildVenetianBars();
            yield return AnimateVenetian(closing: true);
        }

        _onMidpoint?.Invoke();
        onMidpointCallback?.Invoke();

        yield return new WaitForSeconds(_holdDuration);

        if (mode == TransitionMode.Encounter)
            yield return AnimateEncounter(closing: false);
        else
            yield return AnimateVenetian(closing: false);

        DestroyBars();

        _transitionContainer.gameObject.SetActive(false);

        _isTransitioning = false;
        
        _onComplete?.Invoke();
        onCompleteCallback?.Invoke();
    }
    #endregion

    #region Modo A - Encontro
    private void BuildEncounterBars()
    {
        _leftBarsArray  = new RectTransform[_barCount];
        _rightBarsArray = new RectTransform[_barCount];

        float halfBarWidth = _screenWidth / 4f;

        for (int i = 0; i < _barCount; i++)
        {
            float yCenter = (i + 0.5f) * _barHeight;

            _leftBarsArray[i]  = CreateBar("EL_" + i, new Vector2(-halfBarWidth,                yCenter), _screenWidth / 2f + 1f, _barHeight);
            _rightBarsArray[i] = CreateBar("ER_" + i, new Vector2(_screenWidth + halfBarWidth,  yCenter), _screenWidth / 2f + 1f, _barHeight);
        }
    }

    private IEnumerator AnimateEncounter(bool closing)
    {
        float duration      = closing ? _closeDuration : _openDuration;
        float totalDuration = duration + _staggerDelay * (_barCount - 1);
        float elapsed       = 0f;

        float halfBarWidth = _screenWidth / 4f;

        float leftOutPosition  = -halfBarWidth;
        float leftInPosition   =  halfBarWidth;

        float rightOutPosition =  _screenWidth + halfBarWidth;
        float rightInPosition  =  _screenWidth - halfBarWidth;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < _barCount; i++)
            {
                float barElapsed = Mathf.Clamp(elapsed - i * _staggerDelay, 0f, duration);
                float t          = _easeCurve.Evaluate(barElapsed / duration);
                float yCenter    = (i + 0.5f) * _barHeight;

                float leftPosition  = closing ? Mathf.Lerp(leftOutPosition,  leftInPosition,   t) : Mathf.Lerp(leftInPosition,  leftOutPosition,  t);
                float rightPosition = closing ? Mathf.Lerp(rightOutPosition, rightInPosition,  t) : Mathf.Lerp(rightInPosition, rightOutPosition, t);

                _leftBarsArray[i].anchoredPosition  = new Vector2(leftPosition,  yCenter);
                _rightBarsArray[i].anchoredPosition = new Vector2(rightPosition, yCenter);
            }

            yield return null;
        }

        for (int i = 0; i < _barCount; i++)
        {
            float yCenter = (i + 0.5f) * _barHeight;

            _leftBarsArray[i].anchoredPosition  = new Vector2(closing ? leftInPosition  : leftOutPosition,  yCenter);
            _rightBarsArray[i].anchoredPosition = new Vector2(closing ? rightInPosition : rightOutPosition, yCenter);
        }
    }
    #endregion

    #region Modo B - Veneziana
    private void BuildVenetianBars()
    {
        _fullBarsArray = new RectTransform[_barCount];

        for (int i = 0; i < _barCount; i++)
        {
            float yCenter = (i + 0.5f) * _barHeight;
            float startPositionX  = GetVenetianOutPosition(i, entering: true);

            _fullBarsArray[i] = CreateBar(
                "VB_" + i,
                new Vector2(startPositionX, yCenter),
                _screenWidth + 2f,
                _barHeight
            );
        }
    }

    private IEnumerator AnimateVenetian(bool closing)
    {
        float duration      = closing ? _closeDuration : _openDuration;
        float totalDuration = duration + _staggerDelay * (_barCount - 1);
        float elapsed       = 0f;

        float centerPositionX = _screenWidth / 2f; 

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < _barCount; i++)
            {
                float barElapsed = Mathf.Clamp(elapsed - i * _staggerDelay, 0f, duration);
                float t          = _easeCurve.Evaluate(barElapsed / duration);
                float yCenter    = (i + 0.5f) * _barHeight;

                float fromPositionX = closing ? GetVenetianOutPosition(i, entering: true)  : centerPositionX;
                float toPositionX   = closing ? centerPositionX                              : GetVenetianOutPosition(i, entering: false);

                _fullBarsArray[i].anchoredPosition = new Vector2(Mathf.Lerp(fromPositionX, toPositionX, t), yCenter);
            }

            yield return null;
        }

        for (int i = 0; i < _barCount; i++)
        {
            float yCenter = (i + 0.5f) * _barHeight;
            float endPositionX = closing ? centerPositionX : GetVenetianOutPosition(i, entering: false);

            _fullBarsArray[i].anchoredPosition = new Vector2(endPositionX, yCenter);
        }
    }

    private float GetVenetianOutPosition(int index, bool entering)
    {
        bool goesRight = (index % 2 == 0);

        float offscreenPosition = _screenWidth / 2f + 1f; 

        if (entering)
            return goesRight ? _screenWidth + offscreenPosition : -offscreenPosition;
        else
            return goesRight ? -offscreenPosition : _screenWidth + offscreenPosition;
    }
    #endregion

    #region Helpers
    private void MeasureScreen()
    {
        RectTransform canvasRect = _transitionContainer.GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        _screenWidth  = canvasRect.rect.width;
        _screenHeight = canvasRect.rect.height;
        _barHeight    = _screenHeight / _barCount;
    }

    private RectTransform CreateBar(string barName, Vector2 startPosition, float width, float height)
    {
        GameObject gameObjectInstance = new GameObject(barName, typeof(RectTransform), typeof(Image));

        gameObjectInstance.transform.SetParent(_transitionContainer, false);

        Image imageComponent = gameObjectInstance.GetComponent<Image>();

        imageComponent.color         = _barColor;
        imageComponent.raycastTarget = false;

        RectTransform rectTransform = gameObjectInstance.GetComponent<RectTransform>();

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot     = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = startPosition;

        return rectTransform;
    }

    private void DestroyBars()
    {
        if (_leftBarsArray  != null) foreach (RectTransform bar in _leftBarsArray)  if (bar) Destroy(bar.gameObject);
        if (_rightBarsArray != null) foreach (RectTransform bar in _rightBarsArray) if (bar) Destroy(bar.gameObject);
        if (_fullBarsArray  != null) foreach (RectTransform bar in _fullBarsArray)  if (bar) Destroy(bar.gameObject);

        _leftBarsArray = _rightBarsArray = _fullBarsArray = null;
    }
    #endregion
}