using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controla a interface gráfica do inventário do jogador.
/// Gerencia a exibição de itens, seleção de alvos para consumíveis e equipamentos.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    #region Referências de UI
    [Header("Containers")]
    [SerializeField] private Transform _container;
    [SerializeField] private Transform _crewContainer;
    [SerializeField] private Transform _background;
    [SerializeField] private Transform _title;

    [Header("Prefabs e Estética")]
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private GameObject _buttonPrefab;
    [SerializeField] private Sprite _uiSprite;
    #endregion

    #region Estado do Inventário
    [Header("Estado Interno")]
    [SerializeField] private Inventory _inventory;

    private bool _isInventoryOpen = false;
    private bool _isWaitingForTarget = false;
    private ItemData _pendingItem; 
    private PlayerInputActions _inputActions;
    #endregion

    #region Inicialização e Ciclo de Vida
    private void Awake()
    {
        _inputActions = new();
        UpdateUI();

        _container.gameObject.SetActive(_isInventoryOpen);
        _title.gameObject.SetActive(_isInventoryOpen);
        _crewContainer.gameObject.SetActive(_isInventoryOpen);
        _background.gameObject.SetActive(_isInventoryOpen);
    }

    private void Update()
    {
        if (_inputActions.Player.Inventory.WasPressedThisFrame())
        {
            UpdateUI();
            _isInventoryOpen = !_isInventoryOpen;

            _container.gameObject.SetActive(_isInventoryOpen);
            _title.gameObject.SetActive(_isInventoryOpen);
            _background.gameObject.SetActive(_isInventoryOpen);

            if (_isWaitingForTarget && _isInventoryOpen)
                _crewContainer.gameObject.SetActive(true);
            else
                _crewContainer.gameObject.SetActive(false);
        }

        if (_inputActions.Player.CancelarSeleção.WasPressedThisFrame())
        {
            CancelSelection();
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
    /// Reconstrói visualmente os slots do inventário baseado nos dados da classe Inventory.
    /// </summary>
    public void UpdateUI()
    {
        foreach (Transform inventoryItem in _container)
        {
            Destroy(inventoryItem.gameObject);
        }

        foreach (Inventory.Slot inventorySlot in _inventory.InventorySlots)
        {
            GameObject newSlot = Instantiate(_slotPrefab, _container);
            newSlot.transform.GetChild(0).GetComponent<Image>().sprite = inventorySlot.item.Icon;

            ItemData selectedItem = inventorySlot.item;
            newSlot.GetComponent<Button>().onClick.AddListener(() => PrepareItemUsage(selectedItem));

            if (inventorySlot.quantity <= 1)
                newSlot.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "";
            else
                newSlot.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = inventorySlot.quantity + " x";
        }

        int emptySlotsCount = _inventory.MaxItemsPerInventory - _inventory.InventorySlots.Count;

        for (int i = 0; i < emptySlotsCount; i++)
        {
            GameObject newSlot = Instantiate(_slotPrefab, _container);
            newSlot.transform.GetChild(0).GetComponent<Image>().sprite = _uiSprite;
            newSlot.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "";               
        }
    }

    /// <summary>
    /// Atualiza os botões da tripulação elegível para receber o item selecionado.
    /// </summary>
    public void UpdateCrewUI()
    {
        foreach (Transform crewMember in _crewContainer)
            Destroy(crewMember.gameObject);

        foreach (GameObject npcObject in _inventory.GetComponent<CrewData>().crew)
        {
            NPCsData npcData = npcObject.GetComponent<NPCsData>();

            if (!_pendingItem.possibleTypes.Contains(npcData.creatureType)) continue;

            bool isCompatible = false;

            if (_pendingItem is ConsumableData)
                isCompatible = true;
            else if (_pendingItem is WeaponData weaponData && weaponData.classe.Contains(npcData.creatureClass))
                isCompatible = true;
            else if (_pendingItem is ArmorData armorData && armorData.classe.Contains(npcData.creatureClass))
                isCompatible = true;

            if (!isCompatible) continue;

            GameObject newCrewMemberButton = Instantiate(_buttonPrefab, _crewContainer);
            newCrewMemberButton.GetComponent<Button>().onClick.AddListener(() => ApplyItemToTarget(npcData));
            newCrewMemberButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = npcData.NPC_Name;
        }

        _crewContainer.gameObject.SetActive(true);
    }

    public void ApplyItemToTarget(NPCsData targetNpc)
    {
        if (!_isWaitingForTarget || _pendingItem == null) return;

        if (_pendingItem is ConsumableData consumableData)
        {
            targetNpc.AplicarConsumivel(consumableData);
            _inventory.RemoverItem(_pendingItem, 1);
            SFXManager.Instance?.PlayItem();
        }
        else if (_pendingItem is WeaponData weaponData)
        {
            WeaponData oldWeapon = targetNpc.EquiparArma(weaponData);
            _inventory.RemoverItem(_pendingItem, 1);

            if (oldWeapon != null)
                _inventory.AdicionarItem(oldWeapon, 1);

            SFXManager.Instance?.PlayItem();
        }
        else if (_pendingItem is ArmorData armorData)
        {
            ArmorData oldArmor = targetNpc.EquiparArmadura(armorData);
            _inventory.RemoverItem(_pendingItem, 1);

            if (oldArmor != null)
                _inventory.AdicionarItem(oldArmor, 1);

            SFXManager.Instance?.PlayItem();
        }

        _isWaitingForTarget = false;
        _pendingItem = null;
        _crewContainer.gameObject.SetActive(false);

        UpdateUI();
    }

    public void PrepareItemUsage(ItemData selectedItem)
    {
        if (selectedItem is ConsumableData consumableData 
         || selectedItem is WeaponData weaponData
         || selectedItem is ArmorData armorData)
        {
            _pendingItem = selectedItem;
            _isWaitingForTarget = true;

            Debug.Log("Selecione o membro da tripulação para aplicar o item!");

            UpdateCrewUI();
        }
    }

    public void CancelSelection()
    {
        _isWaitingForTarget = false;
        _pendingItem = null;
        _crewContainer.gameObject.SetActive(false);
    }
    #endregion
}