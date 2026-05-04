using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using JetBrains.Annotations;

/// <summary>
/// Orquestrador central do mundo procedural baseado em chunks.
/// <para>
/// Responsabilidades:
/// <list type="bullet">
///   <item>Manter os chunks visíveis ao redor do jogador (<see cref="viewDistance"/>).</item>
///   <item>Enfileirar, gerar (sync ou async) e destruir chunks conforme o jogador se move.</item>
///   <item>Construir o <b>halo</b> de cada chunk — a borda de tiles de chunks vizinhos
///         que serve de restrição inicial para o WFC.</item>
///   <item>Notificar chunks vizinhos quando um novo chunk é gerado, para que restrinjam
///         suas próprias células de borda.</item>
///   <item>Salvar chunks em disco (<c>.dat</c>) ao descarregá-los e carregá-los ao reentrar.</item>
///   <item>Escanear chunks prontos e gerar estruturas sobre eles.</item>
///   <item>Gerenciar a transição jogador ↔ barco (<see cref="TryGoOut"/>).</item>
/// </list>
/// </para>
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    // =========================================================================
    // Campos Públicos — Inspector
    // =========================================================================

    [Header("Configurações de Mundo")]
    public GameObject chunkPrefab;
    public Transform player;
    public int viewDistance = 2;
    public float noiseScale = 0.05f;
    public List<StructureData> structures;

    [Header("Ferramentas de Desenvolvimento")]
    [Tooltip("Deleta todos os arquivos .dat salvos ao iniciar o jogo.")]
    public bool clearSaveOnStart = false;

    // =========================================================================
    // Tipos de Dados
    // =========================================================================

    /// <summary>
    /// Registro de uma estrutura já gerada no mundo.
    /// Usado para impedir sobreposições via <see cref="StructureData.raioDeIsolamento"/>.
    /// </summary>
    [Serializable]
    public struct StructureSaveData
    {
        public string structureName;
        public Vector3 structureWorldPosition;
        public float raioDeIsolamento;
    }
    public Transform creaturesContainer, structuresContainer, mapContainer;

    // =========================================================================
    // Estado Interno
    // =========================================================================

    /// <summary>
    /// Chunks completamente gerados e visíveis. Chave = posição do chunk na grade de chunks.
    /// </summary>
    private Dictionary<Vector2Int, MapGenerator> activeChunks = new();

    /// <summary>
    /// Chunks que saíram do campo de visão enquanto ainda estavam sendo gerados de forma assíncrona.
    /// São salvos e destruídos ao concluir a geração.
    /// </summary>
    private Dictionary<Vector2Int, MapGenerator> pendingChunks = new();

    /// <summary>Chunks que falharam na geração (contradição WFC) e aguardam nova tentativa.</summary>
    private HashSet<Vector2Int> failedChunks = new();

    /// <summary>Fila de chunks a gerar, ordenada por distância ao jogador.</summary>
    private List<Vector2Int> generationQueue = new();

    /// <summary>Chunks gerados com sucesso que aguardam os vizinhos ficarem prontos para receber estruturas.</summary>
    private List<Vector2Int> chunksAguardandoDecoracao = new();

    /// <summary>Posição do chunk em geração assíncrona no momento. <c>null</c> se ocioso.</summary>
    private Vector2Int? currentlyGenerating = null;

    private Vector2Int lastPlayerChunk;
    private Transform chunksContainer;
    private string savePath;
    private Vector2Int chunkSize;
    private float cachedCellSize;

    /// <summary>Estruturas já instanciadas no mundo. Persistidas em memória durante a sessão.</summary>
    public List<StructureSaveData> savedStructures = new();

    // =========================================================================
    // Unity Callbacks
    // =========================================================================

    void Start()
    {
        // Cria um GameObject container para organizar os chunks na hierarquia
        chunksContainer = new GameObject("-- Chunks --").transform;
        chunksContainer.transform.SetParent(mapContainer);

        savePath = Application.persistentDataPath + "/map_data/";
        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

        if (clearSaveOnStart) ClearSaveData();

        // Lê tamanho do chunk e da célula a partir do prefab, para não hardcodar
        var mgTemplate = chunkPrefab.GetComponent<MapGenerator>();
        chunkSize      = mgTemplate.chunkSize;
        cachedCellSize = chunkPrefab.GetComponent<Grid>().cellSize.x;

        lastPlayerChunk = GetPlayerChunkPos();
        GenerateInitialChunks(lastPlayerChunk);
    }

    void Update()
    {
        Vector2Int currentPlayerChunk = GetPlayerChunkPos();

        if (currentPlayerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = currentPlayerChunk;
            UpdateVisibleChunks(currentPlayerChunk);
            SortQueueByDistance(currentPlayerChunk);
        }

        if (currentlyGenerating == null)
            ProcessNextInQueue();
        else
            Debug.Log($"[WG] Gerando: {currentlyGenerating} | Fila: {generationQueue.Count} | Active: {activeChunks.Count} | Pending: {pendingChunks.Count}");

        ProcessarDecoracoes();
    }

    // =========================================================================
    // Posição de Chunks
    // =========================================================================

    /// <summary>Converte a posição do jogador em coordenadas de chunk na grade.</summary>
    private Vector2Int GetPlayerChunkPos()
    {
        return new Vector2Int(
            Mathf.FloorToInt(player.position.x / (chunkSize.x * cachedCellSize)),
            Mathf.FloorToInt(player.position.y / (chunkSize.y * cachedCellSize))
        );
    }

    /// <summary>Converte coordenadas de chunk para posição world no canto inferior-esquerdo.</summary>
    private Vector3 ChunkWorldPos(Vector2Int pos)
    {
        return new Vector3(
            pos.x * chunkSize.x * cachedCellSize,
            pos.y * chunkSize.y * cachedCellSize,
            0
        );
    }

    // =========================================================================
    // Geração de Chunks
    // =========================================================================

    /// <summary>
    /// Gera sincronamente os chunks dentro do campo de visão inicial.
    /// Os chunks são enfileirados em espiral (do centro para fora) para garantir
    /// que o chunk do jogador apareça primeiro.
    /// </summary>
    private void GenerateInitialChunks(Vector2Int center)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        for (int r = 0; r <= viewDistance; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                {
                    if (Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;
                    positions.Add(new Vector2Int(center.x + x, center.y + y));
                }

        foreach (var pos in positions)
        {
            if (activeChunks.ContainsKey(pos)) continue;
            CreateOrLoadChunkSync(pos);
        }
    }

    /// <summary>
    /// Instancia, configura e gera (ou carrega de disco) um chunk de forma síncrona.
    /// Após a geração, notifica os chunks vizinhos com as bordas deste chunk.
    /// </summary>
    private void CreateOrLoadChunkSync(Vector2Int pos)
    {
        string path     = savePath + $"chunk_{pos.x}_{pos.y}.dat";
        Vector3 worldPos = ChunkWorldPos(pos);

        GameObject go = Instantiate(chunkPrefab, worldPos, Quaternion.identity, chunksContainer);
        MapGenerator mg = go.GetComponent<MapGenerator>();
        mg.Setup(player.gameObject, this);
        activeChunks.Add(pos, mg);

        if (File.Exists(path) && !failedChunks.Contains(pos))
        {
            mg.LoadFromData(File.ReadAllBytes(path));
            mg.SpawnEntities();
        }
        else
        {
            Dictionary<Vector2Int, Tile> halo = BuildHalo(pos);

            if (pos == Vector2Int.zero)
            {
                // Chunk de origem é sempre oceano puro
                mg.ForceWaterChunk(halo);
            }
            else if (mg.GenerateChunk(halo, pos, noiseScale))
            {
                failedChunks.Remove(pos);
                chunksAguardandoDecoracao.Add(pos);
            }
            else
            {
                Debug.LogWarning($"Contradição em {pos} (síncrono). Chunk marcada para nova tentativa.");
                failedChunks.Add(pos);
            }
        }

        NotifyNeighbors(pos, mg);
    }

    /// <summary>
    /// Instancia e gera (ou carrega de disco) um chunk de forma assíncrona.
    /// Se o chunk já existe em <see cref="pendingChunks"/> (estava sendo gerado quando
    /// saiu do view distance e voltou), reutiliza o objeto em vez de criar um novo.
    /// </summary>
    private void CreateOrLoadChunkAsync(Vector2Int pos)
    {
        string path      = savePath + $"chunk_{pos.x}_{pos.y}.dat";
        Vector3 worldPos = ChunkWorldPos(pos);

        // Caso: chunk voltou ao campo de visão enquanto ainda estava pendente
        if (pendingChunks.TryGetValue(pos, out MapGenerator pendingMg))
        {
            pendingChunks.Remove(pos);
            activeChunks.Add(pos, pendingMg);
            pendingMg.tilemap.enabled = true;

            if (!pendingMg.IsGenerating) currentlyGenerating = null;
            return;
        }

        GameObject go = Instantiate(chunkPrefab, worldPos, Quaternion.identity, chunksContainer);
        MapGenerator mg = go.GetComponent<MapGenerator>();
        mg.Setup(player.gameObject, this);
        activeChunks.Add(pos, mg);

        if (File.Exists(path) && !failedChunks.Contains(pos))
        {
            mg.LoadFromData(File.ReadAllBytes(path));
            mg.SpawnEntities();
            NotifyNeighbors(pos, mg);
            currentlyGenerating = null;
        }
        else
        {
            Dictionary<Vector2Int, Tile> halo = BuildHalo(pos);

            mg.OnGenerationComplete = (completedMg, success) =>
            {
                Debug.Log($"[WG] Geração completa: {pos} sucesso={success} | currentlyGenerating={currentlyGenerating} | pending={pendingChunks.ContainsKey(pos)}");

                if (success)
                {
                    failedChunks.Remove(pos);
                    NotifyNeighbors(pos, completedMg);

                    if (activeChunks.ContainsKey(pos))
                        completedMg.SpawnEntities();

                    chunksAguardandoDecoracao.Add(pos);
                }
                else
                {
                    Debug.LogWarning($"Contradição em {pos}. Chunk marcada para nova tentativa ao reentrar.");
                    failedChunks.Add(pos);
                }

                // Se o chunk saiu do view distance durante a geração, salva e destrói
                if (pendingChunks.ContainsKey(pos))
                {
                    SaveAndDestroy(pos, completedMg);
                    pendingChunks.Remove(pos);
                }

                if (currentlyGenerating == pos)
                    currentlyGenerating = null;

                Debug.Log($"[WG] Após callback: currentlyGenerating={currentlyGenerating} | fila={generationQueue.Count}");
            };

            mg.GenerateChunkAsync(halo, pos, noiseScale);
        }
    }

    // =========================================================================
    // Visibilidade de Chunks
    // =========================================================================

    /// <summary>
    /// Atualiza o conjunto de chunks visíveis conforme o jogador se move.
    /// Enfileira chunks novos dentro do view distance e descarrega os que saíram.
    /// Chunks em geração assíncrona que saem do view distance vão para <see cref="pendingChunks"/>.
    /// </summary>
    private void UpdateVisibleChunks(Vector2Int center)
    {
        HashSet<Vector2Int> currentCoords = new HashSet<Vector2Int>();

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int y = -viewDistance; y <= viewDistance; y++)
            {
                Vector2Int chunkPos = new Vector2Int(center.x + x, center.y + y);
                currentCoords.Add(chunkPos);

                bool alreadyActive           = activeChunks.ContainsKey(chunkPos);
                bool alreadyPending          = pendingChunks.ContainsKey(chunkPos);
                bool alreadyInQueue          = generationQueue.Contains(chunkPos);
                bool currentlyBeingGenerated = currentlyGenerating == chunkPos;

                if (!alreadyActive && !alreadyPending && !alreadyInQueue && !currentlyBeingGenerated)
                    EnqueueChunk(chunkPos, center);
            }
        }

        // Remove chunks fora do campo de visão
        List<Vector2Int> toRemove = new List<Vector2Int>();
        foreach (var coord in activeChunks.Keys)
            if (!currentCoords.Contains(coord)) toRemove.Add(coord);

        foreach (var coord in toRemove)
        {
            MapGenerator mg = activeChunks[coord];
            activeChunks.Remove(coord);

            if (mg.IsGenerating)
            {
                // Chunk ainda em geração: mantém vivo mas oculto até concluir
                pendingChunks.Add(coord, mg);
                mg.tilemap.enabled = false;
            }
            else
            {
                SaveAndDestroy(coord, mg);
            }
        }

        // Remove da fila posições que já não estão mais no campo de visão
        generationQueue.RemoveAll(pos => !currentCoords.Contains(pos));
    }

    // =========================================================================
    // Fila de Geração
    // =========================================================================

    /// <summary>Adiciona um chunk à fila e reordena por distância ao jogador.</summary>
    private void EnqueueChunk(Vector2Int pos, Vector2Int playerChunk)
    {
        generationQueue.Add(pos);
        SortQueueByDistance(playerChunk);
    }

    /// <summary>Ordena a fila de geração do mais próximo ao mais distante do jogador.</summary>
    private void SortQueueByDistance(Vector2Int playerChunk)
    {
        generationQueue.Sort((a, b) =>
            (a - playerChunk).sqrMagnitude.CompareTo((b - playerChunk).sqrMagnitude));
    }

    /// <summary>
    /// Inicia a geração assíncrona do próximo chunk da fila, se houver.
    /// Remove da fila posições já ativas ou pendentes antes de processar.
    /// </summary>
    private void ProcessNextInQueue()
    {
        generationQueue.RemoveAll(pos => activeChunks.ContainsKey(pos) || pendingChunks.ContainsKey(pos));
        if (generationQueue.Count == 0) return;

        Vector2Int pos = generationQueue[0];
        generationQueue.RemoveAt(0);

        currentlyGenerating = pos;
        CreateOrLoadChunkAsync(pos);
    }

    // =========================================================================
    // Geração de Estruturas
    // =========================================================================

    /// <summary>
    /// Escaneia o chunk e tenta gerar cada estrutura definida em <see cref="structures"/>.
    /// Chamado somente quando todos os 8 vizinhos do chunk estão prontos (sem geração ativa).
    /// </summary>
    public void EscanearEGerarEstruturas(Vector2Int chunkPos)
    {
        MapGenerator mapGenerator = activeChunks[chunkPos];

        foreach (StructureData structure in structures)
        {
            if (UnityEngine.Random.value > structure.spawnChance) continue;

            bool isGenerated = false;

            for (int x = 0; x < chunkSize.x && !isGenerated; x++)
            {
                for (int y = 0; y < chunkSize.y && !isGenerated; y++)
                {
                    Vector3 worldPos = GetTileWorldPosition(chunkPos, x, y);

                    if (ValidarPlantaBaixa(worldPos, structure))
                    {
                        // Centraliza o prefab sobre a planta baixa
                        worldPos.x += (structure.structureDimensions.x - 1) * cachedCellSize / 2f;
                        worldPos.y += (structure.structureDimensions.y - 1) * cachedCellSize / 2f;

                        Instantiate(structure.structurePrefab, worldPos, Quaternion.identity, structuresContainer);
                        RegistrarEstrutura(structure.structureName, worldPos, structure.raioDeIsolamento);
                        isGenerated = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Verifica se uma estrutura pode ser colocada com o canto inferior-esquerdo
    /// em <paramref name="posMundoInicial"/>, validando:
    /// <list type="bullet">
    ///   <item>Distância mínima de isolamento de todas as estruturas existentes.</item>
    ///   <item>Camada de cada tile coberto pela planta baixa, respeitando os <see cref="StructureData.layerOverrides"/>.</item>
    /// </list>
    /// </summary>
    private bool ValidarPlantaBaixa(Vector3 posMundoInicial, StructureData planta)
    {
        // Checa raio de isolamento contra todas as estruturas já geradas
        foreach (var structure in savedStructures)
        {
            float distancia = Vector3.Distance(structure.structureWorldPosition, posMundoInicial);
            float raioMin   = Mathf.Max(structure.raioDeIsolamento, planta.raioDeIsolamento);
            if (distancia < raioMin) return false;
        }

        // Valida cada tile da planta baixa
        for (int x = 0; x < planta.structureDimensions.x; x++)
        {
            for (int y = 0; y < planta.structureDimensions.y; y++)
            {
                Vector3 tilePos = posMundoInicial + new Vector3(x * cachedCellSize, y * cachedCellSize, 0);
                Tile tile = GetTileAtWorldPosition(tilePos);
                if (tile == null) return false;

                // Verifica se esta coordenada local tem um override de camada
                bool isOnOverride = false;
                foreach (var layerOverride in planta.layerOverrides)
                {
                    if (isOnOverride) break;
                    foreach (Vector2Int coord in layerOverride.localCoordinates)
                    {
                        if (new Vector2Int(x, y) != coord) continue;

                        // Encontrou override: a camada deve bater exatamente
                        if (tile.metadata.camada == layerOverride.layer)
                            isOnOverride = true;
                        else
                            return false;

                        break;
                    }
                }

                if (isOnOverride) continue;

                // Sem override: a camada deve estar na lista de camadas válidas
                if (!planta.validBaseLayers.Contains(tile.metadata.camada)) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Processa a fila de chunks aguardando decoração.
    /// Um chunk só recebe estruturas quando todos os seus 8 vizinhos (3×3) estão
    /// ativos e não estão mais em geração, garantindo que <see cref="GetTileAtWorldPosition"/>
    /// retorne resultados válidos para toda a área coberta.
    /// </summary>
    public void ProcessarDecoracoes()
    {
        for (int i = chunksAguardandoDecoracao.Count - 1; i >= 0; i--)
        {
            if (TodosVizinhosProntos(chunksAguardandoDecoracao[i]))
            {
                EscanearEGerarEstruturas(chunksAguardandoDecoracao[i]);
                chunksAguardandoDecoracao.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Retorna <c>true</c> se todos os 9 chunks do bloco 3×3 centrado em
    /// <paramref name="pos"/> estão em <see cref="activeChunks"/> e sem geração ativa.
    /// </summary>
    private bool TodosVizinhosProntos(Vector2Int pos)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int vizinho = pos + new Vector2Int(x, y);
                if (!activeChunks.TryGetValue(vizinho, out var mg) || mg.IsGenerating)
                    return false;
            }
        return true;
    }

    // =========================================================================
    // Save e Destroy
    // =========================================================================

    /// <summary>Salva o chunk em disco (se não falhou) e destrói o GameObject.</summary>
    private void SaveAndDestroy(Vector2Int pos, MapGenerator mg)
    {
        if (!failedChunks.Contains(pos))
        {
            byte[] data = mg.GetChunkData();
            if (data != null) SaveChunkToDisk(pos, mg);
        }
        Destroy(mg.gameObject);
    }

    /// <summary>Grava os bytes do chunk no arquivo <c>chunk_X_Y.dat</c>.</summary>
    private void SaveChunkToDisk(Vector2Int pos, MapGenerator mg)
    {
        byte[] data = mg.GetChunkData();
        if (data != null)
            File.WriteAllBytes(savePath + $"chunk_{pos.x}_{pos.y}.dat", data);
    }

    /// <summary>
    /// Deleta todos os arquivos <c>.dat</c> da pasta de save.
    /// Disponível via ContextMenu no Inspector e acionável pelo flag <see cref="clearSaveOnStart"/>.
    /// </summary>
    [ContextMenu("Limpar Dados Salvos")]
    public void ClearSaveData()
    {
        if (!Directory.Exists(savePath)) return;

        int count = 0;
        foreach (string file in Directory.GetFiles(savePath, "*.dat"))
        {
            File.Delete(file);
            count++;
        }

        failedChunks.Clear();
        Debug.Log($"[WorldGenerator] {count} arquivo(s) .dat deletado(s) de {savePath}");
    }

    /// <summary>Registra uma estrutura gerada para impedir sobreposições futuras.</summary>
    public void RegistrarEstrutura(string nome, Vector3 posicao, float raioDeIsolamento)
    {
        savedStructures.Add(new StructureSaveData
        {
            structureName           = nome,
            structureWorldPosition  = posicao,
            raioDeIsolamento        = raioDeIsolamento
        });
    }

    // =========================================================================
    // Halo
    // =========================================================================

    /// <summary>
    /// Constrói o dicionário de halo para um chunk: reúne os tiles das bordas dos
    /// chunks vizinhos (4 lados + 4 cantos diagonais) e os mapeia para as coordenadas
    /// extras da grade interna do <see cref="MapGenerator"/> (linha/coluna 0 e chunkSize+1).
    /// <para>
    /// Se o vizinho está ativo em memória, os tiles são lidos diretamente.
    /// Se está apenas em disco, os dados são lidos do arquivo <c>.dat</c>.
    /// </para>
    /// </summary>
    private Dictionary<Vector2Int, Tile> BuildHalo(Vector2Int pos)
    {
        var halo = new Dictionary<Vector2Int, Tile>();

        FillHaloEdge(pos, Vector2Int.left,  neighborCol: chunkSize.x - 1, isVertical: true,  haloFixed: 0,               halo: halo);
        FillHaloEdge(pos, Vector2Int.right, neighborCol: 0,               isVertical: true,  haloFixed: chunkSize.x + 1, halo: halo);
        FillHaloEdge(pos, Vector2Int.down,  neighborCol: chunkSize.y - 1, isVertical: false, haloFixed: 0,               halo: halo);
        FillHaloEdge(pos, Vector2Int.up,    neighborCol: 0,               isVertical: false, haloFixed: chunkSize.y + 1, halo: halo);

        AddHaloCorner(pos, new Vector2Int(-1, -1), new Vector2Int(chunkSize.x - 1, chunkSize.y - 1), new Vector2Int(0, 0),                             halo);
        AddHaloCorner(pos, new Vector2Int( 1, -1), new Vector2Int(0, chunkSize.y - 1),               new Vector2Int(chunkSize.x + 1, 0),               halo);
        AddHaloCorner(pos, new Vector2Int(-1,  1), new Vector2Int(chunkSize.x - 1, 0),               new Vector2Int(0, chunkSize.y + 1),               halo);
        AddHaloCorner(pos, new Vector2Int( 1,  1), new Vector2Int(0, 0),                             new Vector2Int(chunkSize.x + 1, chunkSize.y + 1), halo);

        return halo;
    }

    /// <summary>
    /// Preenche uma aresta do halo lendo os tiles da borda correspondente do chunk vizinho.
    /// </summary>
    /// <param name="pos">Posição do chunk sendo construído.</param>
    /// <param name="dir">Direção do vizinho.</param>
    /// <param name="neighborCol">Coluna/linha do vizinho a ser lida.</param>
    /// <param name="isVertical"><c>true</c> = borda vertical (esquerda/direita); <c>false</c> = horizontal.</param>
    /// <param name="haloFixed">Índice fixo (x ou y) das células de halo a serem preenchidas.</param>
    /// <param name="halo">Dicionário de saída.</param>
    private void FillHaloEdge(Vector2Int pos, Vector2Int dir, int neighborCol, bool isVertical, int haloFixed, Dictionary<Vector2Int, Tile> halo)
    {
        Vector2Int neighborPos = pos + dir;
        int count = isVertical ? chunkSize.y : chunkSize.x;

        MapGenerator neighbor = null;
        activeChunks.TryGetValue(neighborPos, out neighbor);
        if (neighbor == null) pendingChunks.TryGetValue(neighborPos, out neighbor);

        if (neighbor != null)
        {
            for (int i = 0; i < count; i++)
            {
                Tile t = isVertical ? neighbor.GetTileAt(neighborCol, i) : neighbor.GetTileAt(i, neighborCol);
                if (t == null) continue;
                halo[isVertical ? new Vector2Int(haloFixed, i + 1) : new Vector2Int(i + 1, haloFixed)] = t;
            }
        }
        else
        {
            // Vizinho não está em memória: lê do arquivo .dat
            string path = savePath + $"chunk_{neighborPos.x}_{neighborPos.y}.dat";
            if (!File.Exists(path)) return;

            byte[] data   = File.ReadAllBytes(path);
            MapGenerator refMg = GetAnyActiveChunk();
            if (refMg == null) return;

            for (int i = 0; i < count; i++)
            {
                int idx = isVertical ? (neighborCol * chunkSize.y + i) : (i * chunkSize.y + neighborCol);
                if (idx < 0 || idx >= data.Length) continue;
                halo[isVertical ? new Vector2Int(haloFixed, i + 1) : new Vector2Int(i + 1, haloFixed)] = refMg.tilesetData.tileset[data[idx]];
            }
        }
    }

    /// <summary>
    /// Preenche uma célula de canto do halo lendo o tile do canto do chunk diagonal.
    /// </summary>
    /// <param name="pos">Posição do chunk sendo construído.</param>
    /// <param name="diagDir">Direção diagonal do vizinho (ex.: (-1,-1) para sudoeste).</param>
    /// <param name="neighborCoord">Coordenada local dentro do vizinho a ser lida.</param>
    /// <param name="haloCoord">Coordenada no halo a ser preenchida.</param>
    /// <param name="halo">Dicionário de saída.</param>
    private void AddHaloCorner(Vector2Int pos, Vector2Int diagDir, Vector2Int neighborCoord, Vector2Int haloCoord, Dictionary<Vector2Int, Tile> halo)
    {
        if (halo.ContainsKey(haloCoord)) return;

        Vector2Int neighborPos = pos + diagDir;

        MapGenerator neighbor = null;
        activeChunks.TryGetValue(neighborPos, out neighbor);
        if (neighbor == null) pendingChunks.TryGetValue(neighborPos, out neighbor);

        if (neighbor != null)
        {
            Tile t = neighbor.GetTileAt(neighborCoord.x, neighborCoord.y);
            if (t != null) halo[haloCoord] = t;
        }
        else
        {
            string path = savePath + $"chunk_{neighborPos.x}_{neighborPos.y}.dat";
            if (!File.Exists(path)) return;

            byte[] data = File.ReadAllBytes(path);
            MapGenerator refMg = GetAnyActiveChunk();
            if (refMg == null) return;

            int idx = neighborCoord.x * chunkSize.y + neighborCoord.y;
            if (idx >= 0 && idx < data.Length)
                halo[haloCoord] = refMg.tilesetData.tileset[data[idx]];
        }
    }

    // =========================================================================
    // Notificação de Vizinhos
    // =========================================================================

    /// <summary>
    /// Após a geração de um chunk, envia suas bordas para os chunks vizinhos
    /// via <see cref="MapGenerator.UpdateHaloAndRepropagate"/>, permitindo que
    /// eles restrinjam suas células internas de borda.
    /// </summary>
    private void NotifyNeighbors(Vector2Int pos, MapGenerator newMg)
    {
        // Define as 4 bordas: direção, coluna/linha a ser lida, orientação e índice do halo de destino
        var sides = new[]
        {
            (dir: Vector2Int.left,  sourceCol: 0,               isVert: true,  haloFixed: chunkSize.x + 1),
            (dir: Vector2Int.right, sourceCol: chunkSize.x - 1, isVert: true,  haloFixed: 0),
            (dir: Vector2Int.down,  sourceCol: 0,               isVert: false, haloFixed: chunkSize.y + 1),
            (dir: Vector2Int.up,    sourceCol: chunkSize.y - 1, isVert: false, haloFixed: 0),
        };

        foreach (var s in sides)
        {
            Vector2Int neighborPos = pos + s.dir;

            MapGenerator neighborMg = null;
            activeChunks.TryGetValue(neighborPos, out neighborMg);
            if (neighborMg == null) pendingChunks.TryGetValue(neighborPos, out neighborMg);
            if (neighborMg == null) continue;

            var haloUpdate = new Dictionary<Vector2Int, Tile>();
            int count = s.isVert ? chunkSize.y : chunkSize.x;

            for (int i = 0; i < count; i++)
            {
                Tile t = s.isVert ? newMg.GetTileAt(s.sourceCol, i) : newMg.GetTileAt(i, s.sourceCol);
                if (t == null) continue;

                Vector2Int haloCoord = s.isVert
                    ? new Vector2Int(s.haloFixed, i + 1)
                    : new Vector2Int(i + 1, s.haloFixed);

                haloUpdate[haloCoord] = t;
            }

            if (haloUpdate.Count > 0)
                neighborMg.UpdateHaloAndRepropagate(haloUpdate);
        }
    }

    // =========================================================================
    // Helpers e API Pública de Consulta
    // =========================================================================

    /// <summary>
    /// Retorna qualquer chunk ativo ou pendente. Usado como referência de tileset
    /// ao ler arquivos <c>.dat</c> sem ter o chunk em memória.
    /// </summary>
    private MapGenerator GetAnyActiveChunk()
    {
        foreach (var mg in activeChunks.Values) if (mg != null) return mg;
        foreach (var mg in pendingChunks.Values) if (mg != null) return mg;
        return chunkPrefab.GetComponent<MapGenerator>();
    }

    /// <summary>Converte coordenadas locais de um chunk para posição world de um tile.</summary>
    public Vector3 GetTileWorldPosition(Vector2Int chunkPos, int localX, int localY)
    {
        Vector3 chunkOrigin = ChunkWorldPos(chunkPos);
        return new Vector3(
            chunkOrigin.x + localX * cachedCellSize,
            chunkOrigin.y + localY * cachedCellSize,
            0
        );
    }

    /// <returns><c>true</c> se o chunk está ativo ou pendente (em memória).</returns>
    public bool IsChunkActive(Vector2Int chunkPos)
        => activeChunks.ContainsKey(chunkPos) || pendingChunks.ContainsKey(chunkPos);

    /// <summary>Converte uma posição world para as coordenadas de chunk correspondentes.</summary>
    public Vector2Int GetChunkPosFromWorld(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / (chunkSize.x * cachedCellSize)),
            Mathf.FloorToInt(worldPos.y / (chunkSize.y * cachedCellSize))
        );
    }

    /// <summary>Retorna o tile na posição atual do jogador, ou <c>null</c> se o chunk não estiver ativo.</summary>
    public Tile GetTileAtPlayerPosition()
    {
        Vector2Int chunkPos = GetPlayerChunkPos();
        if (!activeChunks.TryGetValue(chunkPos, out MapGenerator chunk)) return null;

        float relX = player.position.x - chunk.transform.position.x;
        float relY = player.position.y - chunk.transform.position.y;

        int localX = Mathf.Clamp(Mathf.FloorToInt(relX / cachedCellSize), 0, chunkSize.x - 1);
        int localY = Mathf.Clamp(Mathf.FloorToInt(relY / cachedCellSize), 0, chunkSize.y - 1);

        return chunk.GetTileAt(localX, localY);
    }

    /// <summary>
    /// Retorna o tile na posição world informada, ou <c>null</c> se o chunk não estiver ativo.
    /// </summary>
    public Tile GetTileAtWorldPosition(Vector3 worldPos)
    {
        Vector2Int chunkPos = GetChunkPosFromWorld(worldPos);

        if (!activeChunks.TryGetValue(chunkPos, out MapGenerator chunk)) return null;

        float relX = worldPos.x - chunk.transform.position.x;
        float relY = worldPos.y - chunk.transform.position.y;

        int localX = Mathf.FloorToInt(relX / cachedCellSize);
        int localY = Mathf.FloorToInt(relY / cachedCellSize);

        return chunk.GetTileAt(localX, localY);
    }

    // =========================================================================
    // Transição Jogador ↔ Barco
    // =========================================================================

    /// <summary>
    /// Gerencia a entrada e saída do barco.
    /// <list type="bullet">
    ///   <item>Na água: verifica se há um tile de costa adjacente para desembarcar.</item>
    ///   <item>Em terra: verifica se o capitão está próximo do barco para embarcar.</item>
    /// </list>
    /// </summary>
    public void TryGoOut(Camera camera)
    {
        PlayerMovement boatMov = FindFirstObjectByType<PlayerMovement>();
        if (boatMov == null) return;

        GameObject barcoObj   = boatMov.gameObject;
        GameObject capitãoObj = boatMov.capitão;

        if (boatMov.isOnWater)
        {
            // Tenta desembarcar em um tile de costa (camada 1)
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (Vector3 dir in directions)
            {
                Vector3 targetWorldPos = barcoObj.transform.position + dir * cachedCellSize;
                Tile tile = GetTileAtWorldPosition(targetWorldPos);

                if (tile != null && tile.metadata.camada == 1)
                {
                    boatMov.isOnWater      = false;
                    GameState.IsOnWater    = false;
                    capitãoObj.SetActive(true);
                    capitãoObj.transform.position = targetWorldPos;
                    camera.orthographicSize = Mathf.Lerp(3.5f, 5f, Time.deltaTime * 0.5f);

                    this.player = capitãoObj.transform;
                    barcoObj.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    AtualizarReferenciaNasChunks();
                    return;
                }
            }
        }
        else
        {
            // Tenta embarcar se o capitão estiver próximo o suficiente do barco
            float distanciaProBarco = Vector3.Distance(capitãoObj.transform.position, barcoObj.transform.position);
            if (distanciaProBarco < cachedCellSize * 1.5f)
            {
                boatMov.isOnWater   = true;
                GameState.IsOnWater = true;
                capitãoObj.SetActive(false);
                camera.orthographicSize = Mathf.Lerp(5f, 3.5f, Time.deltaTime * 0.5f);

                this.player = barcoObj.transform;
                AtualizarReferenciaNasChunks();
            }
        }
    }

    /// <summary>
    /// Atualiza a referência de <see cref="player"/> em todos os chunks ativos.
    /// Chamado após a transição barco ↔ capitão para que NPCs e chunks
    /// usem a entidade correta como alvo de seguimento.
    /// </summary>
    private void AtualizarReferenciaNasChunks()
    {
        foreach (var mg in activeChunks.Values)
            mg.Setup(this.player.gameObject, this);
    }

    /// <summary>
    /// Teleporta o jogador para o tile de água mais próximo dentro de um raio de busca.
    /// Útil para depuração ou para recuperar o jogador de posições inválidas.
    /// </summary>
    public void TryFindWaterTile()
    {
        if (player == null) return;

        Vector3 startPos     = player.position;
        int     searchRadius = 5;

        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int y = -searchRadius; y <= searchRadius; y++)
            {
                Vector3 checkPos = startPos + new Vector3(x * cachedCellSize, y * cachedCellSize, 0);
                Tile tile = GetTileAtWorldPosition(checkPos);

                if (tile != null && tile.metadata.camada == 0)
                {
                    player.position = checkPos;
                    Debug.Log("Jogador movido para a água!");
                    return;
                }
            }
        }

        Debug.LogWarning("Nenhum tile de água encontrado por perto.");
    }
}