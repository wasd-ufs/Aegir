using System.Collections.Generic;
using UnityEngine;

public class ChunkLifecycleManager : MonoBehaviour
{
    [SerializeField] private GameObject _chunkPrefab;
    [SerializeField] private Transform _chunksContainer;
    [SerializeField] private float _noiseScale = 0.05f;

    private Dictionary<Vector2Int, MapGenerator> _activeChunksDictionary = new Dictionary<Vector2Int, MapGenerator>();
    private Dictionary<Vector2Int, MapGenerator> _pendingChunksDictionary = new Dictionary<Vector2Int, MapGenerator>();
    private HashSet<Vector2Int> _failedChunksSet = new HashSet<Vector2Int>();
    private List<Vector2Int> _chunksWaitingForDecorationList = new List<Vector2Int>();

    private ChunkPersistence _persistence;
    private HaloBuilder _haloBuilder;
    private ChunkNeighborNotifier _neighborNotifier;
    private WorldGenerator _worldGenerator;
    private Transform _playerTransform;

    public void Setup(ChunkPersistence persistence, HaloBuilder haloBuilder, ChunkNeighborNotifier neighborNotifier, WorldGenerator worldGenerator, Transform initialPlayerTransform)
    {
        _persistence = persistence;
        _haloBuilder = haloBuilder;
        _neighborNotifier = neighborNotifier;
        _worldGenerator = worldGenerator;
        _playerTransform = initialPlayerTransform;
    }

    public MapGenerator GetActiveChunk(Vector2Int position)
    {
        _activeChunksDictionary.TryGetValue(position, out var chunk);
        return chunk;
    }

    public MapGenerator GetActiveOrPendingChunk(Vector2Int position)
    {
        if (_activeChunksDictionary.TryGetValue(position, out var chunk)) return chunk;
        if (_pendingChunksDictionary.TryGetValue(position, out chunk)) return chunk;
        return null;
    }

    public List<Vector2Int> GetChunksWaitingForDecoration() => _chunksWaitingForDecorationList;
    public void RemoveChunkWaitingForDecoration(Vector2Int position) => _chunksWaitingForDecorationList.Remove(position);

    public void SetPlayerTransform(Transform newPlayerTransform)
    {
        _playerTransform = newPlayerTransform;
        foreach (var chunk in _activeChunksDictionary.Values)
        {
            chunk.Setup(_playerTransform.gameObject, _worldGenerator);
        }
    }

    public void CreateOrLoadChunkSync(Vector2Int position, Vector3 worldPosition)
    {
        GameObject chunkObject = Instantiate(_chunkPrefab, worldPosition, Quaternion.identity, _chunksContainer);
        MapGenerator mapGenerator = chunkObject.GetComponent<MapGenerator>();
        mapGenerator.Setup(_playerTransform.gameObject, _worldGenerator);
        _activeChunksDictionary.Add(position, mapGenerator);

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

    public void CreateOrLoadChunkAsync(Vector2Int position, Vector3 worldPosition, ChunkGenerationQueue queue)
    {
        if (_pendingChunksDictionary.TryGetValue(position, out MapGenerator pendingGenerator))
        {
            _pendingChunksDictionary.Remove(position);
            _activeChunksDictionary.Add(position, pendingGenerator);
            pendingGenerator.Renderer.SetTilemapEnabled(true);

            if (!pendingGenerator.IsGenerating) queue.CurrentlyGenerating = null;
            return;
        }

        GameObject chunkObject = Instantiate(_chunkPrefab, worldPosition, Quaternion.identity, _chunksContainer);
        MapGenerator mapGenerator = chunkObject.GetComponent<MapGenerator>();
        mapGenerator.Setup(_playerTransform.gameObject, _worldGenerator);
        _activeChunksDictionary.Add(position, mapGenerator);

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
                    {
                        completedGenerator.SpawnEntities();
                    }
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

    public void UpdateVisibleChunks(Vector2Int centerPosition, int viewDistance, ChunkGenerationQueue queue)
    {
        HashSet<Vector2Int> visibleCoordinatesSet = new HashSet<Vector2Int>();

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int chunkPosition = new Vector2Int(centerPosition.x + x, centerPosition.y + y);
                visibleCoordinatesSet.Add(chunkPosition);

                bool isAlreadyActive = _activeChunksDictionary.ContainsKey(chunkPosition);
                bool isAlreadyPending = _pendingChunksDictionary.ContainsKey(chunkPosition);
                bool isAlreadyInQueue = queue.Contains(chunkPosition);
                bool isCurrentlyBeingGenerated = queue.CurrentlyGenerating == chunkPosition;

                if (!isAlreadyActive && !isAlreadyPending && !isAlreadyInQueue && !isCurrentlyBeingGenerated)
                {
                    queue.EnqueueChunk(chunkPosition, centerPosition);
                }
            }
        }

        List<Vector2Int> chunksToRemoveList = new List<Vector2Int>();
        foreach (var coordinate in _activeChunksDictionary.Keys)
        {
            if (!visibleCoordinatesSet.Contains(coordinate)) chunksToRemoveList.Add(coordinate);
        }

        foreach (var coordinate in chunksToRemoveList)
        {
            MapGenerator generator = _activeChunksDictionary[coordinate];
            _activeChunksDictionary.Remove(coordinate);

            if (generator.IsGenerating)
            {
                _pendingChunksDictionary.Add(coordinate, generator);
                generator.Renderer.SetTilemapEnabled(false);
            }
            else
            {
                SaveAndDestroy(coordinate, generator);
            }
        }

        queue.RemoveAll(position => !visibleCoordinatesSet.Contains(position));
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