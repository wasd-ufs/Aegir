using TMPro;
using UnityEngine;

/// <summary>
/// Interface para o sistema de interação com estruturas.
/// Exibe a interface contendo informações de cada estrutura.
/// </summary>
public class StructuresUI : MonoBehaviour
{
    [SerializeField] private RectTransform _backgroundTransform;
    [SerializeField] private TextMeshProUGUI _structureNameText;

    private void Awake()
    {
        CloseScreen();
    }

    public void ShowScreen(InteractableStructures interactableStructure, string structureName)
    {
        _structureNameText.text = structureName;
        _backgroundTransform.gameObject.SetActive(true);
        _structureNameText.gameObject.SetActive(true);
    }

    public void CloseScreen()
    {
        _backgroundTransform.gameObject.SetActive(false);
        _structureNameText.gameObject.SetActive(false);
    }
}