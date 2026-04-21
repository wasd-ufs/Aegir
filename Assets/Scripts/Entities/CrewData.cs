using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mantém o registro do coletivo da tripulação (seja a do jogador ou a de um grupo de inimigos).
/// Ouve ativamente a morte das unidades e lida com sua exclusão do grupo ou desativação.
/// Centraliza também o Inventário compartilhado daquele grupo.
/// </summary>
[RequireComponent(typeof(Inventory))]
public class CrewData : MonoBehaviour
{
    #region Membros e Inventário
    [Header("Membros da Tripulação")]
    public List<GameObject> crew = new();
    public int maxCrewLength;
    
    [HideInInspector]
    public Inventory inventory;
    
    private bool inicializadoManualmente = false;
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        if (inicializadoManualmente) return;
        
        SubscreverEventosMorte();
        
        // Garante que todos da tripulação inicial comecem de vida cheia
        foreach(GameObject npc in crew)
        {
            NPCsData data = npc.GetComponent<NPCsData>();
            data.Heal(data.vidaMáxima);
        }
        inventory = GetComponent<Inventory>();
    }

    void Start()
    {
        // Garante subscrição para membros adicionados manualmente via script
        if (inicializadoManualmente)
            SubscreverEventosMorte();
    }
    #endregion

    #region Inicialização e Eventos
    private void SubscreverEventosMorte()
    {
        foreach (GameObject membro in crew)
        {
            NPCsData npc = membro?.GetComponent<NPCsData>();
            if (npc != null)
                npc.OnMorte += OnMembroMorreu;
        }
    }

    /// <summary>
    /// Utilizado pela mecânica de "StartFight" caso uma criatura solitária precise
    /// de um CrewData temporário em runtime para participar da batalha.
    /// </summary>
    public void InicializarManualmente(GameObject membro)
    {
        inicializadoManualmente = true;
        maxCrewLength = 1;
        crew.Clear();
        crew.Add(membro);
    }
    #endregion

    #region Gerenciamento de Dano e Cura Coletivos
    public List<float> GetCrewHP()
    {
        List<float> cHP = new();
        foreach (GameObject NPC in crew)
            cHP.Add(NPC.GetComponent<NPCsData>().GetVidaAtual());
        return cHP;
    }

    /// <summary>
    /// Distribui dano aleatoriamente a um determinado número máximo de alvos dentro desta tripulação.
    /// </summary>
    public void DoDamage(List<GameObject> alvos, float dano, NPCsData.DamageType damageType, int qtdMaximaDeAlvos)
    {
        int qtdAlvos = Mathf.Min(crew.Count, Random.Range(0, qtdMaximaDeAlvos + 1));
        int alvosAcessados = 0;

        foreach (GameObject alvo in alvos)
        {
            if (crew.Contains(alvo))
            {
                alvo.GetComponent<NPCsData>().TakeDamage(dano, damageType);
                alvosAcessados++;
                if (alvosAcessados >= qtdAlvos) break;
            }
        }
    }

    /// <summary>
    /// Distribui cura aleatoriamente a um determinado número máximo de alvos dentro desta tripulação.
    /// </summary>
    public void HealUnits(List<GameObject> alvos, float healAmount, int qtdMaximaDeAlvos)
    {
        int qtdAlvos = Mathf.Min(crew.Count, Random.Range(0, qtdMaximaDeAlvos + 1));
        int alvosAcessados = 0;
        
        foreach (GameObject alvo in alvos)
        {
            if (crew.Contains(alvo))
            {
                alvo.GetComponent<NPCsData>().Heal(healAmount);
                alvosAcessados++;
                if (alvosAcessados >= qtdAlvos) break;
            }
        }
    }
    #endregion

    #region Modificadores de Tripulação
    public void AddToCrew(GameObject NPC)
    {
        if (crew.Count >= maxCrewLength) return;

        crew.Add(NPC);
        NPCsData npc = NPC.GetComponent<NPCsData>();
        if (npc != null)
            npc.OnMorte += OnMembroMorreu;
    }

    public void RemoveFromCrew(GameObject NPC)
    {
        crew.Remove(NPC);
    }

    /// <summary>
    /// Lida com a decisão do que fazer com o GameObject de uma entidade ao ter seu HP zerado.
    /// Unidades aliadas podem sofrer permadeath baseadas em sua "chanceDeMortePermanente",
    /// enquanto o barco ou capitão apenas "desmaiam" (são desativados) aguardando o fim da batalha.
    /// </summary>
    private void OnMembroMorreu(NPCsData npc)
    {
        npc.OnMorte -= OnMembroMorreu; // Evita memory leaks e chamadas duplas
        
        if (gameObject.CompareTag("Player"))
        {
            if (npc.creatureClass == NPCsData.Class.Capitão || npc.creatureClass == NPCsData.Class.Barco)
                npc.gameObject.SetActive(false);
            else
            {
                float randomNumber = Random.Range(0.0f, 1.0f);
                if(randomNumber < npc.chanceDeMortePermanente)
                {
                    crew.Remove(npc.gameObject);
                    Destroy(npc.gameObject);
                }
                else
                {
                    npc.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            npc.gameObject.SetActive(false);
            npc.GetComponent<NPCsData>().isAlive = false;
        }
    }
    #endregion
}