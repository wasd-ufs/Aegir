using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Representa uma célula de estrada planejada com seu tipo morfológico e sockets de transição.
/// </summary>
public class PlannedRoadTile
{
    public Vector2Int Coordinate;
    public int Layer;
    public Tile.TileType TileType;
    public Tile.TileDirection Direction;
    public bool IsVisual = true;
    public Tile.CornerSockets Corners;
}

/// <summary>
/// Estrutura de dados que representa uma construção planejada no grid global.
/// </summary>
public class PlannedStructure
{
    public StructureData Blueprint;
    public Vector2Int GlobalTileOrigin;
    public Vector2Int Dimensions;
    public Vector2Int ChunkPosition;
    public Vector2Int DoorGlobalCoordinate;
}

/// <summary>
/// Plano determinístico de assentamento para uma ilha específica.
/// </summary>
public class IslandSettlementPlan
{
    public Vector2Int IslandCenterChunk;
    public Vector2Int IslandCenterTile;
    public PlannedStructure Harbor;
    public List<PlannedStructure> StructuresList = new List<PlannedStructure>();

    /// <summary>Dicionário de tiles de estrada planejados por coordenada global.</summary>
    public Dictionary<Vector2Int, PlannedRoadTile> RoadTiles = new Dictionary<Vector2Int, PlannedRoadTile>();

    /// <summary>Indexação rápida de tiles de estrada por chunk.</summary>
    public Dictionary<Vector2Int, List<PlannedRoadTile>> RoadTilesByChunk = new Dictionary<Vector2Int, List<PlannedRoadTile>>();

    /// <summary>Indexação rápida de estruturas planejadas por chunk.</summary>
    public Dictionary<Vector2Int, List<PlannedStructure>> StructuresByChunk = new Dictionary<Vector2Int, List<PlannedStructure>>();

    /// <summary>Conjunto de nós (bifurcações/cruzamentos) da malha viária.</summary>
    public HashSet<Vector2Int> RoadNodes = new HashSet<Vector2Int>();
}

/// <summary>
/// Planejador determinístico de assentamentos e estradas completas com quinas, cantos e transições morfológicas.
/// Utiliza autotiling topológico baseado em vértices para garantir que quinas internas (SL Intern)
/// e quinas externas (SL Quintet) sejam colocadas com perfeição matemática nos pontos de encontro e curvas.
/// Garante também que estruturas SÓ surgem se suas portas estiverem diretamente conectadas à estrada.
/// </summary>
public class IslandSettlementPlanner
{
    private readonly IslandMapSampler _sampler;
    private readonly IslandLocator _locator;
    private readonly Vector2Int _chunkSize;
    private readonly int _worldSeed;
    private readonly List<StructureData> _structuresList;
    private readonly Func<int, int, Tile> _tileProvider;

    private readonly Dictionary<Vector2Int, IslandSettlementPlan> _planCache = new Dictionary<Vector2Int, IslandSettlementPlan>();
    private readonly Dictionary<Vector2Int, IslandSettlementPlan> _chunkToPlanCache = new Dictionary<Vector2Int, IslandSettlementPlan>();

    public IslandSettlementPlanner(
        IslandMapSampler sampler,
        IslandLocator locator,
        Vector2Int chunkSize,
        int worldSeed,
        List<StructureData> structuresList,
        Func<int, int, Tile> tileProvider = null)
    {
        _sampler = sampler;
        _locator = locator;
        _chunkSize = chunkSize;
        _worldSeed = worldSeed;
        _structuresList = structuresList ?? new List<StructureData>();
        _tileProvider = tileProvider;
    }

    /// <summary>
    /// Obtém o tile associado a uma coordenada global se o provedor de células estiver configurado.
    /// </summary>
    public Tile GetTileAt(int globalX, int globalY)
    {
        return _tileProvider?.Invoke(globalX, globalY);
    }

    /// <summary>
    /// REGRA DO USUÁRIO: não use a altura para determinar os limites da terra, verifique a célula, veja qual tile e qual camada ele ocupa.
    /// Retorna a camada (Layer) do tile na coordenada global informada:
    /// - Camada 4: Bloco de Terra Firme / Grama (onde estradas visuais e estruturas são permitidas).
    /// - Camada 2: Bloco de Areia.
    /// - Camada 0: Bloco de Água.
    /// - Camadas ímpares (1, 3): Transições.
    /// </summary>
    public int GetCellLayer(int globalX, int globalY)
    {
        Tile tile = GetTileAt(globalX, globalY);
        if ((object)tile != null)
        {
            return tile.Metadata.Layer;
        }

        if (_sampler != null)
        {
            float h = _sampler.Sample(globalX, globalY);
            if (h >= IslandMapSampler.ISLAND_EDGE_THRESHOLD) return 4;
            if (h >= 0.35f || h >= IslandMapSampler.WATER_EDGE_THRESHOLD) return 2;
            return 0;
        }

        return -1;
    }

    /// <summary>
    /// Verifica se a célula é terra firme / apta para estrada (Camada >= 4).
    /// REGRA DO USUÁRIO: as estradas fecham quando encontram um elemento de camada inferior (Layer <= 3).
    /// Portanto, apenas células de Camada >= 4 (Bloco de Terra Firme / Grama) são terra firme para estradas.
    /// </summary>
    public bool IsCellLand(int globalX, int globalY)
    {
        return GetCellLayer(globalX, globalY) >= 4;
    }

    /// <summary>
    /// Retorna o plano determinístico para a ilha contendo o chunk informado,
    /// ou null se o chunk não pertencer a nenhuma ilha.
    /// Assegura continuidade de planos entre chunks vizinhas da mesma ilha.
    /// </summary>
    public IslandSettlementPlan GetPlanForChunk(Vector2Int chunkCoord)
    {
        if (_chunkToPlanCache.TryGetValue(chunkCoord, out var cachedPlan))
            return cachedPlan;

        List<Vector2Int> islandChunks = _locator.GetIslandContaining(chunkCoord);
        if (islandChunks == null || islandChunks.Count == 0) return null;

        Vector2Int centerChunk = _locator.GetCenterOfIsland(islandChunks);
        return GetPlanForIsland(centerChunk, islandChunks);
    }

    /// <summary>
    /// Retorna (ou gera de forma determinística) o plano de assentamento da ilha.
    /// </summary>
    public IslandSettlementPlan GetPlanForIsland(Vector2Int islandCenterChunk, List<Vector2Int> islandChunks = null)
    {
        if (_planCache.TryGetValue(islandCenterChunk, out var existingPlan))
        {
            RegisterPlanChunks(existingPlan, islandChunks);
            return existingPlan;
        }

        IslandSettlementPlan newPlan = CreatePlan(islandCenterChunk, islandChunks);
        _planCache[islandCenterChunk] = newPlan;
        RegisterPlanChunks(newPlan, islandChunks);
        return newPlan;
    }

    private void RegisterPlanChunks(IslandSettlementPlan plan, List<Vector2Int> islandChunks)
    {
        _chunkToPlanCache[plan.IslandCenterChunk] = plan;
        if (islandChunks != null)
        {
            foreach (var c in islandChunks)
                _chunkToPlanCache[c] = plan;
        }

        foreach (var c in plan.RoadTilesByChunk.Keys)
            _chunkToPlanCache[c] = plan;

        foreach (var c in plan.StructuresByChunk.Keys)
            _chunkToPlanCache[c] = plan;
    }

    private IslandSettlementPlan CreatePlan(Vector2Int islandCenterChunk, List<Vector2Int> islandChunks)
    {
        var plan = new IslandSettlementPlan
        {
            IslandCenterChunk = islandCenterChunk,
            IslandCenterTile = new Vector2Int(
                islandCenterChunk.x * _chunkSize.x + _chunkSize.x / 2,
                islandCenterChunk.y * _chunkSize.y + _chunkSize.y / 2
            )
        };

        int islandSeed = HashIslandSeed(_worldSeed, islandCenterChunk.x, islandCenterChunk.y);
        System.Random prng = new System.Random(islandSeed);

        var occupiedTiles = new HashSet<Vector2Int>();

        Vector2Int? harborOrigin = TryPlaceHarbor(plan, prng, occupiedTiles);

        Vector2Int villageCenter = plan.IslandCenterTile;
        var (avenueXs, streetYs) = TraceRoadsWithFullTransitions(plan, harborOrigin, villageCenter, islandChunks, prng);

        AllocateBlockSettlement(plan, avenueXs, streetYs, islandSeed, occupiedTiles);

        IndexPlanByChunks(plan);

        return plan;
    }

    #region 1. Posicionamento do Porto

    private Vector2Int? TryPlaceHarbor(IslandSettlementPlan plan, System.Random prng, HashSet<Vector2Int> occupiedTiles)
    {
        StructureData harborBlueprint = _structuresList.FirstOrDefault(
            s => (object)s != null && (s.Category == StructureCategory.Harbor || (s.StructureName != null && s.StructureName.IndexOf("Porto", StringComparison.OrdinalIgnoreCase) >= 0)));

        if ((object)harborBlueprint == null) return null;

        Vector2Int centerTile = plan.IslandCenterTile;
        Vector2Int dims = harborBlueprint.StructureDimensions;

        int searchRangeX = Mathf.Min(_chunkSize.x * 2, 30);
        int searchDepthY = Mathf.Min(_chunkSize.y * 3, 50);

        List<int> xOffsets = new List<int>();
        for (int i = 0; i <= searchRangeX; i++)
        {
            if (i == 0) xOffsets.Add(0);
            else { xOffsets.Add(i); xOffsets.Add(-i); }
        }

        foreach (int offsetX in xOffsets)
        {
            int testX = centerTile.x + offsetX;

            for (int dy = 0; dy <= searchDepthY; dy++)
            {
                int testY = centerTile.y - dy;

                if (IsValidHarborShoreSpot(testX, testY, dims))
                {
                    Vector2Int origin = new Vector2Int(testX, testY);
                    Vector2Int chunkPos = GetChunkPosition(origin);

                    var harbor = new PlannedStructure
                    {
                        Blueprint = harborBlueprint,
                        GlobalTileOrigin = origin,
                        Dimensions = dims,
                        ChunkPosition = chunkPos,
                        DoorGlobalCoordinate = origin + harborBlueprint.DoorLocalCoordinate
                    };

                    plan.Harbor = harbor;
                    plan.StructuresList.Add(harbor);

                    for (int x = 0; x < dims.x; x++)
                        for (int y = 0; y < dims.y; y++)
                            occupiedTiles.Add(new Vector2Int(origin.x + x, origin.y + y));

                    return origin;
                }
            }
        }

        return null;
    }

    private bool IsValidHarborShoreSpot(int originX, int originY, Vector2Int dims)
    {
        for (int x = 0; x < dims.x; x++)
        {
            for (int y = 0; y < dims.y; y++)
            {
                int layer = GetCellLayer(originX + x, originY + y);
                if (layer < 2)
                    return false;
            }
        }

        int waterTilesSouth = 0;
        for (int x = 0; x < dims.x; x++)
        {
            int layerSouth = GetCellLayer(originX + x, originY - 1);
            if (layerSouth <= 1)
                waterTilesSouth++;
        }

        return waterTilesSouth >= dims.x / 2 + 1;
    }

    #endregion

    #region 2. Traçado de Estradas com Quinas e Cantos (Autotiling Dual-Grid)

    private (List<int> avenueXs, List<int> streetYs) TraceRoadsWithFullTransitions(
        IslandSettlementPlan plan,
        Vector2Int? harborOrigin,
        Vector2Int villageCenter,
        List<Vector2Int> islandChunks,
        System.Random prng)
    {
        int minTileX, maxTileX, minTileY, maxTileY;
        if (islandChunks != null && islandChunks.Count > 0)
        {
            minTileX = islandChunks.Min(c => c.x) * _chunkSize.x;
            maxTileX = (islandChunks.Max(c => c.x) + 1) * _chunkSize.x - 1;
            minTileY = islandChunks.Min(c => c.y) * _chunkSize.y;
            maxTileY = (islandChunks.Max(c => c.y) + 1) * _chunkSize.y - 1;
        }
        else
        {
            minTileX = villageCenter.x - _chunkSize.x * 2;
            maxTileX = villageCenter.x + _chunkSize.x * 2;
            minTileY = villageCenter.y - _chunkSize.y * 2;
            maxTileY = villageCenter.y + _chunkSize.y * 2;
        }

        if (harborOrigin.HasValue)
        {
            minTileY = Mathf.Min(minTileY, harborOrigin.Value.y - 3);
            minTileX = Mathf.Min(minTileX, harborOrigin.Value.x - 4);
            maxTileX = Mathf.Max(maxTileX, harborOrigin.Value.x + plan.Harbor.Dimensions.x + 4);
        }

        int anchorX = harborOrigin.HasValue
            ? harborOrigin.Value.x + plan.Harbor.Dimensions.x / 2
            : villageCenter.x;
        int anchorY = villageCenter.y;

        if (anchorX % 2 != 0)
        {
            anchorX = IsCellLand(anchorX + 1, anchorY) ? anchorX + 1 : anchorX - 1;
        }
        if (anchorY % 2 != 0)
        {
            anchorY = IsCellLand(anchorX, anchorY + 1) ? anchorY + 1 : anchorY - 1;
        }

        if (!Is2x2Land(anchorX, anchorY))
        {
            Vector2Int? best = FindClosestValidEvenNode(anchorX, anchorY, minTileX, maxTileX, minTileY, maxTileY);
            if (best.HasValue)
            {
                anchorX = best.Value.x;
                anchorY = best.Value.y;
            }
        }

        List<int> candidateXs = new List<int> { anchorX };
        int curX = anchorX;
        while (curX + 8 <= maxTileX)
        {
            int spacing = (candidateXs.Count % 2 == 0) ? 8 : 10;
            curX += spacing;
            if (curX <= maxTileX) candidateXs.Add(curX);
        }
        curX = anchorX;
        while (curX - 8 >= minTileX)
        {
            int spacing = (candidateXs.Count % 2 == 0) ? 8 : 10;
            curX -= spacing;
            if (curX >= minTileX) candidateXs.Add(curX);
        }
        candidateXs.Sort();

        List<int> candidateYs = new List<int> { anchorY };
        int curY = anchorY;
        while (curY + 8 <= maxTileY)
        {
            int spacing = (candidateYs.Count % 2 == 0) ? 8 : 10;
            curY += spacing;
            if (curY <= maxTileY) candidateYs.Add(curY);
        }
        curY = anchorY;
        while (curY - 8 >= minTileY)
        {
            int spacing = (candidateYs.Count % 2 == 0) ? 8 : 10;
            curY -= spacing;
            if (curY >= minTileY) candidateYs.Add(curY);
        }
        candidateYs.Sort();

        var validHoriz = new HashSet<(int xi, int yi)>();
        for (int i = 0; i < candidateXs.Count - 1; i++)
        {
            for (int j = 0; j < candidateYs.Count; j++)
            {
                if (IsHorizontalSegmentValid(candidateXs[i], candidateXs[i + 1], candidateYs[j]))
                {
                    validHoriz.Add((i, j));
                }
            }
        }

        var validVert = new HashSet<(int xi, int yi)>();
        for (int i = 0; i < candidateXs.Count; i++)
        {
            for (int j = 0; j < candidateYs.Count - 1; j++)
            {
                if (IsVerticalSegmentValid(candidateXs[i], candidateYs[j], candidateYs[j + 1]))
                {
                    validVert.Add((i, j));
                }
            }
        }

        int rootXi = candidateXs.IndexOf(anchorX);
        int rootYi = candidateYs.IndexOf(anchorY);
        if (rootXi < 0 || rootYi < 0)
        {
            rootXi = 0;
            rootYi = 0;
        }

        var activeNodes = new HashSet<(int xi, int yi)>();
        var activeHoriz = new HashSet<(int xi, int yi)>();
        var activeVert = new HashSet<(int xi, int yi)>();
        var queue = new Queue<(int xi, int yi)>();

        activeNodes.Add((rootXi, rootYi));
        queue.Enqueue((rootXi, rootYi));

        while (queue.Count > 0)
        {
            var (xi, yi) = queue.Dequeue();

            if (xi + 1 < candidateXs.Count && validHoriz.Contains((xi, yi)))
            {
                activeHoriz.Add((xi, yi));
                if (activeNodes.Add((xi + 1, yi)))
                    queue.Enqueue((xi + 1, yi));
            }
            if (xi - 1 >= 0 && validHoriz.Contains((xi - 1, yi)))
            {
                activeHoriz.Add((xi - 1, yi));
                if (activeNodes.Add((xi - 1, yi)))
                    queue.Enqueue((xi - 1, yi));
            }
            if (yi + 1 < candidateYs.Count && validVert.Contains((xi, yi)))
            {
                activeVert.Add((xi, yi));
                if (activeNodes.Add((xi, yi + 1)))
                    queue.Enqueue((xi, yi + 1));
            }
            if (yi - 1 >= 0 && validVert.Contains((xi, yi - 1)))
            {
                activeVert.Add((xi, yi - 1));
                if (activeNodes.Add((xi, yi - 1)))
                    queue.Enqueue((xi, yi - 1));
            }
        }

        foreach (var (xi, yi) in activeNodes)
        {
            Vector2Int nodeCoord = new Vector2Int(candidateXs[xi], candidateYs[yi]);
            if (IsCellLand(nodeCoord.x, nodeCoord.y))
            {
                plan.RoadNodes.Add(nodeCoord);
            }
        }

        var visualRoadCells = new HashSet<Vector2Int>();

        foreach (var (xi, yi) in activeHoriz)
        {
            int xStart = candidateXs[xi];
            int xEnd = candidateXs[xi + 1] + 1;
            int y = candidateYs[yi];
            for (int x = xStart; x <= xEnd; x++)
            {
                visualRoadCells.Add(new Vector2Int(x, y));
                visualRoadCells.Add(new Vector2Int(x, y + 1));
            }
        }

        foreach (var (xi, yi) in activeVert)
        {
            int x = candidateXs[xi];
            int yStart = candidateYs[yi];
            int yEnd = candidateYs[yi + 1] + 1;
            for (int y = yStart; y <= yEnd; y++)
            {
                visualRoadCells.Add(new Vector2Int(x, y));
                visualRoadCells.Add(new Vector2Int(x + 1, y));
            }
        }

        if (plan.Harbor != null)
        {
            Vector2Int doorTile = plan.Harbor.GlobalTileOrigin + plan.Harbor.Blueprint.DoorLocalCoordinate;
            Vector2Int frontTile = doorTile + Vector2Int.down;

            int startY = frontTile.y;
            int endY = plan.Harbor.GlobalTileOrigin.y + plan.Harbor.Dimensions.y;
            int roadX = doorTile.x;

            for (int y = startY; y <= endY; y++)
            {
                Vector2Int roadCoord = new Vector2Int(roadX, y);
                if (!plan.RoadTiles.ContainsKey(roadCoord))
                {
                    plan.RoadTiles[roadCoord] = new PlannedRoadTile
                    {
                        Coordinate = roadCoord,
                        Layer = 2,
                        TileType = Tile.TileType.Block,
                        Direction = Tile.TileDirection.None,
                        IsVisual = false,
                        Corners = new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 }
                    };
                }
            }

            if (roadX != anchorX)
            {
                int minX = Mathf.Min(roadX, anchorX);
                int maxX = Mathf.Max(roadX, anchorX);
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int connectorCoord = new Vector2Int(x, endY);
                    if (!plan.RoadTiles.ContainsKey(connectorCoord))
                    {
                        plan.RoadTiles[connectorCoord] = new PlannedRoadTile
                        {
                            Coordinate = connectorCoord,
                            Layer = 2,
                            TileType = Tile.TileType.Block,
                            Direction = Tile.TileDirection.None,
                            IsVisual = false,
                            Corners = new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 }
                        };
                    }
                }
            }

            int targetY = candidateYs.Where(y => y >= endY).DefaultIfEmpty(anchorY).Min();
            for (int y = endY; y <= targetY; y++)
            {
                if (IsCellLand(anchorX, y) && IsCellLand(anchorX + 1, y))
                {
                    visualRoadCells.Add(new Vector2Int(anchorX, y));
                    visualRoadCells.Add(new Vector2Int(anchorX + 1, y));
                }
                else
                {
                    Vector2Int connectorCoordA = new Vector2Int(anchorX, y);
                    if (!plan.RoadTiles.ContainsKey(connectorCoordA))
                    {
                        plan.RoadTiles[connectorCoordA] = new PlannedRoadTile
                        {
                            Coordinate = connectorCoordA,
                            Layer = 2,
                            TileType = Tile.TileType.Block,
                            Direction = Tile.TileDirection.None,
                            IsVisual = false,
                            Corners = new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 }
                        };
                    }

                    Vector2Int connectorCoordB = new Vector2Int(anchorX + 1, y);
                    if (!plan.RoadTiles.ContainsKey(connectorCoordB))
                    {
                        plan.RoadTiles[connectorCoordB] = new PlannedRoadTile
                        {
                            Coordinate = connectorCoordB,
                            Layer = 2,
                            TileType = Tile.TileType.Block,
                            Direction = Tile.TileDirection.None,
                            IsVisual = false,
                            Corners = new Tile.CornerSockets { NorthWest = 2, NorthEast = 2, SouthWest = 2, SouthEast = 2 }
                        };
                    }
                }
            }
        }

        visualRoadCells.RemoveWhere(c => !IsCellLand(c.x, c.y));

        foreach (var cell in visualRoadCells)
        {
            int sw = GetVertexSocket(cell.x, cell.y, visualRoadCells);
            int se = GetVertexSocket(cell.x + 1, cell.y, visualRoadCells);
            int nw = GetVertexSocket(cell.x, cell.y + 1, visualRoadCells);
            int ne = GetVertexSocket(cell.x + 1, cell.y + 1, visualRoadCells);

            bool hasNorth = visualRoadCells.Contains(cell + Vector2Int.up);
            bool hasSouth = visualRoadCells.Contains(cell + Vector2Int.down);
            bool hasEast  = visualRoadCells.Contains(cell + Vector2Int.right);
            bool hasWest  = visualRoadCells.Contains(cell + Vector2Int.left);

            if (!hasNorth) { nw = 4; ne = 4; }
            if (!hasSouth) { sw = 4; se = 4; }
            if (!hasEast)  { ne = 4; se = 4; }
            if (!hasWest)  { nw = 4; sw = 4; }

            if (!hasSouth && hasNorth)
            {
                if (hasEast && !hasWest) { nw = 4; ne = 2; }
                else if (hasWest && !hasEast) { nw = 2; ne = 4; }
            }
            else if (!hasNorth && hasSouth)
            {
                if (hasEast && !hasWest) { sw = 4; se = 2; }
                else if (hasWest && !hasEast) { sw = 2; se = 4; }
            }
            else if (!hasEast && hasWest)
            {
                if (hasSouth && !hasNorth) { nw = 4; sw = 2; }
                else if (hasNorth && !hasSouth) { nw = 2; sw = 4; }
            }
            else if (!hasWest && hasEast)
            {
                if (hasSouth && !hasNorth) { ne = 4; se = 2; }
                else if (hasNorth && !hasSouth) { ne = 2; se = 4; }
            }

            if (TryResolveRoadTile(nw, ne, sw, se, out var roadTile))
            {
                roadTile.Coordinate = cell;
                roadTile.IsVisual = true;
                plan.RoadTiles[cell] = roadTile;
            }
        }

        var streetList = activeHoriz.Select(h => candidateYs[h.yi]).Distinct().OrderBy(y => y).ToList();
        if (streetList.Count == 0)
        {
            streetList = activeNodes.Select(n => candidateYs[n.yi]).Distinct().OrderBy(y => y).ToList();
        }
        var avenueList = activeVert.Select(v => candidateXs[v.xi]).Distinct().OrderBy(x => x).ToList();
        if (avenueList.Count == 0)
        {
            avenueList = activeNodes.Select(n => candidateXs[n.xi]).Distinct().OrderBy(x => x).ToList();
        }

        return (avenueList, streetList);
    }

    private bool IsTransitionOrSand(int x, int y, IslandSettlementPlan plan)
    {
        if (plan.RoadTiles.TryGetValue(new Vector2Int(x, y), out var planned))
        {
            if (!planned.IsVisual || planned.Layer == 2)
                return true;
        }

        Tile tile = GetTileAt(x, y);
        if ((object)tile != null)
        {
            return tile.Metadata.Layer <= 3;
        }

        if (_sampler != null)
        {
            return _sampler.Sample(x, y) < IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        }

        return false;
    }

    private bool Is2x2Land(int x, int y)
    {
        return IsCellLand(x, y) && IsCellLand(x + 1, y) && IsCellLand(x, y + 1) && IsCellLand(x + 1, y + 1);
    }

    private bool IsHorizontalSegmentValid(int x1, int x2, int y)
    {
        for (int x = x1; x <= x2 + 1; x++)
        {
            if (!IsCellLand(x, y) || !IsCellLand(x, y + 1))
                return false;
        }
        return true;
    }

    private bool IsVerticalSegmentValid(int x, int y1, int y2)
    {
        for (int y = y1; y <= y2 + 1; y++)
        {
            if (!IsCellLand(x, y) || !IsCellLand(x + 1, y))
                return false;
        }
        return true;
    }

    private Vector2Int? FindClosestValidEvenNode(int startX, int startY, int minX, int maxX, int minY, int maxY)
    {
        for (int r = 2; r <= 30; r += 2)
        {
            for (int dx = -r; dx <= r; dx += 2)
            {
                for (int dy = -r; dy <= r; dy += 2)
                {
                    int tx = startX + dx;
                    int ty = startY + dy;
                    if (tx >= minX && tx + 1 <= maxX && ty >= minY && ty + 1 <= maxY)
                    {
                        if (Is2x2Land(tx, ty))
                            return new Vector2Int(tx, ty);
                    }
                }
            }
        }
        return null;
    }

    private static int GetVertexSocket(int vx, int vy, HashSet<Vector2Int> visualCells)
    {
        bool c00 = visualCells.Contains(new Vector2Int(vx - 1, vy - 1));
        bool c10 = visualCells.Contains(new Vector2Int(vx, vy - 1));
        bool c01 = visualCells.Contains(new Vector2Int(vx - 1, vy));
        bool c11 = visualCells.Contains(new Vector2Int(vx, vy));

        return (c00 && c10 && c01 && c11) ? 2 : 4;
    }

    /// <summary>
    /// Valida se os sockets do tile nos 4 lados coincidem perfeitamente com os vizinhos adjacentes.
    /// Retorna false se houver incompatibilidade geométrica nos sockets.
    /// </summary>
    public static bool IsRoadTileSocketsCompatible(
        Vector2Int cell,
        PlannedRoadTile tile,
        Dictionary<Vector2Int, PlannedRoadTile> roadTiles,
        IslandMapSampler sampler)
    {
        Vector2Int northCell = cell + Vector2Int.up;
        int expectedNW, expectedNE;
        if (roadTiles.TryGetValue(northCell, out var northTile))
        {
            expectedNW = northTile.Corners.SouthWest;
            expectedNE = northTile.Corners.SouthEast;
        }
        else
        {
            int groundLayer = sampler.Sample(northCell.x, northCell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ? 4 : 2;
            expectedNW = groundLayer;
            expectedNE = groundLayer;
        }
        if (tile.Corners.NorthWest != expectedNW || tile.Corners.NorthEast != expectedNE)
            return false;

        Vector2Int southCell = cell + Vector2Int.down;
        int expectedSW, expectedSE;
        if (roadTiles.TryGetValue(southCell, out var southTile))
        {
            expectedSW = southTile.Corners.NorthWest;
            expectedSE = southTile.Corners.NorthEast;
        }
        else
        {
            int groundLayer = sampler.Sample(southCell.x, southCell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ? 4 : 2;
            expectedSW = groundLayer;
            expectedSE = groundLayer;
        }
        if (tile.Corners.SouthWest != expectedSW || tile.Corners.SouthEast != expectedSE)
            return false;

        Vector2Int westCell = cell + Vector2Int.left;
        int expectedWestNW, expectedWestSW;
        if (roadTiles.TryGetValue(westCell, out var westTile))
        {
            expectedWestNW = westTile.Corners.NorthEast;
            expectedWestSW = westTile.Corners.SouthEast;
        }
        else
        {
            int groundLayer = sampler.Sample(westCell.x, westCell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ? 4 : 2;
            expectedWestNW = groundLayer;
            expectedWestSW = groundLayer;
        }
        if (tile.Corners.NorthWest != expectedWestNW || tile.Corners.SouthWest != expectedWestSW)
            return false;

        Vector2Int eastCell = cell + Vector2Int.right;
        int expectedEastNE, expectedEastSE;
        if (roadTiles.TryGetValue(eastCell, out var eastTile))
        {
            expectedEastNE = eastTile.Corners.NorthWest;
            expectedEastSE = eastTile.Corners.SouthWest;
        }
        else
        {
            int groundLayer = sampler.Sample(eastCell.x, eastCell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ? 4 : 2;
            expectedEastNE = groundLayer;
            expectedEastSE = groundLayer;
        }
        if (tile.Corners.NorthEast != expectedEastNE || tile.Corners.SouthEast != expectedEastSE)
            return false;

        return true;
    }

    /// <summary>
    /// Compara a compatibilidade direcional entre dois conjuntos de CornerSockets.
    /// </summary>
    public static bool AreSocketsCompatible(Tile.CornerSockets current, Tile.CornerSockets neighbor, Vector2Int direction)
    {
        if (direction == Vector2Int.right)
            return current.NorthEast == neighbor.NorthWest && current.SouthEast == neighbor.SouthWest;
        if (direction == Vector2Int.left)
            return current.NorthWest == neighbor.NorthEast && current.SouthWest == neighbor.SouthEast;
        if (direction == Vector2Int.up)
            return current.NorthWest == neighbor.SouthWest && current.NorthEast == neighbor.SouthEast;
        if (direction == Vector2Int.down)
            return current.SouthWest == neighbor.NorthWest && current.SouthEast == neighbor.NorthEast;
        return false;
    }

    private static bool IsBackingBlockLayer4(
        Vector2Int cell,
        Tile.TileType type,
        Tile.TileDirection dir,
        IslandMapSampler sampler)
    {
        if (type == Tile.TileType.Coast)
        {
            if (dir == Tile.TileDirection.South)
                return sampler.Sample(cell.x, cell.y + 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.North)
                return sampler.Sample(cell.x, cell.y - 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.East)
                return sampler.Sample(cell.x - 1, cell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.West)
                return sampler.Sample(cell.x + 1, cell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        }
        else if (type == Tile.TileType.Corner)
        {
            if (dir == Tile.TileDirection.NorthEast)
                return sampler.Sample(cell.x - 1, cell.y - 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.NorthWest)
                return sampler.Sample(cell.x + 1, cell.y - 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.SouthEast)
                return sampler.Sample(cell.x - 1, cell.y + 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.SouthWest)
                return sampler.Sample(cell.x + 1, cell.y + 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        }
        else if (type == Tile.TileType.InnerCorner)
        {
            if (dir == Tile.TileDirection.NorthEast)
                return sampler.Sample(cell.x, cell.y + 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ||
                       sampler.Sample(cell.x + 1, cell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.NorthWest)
                return sampler.Sample(cell.x, cell.y + 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ||
                       sampler.Sample(cell.x - 1, cell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.SouthEast)
                return sampler.Sample(cell.x, cell.y - 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ||
                       sampler.Sample(cell.x + 1, cell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
            if (dir == Tile.TileDirection.SouthWest)
                return sampler.Sample(cell.x, cell.y - 1) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD ||
                       sampler.Sample(cell.x - 1, cell.y) >= IslandMapSampler.ISLAND_EDGE_THRESHOLD;
        }
        return false;
    }

    /// <summary>
    /// "Tile Inteligente": Re-deriva o tipo correto de tile de estrada para uma célula
    /// lendo os sockets expostos pelos road tiles vizinhos (visuais OU Block não-visuais).
    /// <para>
    /// Tiles de terreno não são amostrados como fallback: no dual-grid, todo road vertex
    /// é sempre constrangido por pelo menos um road tile adjacente. Cantos sem constraintde estrada
    /// default para 4 (terra firme, sem road vertex — matematicamente correto).<br/>
    /// Se dois road tiles discordam num mesmo canto → conflito topológico → retorna null.
    /// </para>
    /// </summary>
    public static PlannedRoadTile DeriveRoadTileFromNeighborSockets(
        Vector2Int cell,
        Dictionary<Vector2Int, PlannedRoadTile> roadTiles,
        IslandMapSampler sampler)
    {
        int nwFromN = -1, neFromN = -1;
        int swFromS = -1, seFromS = -1;
        int nwFromW = -1, swFromW = -1;
        int neFromE = -1, seFromE = -1;

        if (roadTiles.TryGetValue(cell + Vector2Int.up, out var north) &&
            (north.IsVisual || north.TileType == Tile.TileType.Block))
        { nwFromN = north.Corners.SouthWest; neFromN = north.Corners.SouthEast; }

        if (roadTiles.TryGetValue(cell + Vector2Int.down, out var south) &&
            (south.IsVisual || south.TileType == Tile.TileType.Block))
        { swFromS = south.Corners.NorthWest; seFromS = south.Corners.NorthEast; }

        if (roadTiles.TryGetValue(cell + Vector2Int.left, out var west) &&
            (west.IsVisual || west.TileType == Tile.TileType.Block))
        { nwFromW = west.Corners.NorthEast; swFromW = west.Corners.SouthEast; }

        if (roadTiles.TryGetValue(cell + Vector2Int.right, out var east) &&
            (east.IsVisual || east.TileType == Tile.TileType.Block))
        { neFromE = east.Corners.NorthWest; seFromE = east.Corners.SouthWest; }

        if (!ResolveCornerSocket(nwFromN, nwFromW, out int nw)) return null;
        if (!ResolveCornerSocket(neFromN, neFromE, out int ne)) return null;
        if (!ResolveCornerSocket(swFromS, swFromW, out int sw)) return null;
        if (!ResolveCornerSocket(seFromS, seFromE, out int se)) return null;

        if (nw == 4 && ne == 4 && sw == 4 && se == 4) return null;

        if (!TryResolveRoadTile(nw, ne, sw, se, out var derived)) return null;

        if (derived.Layer == 2 || derived.TileType == Tile.TileType.Block) return null;

        derived.IsVisual = IsBackingBlockLayer4(cell, derived.TileType, derived.Direction, sampler);
        derived.Coordinate = cell;
        return derived;
    }

    /// <summary>
    /// Resolve um canto a partir de dois contribuintes road tile (-1 = sem contribuição).
    /// Ambos concordam → usa o valor. Apenas um → usa esse. Nenhum → 4 (terra firme).
    /// Dois discordam → conflito topológico (retorna false).
    /// </summary>
    private static bool ResolveCornerSocket(int fromA, int fromB, out int result)
    {
        bool hasA = fromA >= 0;
        bool hasB = fromB >= 0;

        if (hasA && hasB)
        {
            if (fromA != fromB) { result = 0; return false; }
            result = fromA;
            return true;
        }
        result = hasA ? fromA : (hasB ? fromB : 4);
        return true;
    }

    /// <summary>
    /// Decodificador topológico 1:1 baseado nas regras de sockets do Tile.cs:
    /// - 4 quinas de areia -> Sand Block (Layer 2)
    /// - 2 quinas de areia e 2 de grama -> Coast (Layer 3)
    /// - 3 quinas de areia e 1 de grama -> Corner / Quintet (Layer 3, quina de encontro de estradas)
    /// - 1 quina de areia e 3 de grama -> InnerCorner / Intern (Layer 3, canto de final de estrada/curva)
    /// </summary>
    public static bool TryResolveRoadTile(int nw, int ne, int sw, int se, out PlannedRoadTile roadTile)
    {
        roadTile = null;

        if (nw == 2 && ne == 2 && sw == 2 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 2, TileType = Tile.TileType.Block, Direction = Tile.TileDirection.None };
        }
        else if (nw == 4 && ne == 4 && sw == 2 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Coast, Direction = Tile.TileDirection.South };
        }
        else if (nw == 2 && ne == 2 && sw == 4 && se == 4)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Coast, Direction = Tile.TileDirection.North };
        }
        else if (nw == 4 && ne == 2 && sw == 4 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Coast, Direction = Tile.TileDirection.East };
        }
        else if (nw == 2 && ne == 4 && sw == 2 && se == 4)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Coast, Direction = Tile.TileDirection.West };
        }
        else if (nw == 2 && ne == 2 && sw == 4 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Corner, Direction = Tile.TileDirection.NorthEast };
        }
        else if (nw == 2 && ne == 2 && sw == 2 && se == 4)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Corner, Direction = Tile.TileDirection.NorthWest };
        }
        else if (nw == 4 && ne == 2 && sw == 2 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Corner, Direction = Tile.TileDirection.SouthEast };
        }
        else if (nw == 2 && ne == 4 && sw == 2 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.Corner, Direction = Tile.TileDirection.SouthWest };
        }
        else if (nw == 4 && ne == 4 && sw == 2 && se == 4)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.InnerCorner, Direction = Tile.TileDirection.NorthEast };
        }
        else if (nw == 4 && ne == 4 && sw == 4 && se == 2)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.InnerCorner, Direction = Tile.TileDirection.NorthWest };
        }
        else if (nw == 2 && ne == 4 && sw == 4 && se == 4)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.InnerCorner, Direction = Tile.TileDirection.SouthEast };
        }
        else if (nw == 4 && ne == 2 && sw == 4 && se == 4)
        {
            roadTile = new PlannedRoadTile { Layer = 3, TileType = Tile.TileType.InnerCorner, Direction = Tile.TileDirection.SouthWest };
        }

        if (roadTile != null)
        {
            roadTile.Corners = new Tile.CornerSockets { NorthWest = nw, NorthEast = ne, SouthWest = sw, SouthEast = se };
            return true;
        }

        return false;
    }

    #endregion

    #region 3. Alocação de Estruturas em Quarteirões (City Blocks)

    private void AllocateBlockSettlement(
        IslandSettlementPlan plan,
        List<int> avenueXs,
        List<int> streetYs,
        int islandSeed,
        HashSet<Vector2Int> occupiedTiles)
    {
        if (plan.RoadTiles.Count == 0) return;

        List<StructureData> landBlueprints = _structuresList
            .Where(s => s.Category != StructureCategory.Harbor)
            .ToList();

        if (landBlueprints.Count == 0) return;
        if (streetYs == null || streetYs.Count == 0) return;

        foreach (int streetY in streetYs)
        {
            int lotY = streetY + 2;

            var roadXsOnThisStreet = plan.RoadTiles.Keys
                .Where(k => k.y == streetY + 1)
                .Select(k => k.x)
                .ToList();

            if (roadXsOnThisStreet.Count == 0) continue;

            int minStreetX = roadXsOnThisStreet.Min();
            int maxStreetX = roadXsOnThisStreet.Max();

            var blockIntervals = new List<(int startX, int endX)>();
            int? currentIntervalStart = null;

            for (int x = minStreetX; x <= maxStreetX; x++)
            {
                Vector2Int lotPos = new Vector2Int(x, lotY);
                Vector2Int roadBelowPos = new Vector2Int(x, streetY + 1);

                bool isValidLotTile = plan.RoadTiles.ContainsKey(roadBelowPos) &&
                                      !plan.RoadTiles.ContainsKey(lotPos) &&
                                      !occupiedTiles.Contains(lotPos) &&
                                      IsCellLand(lotPos.x, lotPos.y);

                if (isValidLotTile)
                {
                    if (!currentIntervalStart.HasValue)
                        currentIntervalStart = x;
                }
                else
                {
                    if (currentIntervalStart.HasValue)
                    {
                        blockIntervals.Add((currentIntervalStart.Value, x - 1));
                        currentIntervalStart = null;
                    }
                }
            }
            if (currentIntervalStart.HasValue)
            {
                blockIntervals.Add((currentIntervalStart.Value, maxStreetX));
            }

            foreach (var (startX, endX) in blockIntervals)
            {
                int blockWidth = endX - startX + 1;
                if (blockWidth < 2) continue;

                int currentX = startX;
                while (currentX <= endX)
                {
                    int remainingWidth = endX - currentX + 1;
                    if (remainingWidth < 2) break;

                    Vector2Int availableSpace = MeasureAvailableLotSpace(
                        currentX,
                        lotY,
                        occupiedTiles,
                        plan.RoadTiles,
                        maxTestWidth: Mathf.Min(remainingWidth, 6),
                        maxTestHeight: 4);

                    var fittingCandidates = landBlueprints
                        .Where(b => b.StructureDimensions.x <= availableSpace.x && b.StructureDimensions.y <= availableSpace.y)
                        .ToList();

                    fittingCandidates = fittingCandidates.Where(b =>
                    {
                        Vector2Int doorTile = new Vector2Int(currentX + b.DoorLocalCoordinate.x, lotY + b.DoorLocalCoordinate.y);
                        Vector2Int frontTile = doorTile + Vector2Int.down;
                        return plan.RoadTiles.ContainsKey(frontTile);
                    }).ToList();

                    if (fittingCandidates.Count > 0)
                    {
                        int blockSeed = HashBlockSeed(islandSeed, currentX, lotY);
                        System.Random blockPrng = new System.Random(blockSeed);

                        StructureData chosen = SelectWeightedBlueprint(fittingCandidates, blockPrng, emptyWeight: 0.2f);

                        if ((object)chosen != null)
                        {
                            Vector2Int origin = new Vector2Int(currentX, lotY);
                            Vector2Int dims = chosen.StructureDimensions;
                            Vector2Int chunkPos = GetChunkPosition(origin);

                            var structure = new PlannedStructure
                            {
                                Blueprint = chosen,
                                GlobalTileOrigin = origin,
                                Dimensions = dims,
                                ChunkPosition = chunkPos,
                                DoorGlobalCoordinate = origin + chosen.DoorLocalCoordinate
                            };

                            plan.StructuresList.Add(structure);

                            for (int ox = 0; ox < dims.x; ox++)
                                for (int oy = 0; oy < dims.y; oy++)
                                    occupiedTiles.Add(new Vector2Int(origin.x + ox, origin.y + oy));

                            currentX += dims.x + 1;
                        }
                        else
                        {
                            currentX += 2;
                        }
                    }
                    else
                    {
                        currentX += 1;
                    }
                }
            }
        }
    }

    private Vector2Int MeasureAvailableLotSpace(
        int startX,
        int startY,
        HashSet<Vector2Int> occupiedTiles,
        Dictionary<Vector2Int, PlannedRoadTile> roadTiles,
        int maxTestWidth = 5,
        int maxTestHeight = 4)
    {
        int maxW = 0;
        int maxH = maxTestHeight;

        for (int w = 1; w <= maxTestWidth; w++)
        {
            int testX = startX + w - 1;
            int heightForThisCol = 0;

            for (int h = 1; h <= maxH; h++)
            {
                int testY = startY + h - 1;
                Vector2Int tile = new Vector2Int(testX, testY);

                if (!IsCellLand(tile.x, tile.y))
                    break;

                if (occupiedTiles.Contains(tile) || roadTiles.ContainsKey(tile))
                    break;

                heightForThisCol = h;
            }

            if (heightForThisCol < 2)
                break;

            maxH = Mathf.Min(maxH, heightForThisCol);
            maxW = w;
        }

        return new Vector2Int(maxW, maxH);
    }

    private static int HashBlockSeed(int islandSeed, int blockX, int blockY)
    {
        uint hash = (uint)islandSeed * 2654435761u;
        hash ^= (uint)(blockX * 1664525 + 1013904223);
        hash ^= (uint)(blockY * 22695477 + 1664525);
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;
        return (int)(hash & 0x7FFFFFFF);
    }

    public static StructureData SelectWeightedBlueprint(List<StructureData> candidates, System.Random prng, float emptyWeight = 1.0f)
    {
        float totalWeight = candidates.Sum(c => c.Weight) + emptyWeight;
        double roll = prng.NextDouble() * totalWeight;

        float accumulated = 0f;
        foreach (var candidate in candidates)
        {
            accumulated += candidate.Weight;
            if (roll <= accumulated)
                return candidate;
        }

        return null;
    }

    #endregion

    #region 4. Helpers e Particionamento

    private void IndexPlanByChunks(IslandSettlementPlan plan)
    {
        foreach (var structure in plan.StructuresList)
        {
            Vector2Int chunkPos = structure.ChunkPosition;
            if (!plan.StructuresByChunk.TryGetValue(chunkPos, out var list))
            {
                list = new List<PlannedStructure>();
                plan.StructuresByChunk[chunkPos] = list;
            }
            list.Add(structure);
        }

        foreach (var roadTile in plan.RoadTiles.Values)
        {
            Vector2Int chunkPos = GetChunkPosition(roadTile.Coordinate);
            if (!plan.RoadTilesByChunk.TryGetValue(chunkPos, out var list))
            {
                list = new List<PlannedRoadTile>();
                plan.RoadTilesByChunk[chunkPos] = list;
            }
            list.Add(roadTile);
        }
    }

    public Vector2Int GetChunkPosition(Vector2Int globalTile)
    {
        int cx = Mathf.FloorToInt((float)globalTile.x / _chunkSize.x);
        int cy = Mathf.FloorToInt((float)globalTile.y / _chunkSize.y);
        return new Vector2Int(cx, cy);
    }

    private static int HashIslandSeed(int worldSeed, int chunkX, int chunkY)
    {
        uint hash = (uint)worldSeed * 2654435761u;
        hash ^= (uint)(chunkX * 1664525 + 1013904223);
        hash ^= (uint)(chunkY * 22695477 + 1664525);
        hash ^= hash >> 16;
        hash *= 0x45d9f3b;
        hash ^= hash >> 16;
        return (int)(hash & 0x7FFFFFFF);
    }

    #endregion
}
