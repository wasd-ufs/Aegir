using System.IO;
using UnityEngine;

public class ChunkPersistence
{
    private string _savePath;

    public ChunkPersistence()
    {
        _savePath = Application.persistentDataPath + "/map_data/";
        if (!Directory.Exists(_savePath)) Directory.CreateDirectory(_savePath);
    }

    public void SaveChunkToDisk(Vector2Int position, byte[] dataArray)
    {
        if (dataArray != null)
        {
            File.WriteAllBytes(_savePath + $"chunk_{position.x}_{position.y}.dat", dataArray);
        }
    }

    public byte[] LoadChunkFromDisk(Vector2Int position)
    {
        string path = _savePath + $"chunk_{position.x}_{position.y}.dat";
        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }
        return null;
    }

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
}