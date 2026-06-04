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
    [SerializeField] private Transform _background;
    [SerializeField] private Transform _inventorySpaces;
    [SerializeField] private Transform _inventoryDivs;
    [SerializeField] private Transform _inventoryMenus;

    [Header("Item Selecionado")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private TextMeshProUGUI _selectedItemDescription;
    [SerializeField] private TextMeshProUGUI _selectedItemName;
    [SerializeField] private TextMeshProUGUI _selectedItemPrice;

    [Header("Prefabs e Estética")]
    [SerializeField] private GameObject _slotPrefab;
    #endregion

    #region Estado do Inventário
    [Header("Estado Interno")]
    [SerializeField] private Inventory _inventory;

    private bool _isInventoryOpen = false;
    private bool _isWaitingForTarget = false;
    private PlayerInputActions _inputActions;
    #endregion

    #region Inicialização e Ciclo de Vida
    private void Awake()
    {
        _inputActions = new();
        UpdateUI();

        _container.gameObject.SetActive(_isInventoryOpen);
        _inventorySpaces.gameObject.SetActive(_isInventoryOpen);
        _background.gameObject.SetActive(_isInventoryOpen);
        _inventoryDivs.gameObject.SetActive(_isInventoryOpen);
        _inventoryMenus.gameObject.SetActive(_isInventoryOpen);

    }

    private void Update()
    {
        if (_inputActions.Player.Inventory.WasPressedThisFrame())
        {
            UpdateUI();
            _isInventoryOpen = !_isInventoryOpen;

            _container.gameObject.SetActive(_isInventoryOpen);
            _inventorySpaces.gameObject.SetActive(_isInventoryOpen);
            _background.gameObject.SetActive(_isInventoryOpen);
            _inventoryDivs.gameObject.SetActive(_isInventoryOpen);
            _inventoryMenus.gameObject.SetActive(_isInventoryOpen);
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

        foreach (Inventory.Slot slot in _inventory.InventorySlots)
        {   
            ConfigurateSlot(slot);
        }
    }

    public void ConfigurateSlot(Inventory.Slot slot)
    {
        GameObject newSlot = Instantiate(_slotPrefab, _container);
        InventorySlotUI slotUI = newSlot.GetComponent<InventorySlotUI>();

        slotUI.ConfigurateVisual(slot.item, slot.quantity);
    
        Button slotButton = newSlot.GetComponent<Button>();
        slotButton.onClick.AddListener(() => SelectItem(slot.item));        
    }

    public void SelectItem(ItemData item)
    {
        _itemImage.sprite = item.Icon;
        _selectedItemDescription.text = item.Description;
        _selectedItemName.text = item.ItemName;
        _selectedItemPrice.text = $"Price: {item.UnitaryPrice:F2}";
    }

    #endregion
}