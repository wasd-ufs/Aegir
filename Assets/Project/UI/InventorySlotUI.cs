using TMPro;
using UnityEngine;

public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _itemQuantityText;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private UnityEngine.UI.Image _image;

    public void ConfigurateVisual(ItemData item, int quantity)
    {
        _image.sprite = item.Icon;
        _itemNameText.text = item.ItemName;

        if (quantity <= 1)
            _itemQuantityText.text = "";
        else
            _itemQuantityText.text = quantity + " x";
    }
}
