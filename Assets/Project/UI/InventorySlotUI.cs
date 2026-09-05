using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
/// <summary>
/// Componente de interface que representa visualmente um único slot no inventário.
/// Exibe ícone, nome, peso acumulado e quantidade do item.
/// </summary>
public class InventorySlotUI : MonoBehaviour, ISelectHandler
{
    [SerializeField] private TextMeshProUGUI _itemQuantityText;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _itemTotalWeight;
    [SerializeField] private UnityEngine.UI.Image _image;
    
    public UnityEvent OnSlotSelected = new();
    public void ConfigurateVisual(BaseItemData item, int quantity)
    {
        _image.sprite = item.Icon;
        _itemNameText.text = item.ItemName;
        _itemTotalWeight.text = $"({item.UnitaryWeight * quantity} kg)"; 
        if (quantity <= 1)
            _itemQuantityText.text = "";
        else
            _itemQuantityText.text = quantity + "x";
    }

    public void OnSelect(BaseEventData eventData)
    {
        OnSlotSelected.Invoke();
    }
}
