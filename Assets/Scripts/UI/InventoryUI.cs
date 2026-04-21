using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class InventoryUI : MonoBehaviour
{
    public Inventory inventory;
    public Transform container, crewContainer, fundo, title;
    public Sprite uiSprite;
    public GameObject slot, button;
    private bool inventarioAberto = false;
    private ItemData itemPendente; 
     private PlayerInputActions inputActions;
    private bool esperandoAlvo = false;

    void Awake()
    {
        inputActions = new();
        AtualizarUI();

        container.gameObject.SetActive(inventarioAberto);
        title.gameObject.SetActive(inventarioAberto);
        crewContainer.gameObject.SetActive(inventarioAberto);
        fundo.gameObject.SetActive(inventarioAberto);
    }

    void Update()
    {
        if (inputActions.Player.Inventory.WasPressedThisFrame())
        {
            AtualizarUI();
            inventarioAberto = !inventarioAberto;
            container.gameObject.SetActive(inventarioAberto);
            title.gameObject.SetActive(inventarioAberto);
            fundo.gameObject.SetActive(inventarioAberto);
            if (esperandoAlvo && inventarioAberto)
                crewContainer.gameObject.SetActive(true);
            else
                crewContainer.gameObject.SetActive(false);
        }

        if (inputActions.Player.CancelarSeleção.WasPressedThisFrame())
        {
            CancelarSeleção();
        }
    }

    public void AtualizarUI()
    {
        foreach (Transform item in container)
        {
            Destroy(item.gameObject);
        }

        foreach (Inventory.Slot item in inventory.InventorySlots)
        {
            GameObject newSlot = Instantiate(slot, container);
            newSlot.transform.GetChild(0).GetComponent<Image>().sprite = item.item.Icon;

            ItemData itemSelecionado = item.item;
            newSlot.GetComponent<Button>().onClick.AddListener(() => PrepararUsoItem(itemSelecionado));

            if (item.quantity <= 1)
                newSlot.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "";
            else
                newSlot.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = item.quantity + " x";
        }

        int slotsVazios = inventory.MaxItemsPerInventory - inventory.InventorySlots.Count;

        for (int i = 0; i < slotsVazios; i++)
        {
            GameObject newSlot = Instantiate(slot, container);
            newSlot.transform.GetChild(0).GetComponent<Image>().sprite = uiSprite;
            newSlot.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "";               
        }
    }

    public void AtualizarTripulaçãoUI()
    {
        foreach (Transform item in crewContainer)
            Destroy(item.gameObject);

        foreach (GameObject npc in inventory.GetComponent<CrewData>().crew)
        {
            NPCsData nPCs = npc.GetComponent<NPCsData>();
            if (!itemPendente.possibleTypes.Contains(nPCs.creatureType)) continue;

            bool compativel = false;

            if (itemPendente is ConsumableData)
                compativel = true;
            else if (itemPendente is WeaponData weaponData && weaponData.classe.Contains(nPCs.creatureClass))
                compativel = true;
            else if (itemPendente is ArmorData armorData && armorData.classe.Contains(nPCs.creatureClass))
                compativel = true;

            if (!compativel) continue;

            GameObject newTripulant = Instantiate(button, crewContainer);
            newTripulant.GetComponent<Button>().onClick.AddListener(() => AplicarItemEmAlvo(nPCs));
            newTripulant.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = nPCs.NPC_Name;
        }

        crewContainer.gameObject.SetActive(true);
    }

    public void AplicarItemEmAlvo(NPCsData alvo)
    {
        if (!esperandoAlvo || itemPendente == null) return;

        if (itemPendente is ConsumableData consumable)
        {
            alvo.AplicarConsumivel(consumable);
            inventory.RemoverItem(itemPendente, 1);
            SFXManager.Instance?.TocarItem();
        }
        else if (itemPendente is WeaponData weaponData)
        {
            WeaponData armaAntiga = alvo.EquiparArma(weaponData);
            inventory.RemoverItem(itemPendente, 1);
            if (armaAntiga != null)
                inventory.AdicionarItem(armaAntiga, 1);
            SFXManager.Instance?.TocarItem();
        }
        else if (itemPendente is ArmorData armorData)
        {
            ArmorData armaduraAntiga = alvo.EquiparArmadura(armorData);
            inventory.RemoverItem(itemPendente, 1);
            if (armaduraAntiga != null)
                inventory.AdicionarItem(armaduraAntiga, 1);
            SFXManager.Instance?.TocarItem();
        }

        esperandoAlvo = false;
        itemPendente = null;
        crewContainer.gameObject.SetActive(false);
        AtualizarUI();
    }

    public void PrepararUsoItem(ItemData itemEscolhido)
    {
        if (itemEscolhido is ConsumableData consumivel 
         || itemEscolhido is WeaponData weaponData
         || itemEscolhido is ArmorData armorData)
        {
            itemPendente = itemEscolhido;
            esperandoAlvo = true;
            Debug.Log("Selecione o membro da tripulação para curar!");
            AtualizarTripulaçãoUI();
        }
    }

    public void CancelarSeleção()
    {
        esperandoAlvo = false;
        itemPendente = null;
        crewContainer.gameObject.SetActive(false);
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
