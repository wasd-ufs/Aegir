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

    [Serializable]
    public class ChunkStructuresSaveWrapper
    {
        public List<StructureSaveData> structures = new List<StructureSaveData>();
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
    private ChunkPersistence _persistence;

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
        RuleManager ruleManager = null,
        ChunkPersistence persistence = null)
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
        _persistence = persistence;

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

    public void SetStructuresList(List<StructureData> list) => _structuresList = list;
    public void SetStructuresContainer(Transform container) => _structuresContainer = container;
    public void SetPersistence(ChunkPersistence persistence) => _persistence = persistence;

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
    /// Serializa e grava em disco todas as estruturas pertencentes ao chunk especificado.
    /// </summary>
    public void SaveStructuresForChunk(Vector2Int chunkPosition, ChunkPersistence persistence = null)
    {
        ChunkPersistence targetPersistence = persistence ?? _persistence;
        if (targetPersistence == null) return;

        var chunkStructures = SavedStructuresList.Where(s => s.chunkPosition == chunkPosition).ToList();
        var wrapper = new ChunkStructuresSaveWrapper { structures = chunkStructures };
        string json = JsonUtility.ToJson(wrapper);
        targetPersistence.SaveStructuresToDisk(chunkPosition, json);
    }

    /// <summary>
    /// Carrega as estruturas do disco e reinstancia seus GameObjects na cena.
    /// Caso não exista arquivo salvo, utiliza o plano da ilha como fallback.
    /// </summary>
    public void LoadAndInstantiateStructuresForChunk(Vector2Int chunkPosition, ChunkPersistence persistence = null)
    {
        ChunkPersistence targetPersistence = persistence ?? _persistence;
        if (targetPersistence == null) return;

        // Evita duplicar se já existirem instâncias ativas na cena para este chunk
        if (SavedStructuresList.Any(s => s.chunkPosition == chunkPosition && s.instance != null))
        {
            return;
        }

        string json = targetPersistence.LoadStructuresFromDisk(chunkPosition);
        if (!string.IsNullOrEmpty(json))
        {
            ChunkStructuresSaveWrapper wrapper = JsonUtility.FromJson<ChunkStructuresSaveWrapper>(json);
            if (wrapper != null && wrapper.structures != null)
            {
                foreach (var saved in wrapper.structures)
                {
                    StructureData blueprint = _structuresList?.FirstOrDefault(s => s.StructureName == saved.structureName);
                    if (blueprint != null && blueprint.StructurePrefab != null)
                    {
                        GameObject instance = Instantiate(blueprint.StructurePrefab, saved.structureWorldPosition, Quaternion.identity, _structuresContainer);
                        RegisterStructure(saved.structureName, saved.structureWorldPosition, saved.isolationRadius, chunkPosition, instance);
                    }
                    else
                    {
                        Debug.LogWarning($"[StructureGenerator] Could not find blueprint or prefab for structure '{saved.structureName}' when reloading chunk {chunkPosition}");
                    }
                }
            }
        }
        else
        {
            // Fallback caso o chunk tenha sido gerado antes da persistência de estruturas:
            IslandSettlementPlan plan = _settlementPlanner?.GetPlanForChunk(chunkPosition);
            if (plan != null)
            {
                SpawnPlannedStructuresInChunk(chunkPosition, plan);
                SaveStructuresForChunk(chunkPosition, targetPersistence);
            }
        }
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
                    if (Application.isPlaying)
                    {
                        Destroy(SavedStructuresList[i].instance);
                    }
                    else
                    {
                        DestroyImmediate(SavedStructuresList[i].instance);
                    }
                }
                SavedStructuresList.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Remove e destrói todas as instâncias de estruturas ativas na cena e limpa o registro de memória.
    /// </summary>
    public void ClearAllSavedStructures()
    {
        for (int i = SavedStructuresList.Count - 1; i >= 0; i--)
        {
            if (SavedStructuresList[i].instance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(SavedStructuresList[i].instance);
                }
                else
                {
                    DestroyImmediate(SavedStructuresList[i].instance);
                }
            }
        }
        SavedStructuresList.Clear();
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
            SaveStructuresForChunk(chunkPosition, _persistence);
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

            if (!roadTile.IsVisual)
            {
                continue;
            }

            Tile existingTile = activeChunk.GetTileAt(localX, localY);
            if ((object)existingTile == null || existingTile.Metadata.Layer < 4)
            {
                continue;
            }

            if (existingTile.Metadata.Layer == roadTile.Layer &&
                existingTile.Metadata.Type == roadTile.TileType &&
                existingTile.Metadata.Direction == roadTile.Direction)
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
    private bool SetTileSafe(MapGenerator activeChunk, int localX, int localY, int layer, Tile.TileType type, Tile.TileDirection direction)
    {
        if (localX < 0 || localX >= _chunkSize.x || localY < 0 || localY >= _chunkSize.y) return false;
        Tile targetTile = FindTile(layer, type, direction);
        if ((object)targetTile != null)
        {
            int tileIndex = _tilesetData.TilesetList.FindIndex(t => ReferenceEquals(t, targetTile));
            if (tileIndex >= 0)
            {
                activeChunk.SetTileAt(localX, localY, tileIndex);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Resolve anomalias morfológicas de transição grama-areia e encontros via-costa:
    /// 1. Costas duplicadas na mesma direção (ex: Costa N acima de Costa N) -> substitui por Bloco de Areia.
    /// 2. Vias (Coast East/West) encontrando costas ortogonais (Coast North/South) -> converte para Quinas ou Internas.
    /// <summary>
    /// Resolve padrões morfológicos acoplados específicos que geram incompatibilidades locais
    /// de costa/via e não conseguem colapsar individualmente no WFC:
    /// 1. Costas duplicadas na mesma direção (ex: Coast North acima de Coast North) -> vira Sand Block.
    /// 2. Vias acopladas de 2 células (Coast East + Coast West) encontrando costa ortogonal (Coast North / Coast South)
    ///    -> convertidas em conjunto para Corner (se abrir para areia) ou InnerCorner (se fechar cul-de-sac).
    /// </summary>
    private int ResolveMorphologicalCoastIncompatibilities(Vector2Int chunkPosition, MapGenerator activeChunk)
    {
        int fixedCount = 0;

        // Caso 1: Costas duplicadas na mesma direção
        for (int x = 0; x < _chunkSize.x; x++)
        {
            for (int y = 0; y < _chunkSize.y; y++)
            {
                Tile current = activeChunk.GetTileAt(x, y);
                if ((object)current == null || current.Metadata.Layer != 3 || current.Metadata.Type != Tile.TileType.Coast) continue;

                if (current.Metadata.Direction == Tile.TileDirection.North)
                {
                    Tile below = GetNeighborTile(x, y, Vector2Int.down, chunkPosition, activeChunk);
                    if ((object)below != null && below.Metadata.Layer == 3 && below.Metadata.Type == Tile.TileType.Coast && below.Metadata.Direction == Tile.TileDirection.North)
                    {
                        if (SetTileSafe(activeChunk, x, y, 2, Tile.TileType.Block, Tile.TileDirection.None))
                            fixedCount++;
                    }
                }
                else if (current.Metadata.Direction == Tile.TileDirection.South)
                {
                    Tile above = GetNeighborTile(x, y, Vector2Int.up, chunkPosition, activeChunk);
                    if ((object)above != null && above.Metadata.Layer == 3 && above.Metadata.Type == Tile.TileType.Coast && above.Metadata.Direction == Tile.TileDirection.South)
                    {
                        if (SetTileSafe(activeChunk, x, y, 2, Tile.TileType.Block, Tile.TileDirection.None))
                            fixedCount++;
                    }
                }
                else if (current.Metadata.Direction == Tile.TileDirection.East)
                {
                    Tile left = GetNeighborTile(x, y, Vector2Int.left, chunkPosition, activeChunk);
                    if ((object)left != null && left.Metadata.Layer == 3 && left.Metadata.Type == Tile.TileType.Coast && left.Metadata.Direction == Tile.TileDirection.East)
                    {
                        if (SetTileSafe(activeChunk, x, y, 2, Tile.TileType.Block, Tile.TileDirection.None))
                            fixedCount++;
                    }
                }
                else if (current.Metadata.Direction == Tile.TileDirection.West)
                {
                    Tile right = GetNeighborTile(x, y, Vector2Int.right, chunkPosition, activeChunk);
                    if ((object)right != null && right.Metadata.Layer == 3 && right.Metadata.Type == Tile.TileType.Coast && right.Metadata.Direction == Tile.TileDirection.West)
                    {
                        if (SetTileSafe(activeChunk, x, y, 2, Tile.TileType.Block, Tile.TileDirection.None))
                            fixedCount++;
                    }
                }
            }
        }

        // Caso 2: Transições entre vias de 2 células (Coast East/West ou Coast North/South) e costa ortogonal
        // 2a. Via vertical (x: Coast East, x + 1: Coast West) encontrando costa ortogonal
        for (int x = 0; x < _chunkSize.x - 1; x++)
        {
            for (int y = 0; y < _chunkSize.y; y++)
            {
                Tile tLeft = activeChunk.GetTileAt(x, y);
                Tile tRight = activeChunk.GetTileAt(x + 1, y);
                if ((object)tLeft == null || (object)tRight == null) continue;
                if (tLeft.Metadata.Layer != 3 || tRight.Metadata.Layer != 3) continue;
                if (tLeft.Metadata.Type != Tile.TileType.Coast || tLeft.Metadata.Direction != Tile.TileDirection.East) continue;
                if (tRight.Metadata.Type != Tile.TileType.Coast || tRight.Metadata.Direction != Tile.TileDirection.West) continue;

                // Encontro ao Norte com Coast North
                Tile aboveLeft = GetNeighborTile(x, y, Vector2Int.up, chunkPosition, activeChunk);
                Tile aboveRight = GetNeighborTile(x + 1, y, Vector2Int.up, chunkPosition, activeChunk);
                if ((object)aboveLeft != null && (object)aboveRight != null &&
                    aboveLeft.Metadata.Layer == 3 && aboveLeft.Metadata.Type == Tile.TileType.Coast && aboveLeft.Metadata.Direction == Tile.TileDirection.North &&
                    aboveRight.Metadata.Layer == 3 && aboveRight.Metadata.Type == Tile.TileType.Coast && aboveRight.Metadata.Direction == Tile.TileDirection.North)
                {
                    Tile farN1 = GetNeighborTile(x, y + 1, Vector2Int.up, chunkPosition, activeChunk);
                    Tile farN2 = GetNeighborTile(x + 1, y + 1, Vector2Int.up, chunkPosition, activeChunk);
                    bool opensToSand = (farN1 == null || farN1.Metadata.Layer <= 2) && (farN2 == null || farN2.Metadata.Layer <= 2);

                    if (opensToSand && y + 1 < _chunkSize.y)
                    {
                        if (SetTileSafe(activeChunk, x, y + 1, 3, Tile.TileType.Corner, Tile.TileDirection.NorthEast)) fixedCount++;
                        if (SetTileSafe(activeChunk, x + 1, y + 1, 3, Tile.TileType.Corner, Tile.TileDirection.NorthWest)) fixedCount++;
                    }
                    else if (!opensToSand)
                    {
                        if (SetTileSafe(activeChunk, x, y, 3, Tile.TileType.InnerCorner, Tile.TileDirection.NorthWest)) fixedCount++;
                        if (SetTileSafe(activeChunk, x + 1, y, 3, Tile.TileType.InnerCorner, Tile.TileDirection.NorthEast)) fixedCount++;
                    }
                }

                // Encontro ao Sul com Coast South
                Tile belowLeft = GetNeighborTile(x, y, Vector2Int.down, chunkPosition, activeChunk);
                Tile belowRight = GetNeighborTile(x + 1, y, Vector2Int.down, chunkPosition, activeChunk);
                if ((object)belowLeft != null && (object)belowRight != null &&
                    belowLeft.Metadata.Layer == 3 && belowLeft.Metadata.Type == Tile.TileType.Coast && belowLeft.Metadata.Direction == Tile.TileDirection.South &&
                    belowRight.Metadata.Layer == 3 && belowRight.Metadata.Type == Tile.TileType.Coast && belowRight.Metadata.Direction == Tile.TileDirection.South)
                {
                    Tile farS1 = GetNeighborTile(x, y - 1, Vector2Int.down, chunkPosition, activeChunk);
                    Tile farS2 = GetNeighborTile(x + 1, y - 1, Vector2Int.down, chunkPosition, activeChunk);
                    bool opensToSand = (farS1 == null || farS1.Metadata.Layer <= 2) && (farS2 == null || farS2.Metadata.Layer <= 2);

                    if (opensToSand && y - 1 >= 0)
                    {
                        if (SetTileSafe(activeChunk, x, y - 1, 3, Tile.TileType.Corner, Tile.TileDirection.SouthEast)) fixedCount++;
                        if (SetTileSafe(activeChunk, x + 1, y - 1, 3, Tile.TileType.Corner, Tile.TileDirection.SouthWest)) fixedCount++;
                    }
                    else if (!opensToSand)
                    {
                        if (SetTileSafe(activeChunk, x, y, 3, Tile.TileType.InnerCorner, Tile.TileDirection.SouthWest)) fixedCount++;
                        if (SetTileSafe(activeChunk, x + 1, y, 3, Tile.TileType.InnerCorner, Tile.TileDirection.SouthEast)) fixedCount++;
                    }
                }
            }
        }

        // 2b. Via horizontal (y: Coast North, y + 1: Coast South) encontrando costa ortogonal
        for (int x = 0; x < _chunkSize.x; x++)
        {
            for (int y = 0; y < _chunkSize.y - 1; y++)
            {
                Tile tBottom = activeChunk.GetTileAt(x, y);
                Tile tTop = activeChunk.GetTileAt(x, y + 1);
                if ((object)tBottom == null || (object)tTop == null) continue;
                if (tBottom.Metadata.Layer != 3 || tTop.Metadata.Layer != 3) continue;
                if (tBottom.Metadata.Type != Tile.TileType.Coast || tBottom.Metadata.Direction != Tile.TileDirection.North) continue;
                if (tTop.Metadata.Type != Tile.TileType.Coast || tTop.Metadata.Direction != Tile.TileDirection.South) continue;

                // Encontro a Leste com Coast East
                Tile rightBottom = GetNeighborTile(x, y, Vector2Int.right, chunkPosition, activeChunk);
                Tile rightTop = GetNeighborTile(x, y + 1, Vector2Int.right, chunkPosition, activeChunk);
                if ((object)rightBottom != null && (object)rightTop != null &&
                    rightBottom.Metadata.Layer == 3 && rightBottom.Metadata.Type == Tile.TileType.Coast && rightBottom.Metadata.Direction == Tile.TileDirection.East &&
                    rightTop.Metadata.Layer == 3 && rightTop.Metadata.Type == Tile.TileType.Coast && rightTop.Metadata.Direction == Tile.TileDirection.East)
                {
                    Tile farE1 = GetNeighborTile(x + 1, y, Vector2Int.right, chunkPosition, activeChunk);
                    Tile farE2 = GetNeighborTile(x + 1, y + 1, Vector2Int.right, chunkPosition, activeChunk);
                    bool opensToSand = (farE1 == null || farE1.Metadata.Layer <= 2) && (farE2 == null || farE2.Metadata.Layer <= 2);

                    if (opensToSand && x + 1 < _chunkSize.x)
                    {
                        if (SetTileSafe(activeChunk, x + 1, y, 3, Tile.TileType.Corner, Tile.TileDirection.NorthEast)) fixedCount++;
                        if (SetTileSafe(activeChunk, x + 1, y + 1, 3, Tile.TileType.Corner, Tile.TileDirection.SouthEast)) fixedCount++;
                    }
                    else if (!opensToSand)
                    {
                        if (SetTileSafe(activeChunk, x, y, 3, Tile.TileType.InnerCorner, Tile.TileDirection.NorthEast)) fixedCount++;
                        if (SetTileSafe(activeChunk, x, y + 1, 3, Tile.TileType.InnerCorner, Tile.TileDirection.SouthEast)) fixedCount++;
                    }
                }

                // Encontro a Oeste com Coast West
                Tile leftBottom = GetNeighborTile(x, y, Vector2Int.left, chunkPosition, activeChunk);
                Tile leftTop = GetNeighborTile(x, y + 1, Vector2Int.left, chunkPosition, activeChunk);
                if ((object)leftBottom != null && (object)leftTop != null &&
                    leftBottom.Metadata.Layer == 3 && leftBottom.Metadata.Type == Tile.TileType.Coast && leftBottom.Metadata.Direction == Tile.TileDirection.West &&
                    leftTop.Metadata.Layer == 3 && leftTop.Metadata.Type == Tile.TileType.Coast && leftTop.Metadata.Direction == Tile.TileDirection.West)
                {
                    Tile farW1 = GetNeighborTile(x - 1, y, Vector2Int.left, chunkPosition, activeChunk);
                    Tile farW2 = GetNeighborTile(x - 1, y + 1, Vector2Int.left, chunkPosition, activeChunk);
                    bool opensToSand = (farW1 == null || farW1.Metadata.Layer <= 2) && (farW2 == null || farW2.Metadata.Layer <= 2);

                    if (opensToSand && x - 1 >= 0)
                    {
                        if (SetTileSafe(activeChunk, x - 1, y, 3, Tile.TileType.Corner, Tile.TileDirection.NorthWest)) fixedCount++;
                        if (SetTileSafe(activeChunk, x - 1, y + 1, 3, Tile.TileType.Corner, Tile.TileDirection.SouthWest)) fixedCount++;
                    }
                    else if (!opensToSand)
                    {
                        if (SetTileSafe(activeChunk, x, y, 3, Tile.TileType.InnerCorner, Tile.TileDirection.NorthWest)) fixedCount++;
                        if (SetTileSafe(activeChunk, x, y + 1, 3, Tile.TileType.InnerCorner, Tile.TileDirection.SouthWest)) fixedCount++;
                    }
                }
            }
        }

        return fixedCount;
    }

    public void ResolveTileCompatibilityInChunk(Vector2Int chunkPosition, MapGenerator activeChunk)
    {
        if ((object)_tilesetData == null || _tilesetData.TilesetList == null || _tilesetData.TilesetList.Count == 0) return;
        if ((object)activeChunk == null) return;

        EnsureCompatibilityCache();

        int morphFixed = ResolveMorphologicalCoastIncompatibilities(chunkPosition, activeChunk);

        int tileCount = _tilesetData.TilesetList.Count;
        Vector2Int[] cardinalDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        const int maxPasses = 15;
        const int maxChangesPerCell = 2;
        int[,] cellChangeCount = new int[_chunkSize.x, _chunkSize.y];
        bool anyChangedInPass;
        int passCount = 0;
        int totalTilesFixed = morphFixed;

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

            if ((object)currentTile != null && currentTile.Metadata.Layer == 3 &&
                candidate.Metadata.Layer == 2 && candidate.Metadata.Type == Tile.TileType.Block &&
                neighborList.Any(n => n.neighbor != null && n.neighbor.Metadata.Layer == 2))
            {
                score += 1200f;
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
                if (currentTile.Metadata.Layer == 3 && candidate.Metadata.Layer == 2 &&
                    neighborList.Any(n => n.neighbor != null && n.neighbor.Metadata.Layer == 2))
                {
                    layerDiff = 0;
                }
                score += (10 - layerDiff) * 20f;

                if (currentTile.Metadata.Layer == 3 && candidate.Metadata.Layer == 3)
                    score += 150f;

                if (currentTile.Metadata.Layer == 3 && candidate.Metadata.Layer == 2 && candidate.Metadata.Type == Tile.TileType.Block &&
                    neighborList.Any(n => n.neighbor != null && n.neighbor.Metadata.Layer == 2))
                {
                    score += 200f;
                }

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

    public void RegisterStructure(string name, Vector3 position, float isolationRadius, Vector2Int chunkPosition, GameObject instance)
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