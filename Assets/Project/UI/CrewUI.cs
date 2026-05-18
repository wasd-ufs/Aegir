using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gere a interface visual da tripulação (aliada e inimiga).
/// Renderiza dinamicamente as barras de vida baseadas em corações (Capitão/Barco) 
/// ou segmentos (NPCs) no Canvas.
/// </summary>
public class CrewUI : MonoBehaviour
{
    #region Configurações de Sprites
    [Header("Sprites de Interface")]
    [SerializeField] private Sprite _captainHPSprite;
    [SerializeField] private Sprite _boatHPSprite;
    [SerializeField] private Sprite _startHPBarSprite;
    [SerializeField] private Sprite _bodyHPBarSprite;
    [SerializeField] private Sprite _endHPBarSprite;
    #endregion

    #region Layout e Posicionamento
    [Header("Configurações de Layout")]
    [SerializeField] private Vector3Int _startCoordinates;
    [SerializeField] private Vector3Int _segmentDistances;
    [SerializeField] private int _heartDistance;
    [SerializeField] private int _verticalDistance;
    [SerializeField] private int _healthPerHeart;
    [SerializeField] private int _healthPerSegment;
    #endregion

    #region Referências de Objetos
    [Header("Referências")]
    [SerializeField] private GameObject _imagePrefab;
    [SerializeField] private GameObject _player;
    
    [Header("Canvas Root")]
    [Tooltip("Container dentro do Canvas onde todos os elementos de UI serão instanciados.")]
    [SerializeField] private RectTransform _canvasRoot;

    [Header("Containers")]
    [SerializeField] private RectTransform _captainContainer;
    [SerializeField] private RectTransform _boatContainer;
    [SerializeField] private RectTransform _crewContainer;

    [Header("Texto de HP")]
    [SerializeField] private GameObject _textPrefab;
    [SerializeField] private Vector2 _textOffset;
    #endregion

    #region Estado Interno
    [Header("Modo Inimigo")]
    [SerializeField] private bool _isEnemyMode = false;

    private CrewData _enemyCrewData;

    private float _captainHP;
    private float _boatHP;
    private float _captainMaxHP;
    private float _boatMaxHP;

    private List<float> _unitsHPList = new List<float>();
    private List<float> _unitsMaxHPList = new List<float>();
    
    private List<GameObject> _spawnedObjectsList = new List<GameObject>();

    private float _lastCaptainHP;
    private float _lastBoatHP;

    private List<float> _lastUnitsHPList = new List<float>();
    
    private bool _isValid = false;
    private bool _isInitialized = false;
    #endregion

    #region Ciclo de Vida (Unity)
    void Start()
    {
        if (_isEnemyMode) return;

        _isValid = IsValid();

        if (_isValid)
            StartCoroutine(LateStart());
    }

    void Update()
    {
        if (_isEnemyMode && !GameState.IsInBattle)
        {
            if (_spawnedObjectsList.Count > 0)
                ClearUI();

            foreach(GameObject spawnedObject in _spawnedObjectsList)
                spawnedObject.SetActive(false);

            return;
        }
        
        if (!_isValid || !_isInitialized) return;
        
        FetchHP();

        if (HasHPChanged())
        {
            CacheHP();
            ClearSpawned();
            InstantiateHP();
        }
    }
    #endregion

    #region API Pública
    /// <summary>
    /// Limpa todos os elementos visuais instanciados e reseta o estado do componente.
    /// </summary>
    public void ClearUI()
    {
        ClearSpawned();
        _enemyCrewData = null;
        _isValid = false;
        _isInitialized = false;
    }

    /// <summary>
    /// Reativa a validação da UI para voltar a funcionar como tripulação do jogador.
    /// Chamado pelo BattleData no fim do combate.
    /// </summary>
    public void ReactivateAsPlayer()
    {
        _isValid = true;
    }

    /// <summary>
    /// Configura a UI para monitorar e exibir a tripulação inimiga durante o combate.
    /// </summary>
    public void InitializeAsEnemy(CrewData crew)
    {
        ClearSpawned();
        _isInitialized = false;
        _enemyCrewData = crew;
        _isEnemyMode = true;
        _isValid = true;
        StartCoroutine(LateStart());
    }
    #endregion

    #region Helpers de Inicialização
    private IEnumerator LateStart()
    {
        yield return null;
        ClearSpawned();
        FetchHP();
        CacheHP();
        InstantiateHP();
        _isInitialized = true;
    }

    /// <summary>
    /// Verifica se todas as referências necessárias foram preenchidas no Inspector.
    /// </summary>
    private bool IsValid()
    {
        bool isValidConfiguration = true;

        if (_canvasRoot == null)  { Debug.LogError("[CrewUI] 'canvasRoot' não atribuído.", this);      isValidConfiguration = false; }
        if (_imagePrefab == null) { Debug.LogError("[CrewUI] 'image' não atribuído.", this);           isValidConfiguration = false; }
        if (_player == null)      { Debug.LogError("[CrewUI] 'player' não atribuído.", this);          isValidConfiguration = false; }

        if (_captainHPSprite == null)  { Debug.LogError("[CrewUI] Sprite 'captainHP' não atribuído.", this); isValidConfiguration = false; }
        if (_boatHPSprite == null)     { Debug.LogError("[CrewUI] Sprite 'boatHP' não atribuído.", this);    isValidConfiguration = false; }
        if (_startHPBarSprite == null) { Debug.LogError("[CrewUI] Sprite 'startHPBar' não atribuído.", this);isValidConfiguration = false; }
        if (_bodyHPBarSprite == null)  { Debug.LogError("[CrewUI] Sprite 'bodyHP' não atribuído.", this);    isValidConfiguration = false; }
        if (_endHPBarSprite == null)   { Debug.LogError("[CrewUI] Sprite 'endHPBar' não atribuído.", this);  isValidConfiguration = false; }

        if (_heartDistance <= 0)       { Debug.LogError("[CrewUI] 'heartDistance' precisa ser maior que zero.", this);       isValidConfiguration = false; }
        if (_healthPerHeart <= 0)      { Debug.LogError("[CrewUI] 'vidaPorCoração' precisa ser maior que zero.", this);      isValidConfiguration = false; }
        if (_healthPerSegment <= 0)    { Debug.LogError("[CrewUI] 'vidaPorSegmento' precisa ser maior que zero.", this);     isValidConfiguration = false; }
        if (_segmentDistances.y <= 0)  { Debug.LogError("[CrewUI] 'segmentDistances.Y' precisa ser maior que zero.", this);  isValidConfiguration = false; }

        if (_player != null && _player.GetComponent<CrewData>() == null)
        {
            Debug.LogError("[CrewUI] 'player' não tem CrewData.", this);
            isValidConfiguration = false;
        }

        if (_imagePrefab != null)
        {
            if (_imagePrefab.GetComponent<RectTransform>() == null)
            {
                Debug.LogError("[CrewUI] Prefab 'image' não tem RectTransform.", this);
                isValidConfiguration = false;
            }

            if (_imagePrefab.GetComponent<Image>() == null)
            {
                Debug.LogError("[CrewUI] Prefab 'image' não tem Image.", this);
                isValidConfiguration = false;
            }
        }

        return isValidConfiguration;
    }
    #endregion

    #region Lógica de Cache e Atualização de HP
    private void FetchHP()
    {
        CrewData crewData = _isEnemyMode ? _enemyCrewData : _player.GetComponent<CrewData>();

        if (crewData == null || crewData.CrewList == null || crewData.CrewList.Count == 0)
        {
            Debug.LogWarning("[CrewUI] Crew vazia, aguardando...");
            return;
        }

        float newCaptainHP = 0f;
        float newCaptainMaxHP = 0f;

        float newBoatHP = 0f;
        float newBoatMaxHP = 0f;

        List<float> newUnitsHPList = new();
        List<float> newUnitsMaxHPList = new();

        foreach (GameObject crewMember in crewData.CrewList)
        {
            if (crewMember == null) continue;
            if (crewMember.GetComponent<NPCsData>().isAlive == false) continue;

            NPCsData npcData = crewMember.GetComponent<NPCsData>();
            if (npcData == null) continue;

            switch (npcData.CreatureClass)
            {
                case NPCsData.Class.Captain:
                    newCaptainHP = npcData.GetCurrentHealth();
                    newCaptainMaxHP = npcData.GetMaxHealth();
                    break;

                case NPCsData.Class.Ship:
                    newBoatHP = npcData.GetCurrentHealth();
                    newBoatMaxHP = npcData.GetMaxHealth();
                    break;

                default:
                    newUnitsHPList.Add(npcData.GetCurrentHealth());
                    newUnitsMaxHPList.Add(npcData.GetMaxHealth());
                    break;
            }
        }

        _captainHP = newCaptainHP;
        _captainMaxHP = newCaptainMaxHP;

        _boatHP = newBoatHP;
        _boatMaxHP = newBoatMaxHP;

        _unitsHPList = newUnitsHPList;
        _unitsMaxHPList = newUnitsMaxHPList;
    }

        private bool HasHPChanged()
    {
        if (!Mathf.Approximately(_captainHP, _lastCaptainHP)) return true;
        if (!Mathf.Approximately(_boatHP, _lastBoatHP)) return true;

        if (_unitsHPList.Count != _lastUnitsHPList.Count)
            return true;

        for (int i = 0; i < _unitsHPList.Count; i++)
        {
            if (!Mathf.Approximately(_unitsHPList[i], _lastUnitsHPList[i]))
                return true;
        }

        return false;
    }

    private void CacheHP()
    {
        _lastCaptainHP = _captainHP;
        _lastBoatHP = _boatHP;

        _lastUnitsHPList = new List<float>(_unitsHPList);
    }
    #endregion

    #region Renderização
    private void ClearSpawned()
    {
        foreach (GameObject spawnedObject in _spawnedObjectsList)
        {
            if (spawnedObject != null)
                Destroy(spawnedObject);
        }

        _spawnedObjectsList.Clear();
    }

    private void InstantiateHP()
    {
        int currentRow = 0;

        if (_captainMaxHP > 0)
        {
            DrawHearts(_captainHP, _captainMaxHP, currentRow, _captainHPSprite, _captainContainer);
            currentRow++;
        }

        if (_boatMaxHP > 0)
        {
            DrawHearts(_boatHP, _boatMaxHP, currentRow, _boatHPSprite, _boatContainer);
            currentRow++;
        }

        for (int i = 0; i < _unitsHPList.Count; i++)
        {
            DrawCrewBar(_unitsHPList[i], _unitsMaxHPList[i], currentRow, _crewContainer);
            currentRow++;
        }
    }

    private void DrawHearts(float hp, float maxHP, int row, Sprite sprite, RectTransform container)
    {
        if (hp <= 0 && maxHP <= 0) return;

        RectTransform parent = ResolveParent(container);
        float yOffset = -row * _verticalDistance;

        int index = 0;

        for (float health = 0; health < hp; health += _healthPerHeart)
        {
            float xPixel = index * _heartDistance;

            Spawn(sprite, new Vector2(_isEnemyMode ? -xPixel : xPixel, yOffset), parent);

            index++;
        }

        // Posição do texto: logo após o último coração
        float lastPixel = (index > 0 ? index - 1 : 0) * _heartDistance;
        float textPositionX = lastPixel + _heartDistance;

        SpawnText(hp, maxHP, new Vector2(_isEnemyMode ? -(textPositionX) : textPositionX, yOffset), parent);
    }

    private void DrawCrewBar(float hp, float maxHP, int row, RectTransform container)
    {
        RectTransform parent = ResolveParent(container);
        float yOffset = -row * _verticalDistance;

        int index = 0;

        if (_isEnemyMode)
        {
            Spawn(_endHPBarSprite, new Vector2(0, yOffset), parent);

            for (float health = 0; health < hp; health += _healthPerSegment)
            {
                float xPixel = index * _segmentDistances.y + _segmentDistances.x;

                Spawn(_bodyHPBarSprite, new Vector2(-xPixel, yOffset), parent);

                index++;
            }

            float endPositionX = (index > 0 ? index - 1 : 0) * _segmentDistances.y + _segmentDistances.x + _segmentDistances.z;

            Spawn(_startHPBarSprite, new Vector2(-endPositionX, yOffset), parent);

            SpawnText(hp, maxHP, new Vector2(-(endPositionX + _segmentDistances.z), yOffset), parent);
        }
        else
        {
            Spawn(_startHPBarSprite, new Vector2(0, yOffset), parent);

            for (float health = 0; health < hp; health += _healthPerSegment)
            {
                float xPixel = index * _segmentDistances.y + _segmentDistances.x;

                Spawn(_bodyHPBarSprite, new Vector2(xPixel, yOffset), parent);

                index++;
            }

            float endPositionX = (index > 0 ? index - 1 : 0) * _segmentDistances.y + _segmentDistances.x + _segmentDistances.z;

            Spawn(_endHPBarSprite, new Vector2(endPositionX, yOffset), parent);

            SpawnText(hp, maxHP, new Vector2(endPositionX + _segmentDistances.z, yOffset), parent);
        }
    }

    private RectTransform ResolveParent(RectTransform container)
    {
        if (container != null) return container;
        if (_canvasRoot != null) return _canvasRoot;

        return (RectTransform)transform;
    }

    private void Spawn(Sprite sprite, Vector2 localOffset, RectTransform parent)
    {
        GameObject spawnedObject = Instantiate(_imagePrefab, ResolveParent(parent));

        spawnedObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            _startCoordinates.x + localOffset.x,
            _startCoordinates.y + localOffset.y
        );

        spawnedObject.GetComponent<Image>().sprite = sprite;

        _spawnedObjectsList.Add(spawnedObject);
    }

    private void SpawnText(float hp, float maxHP, Vector2 localOffset, RectTransform parent)
    {
        if (_textPrefab == null) return;

        GameObject spawnedObject = Instantiate(_textPrefab, ResolveParent(parent));

        spawnedObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            _startCoordinates.x + localOffset.x + _textOffset.x,
            _startCoordinates.y + localOffset.y + _textOffset.y
        );

        spawnedObject.GetComponent<TextMeshProUGUI>().text = $"{(int)hp}/{(int)maxHP}";

        _spawnedObjectsList.Add(spawnedObject);
    }
    #endregion
}