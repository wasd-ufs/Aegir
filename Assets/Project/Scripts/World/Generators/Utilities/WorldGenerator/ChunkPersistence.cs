using System.IO;
using UnityEngine;

/// <summary>
/// Responsável por toda a leitura e escrita de chunks em disco.
/// Cada chunk é serializado como um arquivo <c>chunk_X_Y.dat</c> contendo
/// um byte por célula (índice do tile colapsado).
/// </summary>
public class ChunkPersistence
{
    // =========================================================================
    // Campos Privados
    // =========================================================================

    private readonly string _savePath;

    // =========================================================================
    // Inicialização
    // =========================================================================

    public ChunkPersistence()
    {
        _savePath = Application.persistentDataPath + "/map_data/";
        if (!Directory.Exists(_savePath)) Directory.CreateDirectory(_savePath);
    }

    // =========================================================================
    // API Pública
    // =========================================================================

    /// <summary>
    /// Grava os bytes do chunk no arquivo <c>chunk_X_Y.dat</c>.
    /// Não faz nada se <paramref name="dataArray"/> for nulo.
    /// </summary>
    public void SaveChunkToDisk(Vector2Int position, byte[] dataArray)
    {
        if (dataArray != null)
            File.WriteAllBytes(BuildFilePath(position), dataArray);
    }

    /// <summary>
    /// Carrega os bytes do chunk do arquivo <c>chunk_X_Y.dat</c>.
    /// Retorna <c>null</c> se o arquivo não existir.
    /// </summary>
    public byte[] LoadChunkFromDisk(Vector2Int position)
    {
        string path = BuildFilePath(position);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>
    /// Grava os dados JSON de estruturas do chunk no arquivo <c>chunk_X_Y_structures.json</c>.
    /// </summary>
    public void SaveStructuresToDisk(Vector2Int position, string json)
    {
        string path = BuildStructuresFilePath(position);
        if (!string.IsNullOrEmpty(json))
        {
            File.WriteAllText(path, json);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Carrega os dados JSON de estruturas do chunk. Retorna <c>null</c> se o arquivo não existir.
    /// </summary>
    public string LoadStructuresFromDisk(Vector2Int position)
    {
        string path = BuildStructuresFilePath(position);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Verifica se existem estruturas salvas em disco para o chunk.
    /// </summary>
    public bool HasStructuresFile(Vector2Int position)
    {
        return File.Exists(BuildStructuresFilePath(position));
    }

    /// <summary>
    /// Deleta todos os arquivos de save (<c>.dat</c> e <c>.json</c>) da pasta de save.
    /// </summary>
    public void ClearSaveData()
    {
        if (!Directory.Exists(_savePath)) return;

        int deletedCount = 0;
        foreach (string file in Directory.GetFiles(_savePath, "*.*"))
        {
            if (file.EndsWith(".dat") || file.EndsWith(".json"))
            {
                File.Delete(file);
                deletedCount++;
            }
        }

        Debug.Log($"[ChunkPersistence] Cleared {deletedCount} save files from {_savePath}");
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    private string BuildFilePath(Vector2Int position)
        => _savePath + $"chunk_{position.x}_{position.y}.dat";

    private string BuildStructuresFilePath(Vector2Int position)
        => _savePath + $"chunk_{position.x}_{position.y}_structures.json";
}