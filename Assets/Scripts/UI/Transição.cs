using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Transição estilo Game Boy com dois modos sorteados aleatoriamente a cada chamada:
///
/// MODO A — "Encontro": barras vêm da esquerda E da direita e se encontram no centro.
/// MODO B — "Veneziana": cada barra cobre a tela inteira, alternando direção
///           (par → direita, ímpar → esquerda), com stagger entre elas.
///
/// SETUP:
/// 1. Canvas (Screen Space - Overlay, Sort Order alto, ex: 100)
/// 2. GameObject filho do Canvas com este script
/// 3. Panel filho do Canvas (stretch em tudo) → arraste em transitionContainer
///    (remova ou zere o alpha da Image do Panel)
/// 4. Configure onMidpoint no Inspector para ativar seu BattleScene
/// </summary>
public class GameBoyTransition : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Panel que cobre a tela toda — serve de container para as barras")]
    public RectTransform transitionContainer;

    [Header("Configuração das Barras")]
    [Range(2, 30)]
    public int barCount = 8;
    public Color barColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    [Header("Timing")]
    public float closeDuration = 0.5f;
    public float openDuration  = 0.5f;

    [Tooltip("Pausa com a tela completamente fechada")]
    public float holdDuration  = 0.15f;

    [Tooltip("Atraso escalonado entre cada barra")]
    [Range(0f, 0.1f)]
    public float staggerDelay  = 0.03f;

    [Header("Curva de Animação")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Eventos")]
    [Tooltip("Chamado quando a tela está totalmente coberta")]
    public UnityEvent onMidpoint;

    [Tooltip("Chamado quando a transição termina por completo")]
    public UnityEvent onComplete;

    // -----------------------------------------------------------------------

    private enum TransitionMode { Encontro, Veneziana }

    // Barras do modo Encontro (esquerda + direita por faixa)
    private RectTransform[] leftBars;
    private RectTransform[] rightBars;

    // Barras do modo Veneziana (uma barra larga por faixa)
    private RectTransform[] fullBars;

    private float screenWidth, screenHeight, barHeight;
    private bool isTransitioning = false;

    [Header("Sons")]
    public AudioClip somAbertura;
    private AudioSource audioSource;

    public void StartTransition(Action onMidpointCallback = null, Action onCompleteCallback = null)
    {
        if (isTransitioning)
        {
            Debug.LogWarning("[GameBoyTransition] Transição já em andamento!");
            return;
        }

        TransitionMode mode = (UnityEngine.Random.value < 0.5f)
            ? TransitionMode.Encontro
            : TransitionMode.Veneziana;

        StartCoroutine(RunTransition(mode, onMidpointCallback, onCompleteCallback));
    }

    // Versão sem parâmetros — para botões e UnityEvents no Inspector
    public void StartTransition() => StartTransition(null, null);

    private void Awake()
    {
        if (transitionContainer == null)
        {
            Debug.LogError("[GameBoyTransition] Atribua o transitionContainer no Inspector!");
            return;
        }
        transitionContainer.gameObject.SetActive(false);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // Corrotina Principal

    private IEnumerator RunTransition(TransitionMode mode, Action onMidpointCallback, Action onCompleteCallback)
    {
        isTransitioning = true;

        MeasureScreen();

        if (somAbertura != null) audioSource.PlayOneShot(somAbertura);

        transitionContainer.gameObject.SetActive(true);

        if (mode == TransitionMode.Encontro)
        {
            BuildBarsEncontro();
            yield return AnimateEncontro(closing: true);
        }
        else
        {
            BuildBarsVeneziana();
            yield return AnimateVeneziana(closing: true);
        }

        // Tela totalmente coberta
        onMidpoint?.Invoke();
        onMidpointCallback?.Invoke();

        yield return new WaitForSeconds(holdDuration);

        if (mode == TransitionMode.Encontro)
            yield return AnimateEncontro(closing: false);
        else
            yield return AnimateVeneziana(closing: false);

        DestroyBars();
        transitionContainer.gameObject.SetActive(false);

        isTransitioning = false;

        onComplete?.Invoke();
        onCompleteCallback?.Invoke();
    }

    // Modo A - Encontro
    // Barras vêm da esquerda e da direita, se encontram no centro.


    private void BuildBarsEncontro()
    {
        leftBars  = new RectTransform[barCount];
        rightBars = new RectTransform[barCount];

        float halfBarW = screenWidth / 4f;

        for (int i = 0; i < barCount; i++)
        {
            float yCenter = (i + 0.5f) * barHeight;
            leftBars[i]  = CreateBar("EL_" + i, new Vector2(-halfBarW,              yCenter), screenWidth / 2f + 1f, barHeight);
            rightBars[i] = CreateBar("ER_" + i, new Vector2(screenWidth + halfBarW, yCenter), screenWidth / 2f + 1f, barHeight);
        }
    }

    private IEnumerator AnimateEncontro(bool closing)
    {
        float duration      = closing ? closeDuration : openDuration;
        float totalDuration = duration + staggerDelay * (barCount - 1);
        float elapsed       = 0f;

        float halfBarW = screenWidth / 4f;
        float leftOut  = -halfBarW;
        float leftIn   =  halfBarW;
        float rightOut =  screenWidth + halfBarW;
        float rightIn  =  screenWidth - halfBarW;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < barCount; i++)
            {
                float barElapsed = Mathf.Clamp(elapsed - i * staggerDelay, 0f, duration);
                float t          = easeCurve.Evaluate(barElapsed / duration);
                float yCenter    = (i + 0.5f) * barHeight;

                float leftX  = closing ? Mathf.Lerp(leftOut,  leftIn,   t) : Mathf.Lerp(leftIn,  leftOut,  t);
                float rightX = closing ? Mathf.Lerp(rightOut, rightIn,  t) : Mathf.Lerp(rightIn, rightOut, t);

                leftBars[i].anchoredPosition  = new Vector2(leftX,  yCenter);
                rightBars[i].anchoredPosition = new Vector2(rightX, yCenter);
            }

            yield return null;
        }

        for (int i = 0; i < barCount; i++)
        {
            float yCenter = (i + 0.5f) * barHeight;
            leftBars[i].anchoredPosition  = new Vector2(closing ? leftIn  : leftOut,  yCenter);
            rightBars[i].anchoredPosition = new Vector2(closing ? rightIn : rightOut, yCenter);
        }
    }


    // Modo B - Veneziana
    // Cada barra cobre a tela inteira e desliza horizontalmente.
    // Pares entram pela direita, ímpares pela esquerda (alternado).

    private void BuildBarsVeneziana()
    {
        fullBars = new RectTransform[barCount];

        for (int i = 0; i < barCount; i++)
        {
            float yCenter = (i + 0.5f) * barHeight;
            float startX  = VenezianaOutX(i, entering: true);
            fullBars[i]   = CreateBar("VB_" + i, new Vector2(startX, yCenter), screenWidth + 2f, barHeight);
        }
    }

    private IEnumerator AnimateVeneziana(bool closing)
    {
        float duration      = closing ? closeDuration : openDuration;
        float totalDuration = duration + staggerDelay * (barCount - 1);
        float elapsed       = 0f;

        float centerX = screenWidth / 2f; // pivot no centro da barra larga

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < barCount; i++)
            {
                float barElapsed = Mathf.Clamp(elapsed - i * staggerDelay, 0f, duration);
                float t          = easeCurve.Evaluate(barElapsed / duration);
                float yCenter    = (i + 0.5f) * barHeight;

                float fromX = closing ? VenezianaOutX(i, entering: true)  : centerX;
                float toX   = closing ? centerX                            : VenezianaOutX(i, entering: false);

                fullBars[i].anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, t), yCenter);
            }

            yield return null;
        }

        for (int i = 0; i < barCount; i++)
        {
            float yCenter = (i + 0.5f) * barHeight;
            float endX    = closing ? centerX : VenezianaOutX(i, entering: false);
            fullBars[i].anchoredPosition = new Vector2(endX, yCenter);
        }
    }

    private float VenezianaOutX(int i, bool entering)
    {
        bool goesRight = (i % 2 == 0);
        float offscreen = screenWidth / 2f + 1f; // distância do pivot ao lado da tela

        if (entering)
            return goesRight ? screenWidth + offscreen : -offscreen;
        else
            return goesRight ? -offscreen : screenWidth + offscreen;
    }


    // Helpers

    private void MeasureScreen()
    {
        var canvasRect = transitionContainer.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        screenWidth  = canvasRect.rect.width;
        screenHeight = canvasRect.rect.height;
        barHeight    = screenHeight / barCount;
    }

    private RectTransform CreateBar(string barName, Vector2 startPos, float width, float height)
    {
        var go  = new GameObject(barName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transitionContainer, false);

        var img = go.GetComponent<Image>();
        img.color         = barColor;
        img.raycastTarget = false;

        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = startPos;

        return rt;
    }

    private void DestroyBars()
    {
        if (leftBars  != null) foreach (var b in leftBars)  if (b) Destroy(b.gameObject);
        if (rightBars != null) foreach (var b in rightBars) if (b) Destroy(b.gameObject);
        if (fullBars  != null) foreach (var b in fullBars)  if (b) Destroy(b.gameObject);
        leftBars = rightBars = fullBars = null;
    }

#if UNITY_EDITOR
    [ContextMenu("Testar Transição (Play Mode)")]
    private void TestTransition() => StartTransition();
#endif
}