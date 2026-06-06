using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class InventoryUI : MonoBehaviour
{
    #region Referencias de UI
    [Header("Containers")]
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private Transform _menuContainer;
    [SerializeField] private Transform _actionsContainer;
    [SerializeField] private Transform _background;
    [SerializeField] private Transform _inventoryDivs;
    [SerializeField] private Transform _selectedItemDiv;
    [SerializeField] private Transform _extras;
    [SerializeField] private Transform _popUpContainer;
    [SerializeField] private Transform _popUpItemsContainer;

    [Header("Item Selecionado")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private TextMeshProUGUI _selectedItemNameText;
    [SerializeField] private TextMeshProUGUI _selectedItemDescriptionText;
    [SerializeField] private TextMeshProUGUI _selectedItemPriceText;
    [SerializeField] private TextMeshProUGUI _selectedItemFullInfoText;

    [Header("Prefabs")]
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private GameObject _actionPrefab;
    [SerializeField] private GameObject _popUpPrefab;
    [SerializeField] private GameObject _menuPrefab;
    [SerializeField] private GameObject _menuBarPrefab;

    [Header("Estado Interno")]
    [SerializeField] private Inventory _inventory;
    [SerializeField] private TextMeshProUGUI _totalWeightText;
    #endregion

    #region Estado
    private bool _isInventoryOpen = false;
    private Inventory.Slot _selectedSlot = new();
    private List<GameObject> _currentSlotsList = new();
    private PlayerInputActions _inputActions;
    private int _lastSelectedSlotIndex;

    // Traducao das categorias para portugues sem acento
    private static readonly Dictionary<ItemData.ItemCategory, string> _categoryNamesDictionary = new()
    {
        { ItemData.ItemCategory.Weapon,       "Arma"              },
        { ItemData.ItemCategory.Armor,        "Armadura"          },
        { ItemData.ItemCategory.Consumable,   "Consumivel"        },
        { ItemData.ItemCategory.ShipMaterial, "Material do Navio" },
        { ItemData.ItemCategory.KeyItem,      "Item Chave"        },
        { ItemData.ItemCategory.Collectible,  "Colecionavel"      },
        { ItemData.ItemCategory.Misc,         "Misc"              },
    };
    #endregion

    #region Ciclo de Vida
    private void Awake()
    {
        _inputActions = new();
        UpdateUI(_inventory.InventorySlots);

        _slotContainer.gameObject.SetActive(false);
        _background.gameObject.SetActive(false);
        _inventoryDivs.gameObject.SetActive(false);
        _extras.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_inputActions.Player.Inventory.WasPressedThisFrame())
            HandleInventoryToggle();

        if (_inputActions.Player.CancelarSelecao.WasPressedThisFrame())
            HandleCancelSelection();

        if (_inputActions.Player.Descartar.WasPressedThisFrame() && _selectedSlot.item != null)
            DiscardItem();

        if (_inputActions.Player.Usar.WasPressedThisFrame() && _selectedSlot.item != null)
            UseItem();

        if (_inputActions.Player.OrganizarInventario.WasPressedThisFrame())
            OpenSortPopUp();
    }

    private void OnEnable()  => _inputActions.Enable();
    private void OnDisable() => _inputActions.Disable();
    #endregion

    #region Handlers de Input
    private void HandleInventoryToggle()
    {
        _isInventoryOpen = !_isInventoryOpen;
        UpdateUI(_inventory.InventorySlots);
        UpdateActionsContainer();

        if (!_isInventoryOpen)
            _selectedSlot = new();

        _slotContainer.gameObject.SetActive(_isInventoryOpen);
        _background.gameObject.SetActive(_isInventoryOpen);
        _inventoryDivs.gameObject.SetActive(_isInventoryOpen);
        _extras.gameObject.SetActive(_isInventoryOpen);
    }

    private void HandleCancelSelection()
    {
        if (_popUpContainer.gameObject.activeSelf)
        {
            _popUpContainer.gameObject.SetActive(false);
            if (_currentSlotsList.Count > 0)
                EventSystem.current.SetSelectedGameObject(_currentSlotsList[0]);
            return;
        }

        _selectedSlot = new();
        _selectedItemDiv.gameObject.SetActive(false);
        EventSystem.current.SetSelectedGameObject(null);
        UpdateActionsContainer();
    }
    #endregion

    #region UI Principal
    public void UpdateUI(List<Inventory.Slot> slots)
    {
        foreach (Transform child in _slotContainer)
            Destroy(child.gameObject);

        _currentSlotsList = new();

        for (int i = 0; i < slots.Count; i++)
            BuildSlot(i, slots);

        _selectedItemDiv.gameObject.SetActive(_selectedSlot.item != null);

        if (_currentSlotsList.Count > 0)
            EventSystem.current.SetSelectedGameObject(_currentSlotsList[0]);

        GameObject allButton = BuildMenu();
        RefreshWeightText();

        if (_currentSlotsList.Count <= 0)
            EventSystem.current.SetSelectedGameObject(allButton);
    }

    private void BuildSlot(int index, List<Inventory.Slot> slots)
    {
        Inventory.Slot slot = slots[index];

        GameObject newSlot = Instantiate(_slotPrefab, _slotContainer);
        _currentSlotsList.Add(newSlot);

        InventorySlotUI slotUI = newSlot.GetComponent<InventorySlotUI>();
        slotUI.ConfigurateVisual(slot.item, slot.quantity);
        slotUI.OnSlotSelected.AddListener(() => SelectItem(slot));
        slotUI.OnSlotSelected.AddListener(() => _lastSelectedSlotIndex = index);
    }

    public void SelectItem(Inventory.Slot slot)
    {
        _itemImage.sprite                = slot.item.Icon;
        _selectedItemNameText.text       = slot.item.ItemName;
        _selectedItemDescriptionText.text = slot.item.Description;
        _selectedItemPriceText.text      = $"Preco: {slot.item.UnitaryPrice:F2}";
        _selectedItemFullInfoText.text   = slot.item.GetFullDescriptionText();

        _selectedSlot = slot;
        _selectedItemDiv.gameObject.SetActive(true);
        UpdateActionsContainer();
    }

    public void UpdateActionsContainer()
    {
        foreach (Transform child in _actionsContainer)
            Destroy(child.gameObject);

        foreach (InputAction action in GetCurrentActions())
        {
            GameObject newAction = Instantiate(_actionPrefab, _actionsContainer);
            newAction.GetComponent<TextMeshProUGUI>().text = $"[{action.GetBindingDisplayString()}] {action.name}";
        }
    }

    private List<InputAction> GetCurrentActions()
    {
        var actionsList = new List<InputAction>();

        if (!_isInventoryOpen) return actionsList;

        if (_selectedSlot.item != null)
        {
            actionsList.Add(_inputActions.Player.Usar);
            actionsList.Add(_inputActions.Player.Descartar);
            actionsList.Add(_inputActions.Player.CancelarSelecao);
        }

        actionsList.Add(_inputActions.Player.OrganizarInventario);
        actionsList.Add(_inputActions.Player.Sair);
        return actionsList;
    }

    private GameObject BuildMenu()
    {
        foreach (Transform child in _menuContainer)
            Destroy(child.gameObject);

        // Botao "Tudo"
        GameObject allButton = Instantiate(_menuPrefab, _menuContainer);
        allButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Tudo";
        Instantiate(_menuBarPrefab, _menuContainer);

        if (allButton.TryGetComponent<Button>(out Button allBtn))
            allBtn.onClick.AddListener(() => UpdateUI(_inventory.InventorySlots));

        // Botao por categoria
        var categoriesArray = (ItemData.ItemCategory[]) System.Enum.GetValues(typeof(ItemData.ItemCategory));

        foreach (ItemData.ItemCategory category in categoriesArray)
        {
            GameObject menuButton = Instantiate(_menuPrefab, _menuContainer);
            menuButton.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = _categoryNamesDictionary[category];

            if (menuButton.TryGetComponent<Button>(out Button btn))
                btn.onClick.AddListener(() => UpdateUI(_inventory.FilterByItemType(category)));

            bool isLast = category == categoriesArray[categoriesArray.Length - 1];
            if (!isLast)
                Instantiate(_menuBarPrefab, _menuContainer);
        }
        return allButton;
    }

    private void RefreshWeightText()
    {
        _totalWeightText.text = $"peso total: {_inventory.CalculateTotalWeight():F1}/{_inventory.GetMaxInventoryWeight():F1} kg\n[alinhamento: pirata procurado]";
    }
    #endregion

    #region Acoes de Item
    public void UseItem()
    {
        if (_selectedSlot.item == null) return;

        _selectedSlot.item.UseItem();
        _selectedSlot = _inventory.RemoveItemAt(_lastSelectedSlotIndex);

        if (_selectedSlot.quantity > 0)
            RefreshCurrentSlotUI();
        else
            HandleSlotRemoved();
    }

    public void DiscardItem()
    {
        if (_selectedSlot.item == null) return;

        _selectedSlot = _inventory.RemoveItemAt(_lastSelectedSlotIndex);

        if (_selectedSlot.quantity > 0)
            RefreshCurrentSlotUI();
        else
            HandleSlotRemoved();
    }

    private void RefreshCurrentSlotUI()
    {
        InventorySlotUI slotUI = _slotContainer.GetChild(_lastSelectedSlotIndex).GetComponent<InventorySlotUI>();
        slotUI.ConfigurateVisual(_selectedSlot.item, _selectedSlot.quantity);
        RefreshWeightText();
        UpdateActionsContainer();
    }

    private void HandleSlotRemoved()
    {
        UpdateUI(_inventory.InventorySlots);
        UpdateActionsContainer();

        if (_currentSlotsList.Count == 0) return;

        int targetIndex = _lastSelectedSlotIndex > 0 ? _lastSelectedSlotIndex - 1 : 0;
        EventSystem.current.SetSelectedGameObject(_currentSlotsList[targetIndex]);
    }
    #endregion

    #region PopUp de Ordenacao
    public void OpenSortPopUp()
    {
        foreach (Transform child in _popUpItemsContainer)
            Destroy(child.gameObject);

        // Titulo do popup esta no primeiro filho do container
        _popUpContainer.GetChild(0).GetComponentInChildren<TextMeshProUGUI>().text = "Organizar por:";

        var sortOptionsList = new List<(string label, System.Action callback)>
        {
            ("Tipo de Item",     () => _inventory.SortByItemType()),
            ("Ordem Alfabetica", () => _inventory.SortAlphabetically()),
            ("Raridade / Nivel", () => _inventory.SortByRarityOrLevel()),
            ("Preco / Valor",    () => _inventory.SortByPrice()),
        };

        GameObject firstButton = null;

        foreach (var (label, callback) in sortOptionsList)
        {
            GameObject option = Instantiate(_popUpPrefab, _popUpItemsContainer);
            option.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = label;

            if (option.TryGetComponent<Button>(out Button btn))
            {
                btn.onClick.AddListener(() =>
                {
                    callback.Invoke();
                    UpdateUI(_inventory.InventorySlots);
                    _popUpContainer.gameObject.SetActive(false);
                });

                firstButton ??= option;
            }
        }

        _popUpContainer.gameObject.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstButton);
    }
    #endregion
}