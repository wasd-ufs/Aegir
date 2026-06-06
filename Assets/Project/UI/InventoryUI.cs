using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

/// <summary>
/// Controla a interface grafica do inventario do jogador.
/// Gerencia a exibicao de itens, selecao de alvos para consumiveis e equipamentos.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    #region Referencias de UI
    [Header("Containers")]
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private Transform _actionsContainer;
    [SerializeField] private Transform _background;
    [SerializeField] private Transform _inventoryDivs;
    [SerializeField] private Transform _selectedItemDiv;
    [SerializeField] private Transform _extras;
    [SerializeField] private Transform _popUpContainer;
    

    [Header("Item Selecionado")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private TextMeshProUGUI _selectedItemDescription;
    [SerializeField] private TextMeshProUGUI _selectedItemName;
    [SerializeField] private TextMeshProUGUI _selectedItemPrice;
    // Exibe tipo, peso e atributos especificos do item num unico bloco de texto.
    [SerializeField] private TextMeshProUGUI _selectedItemFullInformationalText;

    [Header("PopUp")]
    [SerializeField] private Transform _popUpItemsContainer;
    [SerializeField] private TextMeshProUGUI _popUpTitle;
    

    [Header("Prefabs e Estetica")]
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private GameObject _actionPrefab;
    [SerializeField] private GameObject _popUpPrefab;
    #endregion

    #region Estado do Inventario
    [Header("Estado Interno")]
    [SerializeField] private Inventory _inventory;
    [SerializeField] private TextMeshProUGUI _totalWeight;

    private bool _isInventoryOpen = false;
    private Inventory.Slot _selectedSlot = new();
    private List<GameObject> currentSlots = new();
    private PlayerInputActions _inputActions;
    private int _lastSelectedSlotIndex;
    #endregion

    #region Inicializacao e Ciclo de Vida
    private void Awake()
    {
        _inputActions = new();
        UpdateUI();

        _slotContainer.gameObject.SetActive(_isInventoryOpen);
        _background.gameObject.SetActive(_isInventoryOpen);
        _inventoryDivs.gameObject.SetActive(_isInventoryOpen);
        _extras.gameObject.SetActive(_isInventoryOpen);
    }

    private void Update()
    {
        if (_inputActions.Player.Inventory.WasPressedThisFrame())
        {
            UpdateUI();
            _isInventoryOpen = !_isInventoryOpen;
            UpdateActionsContainer();

            if (!_isInventoryOpen)
            {
                _selectedSlot = new();
            }

            _slotContainer.gameObject.SetActive(_isInventoryOpen);
            _background.gameObject.SetActive(_isInventoryOpen);
            _inventoryDivs.gameObject.SetActive(_isInventoryOpen);
            _extras.gameObject.SetActive(_isInventoryOpen);
        }
        
        if(_inputActions.Player.CancelarSelecao.WasPressedThisFrame())
        {
            if (_popUpContainer.gameObject.activeSelf)
            {
                _popUpContainer.gameObject.SetActive(false);
                return;
            }

            _selectedSlot = new();
            _selectedItemDiv.gameObject.SetActive(false);
            EventSystem.current.SetSelectedGameObject(null);
            UpdateActionsContainer();
        }

        if(_inputActions.Player.Descartar.WasPressedThisFrame() && _selectedSlot.item != null)
        {
            DiscardItem();
        }

        if(_inputActions.Player.Usar.WasPressedThisFrame() && _selectedSlot.item != null)
        {
            UseItem();
        }

        if(_inputActions.Player.OrganizarInventario.WasPressedThisFrame())
        {
            OpenOrganizePopUp();
        }
    }

    private void OnEnable()  => _inputActions.Enable();
    private void OnDisable() => _inputActions.Disable();
    #endregion

    #region Logica de UI
    /// <summary>
    /// Reconstroi visualmente os slots do inventario baseado nos dados da classe Inventory.
    /// </summary>
    public void UpdateUI()
    {
        foreach (Transform inventoryItem in _slotContainer)
            Destroy(inventoryItem.gameObject);

        currentSlots = new();

        for (int i = 0; i < _inventory.InventorySlots.Count; i++)
            ConfigurateSlot(i);
        
        if (_selectedSlot.item != null)
            _selectedItemDiv.gameObject.SetActive(true); 
        else
            _selectedItemDiv.gameObject.SetActive(false);    

        if(currentSlots.Count > 0)
            EventSystem.current.SetSelectedGameObject(currentSlots[currentSlots.Count - 1]);

        _totalWeight.text = $"peso total: {_inventory.CalculateTotalWeight():F1}/{_inventory.GetMaxInventoryWeight():F1} kg\n[alinhamento: pirata procurado]";
    }

    public void ConfigurateSlot(int index)
    {
        Inventory.Slot slot = _inventory.InventorySlots[index];

        GameObject newSlot = Instantiate(_slotPrefab, _slotContainer);
        currentSlots.Add(newSlot);

        InventorySlotUI slotUI = newSlot.GetComponent<InventorySlotUI>();

        slotUI.ConfigurateVisual(slot.item, slot.quantity);

        slotUI.OnSlotSelected.AddListener(() => SelectItem(slot));
        slotUI.OnSlotSelected.AddListener(() => _lastSelectedSlotIndex = index);
    }

    public void SelectItem(Inventory.Slot slot)
    {
        ItemData item = slot.item;
        _itemImage.sprite = item.Icon;
        _selectedItemName.text = item.ItemName;
        _selectedItemDescription.text = item.Description;
        _selectedItemPrice.text = $"Preco: {item.UnitaryPrice:F2}";
        _selectedItemFullInformationalText.text = item.GetFullDescriptionText();

        _selectedSlot = slot;
        _selectedItemDiv.gameObject.SetActive(true);
        UpdateActionsContainer();
    }


    public void UpdateActionsContainer()
    {
        foreach (Transform action in _actionsContainer)
            Destroy(action.gameObject);

        foreach(InputAction action in GetCurrentPossibleActions())
        {
            GameObject newAction = Instantiate(_actionPrefab, _actionsContainer);
            newAction.GetComponent<TextMeshProUGUI>().text = $"[{action.GetBindingDisplayString()}] {action.name}";
        }
    }

    public List<InputAction> GetCurrentPossibleActions()
    {
        List<InputAction> inputActions = new();
        if(_isInventoryOpen)
        {
            if(_selectedSlot.item != null)
            {
                inputActions.Add(_inputActions.Player.Usar);
                inputActions.Add(_inputActions.Player.Descartar);
                inputActions.Add(_inputActions.Player.CancelarSelecao);
            }
            inputActions.Add(_inputActions.Player.OrganizarInventario);
            inputActions.Add(_inputActions.Player.Sair);
        }
        return inputActions;
    }

    public void UseItem()
    {
        if (_selectedSlot.item == null) return;

        _selectedSlot.item.UseItem();

        _selectedSlot = _inventory.RemoveItemAt(_lastSelectedSlotIndex); 
        
        if (_selectedSlot.quantity > 0)
            UpdateIndividualSlotUI();
        else
        {
            UpdateUI();
            UpdateActionsContainer();
            if (currentSlots.Count > 0 && _lastSelectedSlotIndex > 0)
                    EventSystem.current.SetSelectedGameObject(currentSlots[_lastSelectedSlotIndex - 1]);
                else if (currentSlots.Count > 0 && _lastSelectedSlotIndex == 0)
                    EventSystem.current.SetSelectedGameObject(currentSlots[currentSlots.Count - 1]);
        }
    }
    public void DiscardItem()
    {
        if (_selectedSlot.item == null) return;

        _selectedSlot = _inventory.RemoveItemAt(_lastSelectedSlotIndex); 

        if(_selectedSlot.quantity > 0)
            UpdateIndividualSlotUI();
        else
        {
            UpdateUI();
            UpdateActionsContainer();
            if (currentSlots.Count > 0 && _lastSelectedSlotIndex > 0)
                EventSystem.current.SetSelectedGameObject(currentSlots[_lastSelectedSlotIndex - 1]);
            else if (currentSlots.Count > 0 && _lastSelectedSlotIndex == 0)
                EventSystem.current.SetSelectedGameObject(currentSlots[currentSlots.Count - 1]);
        }
    }

    /// <summary>
    /// Abre o pop-up de organização do inventário com as opções de ordenação disponíveis.
    /// A lógica de ordenação em si é delegada à classe <see cref="Inventory"/>.
    /// </summary>
    public void OpenOrganizePopUp()
    {
        GameObject firstButton = null;
        foreach (Transform child in _popUpItemsContainer)
            Destroy(child.gameObject);

        _popUpContainer.GetChild(0).GetComponentInChildren<TextMeshProUGUI>().text = "Organizar por: ";

        var sortOptions = new List<(string label, System.Action callback)>
        {
            ("Tipo de Item",       () => _inventory.SortByItemType()),
            ("Ordem Alfabetica",   () => _inventory.SortAlphabetically()),
            ("Raridade / Nivel",   () => _inventory.SortByRarityOrLevel()),
            ("Preco / Valor",      () => _inventory.SortByPrice()),
        };

        foreach (var (label, callback) in sortOptions)
        {
            GameObject popUpOption = Instantiate(_popUpPrefab, _popUpItemsContainer);
            popUpOption.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = label;

            if (popUpOption.TryGetComponent<Button>(out Button btn))
            {
                btn.onClick.AddListener(() =>
                {
                    callback.Invoke();
                    UpdateUI();
                    _popUpContainer.gameObject.SetActive(false);
                });
                if (firstButton == null)
                {
                    firstButton = popUpOption.gameObject;
                }
            }
        }

        _popUpContainer.gameObject.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstButton);
    }

    private void UpdateIndividualSlotUI()
    {
        InventorySlotUI slotUI = _slotContainer.GetChild(_lastSelectedSlotIndex).GetComponent<InventorySlotUI>();
        slotUI.ConfigurateVisual(_selectedSlot.item, _selectedSlot.quantity);
        _totalWeight.text = $"peso total: {_inventory.CalculateTotalWeight():F1}/{_inventory.GetMaxInventoryWeight():F1} kg\n[alinhamento: pirata procurado]";
        UpdateActionsContainer();
    }
    #endregion
}