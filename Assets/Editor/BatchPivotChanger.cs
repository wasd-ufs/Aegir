using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.IO;

namespace Aegir.EditorTools
{
    /// <summary>
    /// Utilitário de editor para automatizar a alteração do ponto de pivô de múltiplos sprites para a base.
    /// </summary>
    public class BatchPivotChanger : EditorWindow
    {
        /// <summary>
        /// Ponto de entrada do menu que localiza os sprites selecionados e altera seus pivôs para a base.
        /// </summary>
        [MenuItem("Aegir Tools/Move Pivots to Bottom")]
        public static void ChangeSelectedSpritesPivotsToBottom()
        {
            var selectedTexturesArray = Selection.GetFiltered(typeof(Texture2D), SelectionMode.DeepAssets);

            if (selectedTexturesArray.Length == 0)
            {
                Debug.LogWarning("Nenhuma textura ou sprite foi selecionada na aba Project!");
                return;
            }

            int modifiedTexturesCount = 0;

            foreach (Object currentObject in selectedTexturesArray)
            {
                string assetPathText = AssetDatabase.GetAssetPath(currentObject);
                TextureImporter textureImporter = AssetImporter.GetAtPath(assetPathText) as TextureImporter;

                if (textureImporter != null)
                {
                    UpdateTextureImporterSettings(textureImporter, assetPathText);
                    AssetDatabase.ImportAsset(assetPathText, ImportAssetOptions.ForceUpdate);
                    modifiedTexturesCount++;
                }
            }

            Debug.Log($"Sucesso! Os pivôs de {modifiedTexturesCount} texturas foram movidos para a base.");
        }

        /// <summary>
        /// Modifica as configurações do TextureImporter aplicando o pivô na base com base no modo do sprite.
        /// </summary>
        private static void UpdateTextureImporterSettings(TextureImporter textureImporter, string assetPathText)
        {
            textureImporter.isReadable = true;

            // Ambas as configurações agora utilizam a interface estável de Data Provider da Unity
            if (textureImporter.spriteImportMode == SpriteImportMode.Single || 
                textureImporter.spriteImportMode == SpriteImportMode.Multiple)
            {
                ApplyPivotToSpriteData(assetPathText);
            }
        }

        /// <summary>
        /// Utiliza a API SpriteDataProvider para aplicar o pivô na base em qualquer configuração de Sprite (Single ou Multiple).
        /// </summary>
        private static void ApplyPivotToSpriteData(string assetPathText)
        {
            var assetDataProviderFactories = new SpriteDataProviderFactories();
            assetDataProviderFactories.Init();
            
            Texture2D textureAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPathText);
            ISpriteEditorDataProvider spriteDataProvider = assetDataProviderFactories.GetSpriteEditorDataProviderFromObject(textureAsset);

            if (spriteDataProvider != null)
            {
                spriteDataProvider.InitSpriteEditorDataProvider();
                
                SpriteRect[] spriteRectsArray = spriteDataProvider.GetSpriteRects();

                for (int i = 0; i < spriteRectsArray.Length; i++)
                {
                    spriteRectsArray[i].alignment = SpriteAlignment.BottomCenter;
                    spriteRectsArray[i].pivot = new Vector2(0.5f, 0.0f);
                }
                spriteDataProvider.SetSpriteRects(spriteRectsArray);
                spriteDataProvider.Apply();
            }
        }
    }
}