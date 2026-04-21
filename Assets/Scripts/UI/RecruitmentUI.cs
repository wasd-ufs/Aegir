using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentUI : MonoBehaviour
{
    public PlayerInputActions inputActions;
    public RectTransform Fundo, Textos, Botões;
    public TextMeshProUGUI Vida, Classe, Tipo, Nome, Força, Custo, Level;
    private RecruitableNPC recruitableNPC;
    public CrewData playerCrew;

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

        Button b1 = Botões.GetChild(0).GetComponent<Button>(), b2 = Botões.GetChild(1).GetComponent<Button>();

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
        Botões.gameObject.SetActive(!true);
        Fundo.gameObject.SetActive(!true);
        Textos.gameObject.SetActive(!true);
    }

    public void Contratar(bool resposta)
    {
        if (resposta)
        {
            recruitableNPC.GetComponent<NPCsMovement>().IrParaOBarco(playerCrew.transform);
            playerCrew.crew.Add(recruitableNPC.gameObject);
            SFXManager.Instance?.TocarContrato();
            FecharTela();
        }
        else
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

}
