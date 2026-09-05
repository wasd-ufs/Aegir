using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AOT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Máquina de estados principal do fluxo de Batalha.
/// Coordena os turnos, a UI de ações do jogador, escolhas de IA inimigas, 
/// processamento de efeitos ao longo do tempo e checagens de vitória/derrota.
/// Implementado como Singleton para facilitar acesso por outros scripts.
/// </summary>
public class BattleManager : MonoBehaviour
{
    #region Singleton e Variáveis Estáticas
    private static WaitForSeconds _waitForSeconds1_5 = new WaitForSeconds(1.5f);
    public static BattleManager Instance { get; private set; }
    #endregion

    #region Dados das Equipes

    [Header("Crews")]
    [SerializeField] private CrewData _playerCrew;
    [HideInInspector] [SerializeField] private CrewData _enemyCrew;
    public CrewData EnemyCrew => _enemyCrew;

    #endregion

    #region Referências de Lógica

    [Header("Combat Scripts")]
    [SerializeField] private CrewAttacks _playerAttacks;
    private CrewAttacks _enemyAttacks;

    #endregion

    #region Referências de UI

    [Header("Canvas e Botões de Ação")]
    [SerializeField] private Transform _actionButtonContainer;
    [SerializeField] private GameObject _actionButtonPrefab;
    [SerializeField] private Transform _crewButtonContainer;
    [SerializeField] private GameObject _crewButtonPrefab;
    [SerializeField] private Button _skipTurnButton;
    [SerializeField] private TextMeshProUGUI _logText;

    [Header("UI e Feedback Visual")]
    [SerializeField] private CrewUI _enemyCrewUI;
    [SerializeField] private BattleData _battleData;
    [SerializeField] private TextMeshProUGUI _battleActionText;
    [SerializeField] private float _fadeSpeed;
    [SerializeField] private float _buttonTextSize = 9f;

    #endregion

    #region Estado de Batalha

    [Header("Estado Interno")]
    [SerializeField] private bool _isActiveBattle = false;
    public bool IsActiveBattle { get => _isActiveBattle; set => _isActiveBattle = value; }
    public bool isActiveBattle { get => _isActiveBattle; set => _isActiveBattle = value; }

    [SerializeField] private bool _shouldPassTurn = false;
    public bool ShouldPassTurn { get => _shouldPassTurn; set => _shouldPassTurn = value; }
    public bool shouldPassTurn { get => _shouldPassTurn; set => _shouldPassTurn = value; }

    [SerializeField] private bool _isShowingMessage = false;
    public bool IsShowingMessage { get => _isShowingMessage; set => _isShowingMessage = value; }
    public bool isShowingMessage { get => _isShowingMessage; set => _isShowingMessage = value; }

    [SerializeField] private bool _isShowingLogMessage = false;
    public bool IsShowingLogMessage { get => _isShowingLogMessage; set => _isShowingLogMessage = value; }
    public bool isShowingLogMessage { get => _isShowingLogMessage; set => _isShowingLogMessage = value; }

    private GameObject _selectedActor;
    private Coroutine _fadeCoroutine;
    private Coroutine _fadeLogCoroutine;
    private List<GameObject> _playerTargets = new();
    private PlayerInputActions _inputActions;

    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _inputActions = new();
    }

    void Update()
    {
        if (_inputActions.Player.CancelarSelecao.WasPressedThisFrame())
            CancelAction();
    }

    void OnEnable()
    {
        _inputActions.Enable();
    }

    void OnDisable()
    {
        _inputActions.Disable();
    }
    #endregion

    #region Inicialização
    /// <summary>
    /// Configura os dados do inimigo e inicializa a corrotina do loop de combate.
    /// </summary>
    /// <param name="enemies">O CrewData representante da equipe inimiga gerada no embate.</param>
    public void InitiateBattle(CrewData enemies)
    {
        _enemyCrew = enemies;
        _isActiveBattle = true;

        _enemyAttacks = enemies.GetComponent<CrewAttacks>();
        if (_enemyAttacks == null)
            Debug.LogWarning("[BattleManager] NPCAttacks não encontrado no GameObject do _enemyCrew — inimigos não poderão agir.");

        if (_enemyCrewUI != null)
            _enemyCrewUI.InitializeAsEnemy(enemies);
        else
            Debug.LogWarning("[BattleManager] _enemyCrewUI não atribuído — HP inimigo não será exibido.");

        StartCoroutine(BattleLoop());
    }

    /// <summary>
    /// Alias para manter compatibilidade com chamadas legadas de inicialização de batalha.
    /// </summary>
    public void IniciateBattle(CrewData enemies) => InitiateBattle(enemies);
    #endregion

    #region Loop Principal
    /// <summary>
    /// Controla a sequência em que o combate ocorre: Turno Player -> Efeitos -> Checa Morte -> Turno Inimigo -> Efeitos -> Checa Morte.
    /// </summary>
    private IEnumerator BattleLoop()
    {
        yield return null;

        while (isActiveBattle)
        {
            // Reseta ações da tripulação do jogador
            foreach (GameObject npc in _playerCrew.CrewList)
                npc.GetComponent<NPCsData>().ResetActions();

            shouldPassTurn = false;

            EnableActionButtons(true);
            GenerateCrewButtons();

            // Espera até que o jogador finalize o turno (manualmente ou ficando sem ações)
            yield return new WaitUntil(() => shouldPassTurn || !HasCrewActions(_playerCrew));
            yield return new WaitWhile(() => isShowingMessage);

            EnableActionButtons(false);

            // Ticks de efeitos após o turno aliado
            TicksFromAllEffects(_playerCrew);
            TicksFromAllEffects(_enemyCrew);

            if (IsBattleOver()) yield break;

            // Turno dos inimigos
            yield return StartCoroutine(EnemyTurns());

            // Ticks de efeitos após o turno inimigo
            TicksFromAllEffects(_playerCrew);
            TicksFromAllEffects(_enemyCrew);

            if (IsBattleOver()) yield break;
        }
    }
    #endregion

    #region Inteligência Inimiga (IA)
    /// <summary>
    /// Gerencia as ações tomadas pelos oponentes iterando pelas ações disponíveis até não restar nenhuma.
    /// </summary>
    private IEnumerator EnemyTurns()
    {
        foreach (GameObject npc in _enemyCrew.CrewList)
            npc.GetComponent<NPCsData>().ResetActions();

        yield return _waitForSeconds1_5;
        if (_enemyAttacks == null) yield break;

        while (HasCrewActions(_enemyCrew))
        {
            CombatBase.ActionData chosenAction = new();
            GameObject actor = null;

            // Tenta sortear uma ação válida para algum inimigo que ainda possa agir
            for (int i = 0; i < 10; i++)
            {
                chosenAction = ChooseAction();
                actor = DrawActor(chosenAction, _enemyCrew);
                if (actor != null) break;
            }

            if (actor == null)
            {
                Debug.LogWarning("[BattleManager] Inimigo tentou 10 vezes e não achou uma ação válida. Pulando o turno.");
                yield break;
            }

            List<GameObject> targets = ChooseNpcTargets(chosenAction);

            _enemyAttacks.allies  = _enemyCrew;
            _enemyAttacks.enemies = _playerCrew;
            actor.GetComponent<NPCsData>().ConsumeAction();
            _enemyAttacks.ExecuteAction(chosenAction, targets, actor);
            yield return new WaitWhile(() => isShowingMessage);
        }

        yield return new WaitForSeconds(0.8f);
    }

    /// <summary>
    /// Seleciona os alvos adequados baseados nos tipos alvejáveis da ação inimiga.
    /// Mistura a lista para dar aleatoriedade aos ataques.
    /// </summary>
    private List<GameObject> ChooseNpcTargets(CombatBase.ActionData action)
    {
        bool canAffectEnemies = action.targetTeams.Contains(CombatBase.TargetTeam.Enemy);
        bool canAffectAllies  = action.targetTeams.Contains(CombatBase.TargetTeam.Ally);

        List<GameObject> targets = new();

        if (canAffectEnemies)
        {
            var livingMembers = _playerCrew.CrewList.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList();
            if (livingMembers.Count > 0) targets.AddRange(Shuffle(livingMembers));
        }

        if (canAffectAllies)
        {
            var livingMembers = _enemyCrew.CrewList.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList();
            if (livingMembers.Count > 0) targets.AddRange(Shuffle(livingMembers));
        }

        return targets;
    }

    /// <summary>
    /// Seleciona uma ação aleatória do rol de ações usando uma roleta baseada nos 'pesos' configurados.
    /// </summary>
    public CombatBase.ActionData ChooseAction()
    {
        float totalWeight = 0;
        foreach (CombatBase.ActionData action in _enemyAttacks.Actions)
            totalWeight += action.weight;

        float betweenLimits = Random.Range(0f, totalWeight);
        CombatBase.ActionData chosenAction = new();

        foreach (CombatBase.ActionData action in _enemyAttacks.Actions)
        {
            betweenLimits -= action.weight;
            if (betweenLimits <= 0)
            {
                chosenAction = action;
                break;
            }
        }

        return chosenAction;
    }
    #endregion

    #region Ações do Jogador
    /// <summary>
    /// Recebe o clique da UI de ações e executa a lógica em cima do alvo selecionado/randômico.
    /// </summary>
    /// <param name="action">A ação clicada pelo jogador na interface.</param>
    public void ExecutePlayerAction(CombatBase.ActionData action)
    {
        GameObject actor = _selectedActor;
        if (actor == null)
        {
            Debug.LogWarning($"[BattleManager] Nenhum membro do crew pode executar a ação '{action.actionName}'.");
            return;
        }

        // Caso o jogador não tenha mirado explicitamente, define todos os possíveis
        if (_playerTargets.Count == 0)
        {
            if (action.targetTeams.Contains(CombatBase.TargetTeam.Enemy))
                _playerTargets.AddRange(Shuffle(_enemyCrew.CrewList.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList()));

            if (action.targetTeams.Contains(CombatBase.TargetTeam.Ally))
                _playerTargets.AddRange(Shuffle(_playerCrew.CrewList.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList()));
        }

        _playerAttacks.allies  = _playerCrew;
        _playerAttacks.enemies = _enemyCrew;
        actor.GetComponent<NPCsData>().ConsumeAction();
        _playerAttacks.ExecuteAction(action, _playerTargets, actor);

        _playerTargets.Clear();
        _selectedActor = null;
        ClearActionButtons();
        GenerateCrewButtons();
    }

    /// <summary>
    /// Escolhe qual tripulante executará a ação gerada pela IA, restrito a quem ainda tem Ações Restantes e classe compatível.
    /// </summary>
    private GameObject DrawActor(CombatBase.ActionData action, CrewData crew)
    {
        bool hasNoRestrictions = action.allowedClasses == null || action.allowedClasses.Count == 0;

        var eligible = crew.CrewList
            .Where(g => {
                NPCsData npc = g?.GetComponent<NPCsData>();
                if (npc == null || !npc.isAlive || !npc.CanAct()) return false;
                return hasNoRestrictions || action.allowedClasses.Contains(npc.CreatureClass);
            })
            .ToList();

        if (eligible.Count == 0) return null;
        return eligible[Random.Range(0, eligible.Count)];
    }

    /// <summary>
    /// Adiciona um alvo à lista de alvos da ação do jogador.
    /// </summary>
    public void SelectTarget(GameObject target)
    {
        if (!_playerTargets.Contains(target))
            _playerTargets.Add(target);
    }

    public void ClearTargets() => _playerTargets.Clear();

    /// <summary>
    /// Seleciona o membro da tripulação aliado para que seus botões de ações fiquem visíveis.
    /// </summary>
    public void SelectActor(GameObject clickedCrewMember)
    {
        if (shouldPassTurn || !isActiveBattle) return;

        NPCsData nPCs = clickedCrewMember.GetComponent<NPCsData>();
        if (!nPCs.isAlive || !nPCs.CanAct()) return;

        _selectedActor = clickedCrewMember;
        ClearCrewButtons();
        GenerateActionButtons();
    }

    private void CancelAction()
    {
        if (_selectedActor != null)
        {
            _selectedActor = null;
            ClearActionButtons();
            ClearTargets();

            GenerateCrewButtons();
        }
    }

    public void PassTurn()
    {
        _selectedActor = null;
        shouldPassTurn = true;
        ClearActionButtons();
        ClearTargets();
        ClearCrewButtons();
    }
    #endregion

    #region Gerenciamento da Interface Dinâmica (Botões)
    /// <summary>
    /// Instancia ou reutiliza os botões de ação para o tripulante recém selecionado.
    /// Filtra as ações que a classe do ator não pode executar.
    /// </summary>
    private void GenerateActionButtons()
    {
        if (_selectedActor == null) return;
        NPCsData.Class actorClass = _selectedActor.GetComponent<NPCsData>().CreatureClass;

        int buttonIndex = 0;
        foreach (CombatBase.ActionData action in _playerAttacks.Actions)
        {
            if (action.allowedClasses != null && action.allowedClasses.Count > 0 && !action.allowedClasses.Contains(actorClass)) continue;

            GameObject buttonObject;
            if (buttonIndex < _actionButtonContainer.childCount)
            {
                buttonObject = _actionButtonContainer.GetChild(buttonIndex).gameObject;
                buttonObject.SetActive(true);
            }
            else
            {
                buttonObject = Instantiate(_actionButtonPrefab, _actionButtonContainer);
            }

            var textComponent = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = action.actionName;
                textComponent.fontSize = _buttonTextSize;
            }

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            CombatBase.ActionData capturedAction = action;
            button.onClick.AddListener(() => ExecutePlayerAction(capturedAction));

            buttonIndex++;
        }

        for (int i = buttonIndex; i < _actionButtonContainer.childCount; i++)
        {
            _actionButtonContainer.GetChild(i).gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Lista os tripulantes aliados vivos e com ações disponíveis, reutilizando botões.
    /// </summary>
    private void GenerateCrewButtons()
    {
        int buttonIndex = 0;
        foreach (GameObject npcObject in _playerCrew.CrewList)
        {
            if (npcObject == null) continue;
            NPCsData nPCs = npcObject.GetComponent<NPCsData>();
            if (nPCs == null || !nPCs.isAlive || !nPCs.CanAct()) continue;

            GameObject buttonObject;
            if (buttonIndex < _crewButtonContainer.childCount)
            {
                buttonObject = _crewButtonContainer.GetChild(buttonIndex).gameObject;
                buttonObject.SetActive(true);
            }
            else
            {
                buttonObject = Instantiate(_crewButtonPrefab, _crewButtonContainer);
            }

            var textComponent = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = nPCs.NpcName;
                textComponent.fontSize = _buttonTextSize;
            }

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            GameObject capturedNPC = npcObject;
            button.onClick.AddListener(() => SelectActor(capturedNPC));

            buttonIndex++;
        }

        for (int i = buttonIndex; i < _crewButtonContainer.childCount; i++)
        {
            _crewButtonContainer.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void EnableActionButtons(bool currentEstate)
    {
        foreach (Transform child in _actionButtonContainer)
        {
            Button button = child.GetComponent<Button>();
            if (button != null) button.interactable = currentEstate;
        }

        foreach (Transform child in _crewButtonContainer)
        {
            Button button = child.GetComponent<Button>();
            if (button != null) button.interactable = currentEstate;
        }

        if (_skipTurnButton != null) _skipTurnButton.interactable = currentEstate;
    }

    public void ClearActionButtons()
    {
        foreach (Transform child in _actionButtonContainer)
            child.gameObject.SetActive(false);
    }

    public void ClearCrewButtons()
    {
        foreach (Transform child in _crewButtonContainer)
            child.gameObject.SetActive(false);
    }
    #endregion

    #region Ticks e Fim de Batalha
    /// <summary>
    /// Reduz a duração (em turnos) de todos os efeitos temporais na equipe.
    /// </summary>
    private void TicksFromAllEffects(CrewData crew)
    {
        for (int i = crew.CrewList.Count - 1; i >= 0; i--)
        {
            NPCsData npc = crew.CrewList[i].GetComponent<NPCsData>();
            if (npc != null) npc.TickEffects();
        }
    }

    /// <summary>
    /// Confirma se ainda há membros vivos que não usaram suas ações no turno corrente.
    /// </summary>
    private bool HasCrewActions(CrewData crew)
    {
        foreach (GameObject npc in crew.CrewList)
            if (npc.GetComponent<NPCsData>().CanAct()) return true;
        return false;
    }

    /// <summary>
    /// Analisa se as condições de Game Over (Barco morto / toda tripulação aniquilada)
    /// ou de Vitória (Todos inimigos derrotados) foram atingidas, finalizando a fase.
    /// Efetua cálculo de espólios/saque em caso de sucesso.
    /// </summary>
    /// <returns>True se a batalha chegou ao fim e não deve continuar o loop.</returns>
    private bool IsBattleOver()
    {
        bool isPlayerDefeated = _playerCrew.CrewList.Any(g => g.GetComponent<NPCsData>()?.CreatureClass == NPCsData.Class.Barco && g.GetComponent<NPCsData>()?.isAlive == false)
                             || _playerCrew.CrewList.Where(g => g.GetComponent<NPCsData>().CreatureClass != NPCsData.Class.Barco).All(g => g.GetComponent<NPCsData>().isAlive == false);
        bool isEnemyDefeated = _enemyCrew.CrewList.Any(g => g.GetComponent<NPCsData>()?.CreatureClass == NPCsData.Class.Barco && g.GetComponent<NPCsData>()?.isAlive == false)
                             || _enemyCrew.CrewList.Where(g => g.GetComponent<NPCsData>().CreatureClass != NPCsData.Class.Barco).All(g => g.GetComponent<NPCsData>().isAlive == false);

        if (isPlayerDefeated)
        {
            isActiveBattle = false;
            Debug.Log("Derrota!");
            _playerCrew.gameObject.transform.position = Vector3.zero;
            _battleData?.EndFight(false, _playerCrew, _enemyCrew);
            return true;
        }

        if (isEnemyDefeated)
        {
            Dictionary<string, int> lootedItems = new Dictionary<string, int>();
            foreach (GameObject npc in _enemyCrew.CrewList)
            {
                NPCsData data = npc.GetComponent<NPCsData>();
                if (data != null)
                {
                    List<Inventory.Slot> drops = data.GenerateLoot();
                    foreach (Inventory.Slot slot in drops)
                    {
                        _playerCrew.Inventory.AddItem(slot.item, slot.quantity);
                        if (lootedItems.ContainsKey(slot.item.ItemName))
                            lootedItems[slot.item.ItemName] += slot.quantity;
                        else
                            lootedItems.Add(slot.item.ItemName, slot.quantity);
                    }
                }
            }

            string messageLoot = "";
            foreach (string i in lootedItems.Keys)
                messageLoot += lootedItems[i] + "x " + i + "\n";

            isActiveBattle = false;
            Debug.Log("Vitória!");
            _battleData?.EndFight(true, _playerCrew, _enemyCrew, messageLoot);
            return true;
        }

        return false;
    }
    #endregion

    #region Exibição de Mensagens (Fade UI)
    public void DisplayMessage(string message)
    {
        _battleActionText.alpha = 0;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(ShowMessage());
        _battleActionText.text = message;
    }

    public IEnumerator ShowMessage()
    {
        isShowingMessage = true;
        while (_battleActionText.alpha < 1)
        {
            _battleActionText.alpha += Time.deltaTime * _fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        while (_battleActionText.alpha > 0)
        {
            _battleActionText.alpha -= Time.deltaTime * _fadeSpeed;
            yield return null;
        }
        isShowingMessage = false;
    }

    public void DisplayLog(string message)
    {
        _logText.alpha = 0;
        if (_fadeLogCoroutine != null) StopCoroutine(_fadeLogCoroutine);
        _fadeLogCoroutine = StartCoroutine(ShowLog());
        _logText.text = message;
    }

    public IEnumerator ShowLog()
    {
        isShowingLogMessage = true;
        while (_logText.alpha < 1)
        {
            _logText.alpha += Time.deltaTime * _fadeSpeed;
            yield return null;
        }

        yield return new WaitForSeconds(4f);

        while (_logText.alpha > 0)
        {
            _logText.alpha -= Time.deltaTime * _fadeSpeed;
            yield return null;
        }
        isShowingLogMessage = false;
    }
    #endregion

    #region Helpers e Funções Utilitárias
    /// <summary>
    /// Embaralha uma lista utilizando o algoritmo Fisher-Yates shuffle.
    /// Muito útil para criar alvos aleatórios para os inimigos e jogadores que selecionam ações amplas.
    /// </summary>
    public List<GameObject> Shuffle(List<GameObject> list)
    {
        int n = list.Count;
        List<GameObject> listCopy = new List<GameObject>(list);
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            GameObject value = listCopy[k];
            listCopy[k] = listCopy[n];
            listCopy[n] = value;
        }
        return listCopy;
    }
    #endregion
}