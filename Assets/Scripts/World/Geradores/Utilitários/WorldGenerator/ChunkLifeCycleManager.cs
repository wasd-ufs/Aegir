using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerencia o ciclo de vida individual de cada chunk: instanciar, carregar, salvar e destruir.
/// <para>
/// Não decide quais chunks devem existir — essa responsabilidade pertence ao
/// <see cref="ChunkVisibilityTracker"/>. Este manager apenas executa as ações
/// solicitadas pelos outros sistemas.
/// </para>
/// </summary>
public class ChunkLifecycleManager : MonoBehaviour
{
    // =========================================================================
    // Campos Serializados
    // =========================================================================

    [SerializeField] private GameObject _chunkPrefab;
    [SerializeField] private Transform _chunksContainer;
    [SerializeField] private float _noiseScale = 0.05f;

    // =========================================================================
    // Estado Interno
    // =========================================================================

    private Dictionary<Vector2Int, MapGenerator> _activeChunksDictionary  = new Dictionary<Vector2Int, MapGenerator>();
    private Dictionary<Vector2Int, MapGenerator> _pendingChunksDictionary = new Dictionary<Vector2Int, MapGenerator>();
    private HashSet<Vector2Int> _failedChunksSet                          = new HashSet<Vector2Int>();
    private List<Vector2Int> _chunksWaitingForDecorationList              = new List<Vector2Int>();

    // =========================================================================
    // Dependências
    // =========================================================================

    private ChunkPersistence _persistence;
    private HaloBuilder _haloBuilder;
    private ChunkNeighborNotifier _neighborNotifier;
    private WorldGenerator _worldGenerator;
    private Transform _playerTransform;
    public Transform ActivePlayer => _playerTransform;

    // =========================================================================
    // Inicialização
    // =========================================================================

    public void Setup(
        ChunkPersistence persistence,
        HaloBuilder haloBuilder,
        ChunkNeighborNotifier neighborNotifier,
        WorldGenerator worldGenerator,
        Transform initialPlayerTransform)
    {
        _persistence      = persistence;
        _haloBuilder      = haloBuilder;
        _neighborNotifier = neighborNotifier;
        _worldGenerator   = worldGenerator;
        _playerTransform  = initialPlayerTransform;
    }

    // =========================================================================
    // Consulta de Chunks
    // =========================================================================

    /// <summary>Retorna o chunk ativo na posição informada, ou <c>null</c>.</summary>
    public MapGenerator GetActiveChunk(Vector2Int position)
    {
        _activeChunksDictionary.TryGetValue(position, out var chunk);
        return chunk;
    }

    /// <summary>Retorna o chunk ativo ou pendente na posição informada, ou <c>null</c>.</summary>
    public MapGenerator GetActiveOrPendingChunk(Vector2Int position)
    {
        if (_activeChunksDictionary.TryGetValue(position, out var chunk)) return chunk;
        if (_pendingChunksDictionary.TryGetValue(position, out chunk)) return chunk;
        return null;
    }

    public List<Vector2Int> GetChunksWaitingForDecoration() => _chunksWaitingForDecorationList;

    public void RemoveChunkWaitingForDecoration(Vector2Int position)
        => _chunksWaitingForDecorationList.Remove(position);

    // =========================================================================
    // Atualização do Jogador
    // =========================================================================

    /// <summary>
    /// Atualiza o transform do jogador em todos os chunks ativos.
    /// Chamado após transição barco ↔ capitão.
    /// </summary>
    public void SetPlayerTransform(Transform newPlayerTransform)
    {
        _playerTransform = newPlayerTransform;
        foreach (var chunk in _activeChunksDictionary.Values)
            chunk.Setup(_playerTransform.gameObject, _worldGenerator);
    }

    // =========================================================================
    // Criação e Carregamento
    // =========================================================================

    /// <summary>
    /// Instancia e gera (ou carrega do disco) um chunk de forma síncrona.
    /// </summary>
    public void CreateOrLoadChunkSync(Vector2Int position, Vector3 worldPosition)
    {
        MapGenerator mapGenerator = InstantiateChunk(position, worldPosition);

        byte[] savedDataArray = _persistence.LoadChunkFromDisk(position);
        if (savedDataArray != null && !_failedChunksSet.Contains(position))
        {
            mapGenerator.LoadFromData(savedDataArray);
            mapGenerator.SpawnEntities();
        }
        else
        {
            var haloDictionary = _haloBuilder.BuildHalo(position);

            if (position == Vector2Int.zero)
            {
                mapGenerator.ForceWaterChunk(haloDictionary);
            }
            else if (mapGenerator.GenerateChunkSync(haloDictionary, position, _noiseScale))
            {
                _failedChunksSet.Remove(position);
                _chunksWaitingForDecorationList.Add(position);
            }
            else
            {
                Debug.LogWarning($"[Lifecycle] Contradiction at {position} (sync). Marked for retry.");
                _failedChunksSet.Add(position);
            }
        }

        _neighborNotifier.NotifyNeighbors(position, mapGenerator);
    }

    /// <summary>
    /// Instancia e gera (ou carrega do disco) um chunk de forma assíncrona.
    /// Se o chunk já estava pendente (saiu do view distance durante a geração),
    /// reutiliza o objeto existente em vez de criar um novo.
    /// </summary>
    public void CreateOrLoadChunkAsync(Vector2Int position, Vector3 worldPosition, ChunkGenerationQueue queue)
    {
        // Chunk voltou ao campo de visão enquanto ainda estava pendente
        if (_pendingChunksDictionary.TryGetValue(position, out MapGenerator pendingGenerator))
        {
            _pendingChunksDictionary.Remove(position);
            _activeChunksDictionary.Add(position, pendingGenerator);
            pendingGenerator.Renderer.SetTilemapEnabled(true);

            if (!pendingGenerator.IsGenerating) queue.CurrentlyGenerating = null;
            return;
        }

        MapGenerator mapGenerator = InstantiateChunk(position, worldPosition);

        byte[] savedDataArray = _persistence.LoadChunkFromDisk(position);
        if (savedDataArray != null && !_failedChunksSet.Contains(position))
        {
            mapGenerator.LoadFromData(savedDataArray);
            mapGenerator.SpawnEntities();
            _neighborNotifier.NotifyNeighbors(position, mapGenerator);
            queue.CurrentlyGenerating = null;
        }
        else
        {
            var haloDictionary = _haloBuilder.BuildHalo(position);

            mapGenerator.OnGenerationComplete = (completedGenerator, isSuccess) =>
            {
                if (isSuccess)
                {
                    _failedChunksSet.Remove(position);
                    _neighborNotifier.NotifyNeighbors(position, completedGenerator);

                    if (_activeChunksDictionary.ContainsKey(position))
                        completedGenerator.SpawnEntities();

                    _chunksWaitingForDecorationList.Add(position);
                }
                else
                {
                    Debug.LogWarning($"[Lifecycle] Contradiction at {position} (async). Marked for retry.");
                    _failedChunksSet.Add(position);
                }

                if (_pendingChunksDictionary.ContainsKey(position))
                {
                    SaveAndDestroy(position, completedGenerator);
                    _pendingChunksDictionary.Remove(position);
                }

                if (queue.CurrentlyGenerating == position) queue.CurrentlyGenerating = null;
            };

            mapGenerator.GenerateChunkAsync(haloDictionary, position, _noiseScale);
        }
    }

    // =========================================================================
    // Remoção (Chamada pelo ChunkVisibilityTracker)
    // =========================================================================

    /// <summary>
    /// Remove os chunks que não estão mais no conjunto visível.
    /// Chunks ainda em geração vão para <c>pendingChunks</c>; os demais são salvos e destruídos.
    /// </summary>
    public void RemoveChunksOutOfRange(HashSet<Vector2Int> visibleCoordinatesSet, ChunkGenerationQueue queue)
    {
        var chunksToRemoveList = new List<Vector2Int>();

        foreach (var coordinate in _activeChunksDictionary.Keys)
            if (!visibleCoordinatesSet.Contains(coordinate))
                chunksToRemoveList.Add(coordinate);

        foreach (var coordinate in chunksToRemoveList)
        {
            MapGenerator generator = _activeChunksDictionary[coordinate];
            _activeChunksDictionary.Remove(coordinate);

            if (generator.IsGenerating)
            {
                // Mantém vivo mas oculto até concluir a geração
                _pendingChunksDictionary.Add(coordinate, generator);
                generator.Renderer.SetTilemapEnabled(false);
            }
            else
            {
                SaveAndDestroy(coordinate, generator);
            }
        }
    }

    // =========================================================================
    // Helpers Privados
    // =========================================================================

    /// <summary>
    /// Instancia o prefab do chunk, configura e registra no dicionário ativo.
    /// </summary>
    private MapGenerator InstantiateChunk(Vector2Int position, Vector3 worldPosition)
    {
        GameObject chunkObject   = Instantiate(_chunkPrefab, worldPosition, Quaternion.identity, _chunksContainer);
        MapGenerator mapGenerator = chunkObject.GetComponent<MapGenerator>();
        mapGenerator.Setup(_playerTransform.gameObject, _worldGenerator);
        _activeChunksDictionary.Add(position, mapGenerator);
        return mapGenerator;
    }

    private void SaveAndDestroy(Vector2Int position, MapGenerator mapGenerator)
    {
        if (!_failedChunksSet.Contains(position))
        {
            byte[] chunkDataArray = mapGenerator.GetChunkData();
            _persistence.SaveChunkToDisk(position, chunkDataArray);
        }

        Destroy(mapGenerator.gameObject);
    }
}