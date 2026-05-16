using UnityEngine;
using UnityEditor;

namespace Aegir.EditorTools
{
    /// <summary>
    /// Utilitário de editor para alterar o Sprite Sort Point de múltiplos Prefabs para 'Pivot'.
    /// </summary>
    public class PrefabSortPointUpdater : EditorWindow
    {
        /// <summary>
        /// Localiza os Prefabs selecionados e atualiza o componente SpriteRenderer de todos eles.
        /// </summary>
        [MenuItem("Aegir Tools/Update Prefabs Sort Point to Pivot")]
        public static void UpdateSelectedPrefabsSortPoint()
        {
            var selectedGameObjectsArray = Selection.GetFiltered(typeof(GameObject), SelectionMode.DeepAssets);

            if (selectedGameObjectsArray.Length == 0)
            {
                Debug.LogWarning("Nenhum Prefab foi selecionado na aba Project!");
                return;
            }

            int modifiedPrefabsCount = 0;

            foreach (GameObject currentPrefab in selectedGameObjectsArray)
            {
                string assetPathText = AssetDatabase.GetAssetPath(currentPrefab);
                
                using (var editingScope = new PrefabUtility.EditPrefabContentsScope(assetPathText))
                {
                    GameObject prefabRootObject = editingScope.prefabContentsRoot;

                    SpriteRenderer[] spriteRenderersArray = prefabRootObject.GetComponentsInChildren<SpriteRenderer>(true);
                    bool hasModifiedPrefab = false;

                    foreach (SpriteRenderer currentRenderer in spriteRenderersArray)
                    {
                        if (currentRenderer.spriteSortPoint != SpriteSortPoint.Pivot)
                        {
                            currentRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
                            hasModifiedPrefab = true;
                        }
                    }

                    if (hasModifiedPrefab)
                    {
                        modifiedPrefabsCount++;
                    }
                }
            }

            Debug.Log($"Sucesso! O Sort Point de {modifiedPrefabsCount} Prefabs foi alterado para Pivot.");
        }
    }
}