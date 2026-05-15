using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface para o sistema de recrutamento de NPCs.
/// Exibe atributos aleatórios gerados pelo NPC_Randomizer e processa a contratação.
/// </summary>
public class RecruitmentUI : MonoBehaviour
{
    #region Referências de UI e Dados
    [Header("Entradas e Dados")]
    public PlayerInputActions inputActions;
    private RecruitableNPC recruitableNPC;
    public CrewData playerCrew;

    [Header("Painéis")]
    public RectTransform Fundo, Textos, Botões;
    
    [Header("Textos")]
    public TextMeshProUGUI Vida, Classe, Tipo, Nome, Força, Custo, Level;
    #endregion

    #region Inicialização e Ciclo de Vida
    void Awake()
    {
        inputActions = new();
        FecharTela();
    }

    void Update()
    {
        if (inputActions.Player.CancelarSeleção.WasPressedThisFrame())
        {
           FecharTela(); 
        }
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

    #region Lógica de Negócio (UI)
    /// <summary>
    /// Preenche os campos de texto com os dados do NPC e abre o painel de recrutamento.
    /// </summary>
    public void AbrirTela(RecruitableNPC npcSelecionado, NPCsData dadosDoNPC)
    {
        recruitableNPC = npcSelecionado;
        Vida.text = "Vida: " + $"{dadosDoNPC.vidaMáxima:F2}";
        Classe.text = "Classe: " + dadosDoNPC.creatureClass;
        Tipo.text = "Tipo: " + dadosDoNPC.creatureType;
        Nome.text = "Nome: " + dadosDoNPC.NPC_Name;
        Força.text = "Forca: " + $"{dadosDoNPC.força:F2}";
        Custo.text = "Custo: " + $"{dadosDoNPC.custo:F2}";
        Level.text = "Level: " + dadosDoNPC.level;

        Button b1 = Botões.GetChild(0).GetComponent<Button>();
        Button b2 = Botões.GetChild(1).GetComponent<Button>();

        b1.onClick.RemoveAllListeners();
        b1.onClick.AddListener(() => Contratar(true));
        b2.onClick.RemoveAllListeners();
        b2.onClick.AddListener(() => Contratar(false));

        Botões.gameObject.SetActive(true);
        Fundo.gameObject.SetActive(true);
        Textos.gameObject.SetActive(true);
    }

    public void FecharTela()
    {
        Botões.gameObject.SetActive(false);
        Fundo.gameObject.SetActive(false);
        Textos.gameObject.SetActive(false);
    }

    /// <summary>
    /// Processa a decisão do jogador sobre a contratação.
    /// </summary>
    public void Contratar(bool resposta)
    {
        if (resposta)
        {
            recruitableNPC.GetComponent<NPCsMovement>().IrParaOBarco(playerCrew.transform);
            playerCrew.crew.Add(recruitableNPC.gameObject);
            SFXManager.Instance?.PlayContract();
            FecharTela();
        }
        else
        {
            FecharTela();
        }
    }
    #endregion
}