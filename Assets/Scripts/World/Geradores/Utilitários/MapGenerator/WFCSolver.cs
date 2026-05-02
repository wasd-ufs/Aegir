using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Executa o algoritmo Wave Function Collapse (WFC) sobre uma grade de células.
/// Recebe todos os dados necessários via <see cref="Setup"/> e não depende de
/// SerializeField próprios — o cache de compatibilidade já encapsula as regras do tileset.
/// </summary>
public class WFCSolver : MonoBehaviour
{
    // =========================================================================
    // Campos Privados
    // =========================================================================

    [SerializeField] private int _collapsesPerFrame = 10;

    private ChunkCellGrid _grid;
    private CompatibilityCache _compatibilityCache;
    private TilesetData _tilesetData;

    private Vector2Int _chunkSize;
    private Vector2Int _currentChunkCoord;
    private float _currentNoiseScale;

    private readonly Vector2Int[] _directionsArray =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // =========================================================================
    // Propriedades e Eventos
    // =========================================================================

    public bool IsGenerating { get; private set; }
    public bool HasGenerationSucceeded { get; private set; }

    public System.Action<bool> OnGenerationComplete;
    public System.Action OnMapRenderRequested;

    // =========================================================================
    // Inicialização
    // =========================================================================

    /// <summary>
    /// Vincula o solver à grade, ao tamanho do chunk e ao cache de compatibilidade.
    /// O tileset é recebido separadamente para o cálculo de noise e pesos.
    /// </summary>
    public void Setup(ChunkCellGrid grid, Vector2Int chunkSize, CompatibilityCache compatibilityCache, TilesetData tilesetData)
    {
        // Guard: evita sobrescrever o estado se EnsureCache() for chamado mais de uma vez
        if (_grid != null) return;

        _grid               = grid;
        _chunkSize          = chunkSize;
        _compatibilityCache = compatibilityCache;
        _tilesetData        = tilesetData;
    }

    // =========================================================================
    // Configuração de Parâmetros
    // =========================================================================

    /// <summary>
    /// Define a posição do chunk e a escala de ruído antes de qualquer execução
    /// do algoritmo — síncrona ou assíncrona. Deve ser chamado antes de
    /// <see cref="RunCollapseSync"/> ou <see cref="StartAsyncGeneration"/>.
    /// </summary>
    public void SetParameters(Vector2Int chunkCoord, float noiseScale)
    {
        _currentChunkCoord = chunkCoord;
        _currentNoiseScale = noiseScale;
    }

    // =========================================================================
    // Execução Síncrona
    // =========================================================================

    /// <summary>
    /// Executa o loop WFC de forma síncrona.
    /// Chame <see cref="SetParameters"/> antes deste método.
    /// </summary>
    /// <returns><c>true</c> se o chunk foi colapsado sem contradição.</returns>
    public bool RunCollapseSync()
    {
        int totalCells    = _chunkSize.x * _chunkSize.y;
        int collapsedCount = 0;
        int maxAttempts   = totalCells * 3;
        int attemptsCount = 0;

        while (collapsedCount < totalCells && attemptsCount < maxAttempts)
        {
            Cell chosenCell = ChooseCell();
            if (chosenCell == null) break;

            float cellNoise = CalculateCellNoise(chosenCell.Coordinates);
            CollapseAndPropagate(chosenCell, cellNoise);

            if (HasContradiction())
            {
                _grid.RestartFromHalo();
                collapsedCount = 0;
                attemptsCount++;
                continue;
            }

            collapsedCount++;
        }

        return !HasContradiction();
    }

    // =========================================================================
    // Execução Assíncrona
    // =========================================================================

    /// <summary>
    /// Inicia a geração assíncrona via coroutine.
    /// Chame <see cref="SetParameters"/> antes deste método.
    /// Ao concluir, dispara <see cref="OnGenerationComplete"/> com o resultado.
    /// </summary>
    public void StartAsyncGeneration()
    {
        StartCoroutine(RunCollapseAsyncCoroutine());
    }

    private IEnumerator RunCollapseAsyncCoroutine()
    {
        IsGenerating           = true;
        HasGenerationSucceeded = false;

        int totalCells         = _chunkSize.x * _chunkSize.y;
        int collapsedCount     = 0;
        int maxAttempts        = totalCells * 3;
        int attemptsCount      = 0;
        int collapsesThisFrame = 0;

        while (collapsedCount < totalCells && attemptsCount < maxAttempts)
        {
            Cell chosenCell = ChooseCell();
            if (chosenCell == null) break;

            float cellNoise = CalculateCellNoise(chosenCell.Coordinates);
            CollapseAndPropagate(chosenCell, cellNoise);

            if (HasContradiction())
            {
                _grid.RestartFromHalo();
                collapsedCount = 0;
                attemptsCount++;
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

        HasGenerationSucceeded = !HasContradiction();
        IsGenerating           = false;
        OnGenerationComplete?.Invoke(HasGenerationSucceeded);
    }

    // =========================================================================
    // Propagação (Pública — usada pelo ChunkCellGrid na init do halo)
    // =========================================================================

    /// <summary>
    /// Propaga as restrições a partir de uma célula modificada usando BFS.
    /// Exposto publicamente para ser chamado pelo <see cref="ChunkCellGrid"/>
    /// durante a inicialização das bordas do halo.
    /// </summary>
    public void PropagateConsequences(Cell startCell)
    {
        var cellQueue = new Queue<Cell>();
        cellQueue.Enqueue(startCell);

        while (cellQueue.Count > 0)
        {
            Cell currentCell = cellQueue.Dequeue();
            ProcessNeighbors(currentCell, cellQueue);
        }
    }

    // =========================================================================
    // Helpers Privados — Propagação
    // =========================================================================

    private void ProcessNeighbors(Cell currentCell, Queue<Cell> cellQueue)
    {
        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            Vector2Int neighborPosition = currentCell.Coordinates + _directionsArray[directionIndex];
            if (!_grid.IsInsideBounds(neighborPosition)) continue;

            Cell neighborCell = _grid.CellsArray[neighborPosition.x, neighborPosition.y];
            if (neighborCell.IsCollapsed()) continue;

            if (RemoveUnsupportedTiles(currentCell, neighborCell, directionIndex))
                cellQueue.Enqueue(neighborCell);
        }
    }

    private bool RemoveUnsupportedTiles(Cell currentCell, Cell neighborCell, int directionIndex)
    {
        bool hasChanged = false;
        int  tileCount  = _tilesetData.TilesetList.Count;

        for (int neighborTileIndex = 0; neighborTileIndex < tileCount; neighborTileIndex++)
        {
            if (!neighborCell.PossibleBitsArray[neighborTileIndex]) continue;

            if (!HasSupport(currentCell, neighborTileIndex, directionIndex))
            {
                neighborCell.PossibleBitsArray[neighborTileIndex] = false;
                hasChanged = true;
            }
        }

        return hasChanged;
    }

    private bool HasSupport(Cell currentCell, int neighborTileIndex, int directionIndex)
    {
        int tileCount = _tilesetData.TilesetList.Count;

        for (int currentTileIndex = 0; currentTileIndex < tileCount; currentTileIndex++)
        {
            if (!currentCell.PossibleBitsArray[currentTileIndex]) continue;
            if (_compatibilityCache.IsCompatible(currentTileIndex, neighborTileIndex, directionIndex))
                return true;
        }

        return false;
    }

    // =========================================================================
    // Helpers Privados — WFC
    // =========================================================================

    /// <summary>
    /// Seleciona a célula com menor entropia (MRE) ainda não colapsada,
    /// com desempate aleatório entre candidatas de igual entropia.
    /// </summary>
    private Cell ChooseCell()
    {
        int minimumPossibilities = int.MaxValue;
        var candidateCellsList   = new List<Cell>();

        for (int x = 1; x <= _chunkSize.x; x++)
        {
            for (int y = 1; y <= _chunkSize.y; y++)
            {
                Cell cell = _grid.CellsArray[x, y];
                if (cell.IsCollapsed()) continue;

                int possibilitiesCount = cell.CountPossible();
                if (possibilitiesCount == 0) continue;

                if (possibilitiesCount < minimumPossibilities)
                {
                    minimumPossibilities = possibilitiesCount;
                    candidateCellsList.Clear();
                    candidateCellsList.Add(cell);
                }
                else if (possibilitiesCount == minimumPossibilities)
                {
                    candidateCellsList.Add(cell);
                }
            }
        }

        return candidateCellsList.Count > 0
            ? candidateCellsList[Random.Range(0, candidateCellsList.Count)]
            : null;
    }

    /// <summary>
    /// Colapsa a célula em um tile escolhido por rolagem de peso ponderada pelo noise,
    /// depois propaga as restrições para os vizinhos.
    /// </summary>
    private void CollapseAndPropagate(Cell cell, float noiseValue)
    {
        int   tileCount   = _tilesetData.TilesetList.Count;
        float totalWeight = 0;

        for (int i = 0; i < tileCount; i++)
        {
            if (!cell.PossibleBitsArray[i]) continue;
            Tile tile = _tilesetData.TilesetList[i];
            totalWeight += IsLandTile(tile) ? tile.Weight * (noiseValue * 10) : tile.Weight;
        }

        float randomRoll  = Random.Range(0, totalWeight);
        int   chosenIndex = -1;

        for (int i = 0; i < tileCount; i++)
        {
            if (!cell.PossibleBitsArray[i]) continue;
            Tile tile = _tilesetData.TilesetList[i];
            randomRoll -= IsLandTile(tile) ? tile.Weight * (noiseValue * 10) : tile.Weight;
            if (randomRoll <= 0) { chosenIndex = i; break; }
        }

        // Fallback: garante que algum tile seja sempre escolhido
        if (chosenIndex < 0)
            for (int i = tileCount - 1; i >= 0; i--)
                if (cell.PossibleBitsArray[i]) { chosenIndex = i; break; }

        cell.CollapseCell(chosenIndex);
        PropagateConsequences(cell);
    }

    /// <summary>
    /// Retorna <c>true</c> se alguma célula interna estiver sem tiles possíveis.
    /// Privado — o resultado é exposto apenas via <see cref="HasGenerationSucceeded"/>.
    /// </summary>
    private bool HasContradiction()
    {
        for (int x = 1; x <= _chunkSize.x; x++)
            for (int y = 1; y <= _chunkSize.y; y++)
                if (_grid.CellsArray[x, y].IsEmpty()) return true;
        return false;
    }

    // =========================================================================
    // Helpers Privados — Noise e Tile
    // =========================================================================

    /// <summary>
    /// Terra = camada par e diferente de 0 (ex.: 2, 4...).
    /// Tiles de terra recebem peso amplificado pelo noise para favorecer ilhas.
    /// </summary>
    private bool IsLandTile(Tile tile)
        => tile.Metadata.Layer % 2 == 0 && tile.Metadata.Layer != 0;

    /// <summary>
    /// Calcula Perlin Noise contínuo entre chunks para a célula informada.
    /// </summary>
    private float CalculateCellNoise(Vector2Int localCoordinates)
    {
        int localX = localCoordinates.x - 1;
        int localY = localCoordinates.y - 1;

        float globalX = (_currentChunkCoord.x * _chunkSize.x) + localX;
        float globalY = (_currentChunkCoord.y * _chunkSize.y) + localY;

        return Mathf.PerlinNoise(globalX * _currentNoiseScale + 100.5f, globalY * _currentNoiseScale + 100.5f);
    }
}