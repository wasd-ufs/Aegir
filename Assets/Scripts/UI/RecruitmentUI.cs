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
    [SerializeField] private PlayerInputActions _inputActions;
    private RecruitableNPC _recruitableNpc;
    
    [SerializeField] private CrewData _playerCrew;

    [Header("Painéis")]
    [SerializeField] private RectTransform _background;
    [SerializeField] private RectTransform _texts;
    [SerializeField] private RectTransform _buttons;
    
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI _healthText;
    [SerializeField] private TextMeshProUGUI _classText;
    [SerializeField] private TextMeshProUGUI _typeText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _strengthText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private TextMeshProUGUI _levelText;
    #endregion

    #region Inicialização e Ciclo de Vida
    private void Awake()
    {
        _inputActions = new();
        CloseScreen();
    }

    private void Update()
    {
        if (_inputActions.Player.CancelarSeleção.WasPressedThisFrame())
        {
           CloseScreen(); 
        }
    }

    private void OnEnable()
    {
        _inputActions.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Disable();
    }
    #endregion

    #region Lógica de Negócio (UI)
    /// <summary>
    /// Preenche os campos de texto com os dados do NPC e abre o painel de recrutamento.
    /// </summary>
    public void OpenScreen(RecruitableNPC selectedNpc, NPCsData npcData)
    {
        _recruitableNpc = selectedNpc;

        _healthText.text = "Vida: " + $"{npcData.MaxHealth:F2}";
        _classText.text = "Classe: " + npcData.CreatureClass;
        _typeText.text = "Tipo: " + npcData.CreatureType;
        _nameText.text = "Nome: " + npcData.NpcName;
        _strengthText.text = "Forca: " + $"{npcData.Strength:F2}";
        _costText.text = "Custo: " + $"{npcData.Cost:F2}";
        _levelText.text = "Level: " + npcData.Level;

        Button acceptButton = _buttons.GetChild(0).GetComponent<Button>();
        Button declineButton = _buttons.GetChild(1).GetComponent<Button>();

        acceptButton.onClick.RemoveAllListeners();
        acceptButton.onClick.AddListener(() => Recruit(true));

        declineButton.onClick.RemoveAllListeners();
        declineButton.onClick.AddListener(() => Recruit(false));

        _buttons.gameObject.SetActive(true);
        _background.gameObject.SetActive(true);
        _texts.gameObject.SetActive(true);
    }

    public void CloseScreen()
    {
        _buttons.gameObject.SetActive(false);
        _background.gameObject.SetActive(false);
        _texts.gameObject.SetActive(false);
    }

    /// <summary>
    /// Processa a decisão do jogador sobre a contratação.
    /// </summary>
    public void Recruit(bool shouldRecruit)
    {
        if (shouldRecruit)
        {
            _recruitableNpc.GetComponent<NPCsMovement>().IrParaOBarco(_playerCrew.transform);
            _playerCrew.CrewList.Add(_recruitableNpc.gameObject);

            SFXManager.Instance?.PlayContract();

            CloseScreen();
        }
        else
        {
            CloseScreen();
        }
    }
    #endregion
}