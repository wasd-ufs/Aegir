using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Avalia a matriz colapsada e instancia estruturas e decorações no mapa procedural.
/// Coordena-se com o IslandSettlementPlanner para traçar caminhos (Layer - 2) e dispor
/// estruturas adaptadas aos lotes com fachadas voltadas para o Sul (estilo Pokémon).
/// </summary>
public class StructureGenerator : MonoBehaviour
{
    [SerializeField] private List<StructureData> _structuresList;
    [SerializeField] private Transform _structuresContainer;

    [Serializable]
    public struct StructureSaveData
    {
        public string structureName;
        public Vector3 structureWorldPosition;
        public float isolationRadius;
        public Vector2Int chunkPosition;
        [NonSerialized] public GameObject instance;
    }

    public List<StructureSaveData> SavedStructuresList { get; private set; } = new List<StructureSaveData>();

    private WorldTileQuery _tileQuery;
    private Vector2Int _chunkSize;
    private float _cachedCellSize;
    private ChunkLifecycleManager _lifecycleManager;
    private int _worldSeed;
    private IslandLocator _islandLocator;
    private IslandMapSampler _islandMapSampler;
    private TilesetData _tilesetData;
    private IslandSettlementPlanner _settlementPlanner;
    private RuleManager _ruleManager;
    private CompatibilityCache _compatibilityCache;

    public IslandSettlementPlanner SettlementPlanner => _settlementPlanner;

    public void Setup(
        WorldTileQuery tileQuery,
        ChunkLifecycleManager lifecycleManager,
        Vector2Int chunkSize,
        float cachedCellSize,
        int worldSeed,
        IslandLocator islandLocator = null,
        IslandMapSampler islandMapSampler = null,
        TilesetData tilesetData = null,
        RuleManager ruleManager = null)
    {
        _tileQuery = tileQuery;
        _lifecycleManager = lifecycleManager;
        _chunkSize = chunkSize;
        _cachedCellSize = cachedCellSize;
        _worldSeed = worldSeed;
        _islandLocator = islandLocator;
        _islandMapSampler = islandMapSampler;
        _tilesetData = tilesetData;
        _ruleManager = ruleManager;

        EnsureCompatibilityCache();

        if (_islandLocator != null && _islandMapSampler != null)
        {
            _settlementPlanner = new IslandSettlementPlanner(
                _islandMapSampler,
                _islandLocator,
                _chunkSize,
                _worldSeed,
                _structuresList,
                GetTileAtGlobal);
        }
    }

    /// <summary>
    /// Consulta o tile em coordenadas globais do mundo a partir dos chunks ativos gerenciados pelo LifecycleManager.
    /// </summary>
    public Tile GetTileAtGlobal(int globalX, int globalY)
    {
        if (_lifecycleManager == null || _chunkSize.x <= 0 || _chunkSize.y <= 0) return null;

        int chunkX = Mathf.FloorToInt((float)globalX / _chunkSize.x);
        int chunkY = Mathf.FloorToInt((float)globalY / _chunkSize.y);
        int localX = (globalX % _chunkSize.x + _chunkSize.x) % _chunkSize.x;
        int localY = (globalY % _chunkSize.y + _chunkSize.y) % _chunkSize.y;

        MapGenerator chunk = _lifecycleManager.GetActiveChunk(new Vector2Int(chunkX, chunkY));
        if (chunk != null)
        {
            return chunk.GetTileAt(localX, localY);
        }
        return null;
    }

    /// <summary>
    /// Remove e destrói todas as instâncias de estruturas que pertencem ao chunk destruído,
    /// prevenindo vazamentos de memória e duplicação de GameObjects.
    /// </summary>
    public void ClearStructuresForChunk(Vector2Int chunkPosition)
    {
        for (int i = SavedStructuresList.Count - 1; i >= 0; i--)
        {
            if (SavedStructuresList[i].chunkPosition == chunkPosition)
            {
                if (SavedStructuresList[i].instance != null)
                {
                    Destroy(SavedStructuresList[i].instance);
                }
                SavedStructuresList.RemoveAt(i);
            }
        }
    }

    public void ProcessDecorations()
    {
        var waitingChunksList = _lifecycleManager.GetChunksWaitingForDecoration();

        for (int i = waitingChunksList.Count - 1; i >= 0; i--)
        {
            if (AreAllNeighborsReady(waitingChunksList[i]))
            {
                ScanAndGenerateStructures(waitingChunksList[i]);
                _lifecycleManager.RemoveChunkWaitingForDecoration(waitingChunksList[i]);
            }
        }
    }

    private bool AreAllNeighborsReady(Vector2Int chunkPosition)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int neighborPosition = chunkPosition + new Vector2Int(x, y);
                MapGenerator chunk = _lifecycleManager.GetActiveChunk(neighborPosition);

                if (chunk == null || chunk.IsGenerating) return false;
            }
        }
        return true;
    }

    private void ScanAndGenerateStructures(Vector2Int chunkPosition)
    {
        MapGenerator activeChunk = _lifecycleManager.GetActiveChunk(chunkPosition);
        if ((object)activeChunk == null) return;

        IslandSettlementPlan plan = _settlementPlanner?.GetPlanForChunk(chunkPosition);

        if (plan != null)
        {
            CarveRoadsInChunk(chunkPosition, activeChunk, plan);

            SpawnPlannedStructuresInChunk(chunkPosition, plan);
        }

        activeChunk.StructuresGenerated = true;

        SafeLog($"[StructureGenerator] Iniciando verificação final de compatibilidade para chunk {chunkPosition}...");

        ResolveTileCompatibilityInChunk(chunkPosition, activeChunk);
    }

    private void CarveRoadsInChunk(Vector2Int chunkPosition, MapGenerator activeChunk, IslandSettlementPlan plan)
    {
        if ((object)_tilesetData == null) return;
        if (!plan.RoadTilesByChunk.TryGetValue(chunkPosition, out var roadTiles)) return;

        int chunkOriginX = chunkPosition.x * _chunkSize.x;
        int chunkOriginY = chunkPosition.y * _chunkSize.y;

        foreach (PlannedRoadTile roadTile in roadTiles)
        {
            int localX = roadTile.Coordinate.x - chunkOriginX;
            int localY = roadTile.Coordinate.y - chunkOriginY;

            if (localX < 0 || localX >= _chunkSize.x || localY < 0 || localY >= _chunkSize.y) continue;

            if (!roadTile.IsVisual || roadTile.Layer == 2)
            {
                continue;
            }

            Tile existingTile = activeChunk.GetTileAt(localX, localY);
            if ((object)existingTile == null || existingTile.Metadata.Layer < 4)
            {
                continue;
            }

            Tile targetTile = FindTile(roadTile.Layer, roadTile.TileType, roadTile.Direction);
            if ((object)targetTile != null)
            {
                int tileIndex = _tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, targetTile));
                if (tileIndex >= 0)
                {
                    activeChunk.SetTileAt(localX, localY, tileIndex);
                }
            }
        }
    }

    private bool IsRoadTileCompatibleWithNeighbors(
        Tile targetTile,
        Vector2Int globalCoord,
        int localX,
        int localY,
        Vector2Int chunkPosition,
        MapGenerator activeChunk,
        IslandSettlementPlan plan)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            Vector2Int neighborGlobal = globalCoord + dir;
            Tile neighborTile = null;

            if (plan.RoadTiles.TryGetValue(neighborGlobal, out var plannedNeighbor) && plannedNeighbor.IsVisual)
            {
                neighborTile = FindTile(plannedNeighbor.Layer, plannedNeighbor.TileType, plannedNeighbor.Direction);
            }
            else
            {
                neighborTile = GetNeighborTile(localX, localY, dir, chunkPosition, activeChunk);
            }

            if (neighborTile != null)
            {
                if (!targetTile.IsCompatibleWith(neighborTile, dir))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public Tile GetNeighborTile(int localX, int localY, Vector2Int dir, Vector2Int chunkPosition, MapGenerator activeChunk)
    {
        int nLocalX = localX + dir.x;
        int nLocalY = localY + dir.y;

        if (nLocalX >= 0 && nLocalX < _chunkSize.x && nLocalY >= 0 && nLocalY < _chunkSize.y)
        {
            return activeChunk.GetTileAt(nLocalX, nLocalY);
        }

        if (_lifecycleManager != null && _chunkSize.x > 0 && _chunkSize.y > 0)
        {
            Vector2Int nChunkPos = chunkPosition + new Vector2Int(
                nLocalX < 0 ? -1 : (nLocalX >= _chunkSize.x ? 1 : 0),
                nLocalY < 0 ? -1 : (nLocalY >= _chunkSize.y ? 1 : 0)
            );
            MapGenerator nChunk = _lifecycleManager.GetActiveChunk(nChunkPos);
            if ((object)nChunk != null)
            {
                int wrappedX = (nLocalX % _chunkSize.x + _chunkSize.x) % _chunkSize.x;
                int wrappedY = (nLocalY % _chunkSize.y + _chunkSize.y) % _chunkSize.y;
                return nChunk.GetTileAt(wrappedX, wrappedY);
            }
        }

        return null;
    }

    private void EnsureCompatibilityCache()
    {
        if (_compatibilityCache == null && _ruleManager != null && _tilesetData != null)
        {
            _compatibilityCache = new CompatibilityCache(_ruleManager, _tilesetData);
            _compatibilityCache.BuildCache();
        }
    }

    /// <summary>
    /// Verifica se há incompatibilidade entre dois tiles adjacentes usando a mesma lógica
    /// que o WFC usa durante a geração (CompatibilityCache via RuleManager + CornerSockets).
    /// Quando o cache não está disponível (testes unitários ou chamada sem cache), faz fallback
    /// para IsCompatibleWith direto — sem hardcodes de layer que podem divergir do RuleManager.
    /// </summary>
    public static bool HasCompatibilityError(
        Tile currentTile,
        Tile neighbor,
        Vector2Int dir,
        CompatibilityCache compatibilityCache = null,
        TilesetData tilesetData = null)
    {
        if ((object)currentTile == null || (object)neighbor == null) return false;

        if (compatibilityCache != null && tilesetData != null && tilesetData.TilesetList != null)
        {
            int currentIdx = tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, currentTile));
            int neighborIdx = tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, neighbor));
            if (currentIdx >= 0 && neighborIdx >= 0)
            {
                int dirIdx = dir == Vector2Int.up ? 0 : dir == Vector2Int.down ? 1 : dir == Vector2Int.left ? 2 : 3;
                return !compatibilityCache.IsCompatible(currentIdx, neighborIdx, dirIdx);
            }
        }

        return !currentTile.IsCompatibleWith(neighbor, dir);
    }

    /// <summary>
    /// REGRA DO USUÁRIO: após a geração de estruturas, percorre toda a matriz da chunk
    /// em loop aninhado (for x / for y). Para cada célula colapsada:
    ///   1. Garante que o tile corresponde às informações corretas do tileset.
    ///   2. Verifica se seus sockets se encaixam com os de cada vizinho cardinal.
    ///   3. Se não encaixar, a célula volta ao estado de superposição e perde
    ///      todas as opções inválidas (elimina do PossibleBitsArray).
    ///   4. Se sobrar exatamente 1 opção válida, a célula é recolapsada para ela.
    ///      Se sobrarem várias, escolhe a de maior pontuação (FindCompatibleTile).
    ///      Se não sobrar nenhuma, restaura o tile original (evita célula vazia).
    /// O loop se repete tantas vezes quanto necessário, até que nenhuma célula seja
    /// alterada em um passe completo (convergência total).
    /// Cada passe usa um snapshot do estado anterior para ler os vizinhos, garantindo
    /// que mudanças feitas dentro do mesmo passe não influenciem a avaliação de outras
    /// células — comportamento correto do WFC de compatibilidade.
    /// </summary>
    public void ResolveTileCompatibilityInChunk(Vector2Int chunkPosition, MapGenerator activeChunk)
    {
        if ((object)_tilesetData == null || _tilesetData.TilesetList == null || _tilesetData.TilesetList.Count == 0) return;
        if ((object)activeChunk == null) return;

        EnsureCompatibilityCache();

        int tileCount = _tilesetData.TilesetList.Count;
        Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        const int maxPasses = 15;
        const int maxChangesPerCell = 2;
        int[,] cellChangeCount = new int[_chunkSize.x, _chunkSize.y];
        bool anyChangedInPass;
        int passCount = 0;
        int totalTilesFixed = 0;

        do
        {
            anyChangedInPass = false;
            passCount++;

            int[,] snapshot = new int[_chunkSize.x, _chunkSize.y];
            for (int sy = 0; sy < _chunkSize.y; sy++)
                for (int sx = 0; sx < _chunkSize.x; sx++)
                {
                    Cell sc = activeChunk.GetCellAt(sx, sy);
                    snapshot[sx, sy] = (sc != null && sc.IsCollapsed()) ? sc.CollapsedIndex() : -1;
                }

            for (int y = 0; y < _chunkSize.y; y++)
            {
                for (int x = 0; x < _chunkSize.x; x++)
                {
                    if (cellChangeCount[x, y] >= maxChangesPerCell) continue;

                    Cell cell = activeChunk.GetCellAt(x, y);
                    if (cell == null || !cell.IsCollapsed()) continue;

                    int currentIndex = snapshot[x, y];
                    if (currentIndex < 0 || currentIndex >= tileCount) continue;
                    Tile currentTile = _tilesetData.TilesetList[currentIndex];
                    if ((object)currentTile == null) continue;

                    bool isCompatibleWithAll = true;
                    var neighborList = new List<(Vector2Int dir, Tile neighbor)>(4);

                    foreach (var dir in cardinalDirs)
                    {
                        Tile neighbor = GetNeighborTileFromSnapshot(x, y, dir, chunkPosition, activeChunk, snapshot);
                        if ((object)neighbor == null) continue;

                        neighborList.Add((dir, neighbor));
                        if (HasCompatibilityError(currentTile, neighbor, dir, _compatibilityCache, _tilesetData))
                            isCompatibleWithAll = false;
                    }

                    if (isCompatibleWithAll || neighborList.Count == 0) continue;

                    cell.PossibleBitsArray.SetAll(true);

                    for (int i = 0; i < tileCount; i++)
                    {
                        if (!cell.PossibleBitsArray[i]) continue;

                        Tile candidate = _tilesetData.TilesetList[i];
                        if ((object)candidate == null)
                        {
                            cell.PossibleBitsArray[i] = false;
                            continue;
                        }

                        foreach (var (dir, neighbor) in neighborList)
                        {
                            if (HasCompatibilityError(candidate, neighbor, dir, _compatibilityCache, _tilesetData))
                            {
                                cell.PossibleBitsArray[i] = false;
                                break;
                            }
                        }
                    }

                    int remaining = cell.CountPossible();
                    int chosenIndex = -1;

                    if (remaining == 0)
                    {
                        Tile relaxed = FindCompatibleTile(currentTile, neighborList, _tilesetData, allowRelaxed: true, compatibilityCache: _compatibilityCache);
                        if ((object)relaxed != null)
                            chosenIndex = _tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, relaxed));
                    }
                    else if (remaining == 1)
                    {
                        chosenIndex = cell.CollapsedIndex();
                    }
                    else
                    {
                        Tile best = FindCompatibleTile(currentTile, neighborList, _tilesetData, allowRelaxed: false, compatibilityCache: _compatibilityCache);
                        if ((object)best != null)
                            chosenIndex = _tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, best));
                        else
                            chosenIndex = cell.CollapsedIndex();
                    }

                    if (chosenIndex < 0)
                    {
                        cell.CollapseCell(currentIndex);
                        continue;
                    }

                    cell.CollapseCell(chosenIndex);
                    if (chosenIndex != currentIndex)
                    {
                        activeChunk.RefreshTileFromCell(x, y);
                        anyChangedInPass = true;
                        totalTilesFixed++;
                        cellChangeCount[x, y]++;
                    }
                }
            }
        }
        while (anyChangedInPass && passCount < maxPasses);

        SafeLog($"[StructureGenerator] Verificação de compatibilidade concluída no chunk {chunkPosition}: {totalTilesFixed} célula(s) ajustada(s) em {passCount} passe(s).");
    }

    private static void SafeLog(string message)
    {
        try
        {
            Debug.Log(message);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Obtém o tile vizinho para verificação de compatibilidade.
    /// Para células internas ao chunk, lê o estado atualizado em tempo real (Gauss-Seidel),
    /// eliminando o ciclo oscilatório gerado por snapshots estáticos.
    /// Vizinhos externos ao chunk só são considerados se o chunk vizinho já passou pela
    /// Fase 3 (StructuresGenerated == true) — evita comparar com tiles que ainda serão
    /// corrigidos, o que causaria falsos positivos de incompatibilidade na borda.
    /// </summary>
    private Tile GetNeighborTileFromSnapshot(int localX, int localY, Vector2Int dir, Vector2Int chunkPosition, MapGenerator activeChunk, int[,] snapshot)
    {
        int nLocalX = localX + dir.x;
        int nLocalY = localY + dir.y;

        if (nLocalX >= 0 && nLocalX < _chunkSize.x && nLocalY >= 0 && nLocalY < _chunkSize.y)
        {
            if (snapshot != null)
            {
                int snapshotIndex = snapshot[nLocalX, nLocalY];
                if (snapshotIndex < 0 || snapshotIndex >= _tilesetData.TilesetList.Count) return null;
                return _tilesetData.TilesetList[snapshotIndex];
            }
            Cell nc = activeChunk.GetCellAt(nLocalX, nLocalY);
            if (nc != null && nc.IsCollapsed())
            {
                int idx = nc.CollapsedIndex();
                if (idx >= 0 && idx < _tilesetData.TilesetList.Count)
                    return _tilesetData.TilesetList[idx];
            }
            return null;
        }

        if (_lifecycleManager != null)
        {
            Vector2Int neighborChunkPos = chunkPosition + dir;
            MapGenerator neighborChunk = _lifecycleManager.GetActiveChunk(neighborChunkPos);
            if (neighborChunk == null || neighborChunk.IsGenerating) return null;
        }

        return GetNeighborTile(localX, localY, dir, chunkPosition, activeChunk);
    }

    /// <summary>
    /// Procura no tileset um tile que seja compatível com todos os vizinhos fornecidos.
    /// Prioriza tiles da mesma camada e tipo, e maior peso.
    /// </summary>
    public static Tile FindCompatibleTile(
        Tile currentTile,
        IEnumerable<(Vector2Int dir, Tile neighbor)> neighbors,
        TilesetData tilesetData,
        bool allowRelaxed = true,
        CompatibilityCache compatibilityCache = null)
    {
        if ((object)tilesetData == null || tilesetData.TilesetList == null || tilesetData.TilesetList.Count == 0)
            return null;

        var neighborList = neighbors as IList<(Vector2Int dir, Tile neighbor)> ?? neighbors.ToList();

        Tile bestCandidate = null;
        float bestScore = float.MinValue;

        foreach (Tile candidate in tilesetData.TilesetList)
        {
            if ((object)candidate == null) continue;

            bool compatible = true;
            foreach (var (dir, neighbor) in neighborList)
            {
                if ((object)neighbor != null && HasCompatibilityError(candidate, neighbor, dir, compatibilityCache, tilesetData))
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible) continue;

            float score = 0f;
            if ((object)currentTile != null)
            {
                int layerDiff = Math.Abs(candidate.Metadata.Layer - currentTile.Metadata.Layer);
                score += (10 - layerDiff) * 1000f;

                if (candidate.Metadata.Type == currentTile.Metadata.Type)
                    score += 500f;

                if (candidate.Metadata.Direction == currentTile.Metadata.Direction)
                    score += 100f;

                if (ReferenceEquals(candidate, currentTile))
                    score += 50f;
            }

            bool touchesSandOrTransition = false;
            bool touchesRoad = false;
            foreach (var (dir, neighbor) in neighborList)
            {
                if ((object)neighbor != null)
                {
                    if (neighbor.Metadata.Layer == 2 || (neighbor.Metadata.Layer == 3 && neighbor.Metadata.Type == Tile.TileType.Coast))
                        touchesSandOrTransition = true;
                    if (neighbor.Metadata.Layer == 3 && (neighbor.Metadata.Type == Tile.TileType.Coast || neighbor.Metadata.Type == Tile.TileType.Corner || neighbor.Metadata.Type == Tile.TileType.InnerCorner))
                        touchesRoad = true;
                }
            }

            if (touchesSandOrTransition && touchesRoad && candidate.Metadata.Type == Tile.TileType.InnerCorner)
            {
                score += 800f;
            }

            score += candidate.Weight;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != null || !allowRelaxed) return bestCandidate;

        Tile bestRelaxed = null;
        float bestRelaxedScore = float.MinValue;

        foreach (Tile candidate in tilesetData.TilesetList)
        {
            if ((object)candidate == null) continue;

            float score = 0f;
            bool hasFatalMismatch = false;

            foreach (var (dir, neighbor) in neighborList)
            {
                if ((object)neighbor == null) continue;

                var (c1, c2) = GetCandidateEdgeSockets(candidate.Metadata.Corners, dir);
                var (n1, n2) = GetNeighborEdgeSockets(neighbor.Metadata.Corners, dir);

                if (neighbor.Metadata.Layer == 0)
                {
                    if (candidate.Metadata.Layer >= 2 || c1 != 0 || c2 != 0)
                    {
                        hasFatalMismatch = true;
                        break;
                    }
                }

                if (neighbor.Metadata.Layer >= 2 && candidate.Metadata.Layer == 0)
                {
                    hasFatalMismatch = true;
                    break;
                }

                if (candidate.Metadata.Layer == 2 && candidate.Metadata.Type == Tile.TileType.Block &&
                    neighbor.Metadata.Layer == 4 && neighbor.Metadata.Type == Tile.TileType.Block)
                {
                    hasFatalMismatch = true;
                    break;
                }

                if (candidate.Metadata.Layer == 4 && candidate.Metadata.Type == Tile.TileType.Block &&
                    neighbor.Metadata.Layer == 2 && neighbor.Metadata.Type == Tile.TileType.Block)
                {
                    hasFatalMismatch = true;
                    break;
                }

                if (c1 == n1) score += 300f;
                else score -= 150f * Math.Max(1, Math.Abs(c1 - n1));

                if (c2 == n2) score += 300f;
                else score -= 150f * Math.Max(1, Math.Abs(c2 - n2));
            }

            if (hasFatalMismatch) continue;

            if ((object)currentTile != null)
            {
                int layerDiff = Math.Abs(candidate.Metadata.Layer - currentTile.Metadata.Layer);
                score += (10 - layerDiff) * 20f;

                if (currentTile.Metadata.Layer == 3 && candidate.Metadata.Layer == 3)
                    score += 150f;

                if (candidate.Metadata.Type == currentTile.Metadata.Type)
                    score += 30f;

                if (ReferenceEquals(candidate, currentTile))
                    score += 50f;
            }

            score += candidate.Weight;

            if (score > bestRelaxedScore)
            {
                bestRelaxedScore = score;
                bestRelaxed = candidate;
            }
        }

        return bestRelaxed;
    }

    private static (int, int) GetCandidateEdgeSockets(Tile.CornerSockets corners, Vector2Int dir)
    {
        if (dir == Vector2Int.right) return (corners.NorthEast, corners.SouthEast);
        if (dir == Vector2Int.left)  return (corners.NorthWest, corners.SouthWest);
        if (dir == Vector2Int.up)    return (corners.NorthWest, corners.NorthEast);
        if (dir == Vector2Int.down)  return (corners.SouthWest, corners.SouthEast);
        return (0, 0);
    }

    private static (int, int) GetNeighborEdgeSockets(Tile.CornerSockets corners, Vector2Int dir)
    {
        if (dir == Vector2Int.right) return (corners.NorthWest, corners.SouthWest);
        if (dir == Vector2Int.left)  return (corners.NorthEast, corners.SouthEast);
        if (dir == Vector2Int.up)    return (corners.SouthWest, corners.SouthEast);
        if (dir == Vector2Int.down)  return (corners.NorthWest, corners.NorthEast);
        return (0, 0);
    }

    /// <summary>
    /// Sobrecarga conveniente para busca com dicionário de vizinhos.
    /// </summary>
    public static Tile FindCompatibleTile(
        Tile currentTile,
        Dictionary<Vector2Int, Tile> neighbors,
        TilesetData tilesetData)
    {
        if (neighbors == null) return null;
        var list = neighbors.Select(kvp => (kvp.Key, kvp.Value));
        return FindCompatibleTile(currentTile, list, tilesetData);
    }

    private void SpawnPlannedStructuresInChunk(Vector2Int chunkPosition, IslandSettlementPlan plan)
    {
        if (!plan.StructuresByChunk.TryGetValue(chunkPosition, out var structures)) return;

        int chunkOriginX = chunkPosition.x * _chunkSize.x;
        int chunkOriginY = chunkPosition.y * _chunkSize.y;

        foreach (PlannedStructure planned in structures)
        {
            int localX = planned.GlobalTileOrigin.x - chunkOriginX;
            int localY = planned.GlobalTileOrigin.y - chunkOriginY;

            Vector3 tileOrigin = _tileQuery.GetTileWorldPosition(chunkPosition, localX, localY);

            float posX = tileOrigin.x + (planned.Dimensions.x * _cachedCellSize) / 2f;
            float posY = (planned.Blueprint.PivotMode == StructurePivotMode.BottomCenter)
                ? tileOrigin.y
                : tileOrigin.y + (planned.Dimensions.y * _cachedCellSize) / 2f;

            Vector3 worldPosition = new Vector3(posX, posY, 0f);

            GameObject instance = Instantiate(planned.Blueprint.StructurePrefab, worldPosition, Quaternion.identity, _structuresContainer);
            RegisterStructure(planned.Blueprint.StructureName, worldPosition, planned.Blueprint.IsolationRadius, chunkPosition, instance);
        }
    }

    private Tile FindTile(int layer, Tile.TileType type, Tile.TileDirection direction)
    {
        if ((object)_tilesetData == null || _tilesetData.TilesetList == null) return null;

        return _tilesetData.TilesetList.FirstOrDefault(t =>
            (object)t != null &&
            t.Metadata.Layer == layer &&
            t.Metadata.Type == type &&
            (type == Tile.TileType.Block || t.Metadata.Direction == direction));
    }

    private void RegisterStructure(string name, Vector3 position, float isolationRadius, Vector2Int chunkPosition, GameObject instance)
    {
        SavedStructuresList.Add(new StructureSaveData
        {
            structureName = name,
            structureWorldPosition = position,
            isolationRadius = isolationRadius,
            chunkPosition = chunkPosition,
            instance = instance
        });
    }

    private static List<int> BuildShuffledIndices(int count, System.Random random)
    {
        var indices = new List<int>(count);
        for (int i = 0; i < count; i++) indices.Add(i);

        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            int temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
        }

        return indices;
    }

    private static int HashChunkSeed(int worldSeed, int chunkX, int chunkY)
    {
        uint hash = (uint)worldSeed * 2654435761u;
        hash ^= (uint)(chunkX * 1664525 + 1013904223);
        hash ^= (uint)(chunkY * 22695477 + 1664525);
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;
        return (int)(hash & 0x7FFFFFFF);
    }
}