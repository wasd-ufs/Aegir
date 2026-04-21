using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Inventory))]
public class CrewData : MonoBehaviour
{
    public List<GameObject> crew = new();
    public int maxCrewLength;
    public Inventory inventory;

    private bool inicializadoManualmente = false;

    void Awake()
    {
        if (inicializadoManualmente) return;
        SubscreverEventosMorte();
        foreach(GameObject npc in crew)
        {
            NPCsData data = npc.GetComponent<NPCsData>();
            data.Heal(data.vidaMáxima);
        }
        inventory = GetComponent<Inventory>();
    }

    void Start()
    {
        // Garante subscrição para membros adicionados manualmente via InicializarManualmente
        if (inicializadoManualmente)
            SubscreverEventosMorte();
    }

    private void SubscreverEventosMorte()
    {
        foreach (GameObject membro in crew)
        {
            NPCsData npc = membro?.GetComponent<NPCsData>();
            if (npc != null)
                npc.OnMorte += OnMembroMorreu;
        }
    }

    public void InicializarManualmente(GameObject membro)
    {
        inicializadoManualmente = true;
        maxCrewLength = 1;
        crew.Clear();
        crew.Add(membro);
    }

    public List<float> GetCrewHP()
    {
        List<float> cHP = new();
        foreach (GameObject NPC in crew)
            cHP.Add(NPC.GetComponent<NPCsData>().GetVidaAtual());
        return cHP;
    }

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

    public void AddToCrew(GameObject NPC)
    {
        if (crew.Count >= maxCrewLength) return;

        crew.Add(NPC);
        NPCsData npc = NPC.GetComponent<NPCsData>();
        if (npc != null)
            npc.OnMorte += OnMembroMorreu;
    }

    private void OnMembroMorreu(NPCsData npc)
    {
        npc.OnMorte -= OnMembroMorreu;
        
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

    public void RemoveFromCrew(GameObject NPC)
    {
        crew.Remove(NPC);
    }

    public void HealUnits(List<GameObject> alvos, float healAmount, int qtdMaximaDeAlvos)
    {
        int qtdAlvos = Mathf.Min(crew.Count, Random.Range(0, qtdMaximaDeAlvos + 1));
        int alvosAcessados = 0;
        foreach (GameObject alvo in alvos)
            if (crew.Contains(alvo))
            {
                alvo.GetComponent<NPCsData>().Heal(healAmount);
                alvosAcessados++;
                if (alvosAcessados >= qtdAlvos) break;
            }
    }
}

