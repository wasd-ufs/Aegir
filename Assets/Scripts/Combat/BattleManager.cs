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
    public CrewData playerCrew;
    [HideInInspector] public CrewData enemyCrew;
    #endregion

    #region Referências de Lógica
    [Header("Combat Scripts")]
    public CrewAttacks ataquesPlayer;
    private CrewAttacks ataquesInimigo;
    #endregion

    #region Referências de UI
    [Header("Canvas e Botões de Ação")]
    public Transform actionButtonContainer;
    public GameObject actionButtonPrefab;
    public Transform crewButtonContainer;
    public GameObject crewButtonPrefab;
    public Button botaoPassarTurno;
    public TextMeshProUGUI textoDoLog;

    [Header("UI e Feedback Visual")]
    public CrewUI enemyCrewUI;
    public BattleData battleData;
    public TextMeshProUGUI textoDeAcaoDaBatalha;
    public float velocidadeDoFade;
    #endregion

    #region Estado de Batalha
    [Header("Estado Interno")]
    public bool batalhaAtiva = false;
    public bool passarTurno = false;
    public bool exibindoMensagem = false;
    public bool exibindoMensagemLog = false;
    private GameObject atorSelecionado;
    private Coroutine fadeCoroutine, fadeLogCoroutine;
    private List<GameObject> alvosDoPlayer = new();
    private PlayerInputActions inputActions;
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        inputActions = new();
    }

    void Update()
    {
        if (inputActions.Player.CancelarSeleção.WasPressedThisFrame())
            CancelarAcao();
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }
    #endregion

    #region Inicialização
    /// <summary>
    /// Configura os dados do inimigo e inicializa a corrotina do loop de combate.
    /// </summary>
    /// <param name="inimigos">O CrewData representante da equipe inimiga gerada no embate.</param>
    public void IniciarBatalha(CrewData inimigos)
    {
        enemyCrew    = inimigos;
        batalhaAtiva = true;

        ataquesInimigo = inimigos.GetComponent<CrewAttacks>();
        if (ataquesInimigo == null)
            Debug.LogWarning("[BattleManager] NPCAttacks não encontrado no GameObject do enemyCrew — inimigos não poderão agir.");

        if (enemyCrewUI != null)
            enemyCrewUI.InicializarComoInimigo(inimigos);
        else
            Debug.LogWarning("[BattleManager] enemyCrewUI não atribuído — HP inimigo não será exibido.");

        StartCoroutine(LoopDeBatalha());
    }
    #endregion

    #region Loop Principal
    /// <summary>
    /// Controla a sequência em que o combate ocorre: Turno Player -> Efeitos -> Checa Morte -> Turno Inimigo -> Efeitos -> Checa Morte.
    /// </summary>
    private IEnumerator LoopDeBatalha()
    {
        yield return null;

        while (batalhaAtiva)
        {
            // Reseta ações da tripulação do jogador
            foreach (GameObject npc in playerCrew.crew)
                npc.GetComponent<NPCsData>().ResetarAcoes();

            passarTurno = false;

            HabilitarBotões(true);
            GerarBotoesDaTripulacao();

            // Espera até que o jogador finalize o turno (manualmente ou ficando sem ações)
            yield return new WaitUntil(() => passarTurno || !EquipeTemAções(playerCrew));
            yield return new WaitWhile(() => exibindoMensagem);

            HabilitarBotões(false);

            // Ticks de efeitos após o turno aliado
            TickTodosEfeitos(playerCrew);
            TickTodosEfeitos(enemyCrew);

            if (VerificarFimDeBatalha()) yield break;

            // Turno dos inimigos
            yield return StartCoroutine(TurnoInimigos());

            // Ticks de efeitos após o turno inimigo
            TickTodosEfeitos(playerCrew);
            TickTodosEfeitos(enemyCrew);

            if (VerificarFimDeBatalha()) yield break;
        }
    }
    #endregion

    #region Inteligência Inimiga (IA)
    /// <summary>
    /// Gerencia as ações tomadas pelos oponentes iterando pelas ações disponíveis até não restar nenhuma.
    /// </summary>
    private IEnumerator TurnoInimigos()
    {
        foreach (GameObject npc in enemyCrew.crew)
            npc.GetComponent<NPCsData>().ResetarAcoes();

        yield return _waitForSeconds1_5;
        if (ataquesInimigo == null) yield break;

        while (EquipeTemAções(enemyCrew))
        {
            CombatBase.Actions açãoEscolhida = new();
            GameObject ator = null;

            // Tenta sortear uma ação válida para algum inimigo que ainda possa agir
            for (int i = 0; i < 10; i++)
            {
                açãoEscolhida = EscolheAção();
                ator = SortearAtor(açãoEscolhida, enemyCrew);
                if (ator != null) break;
            }

            if (ator == null)
            {
                Debug.LogWarning("[BattleManager] Inimigo tentou 10 vezes e não achou uma ação válida. Pulando o turno.");
                yield break;
            }

            List<GameObject> alvos = EscolheAlvosNPC(açãoEscolhida);

            ataquesInimigo.aliados  = enemyCrew;
            ataquesInimigo.inimigos = playerCrew;
            ator.GetComponent<NPCsData>().ConsumirAcao();
            ataquesInimigo.ExecutarAção(açãoEscolhida, alvos, ator);
            yield return new WaitWhile(() => exibindoMensagem);
        }

        yield return new WaitForSeconds(0.8f);
    }

    /// <summary>
    /// Seleciona os alvos adequados baseados nos tipos alvejáveis da ação inimiga.
    /// Mistura a lista para dar aleatoriedade aos ataques.
    /// </summary>
    private List<GameObject> EscolheAlvosNPC(CombatBase.Actions ação)
    {
        bool afetaInimigos = ação.timesAlvos.Contains(CombatBase.TimeAlvo.Inimigo);
        bool afetaAliados  = ação.timesAlvos.Contains(CombatBase.TimeAlvo.Aliado);

        List<GameObject> alvos = new();

        if (afetaInimigos)
        {
            var vivos = playerCrew.crew.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList();
            if (vivos.Count > 0) alvos.AddRange(Shuffle(vivos));
        }

        if (afetaAliados)
        {
            var vivos = enemyCrew.crew.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList();
            if (vivos.Count > 0) alvos.AddRange(Shuffle(vivos));
        }

        return alvos;
    }

    /// <summary>
    /// Seleciona uma ação aleatória do rol de ações usando uma roleta baseada nos 'pesos' configurados.
    /// </summary>
    public CombatBase.Actions EscolheAção()
    {
        float pesoTotal = 0;
        foreach (CombatBase.Actions ação in ataquesInimigo.actions)
            pesoTotal += ação.peso;

        float entreLimites = Random.Range(0f, pesoTotal);
        CombatBase.Actions açãoEscolhida = new();

        foreach (CombatBase.Actions ação in ataquesInimigo.actions)
        {
            entreLimites -= ação.peso;
            if (entreLimites <= 0)
            {
                açãoEscolhida = ação;
                break;
            }
        }

        return açãoEscolhida;
    }
    #endregion

    #region Ações do Jogador
    /// <summary>
    /// Recebe o clique da UI de ações e executa a lógica em cima do alvo selecionado/randômico.
    /// </summary>
    /// <param name="ação">A ação clicada pelo jogador na interface.</param>
    public void ExecutarAçãoPlayer(CombatBase.Actions ação)
    {
        GameObject ator = atorSelecionado;
        if (ator == null)
        {
            Debug.LogWarning($"[BattleManager] Nenhum membro do crew pode executar a ação '{ação.nomeAção}'.");
            return;
        }

        // Caso o jogador não tenha mirado explicitamente, define todos os possíveis
        if (alvosDoPlayer.Count == 0)
        {
            if (ação.timesAlvos.Contains(CombatBase.TimeAlvo.Inimigo))
                alvosDoPlayer.AddRange(Shuffle(enemyCrew.crew.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList()));

            if (ação.timesAlvos.Contains(CombatBase.TimeAlvo.Aliado))
                alvosDoPlayer.AddRange(Shuffle(playerCrew.crew.Where(g => g.GetComponent<NPCsData>()?.isAlive == true).ToList()));
        }

        ataquesPlayer.aliados  = playerCrew;
        ataquesPlayer.inimigos = enemyCrew;
        ator.GetComponent<NPCsData>().ConsumirAcao();
        ataquesPlayer.ExecutarAção(ação, alvosDoPlayer, ator);

        alvosDoPlayer.Clear();
        atorSelecionado = null;
        LimparBotões();
        GerarBotoesDaTripulacao();
    }

    /// <summary>
    /// Escolhe qual tripulante executará a ação gerada pela IA, restrito a quem ainda tem Ações Restantes e classe compatível.
    /// </summary>
    private GameObject SortearAtor(CombatBase.Actions ação, CrewData crew)
    {
        bool semRestrição = ação.classesPermitidas == null || ação.classesPermitidas.Count == 0;

        var elegíveis = crew.crew
            .Where(g => {
                NPCsData npc = g?.GetComponent<NPCsData>();
                if (npc == null || !npc.isAlive || !npc.PodeAgir()) return false;
                return semRestrição || ação.classesPermitidas.Contains(npc.creatureClass);
            })
            .ToList();

        if (elegíveis.Count == 0) return null;
        return elegíveis[Random.Range(0, elegíveis.Count)];
    }

    public void SelecionarAlvo(GameObject alvo)
    {
        if (!alvosDoPlayer.Contains(alvo))
            alvosDoPlayer.Add(alvo);
    }

    public void LimparAlvos() => alvosDoPlayer.Clear();

    /// <summary>
    /// Seleciona o membro da tripulação aliado para que seus botões de ações fiquem visíveis.
    /// </summary>
    public void SelecionarAtor(GameObject tripulanteClicado)
    {
        if (passarTurno || !batalhaAtiva) return;

        NPCsData nPCs = tripulanteClicado.GetComponent<NPCsData>();
        if (!nPCs.isAlive || !nPCs.PodeAgir()) return;

        atorSelecionado = tripulanteClicado;

        foreach (Transform filho in crewButtonContainer)
            Destroy(filho.gameObject);

        GerarBotõesDeAção();
    }

    private void CancelarAcao()
    {
        if (atorSelecionado != null)
        {
            atorSelecionado = null;
            LimparBotões();
            LimparAlvos();

            GerarBotoesDaTripulacao();
        }
    }

    public void PassarTurno()
    {
        atorSelecionado = null;
        passarTurno = true;
        LimparBotões();
        LimparAlvos();

        foreach (Transform filho in crewButtonContainer) 
            Destroy(filho.gameObject);
    }
    #endregion

    #region Gerenciamento da Interface Dinâmica (Botões)
    /// <summary>
    /// Instancia e popula os botões de ação para o tripulante recém selecionado.
    /// Filtra as ações que a classe do ator não pode executar.
    /// </summary>
    private void GerarBotõesDeAção()
    {
        if (atorSelecionado == null) return;
        NPCsData.Class classeDoAtor = atorSelecionado.GetComponent<NPCsData>().creatureClass;

        foreach (Transform filho in actionButtonContainer)
            Destroy(filho.gameObject);

        foreach (CombatBase.Actions ação in ataquesPlayer.actions)
        {
            if (ação.classesPermitidas != null && ação.classesPermitidas.Count > 0 && !ação.classesPermitidas.Contains(classeDoAtor)) continue;

            GameObject btnObj = Instantiate(actionButtonPrefab, actionButtonContainer);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = ação.nomeAção;

            Button btn = btnObj.GetComponent<Button>();
            CombatBase.Actions açãoCapturada = ação;
            btn.onClick.AddListener(() => ExecutarAçãoPlayer(açãoCapturada));
        }
    }

    /// <summary>
    /// Lista os tripulantes aliados vivos e com ações disponíveis.
    /// </summary>
    private void GerarBotoesDaTripulacao()
    {
        foreach (Transform filho in crewButtonContainer)
            Destroy(filho.gameObject);

        foreach (GameObject npcObj in playerCrew.crew)
        {
            NPCsData nPCs = npcObj.GetComponent<NPCsData>();
            if (!nPCs.isAlive || !nPCs.PodeAgir()) continue;

            GameObject btnObj = Instantiate(crewButtonPrefab, crewButtonContainer);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = nPCs.NPC_Name;

            Button btn = btnObj.GetComponent<Button>();
            GameObject npcCapturado = npcObj;
            btn.onClick.AddListener(() => SelecionarAtor(npcCapturado));
        }
    }

    private void HabilitarBotões(bool estado)
    {
        foreach (Transform filho in actionButtonContainer)
        {
            Button btn = filho.GetComponent<Button>();
            if (btn != null) btn.interactable = estado;
        }

        foreach (Transform filho in crewButtonContainer)
        {
            Button btn = filho.GetComponent<Button>();
            if (btn != null) btn.interactable = estado;
        }

        if (botaoPassarTurno != null) botaoPassarTurno.interactable = estado;
    }

    public void LimparBotões()
    {
        foreach (Transform filho in actionButtonContainer)
            Destroy(filho.gameObject);
    }
    #endregion

    #region Ticks e Fim de Batalha
    /// <summary>
    /// Reduz a duração (em turnos) de todos os efeitos temporais na equipe.
    /// </summary>
    private void TickTodosEfeitos(CrewData crew)
    {
        for (int i = crew.crew.Count - 1; i >= 0; i--)
        {
            NPCsData npc = crew.crew[i].GetComponent<NPCsData>();
            if (npc != null) npc.TickEffects();
        }
    }

    /// <summary>
    /// Confirma se ainda há membros vivos que não usaram suas ações no turno corrente.
    /// </summary>
    private bool EquipeTemAções(CrewData crew)
    {
        foreach (GameObject npc in crew.crew)
            if (npc.GetComponent<NPCsData>().PodeAgir()) return true;
        return false;
    }

    /// <summary>
    /// Analisa se as condições de Game Over (Barco morto / toda tripulação aniquilada)
    /// ou de Vitória (Todos inimigos derrotados) foram atingidas, finalizando a fase.
    /// Efetua cálculo de espólios/saque em caso de sucesso.
    /// </summary>
    /// <returns>True se a batalha chegou ao fim e não deve continuar o loop.</returns>
    private bool VerificarFimDeBatalha()
    {
        bool playerDerrotado  = playerCrew.crew.Any(g => g.GetComponent<NPCsData>()?.creatureClass == NPCsData.Class.Barco && g.GetComponent<NPCsData>()?.isAlive == false)
                             || playerCrew.crew.Where(g => g.GetComponent<NPCsData>().creatureClass != NPCsData.Class.Barco).All(g => g.GetComponent<NPCsData>().isAlive == false);
        bool inimigoDerrotado = enemyCrew.crew.Any(g => g.GetComponent<NPCsData>()?.creatureClass == NPCsData.Class.Barco && g.GetComponent<NPCsData>()?.isAlive == false)
                             || enemyCrew.crew.Where(g => g.GetComponent<NPCsData>().creatureClass != NPCsData.Class.Barco).All(g => g.GetComponent<NPCsData>().isAlive == false);

        if (playerDerrotado)
        {
            batalhaAtiva = false;
            Debug.Log("Derrota!");
            playerCrew.gameObject.transform.position = Vector3.zero;
            battleData?.EndFight(false, playerCrew, enemyCrew);
            return true;
        }

        if (inimigoDerrotado)
        {
            Dictionary<string, int> itensSaqueados = new Dictionary<string, int>();
            foreach (GameObject npc in enemyCrew.crew)
            {
                NPCsData data = npc.GetComponent<NPCsData>();
                if (data != null)
                {
                    List<Inventory.Slot> drops = data.GerarLoot();
                    foreach (Inventory.Slot slot in drops)
                    {
                        playerCrew.inventory.AdicionarItem(slot.item, slot.quantity);
                        if (itensSaqueados.ContainsKey(slot.item.itemName))
                            itensSaqueados[slot.item.itemName] += slot.quantity;
                        else
                            itensSaqueados.Add(slot.item.itemName, slot.quantity);
                    }
                }
            }

            string messageLoot = "";
            foreach (string i in itensSaqueados.Keys)
                messageLoot += itensSaqueados[i] + "x " + i + "\n";

            batalhaAtiva = false;
            Debug.Log("Vitória!");
            battleData?.EndFight(true, playerCrew, enemyCrew, messageLoot);
            return true;
        }

        return false;
    }
    #endregion

    #region Exibição de Mensagens (Fade UI)
    public void ExibirMensagem(string mensagem)
    {
        textoDeAcaoDaBatalha.alpha = 0;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(MostrarMensagem());
        textoDeAcaoDaBatalha.text = mensagem;
    }

    public IEnumerator MostrarMensagem()
    {
        exibindoMensagem = true;
        while (textoDeAcaoDaBatalha.alpha < 1)
        {
            textoDeAcaoDaBatalha.alpha += Time.deltaTime * velocidadeDoFade;
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        while (textoDeAcaoDaBatalha.alpha > 0)
        {
            textoDeAcaoDaBatalha.alpha -= Time.deltaTime * velocidadeDoFade;
            yield return null;
        }
        exibindoMensagem = false;
    }

    public void ExibirLog(string mensagem)
    {
        textoDoLog.alpha = 0;
        if (fadeLogCoroutine != null) StopCoroutine(fadeLogCoroutine);
        fadeLogCoroutine = StartCoroutine(MostrarLog());
        textoDoLog.text = mensagem;
    }

    public IEnumerator MostrarLog()
    {
        exibindoMensagemLog = true;
        while (textoDoLog.alpha < 1)
        {
            textoDoLog.alpha += Time.deltaTime * velocidadeDoFade;
            yield return null;
        }

        yield return new WaitForSeconds(4f);

        while (textoDoLog.alpha > 0)
        {
            textoDoLog.alpha -= Time.deltaTime * velocidadeDoFade;
            yield return null;
        }
        exibindoMensagemLog = false;
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