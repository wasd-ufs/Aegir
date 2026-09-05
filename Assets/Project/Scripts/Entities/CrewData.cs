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
    public List<GameObject> CrewList = new();
    [SerializeField] private int _maxCrewLength;

    [HideInInspector]
    public Inventory Inventory;

    private bool _isManuallyInitialized = false;

    /// <summary>Disparado quando a composição da tripulação é modificada.</summary>
    public event System.Action OnCrewChanged;
    #endregion

    #region Ciclo de Vida (Unity)
    private void Awake()
    {
        if (_isManuallyInitialized) return;

        SubscribeDeathEvents();

        // Garante que todos da tripulação inicial comecem de vida cheia
        foreach (GameObject npcObject in CrewList)
        {
            NPCsData npcData = npcObject.GetComponent<NPCsData>();
            npcData.Heal(npcData.MaxHealth);
        }

        Inventory = GetComponent<Inventory>();
    }

    private void Start()
    {
        // Garante subscrição para membros adicionados manualmente via script
        if (_isManuallyInitialized)
            SubscribeDeathEvents();
    }
    #endregion

    #region Inicialização e Eventos
    private void SubscribeDeathEvents()
    {
        foreach (GameObject crewMember in CrewList)
        {
            NPCsData npcData = crewMember?.GetComponent<NPCsData>();

            if (npcData != null)
                npcData.OnDeath += OnCrewMemberDied;
        }
    }

    /// <summary>
    /// Utilizado pela mecânica de "StartFight" caso uma criatura solitária precise
    /// de um CrewData temporário em runtime para participar da batalha.
    /// </summary>
    public void InitializeManually(GameObject crewMember)
    {
        _isManuallyInitialized = true;

        _maxCrewLength = 1;

        CrewList.Clear();
        CrewList.Add(crewMember);
        OnCrewChanged?.Invoke();
    }
    #endregion

    #region Gerenciamento de Dano e Cura Coletivos
    public List<float> GetCrewHealthList()
    {
        List<float> crewHealthList = new();

        foreach (GameObject npcObject in CrewList)
        {
            crewHealthList.Add(
                npcObject.GetComponent<NPCsData>().GetCurrentHealth()
            );
        }

        return crewHealthList;
    }

    /// <summary>
    /// Distribui dano aleatoriamente a um determinado número máximo de alvos dentro desta tripulação.
    /// </summary>
    public void DoDamage(
        List<GameObject> targetsList,
        float damageAmount,
        NPCsData.DamageType damageType,
        int maxTargetCount
    )
    {
        int targetCount = Mathf.Min(
            CrewList.Count,
            Random.Range(0, maxTargetCount + 1)
        );

        int accessedTargets = 0;

        foreach (GameObject targetObject in targetsList)
        {
            if (CrewList.Contains(targetObject))
            {
                targetObject.GetComponent<NPCsData>()
                    .TakeDamage(damageAmount, damageType);

                accessedTargets++;

                if (accessedTargets >= targetCount)
                    break;
            }
        }
    }

    /// <summary>
    /// Distribui cura aleatoriamente a um determinado número máximo de alvos dentro desta tripulação.
    /// </summary>
    public void HealUnits(
        List<GameObject> targetsList,
        float healAmount,
        int maxTargetCount
    )
    {
        int targetCount = Mathf.Min(
            CrewList.Count,
            Random.Range(0, maxTargetCount + 1)
        );

        int accessedTargets = 0;

        foreach (GameObject targetObject in targetsList)
        {
            if (CrewList.Contains(targetObject))
            {
                targetObject.GetComponent<NPCsData>()
                    .Heal(healAmount);

                accessedTargets++;

                if (accessedTargets >= targetCount)
                    break;
            }
        }
    }
    #endregion

    #region Modificadores de Tripulação
    public void AddToCrew(GameObject npcObject)
    {
        if (CrewList.Count >= _maxCrewLength)
            return;

        CrewList.Add(npcObject);

        NPCsData npcData = npcObject.GetComponent<NPCsData>();

        if (npcData != null)
            npcData.OnDeath += OnCrewMemberDied;

        OnCrewChanged?.Invoke();
    }

    public void RemoveFromCrew(GameObject npcObject)
    {
        CrewList.Remove(npcObject);
        OnCrewChanged?.Invoke();
    }

    /// <summary>
    /// Lida com a decisão do que fazer com o GameObject de uma entidade ao ter seu HP zerado.
    /// Unidades aliadas podem sofrer permadeath baseadas em sua "chanceDeMortePermanente",
    /// enquanto o barco ou capitão apenas "desmaiam" (são desativados) aguardando o fim da batalha.
    /// </summary>
    private void OnCrewMemberDied(NPCsData npcData)
    {
        npcData.OnDeath -= OnCrewMemberDied;

        // Evita memory leaks e chamadas duplas
        if (gameObject.CompareTag("Player"))
        {
            bool isCaptain =
                npcData.CreatureClass == NPCsData.Class.Capitao;

            bool isBoat =
                npcData.CreatureClass == NPCsData.Class.Barco;

            if (isCaptain || isBoat)
            {
                npcData.gameObject.SetActive(false);
            }
            else
            {
                float randomNumber = Random.Range(0.0f, 1.0f);

                if (randomNumber < npcData.PermanentDeathChance)
                {
                    CrewList.Remove(npcData.gameObject);
                    Destroy(npcData.gameObject);
                }
                else
                {
                    npcData.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            npcData.gameObject.SetActive(false);
            npcData.isAlive = false;
        }
    }
    #endregion
}