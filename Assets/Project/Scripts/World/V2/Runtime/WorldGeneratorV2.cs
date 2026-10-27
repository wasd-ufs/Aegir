using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Aegir.World.V2.Pipeline;
using Aegir.World.V2.Settlements;

namespace Aegir.World.V2.Runtime
{
    /// <summary>
    /// Orquestrador de geração de mundo procedural V2.
    /// Projetado do zero com arquitetura limpa, alta performance, conexões estáveis,
    /// pooling de GameObjects e integração pré-WFC de estruturas.
    /// </summary>
    public class WorldGeneratorV2 : MonoBehaviour, IWorldGenerator
    {
        [Header("Configurações do Mundo")]
        [SerializeField] private GameObject _chunkPrefab;
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private int _viewDistance = 2;
        [SerializeField] private TilesetData _tilesetData;
        [SerializeField] private int _worldSeed = 0;
        [SerializeField, Tooltip("Se verdadeiro, gera uma semente aleatória diferente a cada vez que o jogo inicia.")]
        private bool _randomizeSeedOnStart = false;
        [SerializeField] private Vector2Int _chunkSize = new Vector2Int(16, 16);

        [Header("Estruturas e Assentamentos")]
        [SerializeField] private List<StructureData> _structuresList = new List<StructureData>();
        [SerializeField] private Transform _structuresContainer;
        [SerializeField, Range(0f, 1f), Tooltip("Probabilidade determinística de uma ilha conter vila/estradas.")]
        private float _settlementChance = 0.5f;
        [SerializeField, Min(1), Tooltip("Tamanho mínimo em chunks para uma ilha poder ser habitada.")]
        private int _minIslandChunks = 2;

        [Header("Containers")]
        [SerializeField] private Transform _chunksContainer;
        [SerializeField] private Transform _creaturesContainer;
        [SerializeField, Tooltip("Limite máximo de criaturas e NPCs instanciados por chunk.")]
        private int _maxCreaturesPerChunk = 10;

        public Transform PlayerTransform => _playerTransform;
        public Transform CreaturesContainer => _creaturesContainer;
        public float CellSize => _cellSize;
        public int ViewDistance => _viewDistance;
        public Vector2Int ChunkSize => _chunkSize;
        public int WorldSeed => _worldSeed;

        private ChunkPipelineV2 _pipeline;
        private RuleManager _ruleManager;
        private float _cellSize = 1f;

        private readonly Dictionary<Vector2Int, ChunkViewV2> _activeChunks = new Dictionary<Vector2Int, ChunkViewV2>();
        private readonly Dictionary<Vector2Int, ChunkResult> _generatedCache = new Dictionary<Vector2Int, ChunkResult>();
        private readonly Stack<ChunkViewV2> _chunkPool = new Stack<ChunkViewV2>();
        private readonly Dictionary<Vector2Int, List<GameObject>> _spawnedCreatures = new Dictionary<Vector2Int, List<GameObject>>();

        private Vector2Int _lastPlayerChunkPosition;

        private void Awake()
        {
            if (_randomizeSeedOnStart)
            {
                _worldSeed = Random.Range(1, 10000000);
            }

            if (_chunksContainer == null)
            {
                var container = new GameObject("ChunksContainer_V2");
                container.transform.SetParent(transform);
                _chunksContainer = container.transform;
            }

            if (_structuresContainer == null)
            {
                var container = new GameObject("StructuresContainer_V2");
                container.transform.SetParent(transform);
                _structuresContainer = container.transform;
            }

            if (_creaturesContainer == null)
            {
                var container = new GameObject("CreaturesContainer_V2");
                container.transform.SetParent(transform);
                _creaturesContainer = container.transform;
            }

            if (_chunkPrefab != null)
            {
                var mapGen = _chunkPrefab.GetComponent<MapGenerator>();
                if (mapGen != null && mapGen.ChunkSize.x > 0 && mapGen.ChunkSize.y > 0)
                {
                    _chunkSize = mapGen.ChunkSize;
                }

                var grid = _chunkPrefab.GetComponent<Grid>();
                if (grid != null)
                {
                    _cellSize = grid.cellSize.x;
                }
            }

            if (_structuresList == null || _structuresList.Count == 0)
            {
                var legacyStructGen = FindFirstObjectByType<StructureGenerator>();
                if (legacyStructGen != null)
                {
                    var field = typeof(StructureGenerator).GetField("_structuresList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field?.GetValue(legacyStructGen) is List<StructureData> legacyList && legacyList.Count > 0)
                    {
                        _structuresList = new List<StructureData>(legacyList);
                    }
                }
            }

            _ruleManager = GetComponent<RuleManager>() ?? FindFirstObjectByType<RuleManager>();

            // Inicializa a pipeline V2
            _pipeline = new ChunkPipelineV2(
                _chunkSize,
                _tilesetData,
                _ruleManager,
                _structuresList,
                _worldSeed,
                _cellSize,
                _settlementChance,
                _minIslandChunks
            );
        }

        private void Start()
        {
            if (_playerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) _playerTransform = playerObj.transform;
            }

            _lastPlayerChunkPosition = GetChunkPosFromWorld(_playerTransform != null ? _playerTransform.position : Vector3.zero);
            UpdateVisibleChunks(_lastPlayerChunkPosition);
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            Vector2Int currentChunk = GetChunkPosFromWorld(_playerTransform.position);
            if (currentChunk != _lastPlayerChunkPosition)
            {
                _lastPlayerChunkPosition = currentChunk;
                UpdateVisibleChunks(currentChunk);
            }
        }

        public void UpdateVisibleChunks(Vector2Int centerChunk)
        {
            var visibleSet = new HashSet<Vector2Int>();

            for (int dx = -_viewDistance; dx <= _viewDistance; dx++)
            {
                for (int dy = -_viewDistance; dy <= _viewDistance; dy++)
                {
                    visibleSet.Add(new Vector2Int(centerChunk.x + dx, centerChunk.y + dy));
                }
            }

            // Desativa chunks fora do campo de visão (retorna para o Pool)
            var toRemove = new List<Vector2Int>();
            foreach (var kvp in _activeChunks)
            {
                if (!visibleSet.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var coord in toRemove)
            {
                ChunkViewV2 view = _activeChunks[coord];
                _activeChunks.Remove(coord);
                view.ResetForPool();
                _chunkPool.Push(view);

                // Despawn de criaturas associadas ao chunk reciclado
                if (_spawnedCreatures.TryGetValue(coord, out var creatures))
                {
                    for (int i = 0; i < creatures.Count; i++)
                    {
                        if (creatures[i] != null)
                        {
                            Destroy(creatures[i]);
                        }
                    }
                    _spawnedCreatures.Remove(coord);
                }
            }

            // Ativa ou gera os chunks dentro do campo de visão
            foreach (var coord in visibleSet)
            {
                if (!_activeChunks.ContainsKey(coord))
                {
                    LoadOrCreateChunk(coord);
                }
            }
        }

        private void LoadOrCreateChunk(Vector2Int chunkCoordinate)
        {
            if (!_generatedCache.TryGetValue(chunkCoordinate, out var result))
            {
                result = _pipeline.GenerateChunk(chunkCoordinate, _worldSeed);
                _generatedCache[chunkCoordinate] = result;
            }

            ChunkViewV2 view = GetOrCreateChunkView();
            Vector3 worldPos = GetChunkWorldPosition(chunkCoordinate);
            view.transform.position = worldPos;
            view.gameObject.name = $"ChunkV2_{chunkCoordinate.x}_{chunkCoordinate.y}";
            view.gameObject.SetActive(true);

            view.ApplyChunkResult(result, _tilesetData, _structuresContainer);
            _activeChunks[chunkCoordinate] = view;

            SpawnCreaturesForChunk(chunkCoordinate, result);
        }

        private ChunkViewV2 GetOrCreateChunkView()
        {
            if (_chunkPool.Count > 0)
            {
                return _chunkPool.Pop();
            }

            GameObject chunkObj;
            if (_chunkPrefab != null)
            {
                chunkObj = Instantiate(_chunkPrefab, _chunksContainer);
                // Remove scripts legados do prefab para isolamento
                var legacyMapGen = chunkObj.GetComponent<MapGenerator>();
                if (legacyMapGen != null) Destroy(legacyMapGen);
                var legacySolver = chunkObj.GetComponent<WFCSolver>();
                if (legacySolver != null) Destroy(legacySolver);
            }
            else
            {
                chunkObj = new GameObject("ChunkV2_Dynamic", typeof(Grid));
                chunkObj.transform.SetParent(_chunksContainer);
                var grid = chunkObj.GetComponent<Grid>();
                grid.cellSize = new Vector3(_cellSize, _cellSize, 0);

                var tilemapObj = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                tilemapObj.transform.SetParent(chunkObj.transform);
            }

            var chunkView = chunkObj.GetComponent<ChunkViewV2>();
            if (chunkView == null)
            {
                chunkView = chunkObj.AddComponent<ChunkViewV2>();
                var tilemap = chunkObj.GetComponentInChildren<Tilemap>();
                chunkView.Initialize(tilemap, _tilesetData);
            }

            return chunkView;
        }

        public Vector2Int GetChunkPosFromWorld(Vector3 worldPosition)
        {
            float chunkWorldW = _chunkSize.x * _cellSize;
            float chunkWorldH = _chunkSize.y * _cellSize;

            int cx = Mathf.FloorToInt(worldPosition.x / chunkWorldW);
            int cy = Mathf.FloorToInt(worldPosition.y / chunkWorldH);
            return new Vector2Int(cx, cy);
        }

        public Vector3 GetChunkWorldPosition(Vector2Int chunkCoordinate)
        {
            return new Vector3(
                chunkCoordinate.x * _chunkSize.x * _cellSize,
                chunkCoordinate.y * _chunkSize.y * _cellSize,
                0f
            );
        }

        public Tile GetTileAtWorldPosition(Vector3 worldPosition)
        {
            Vector2Int chunkCoord = GetChunkPosFromWorld(worldPosition);
            if (!_generatedCache.TryGetValue(chunkCoord, out var result))
            {
                if (_pipeline != null)
                {
                    result = _pipeline.GenerateChunk(chunkCoord, _worldSeed);
                    _generatedCache[chunkCoord] = result;
                }
                else
                {
                    return null;
                }
            }

            Vector3 chunkWorldOrigin = GetChunkWorldPosition(chunkCoord);
            int localX = Mathf.FloorToInt((worldPosition.x - chunkWorldOrigin.x) / _cellSize);
            int localY = Mathf.FloorToInt((worldPosition.y - chunkWorldOrigin.y) / _cellSize);

            if (localX < 0 || localX >= _chunkSize.x || localY < 0 || localY >= _chunkSize.y) return null;

            int tileIndex = result.TileIndices[localX, localY];
            if (_tilesetData != null && tileIndex >= 0 && tileIndex < _tilesetData.TilesetList.Count)
            {
                return _tilesetData.TilesetList[tileIndex];
            }

            return null;
        }

        public bool IsChunkActive(Vector2Int chunkPosition)
        {
            return _activeChunks.ContainsKey(chunkPosition);
        }

        /// <summary>
        /// Instancia dinamicamente a fauna e NPCs configurados nos tiles do chunk gerado.
        /// </summary>
        private void SpawnCreaturesForChunk(Vector2Int chunkCoordinate, ChunkResult result)
        {
            if (_tilesetData == null || _tilesetData.TilesetList == null) return;
            if (_spawnedCreatures.ContainsKey(chunkCoordinate)) return;

            var spawnedList = new List<GameObject>();
            int totalSpawned = 0;
            Vector3 chunkOrigin = GetChunkWorldPosition(chunkCoordinate);
            GameObject playerObj = _playerTransform != null ? _playerTransform.gameObject : null;

            int chunkSeed = _worldSeed ^ (chunkCoordinate.x * 73856093) ^ (chunkCoordinate.y * 19349663);
            var rng = new System.Random(chunkSeed);

            for (int x = 0; x < _chunkSize.x; x++)
            {
                for (int y = 0; y < _chunkSize.y; y++)
                {
                    if (totalSpawned >= _maxCreaturesPerChunk) break;

                    int tileIndex = result.TileIndices[x, y];
                    if (tileIndex < 0 || tileIndex >= _tilesetData.TilesetList.Count) continue;

                    Tile tile = _tilesetData.TilesetList[tileIndex];
                    if (tile == null || tile.SpawnableCreaturesList == null || tile.SpawnableCreaturesList.Count == 0) continue;

                    Vector3 basePosition = chunkOrigin + new Vector3(
                        (x + 0.5f) * _cellSize,
                        (y + 0.5f) * _cellSize,
                        0f
                    );

                    foreach (var spawnable in tile.SpawnableCreaturesList)
                    {
                        if (spawnable.CreaturePrefab == null) continue;
                        if (rng.NextDouble() > spawnable.SpawnChance) continue;

                        for (int q = 0; q < spawnable.Quantity; q++)
                        {
                            if (totalSpawned >= _maxCreaturesPerChunk) break;

                            float offsetX = (float)(rng.NextDouble() * 0.4 - 0.2) * _cellSize;
                            float offsetY = (float)(rng.NextDouble() * 0.4 - 0.2) * _cellSize;
                            Vector3 finalPos = basePosition + new Vector3(offsetX, offsetY, 0f);

                            GameObject creatureInstance = Instantiate(
                                spawnable.CreaturePrefab,
                                finalPos,
                                Quaternion.identity,
                                _creaturesContainer
                            );

                            var npcMovement = creatureInstance.GetComponent<NPCsMovement>();
                            if (npcMovement != null)
                            {
                                npcMovement.Setup(playerObj, this);
                            }

                            spawnedList.Add(creatureInstance);
                            totalSpawned++;
                        }
                    }
                }
                if (totalSpawned >= _maxCreaturesPerChunk) break;
            }

            _spawnedCreatures[chunkCoordinate] = spawnedList;
        }

        /// <summary>
        /// Realiza a transição do jogador entre a navegação no barco e o capitão a pé em ilhas.
        /// </summary>
        public void TryGoOut(Camera mainCamera)
        {
            PlayerMovement boatMovement = FindFirstObjectByType<PlayerMovement>();
            if (boatMovement == null) return;

            GameObject boatObject = boatMovement.gameObject;
            GameObject captainObject = boatMovement.captain;
            if (captainObject == null) return;

            if (mainCamera == null) mainCamera = Camera.main;

            if (boatMovement.isOnWater)
            {
                Vector3[] directionsArray = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };
                foreach (Vector3 direction in directionsArray)
                {
                    Vector3 targetWorldPosition = boatObject.transform.position + (direction * _cellSize);
                    Tile tile = GetTileAtWorldPosition(targetWorldPosition);

                    // Layer != 0 corresponde a terra firme / costa
                    if (tile != null && tile.Metadata.Layer != 0)
                    {
                        boatMovement.isOnWater = false;
                        GameState.IsOnWater = false;
                        captainObject.SetActive(true);
                        captainObject.transform.position = targetWorldPosition;
                        _playerTransform = captainObject.transform;

                        var rb = boatObject.GetComponent<Rigidbody2D>();
                        if (rb != null) rb.linearVelocity = Vector2.zero;

                        if (mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null)
                        {
                            mainCamera.orthographicSize = 3.5f;
                        }
                        return;
                    }
                }
            }
            else
            {
                float distanceToBoat = Vector3.Distance(captainObject.transform.position, boatObject.transform.position);
                if (distanceToBoat < _cellSize * 1.5f)
                {
                    boatMovement.isOnWater = true;
                    GameState.IsOnWater = true;
                    captainObject.SetActive(false);
                    _playerTransform = boatObject.transform;

                    if (mainCamera != null && mainCamera.GetComponent<CameraFollow>() == null)
                    {
                        mainCamera.orthographicSize = 5f;
                    }
                }
            }
        }
    }
}
