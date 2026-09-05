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
    /// Deleta todos os arquivos <c>.dat</c> da pasta de save.
    /// </summary>
    public void ClearSaveData()
    {
        if (!Directory.Exists(_savePath)) return;

        int deletedCount = 0;
        foreach (string file in Directory.GetFiles(_savePath, "*.dat"))
        {
            File.Delete(file);
            deletedCount++;
        }

        Debug.Log($"[ChunkPersistence] Cleared {deletedCount} .dat files from {_savePath}");
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    private string BuildFilePath(Vector2Int position)
        => _savePath + $"chunk_{position.x}_{position.y}.dat";
}