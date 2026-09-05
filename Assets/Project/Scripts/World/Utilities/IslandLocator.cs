using UnityEngine;
using System.Collections.Generic;
using System.Linq;
/// <summary>
/// Utilitário de localização e agrupamento espacial de ilhas no mapa oceânico.
/// Detecta clusters de terra e calcula centros de massa para posicionamento de POIs.
/// </summary>
public class IslandLocator
{
    private IslandMapSampler _sampler;
    private Vector2Int _chunkSize;
    private float _landThreshold = IslandMapSampler.ISLAND_EDGE_THRESHOLD;

    public IslandLocator(IslandMapSampler sampler, Vector2Int chunkSize)
    {
        _sampler = sampler;
        _chunkSize = chunkSize;
    }

    /// <summary>
    /// Encontra ilhas dentro de um anel de distância a partir de um ponto de origem.
    /// Retorna os centros das ilhas encontradas em coordenadas de chunk.
    /// </summary>
    public List<Vector2Int> FindIslandsInRange(Vector2Int originChunk, int minRadius, int maxRadius)
    {
        var landChunksList = new List<Vector2Int>();

        for (int x = -maxRadius; x <= maxRadius; x++)
        {
            for (int y = -maxRadius; y <= maxRadius; y++)
            {
                // Filtra pelo anel — só o que está entre min e max
                float distance = Mathf.Sqrt(x * x + y * y);
                if (distance < minRadius || distance > maxRadius) continue;

                Vector2Int chunkCoord = new Vector2Int(originChunk.x + x, originChunk.y + y);
                if (chunkCoord == Vector2Int.zero) continue;

                float globalX = chunkCoord.x * _chunkSize.x + _chunkSize.x / 2f;
                float globalY = chunkCoord.y * _chunkSize.y + _chunkSize.y / 2f;

                if (IsChunkMostlyLand(chunkCoord))
                    landChunksList.Add(chunkCoord);
            }
        }

        return GroupIntoIslands(landChunksList)
            .Select(island => GetIslandCenter(island))
            .ToList();
    }

    private List<List<Vector2Int>> GroupIntoIslands(List<Vector2Int> landChunks)
    {
        var remaining = new HashSet<Vector2Int>(landChunks);
        var islandsList = new List<List<Vector2Int>>();

        while (remaining.Count > 0)
        {
            var island = new List<Vector2Int>();
            var floodQueue = new Queue<Vector2Int>();

            Vector2Int start = System.Linq.Enumerable.First(remaining);
            floodQueue.Enqueue(start);
            remaining.Remove(start);

            while (floodQueue.Count > 0)
            {
                Vector2Int current = floodQueue.Dequeue();
                island.Add(current);

                foreach (var neighbor in GetAllNeighbors(current))
                {
                    if (remaining.Contains(neighbor))
                    {
                        remaining.Remove(neighbor);
                        floodQueue.Enqueue(neighbor);
                    }
                }
            }

            islandsList.Add(island);
        }

        return islandsList;
    }

    private bool IsChunkMostlyLand(Vector2Int chunkCoord)
    {
        // Amostra 4 pontos dentro do chunk (quarts)
        float[] offsetsX = { 0.25f, 0.75f, 0.25f, 0.75f };
        float[] offsetsY = { 0.25f, 0.25f, 0.75f, 0.75f };
        
        int landCount = 0;
        for (int i = 0; i < 4; i++)
        {
            float globalX = chunkCoord.x * _chunkSize.x + _chunkSize.x * offsetsX[i];
            float globalY = chunkCoord.y * _chunkSize.y + _chunkSize.y * offsetsY[i];
            if (_sampler.Sample(globalX, globalY) > _landThreshold)
                landCount++;
        }

        // Só conta como terra se os 4 pontos forem terra
        return landCount >= 4;
    }

    private Vector2Int GetIslandCenter(List<Vector2Int> island)
    {
        int sumX = 0, sumY = 0;
        foreach (var chunk in island) { sumX += chunk.x; sumY += chunk.y; }
        Vector2Int average = new Vector2Int(sumX / island.Count, sumY / island.Count);

        // Retorna o chunk de terra mais próximo do centro calculado
        return island.OrderBy(c => (c - average).sqrMagnitude).First();
    }

    private IEnumerable<Vector2Int> GetAllNeighbors(Vector2Int coord)
    {
        // Direções Cardeais (Ortogonais)
        yield return coord + Vector2Int.up;
        yield return coord + Vector2Int.down;
        yield return coord + Vector2Int.left;
        yield return coord + Vector2Int.right;

        // Direções Diagonais
        yield return coord + new Vector2Int(1, 1);   // Nordeste
        yield return coord + new Vector2Int(1, -1);  // Sudeste
        yield return coord + new Vector2Int(-1, 1);  // Noroeste
        yield return coord + new Vector2Int(-1, -1); // Sudoeste
    }
}