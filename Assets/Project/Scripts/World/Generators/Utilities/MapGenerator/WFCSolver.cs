using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// O Controlador Principal (MonoBehaviour) do WFC. 
/// Atua como orquestrador entre a matemática do terreno (TargetLayerBuilder), 
/// a execução do algoritmo (WFCAlgorithm) e o Unity (Corrotinas e Eventos).
/// </summary>
public class WFCSolver : MonoBehaviour
{
    private const int MAX_ATTEMPT_MULTIPLIER = 10;

    [SerializeField] private int _collapsesPerFrame = 10;
    
    [Header("Beach Settings")]
    [SerializeField, Min(1)] private int _minBeachRadius = 1;
    [SerializeField, Min(1)] private int _maxBeachRadius = 3;
    [SerializeField] private float _beachNoiseScale = 0.05f;
    [SerializeField, Min(2)] private int _oceanMaskPadding = 8;

    private TargetLayerBuilder _layerBuilder;
    private WFCAlgorithm _algorithm;
    private IslandMapSampler _islandSamplerCache;
    private ChunkCellGrid _grid;
    private Vector2Int _chunkSize;
    
    private int _worldSeed;
    private int _chunkSeed;
    private Vector2Int _currentChunkCoordinate;

    public bool IsGenerating { get; private set; }
    public bool HasGenerationSucceeded { get; private set; }

    public event Action<bool> OnGenerationComplete;
    public event Action OnMapRenderRequested;

    public void Setup(ChunkCellGrid grid, Vector2Int chunkSize, CompatibilityCache compatibilityCache, TilesetData tilesetData)
    {
        if (_grid != null) return;
        
        _grid = grid;
        _chunkSize = chunkSize;

        _layerBuilder = new TargetLayerBuilder(chunkSize, _oceanMaskPadding, _minBeachRadius, _maxBeachRadius, _beachNoiseScale);
        
        if (_islandSamplerCache != null)
        {
            _layerBuilder.SetSampler(_islandSamplerCache);
        }

        _algorithm = new WFCAlgorithm(grid, chunkSize, compatibilityCache, tilesetData);
    }

    public void SetParameters(Vector2Int chunkCoordinate, float noiseScale, int worldSeed)
    {
        _currentChunkCoordinate = chunkCoordinate;
        _worldSeed = worldSeed;
        _chunkSeed = HashChunkSeed(worldSeed, chunkCoordinate.x, chunkCoordinate.y);

        // O Builder cria a Planta (Array) -> Passamos a planta para o Algoritmo
        int[,] targetLayerMap = _layerBuilder.Build(chunkCoordinate, noiseScale, worldSeed);
        _algorithm.SetState(targetLayerMap, new System.Random(_chunkSeed), chunkCoordinate);
    }

    public void SetIslandSampler(IslandMapSampler islandMapSampler)
    {
        _islandSamplerCache = islandMapSampler;
        
        // Se o builder já tiver sido criado, atualizamos a referência imediatamente
        if (_layerBuilder != null)
        {
            _layerBuilder.SetSampler(_islandSamplerCache);
        }
    }
    public void PropagateConsequences(Cell startCell) => _algorithm.PropagateConsequences(startCell);

    public bool HasCompletedCollapseSync()
    {
        if (!_algorithm.ApplyTargetLayerConstraints()) return false;

        int totalCells = _chunkSize.x * _chunkSize.y;
        int collapsedCount = 0;
        int maxAttempts = totalCells * MAX_ATTEMPT_MULTIPLIER;
        int attemptsCount = 0;

        while (collapsedCount < totalCells && attemptsCount < maxAttempts)
        {
            Cell chosenCell = _algorithm.ChooseCell();
            if (chosenCell == null) break;

            _algorithm.CollapseAndPropagate(chosenCell);

            Cell contradictionCell = _algorithm.GetContradictionCell();
            if (contradictionCell != null)
            {
                if (attemptsCount == 0) _algorithm.LogContradictionContext(contradictionCell);

                attemptsCount++;
                collapsedCount = 0;

                if (!RestartGenerationAttempt(attemptsCount)) break;
            }
            else
            {
                collapsedCount++;
            }
        }

        return !_algorithm.HasContradiction();
    }

    public void StartAsyncGeneration()
    {
        StartCoroutine(RunCollapseAsyncCoroutine());
    }

    private IEnumerator RunCollapseAsyncCoroutine()
    {
        IsGenerating = true;
        HasGenerationSucceeded = false;

        if (!_algorithm.ApplyTargetLayerConstraints())
        {
            FailGeneration();
            yield break;
        }

        int totalCells = _chunkSize.x * _chunkSize.y;
        int collapsedCount = 0;
        int maxAttempts = totalCells * MAX_ATTEMPT_MULTIPLIER;
        int attemptsCount = 0;
        int collapsesThisFrame = 0;

        while (collapsedCount < totalCells && attemptsCount < maxAttempts)
        {
            Cell chosenCell = _algorithm.ChooseCell();
            if (chosenCell == null) break;

            _algorithm.CollapseAndPropagate(chosenCell);

            Cell contradictionCell = _algorithm.GetContradictionCell();
            if (contradictionCell != null)
            {
                if (attemptsCount == 0) _algorithm.LogContradictionContext(contradictionCell);

                attemptsCount++;
                collapsedCount = 0;

                if (!RestartGenerationAttempt(attemptsCount)) break;
            }
            else
            {
                collapsedCount++;
                OnMapRenderRequested?.Invoke();
            }

            collapsesThisFrame++;
            if (collapsesThisFrame >= _collapsesPerFrame)
            {
                collapsesThisFrame = 0;
                yield return null;
            }
        }

        HasGenerationSucceeded = !_algorithm.HasContradiction();
        IsGenerating = false;
        OnGenerationComplete?.Invoke(HasGenerationSucceeded);
    }

    private bool RestartGenerationAttempt(int attemptsCount)
    {
        _grid.RestartFromHalo();
        _algorithm.SetState(_algorithm.GetTargetLayerMap(), new System.Random(_chunkSeed + attemptsCount), _currentChunkCoordinate);
        return _algorithm.ApplyTargetLayerConstraints();
    }

    private void FailGeneration()
    {
        HasGenerationSucceeded = false;
        IsGenerating = false;
        OnGenerationComplete?.Invoke(false);
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