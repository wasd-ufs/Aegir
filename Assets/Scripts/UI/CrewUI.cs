using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CrewUI : MonoBehaviour
{
    public Sprite captainHP, boatHP, startHPBar, bodyHP, endHPBar;
    private float cHP, bHP;
    private List<float> unitsHP = new List<float>();
    public Vector3Int startCoordinates, segmentDistances;
    public int heartDistance, verticalDistance, vidaPorCoração, vidaPorSegmento;
    public GameObject image, player;

    [Header("Canvas Root")]
    [Tooltip("Container dentro do Canvas onde todos os elementos de UI serão instanciados.")]
    public RectTransform canvasRoot;

    [Header("Containers")]
    public RectTransform captainContainer;
    public RectTransform boatContainer;
    public RectTransform crewContainer;

    [Header("Texto de HP")]
    public GameObject textoPrefab;
    public Vector2 textoOffset;

    [Header("Modo Inimigo")]
    public bool modoInimigo = false;
    private CrewData crewInimigo;

    private float cMaxHP, bMaxHP;
    private List<float> unitsMaxHP = new List<float>();
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private float lastCHP, lastBHP;
    private List<float> lastUnitsHP = new List<float>();
    private bool isValid = false;
    private bool inicializado = false;

    void Start()
    {
        if (modoInimigo) return;
        isValid = Validate();
        if (isValid) StartCoroutine(LateStart());
    }

    public void LimparUI()
    {
        ClearSpawned();
        crewInimigo = null;
        isValid = false;
        inicializado = false;
    }

    public void ReativarComoPlayer()
    {
        isValid = true;
    }

    public void InicializarComoInimigo(CrewData crew)
    {
        ClearSpawned();
        inicializado = false;
        crewInimigo  = crew;
        modoInimigo  = true;
        isValid      = true;
        StartCoroutine(LateStart());
    }

    private IEnumerator LateStart()
    {
        yield return null;
        ClearSpawned();
        FetchHP();
        CacheHP();
        InstantiateHP();
        inicializado = true;
    }

    void Update()
    {
        if (modoInimigo && !GameState.IsInBattle)
        {
            if (spawnedObjects.Count > 0) LimparUI(); 
            foreach(GameObject spawned in spawnedObjects)
                spawned.SetActive(false);
            return;
        }
        if (!isValid || !inicializado) return;
        FetchHP();
        if (HPChanged())
        {
            CacheHP();
            ClearSpawned();
            InstantiateHP();
        }
    }

    private bool Validate()
    {
        bool ok = true;
        if (canvasRoot == null)  { Debug.LogError("[CrewUI] 'canvasRoot' não atribuído.", this);      ok = false; }
        if (image == null)       { Debug.LogError("[CrewUI] 'image' não atribuído.", this);           ok = false; }
        if (player == null)      { Debug.LogError("[CrewUI] 'player' não atribuído.", this);          ok = false; }
        if (captainHP == null)   { Debug.LogError("[CrewUI] Sprite 'captainHP' não atribuído.", this); ok = false; }
        if (boatHP == null)      { Debug.LogError("[CrewUI] Sprite 'boatHP' não atribuído.", this);    ok = false; }
        if (startHPBar == null)  { Debug.LogError("[CrewUI] Sprite 'startHPBar' não atribuído.", this);ok = false; }
        if (bodyHP == null)      { Debug.LogError("[CrewUI] Sprite 'bodyHP' não atribuído.", this);    ok = false; }
        if (endHPBar == null)    { Debug.LogError("[CrewUI] Sprite 'endHPBar' não atribuído.", this);  ok = false; }
        if (heartDistance <= 0)      { Debug.LogError("[CrewUI] 'heartDistance' precisa ser maior que zero.", this);       ok = false; }
        if (vidaPorCoração <= 0)     { Debug.LogError("[CrewUI] 'vidaPorCoração' precisa ser maior que zero.", this);     ok = false; }
        if (vidaPorSegmento <= 0)   { Debug.LogError("[CrewUI] 'vidaPorSegmento' precisa ser maior que zero.", this);   ok = false; }
        if (segmentDistances.y <= 0) { Debug.LogError("[CrewUI] 'segmentDistances.Y' precisa ser maior que zero.", this); ok = false; }
        if (player != null && player.GetComponent<CrewData>() == null)
            { Debug.LogError("[CrewUI] 'player' não tem CrewData.", this); ok = false; }
        if (image != null)
        {
            if (image.GetComponent<RectTransform>() == null) { Debug.LogError("[CrewUI] Prefab 'image' não tem RectTransform.", this); ok = false; }
            if (image.GetComponent<Image>() == null)         { Debug.LogError("[CrewUI] Prefab 'image' não tem Image.", this);         ok = false; }
        }

        return ok;
    }

    private void FetchHP()
    {
        CrewData crew = modoInimigo ? crewInimigo : player.GetComponent<CrewData>();
        if (crew == null || crew.crew == null || crew.crew.Count == 0)
        {
            Debug.LogWarning("[CrewUI] Crew vazia, aguardando...");
            return;
        }

        float newCHP = 0f, newCMaxHP = 0f;
        float newBHP = 0f, newBMaxHP = 0f;
        List<float> newUnitsHP    = new();
        List<float> newUnitsMaxHP = new();

        foreach (GameObject membro in crew.crew)
        {
            if (membro == null) continue;
            if (membro.GetComponent<NPCsData>().isAlive == false) continue;

            NPCsData npc = membro.GetComponent<NPCsData>();
            if (npc == null) continue;

            switch (npc.creatureClass)
            {
                case NPCsData.Class.Capitão:
                    newCHP = npc.GetVidaAtual(); newCMaxHP = npc.GetVidaMaxima(); break;
                case NPCsData.Class.Barco:
                    newBHP = npc.GetVidaAtual(); newBMaxHP = npc.GetVidaMaxima(); break;
                default:
                    newUnitsHP.Add(npc.GetVidaAtual());
                    newUnitsMaxHP.Add(npc.GetVidaMaxima());
                    break;
            }
        }

        cHP = newCHP; cMaxHP = newCMaxHP;
        bHP = newBHP; bMaxHP = newBMaxHP;
        unitsHP    = newUnitsHP;
        unitsMaxHP = newUnitsMaxHP;
    }

    private bool HPChanged()
    {
        if (!Mathf.Approximately(cHP, lastCHP)) return true;
        if (!Mathf.Approximately(bHP, lastBHP)) return true;
        if (unitsHP.Count != lastUnitsHP.Count) return true;
        for (int i = 0; i < unitsHP.Count; i++)
            if (!Mathf.Approximately(unitsHP[i], lastUnitsHP[i])) return true;
        return false;
    }

    private void CacheHP()
    {
        lastCHP = cHP; lastBHP = bHP;
        lastUnitsHP = new List<float>(unitsHP);
    }

    private void ClearSpawned()
    {
        foreach (GameObject go in spawnedObjects)
            if (go != null) Destroy(go);
        spawnedObjects.Clear();
    }

    private void InstantiateHP()
    {
        int linhaAtual = 0;

        if (cMaxHP > 0)
        {
            DrawHearts(cHP, cMaxHP, linhaAtual, captainHP, captainContainer);
            linhaAtual++;
        }

        if (bMaxHP > 0)
        {
            DrawHearts(bHP, bMaxHP, linhaAtual, boatHP, boatContainer);
            linhaAtual++;
        }

        for (int i = 0; i < unitsHP.Count; i++)
        {
            DrawCrewBar(unitsHP[i], unitsMaxHP[i], linhaAtual, crewContainer);
            linhaAtual++;
        }
    }

    private void DrawHearts(float hp, float maxHP, int row, Sprite sprite, RectTransform container)
    {
        if (hp <= 0 && maxHP <= 0) return;

        RectTransform parent = ResolveParent(container);
        float yOffset = -row * verticalDistance;

        int índice = 0;
        for (float v = 0; v < hp; v += vidaPorCoração)
        {
            float xPixel = índice * heartDistance;
            Spawn(sprite, new Vector2(modoInimigo ? -xPixel : xPixel, yOffset), parent);
            índice++;
        }

        // Posição do texto: logo após o último coração
        float últimoPx = (índice > 0 ? índice - 1 : 0) * heartDistance;
        float textoX   = últimoPx + heartDistance;
        SpawnText(hp, maxHP, new Vector2(modoInimigo ? -(textoX) : textoX, yOffset), parent);
    }

    private void DrawCrewBar(float hp, float maxHP, int row, RectTransform container)
    {
        RectTransform parent = ResolveParent(container);
        float yOffset = -row * verticalDistance;

        // hp itera em unidades de HP (vidaPorSegmento)
        // posição em pixels usa segmentDistances (x=offset inicial, y=largura por segmento, z=offset final)
        int índice = 0;

        if (modoInimigo)
        {
            Spawn(endHPBar, new Vector2(0, yOffset), parent);

            for (float v = 0; v < hp; v += vidaPorSegmento)
            {
                float xPixel = índice * segmentDistances.y + segmentDistances.x;
                Spawn(bodyHP, new Vector2(-xPixel, yOffset), parent);
                índice++;
            }

            float endX = (índice > 0 ? índice - 1 : 0) * segmentDistances.y + segmentDistances.x + segmentDistances.z;
            Spawn(startHPBar, new Vector2(-endX, yOffset), parent);
            SpawnText(hp, maxHP, new Vector2(-(endX + segmentDistances.z), yOffset), parent);
        }
        else
        {
            Spawn(startHPBar, new Vector2(0, yOffset), parent);

            for (float v = 0; v < hp; v += vidaPorSegmento)
            {
                float xPixel = índice * segmentDistances.y + segmentDistances.x;
                Spawn(bodyHP, new Vector2(xPixel, yOffset), parent);
                índice++;
            }

            float endX = (índice > 0 ? índice - 1 : 0) * segmentDistances.y + segmentDistances.x + segmentDistances.z;
            Spawn(endHPBar, new Vector2(endX, yOffset), parent);
            SpawnText(hp, maxHP, new Vector2(endX + segmentDistances.z, yOffset), parent);
        }
    }

    private RectTransform ResolveParent(RectTransform container)
    {
        if (container != null) return container;
        if (canvasRoot != null) return canvasRoot;
        return (RectTransform)transform;
    }

    private void Spawn(Sprite sprite, Vector2 localOffset, RectTransform parent)
    {
        GameObject go = Instantiate(image, ResolveParent(parent));
        go.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            startCoordinates.x + localOffset.x,
            startCoordinates.y + localOffset.y
        );
        go.GetComponent<Image>().sprite = sprite;
        spawnedObjects.Add(go);
    }

    private void SpawnText(float hp, float maxHP, Vector2 localOffset, RectTransform parent)
    {
        if (textoPrefab == null) return;
        GameObject go = Instantiate(textoPrefab, ResolveParent(parent));
        go.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            startCoordinates.x + localOffset.x + textoOffset.x,
            startCoordinates.y + localOffset.y + textoOffset.y
        );
        go.GetComponent<TextMeshProUGUI>().text = $"{(int)hp}/{(int)maxHP}";
        spawnedObjects.Add(go);
    }
}