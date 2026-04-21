using UnityEngine;
using UnityEditor;
using System.IO;

public class MapEditorUtils : Editor
{
    [MenuItem("WFC/Limpar Dados do Mapa")]
    public static void ClearMapData()
    {
        string path = Application.persistentDataPath + "/map_data/";
        
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            Debug.Log("<color=green>Sucesso:</color> Pasta 'map_data' foi limpa!");
        }
        else
        {
            Debug.LogWarning("Pasta de dados não encontrada.");
        }
    }
}