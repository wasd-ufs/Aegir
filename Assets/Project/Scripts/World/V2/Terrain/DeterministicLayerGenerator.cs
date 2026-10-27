using UnityEngine;
using Aegir.World.V2.Core;

namespace Aegir.World.V2.Terrain
{
    /// <summary>
    /// Gera mapas de camadas (TargetLayerMap) de forma 100% determinística a partir de coordenadas globais.
    /// Garante que chunks vizinhos tenham exatamente os mesmos valores de fronteira, eliminando descompassos de halo.
    /// </summary>
    public class DeterministicLayerGenerator
    {
        private readonly FastNoiseSampler _noiseSampler;
        private readonly Vector2Int _chunkSize;
        private readonly int _minBeachRadius;
        private readonly int _maxBeachRadius;
        private readonly float _beachNoiseScale;

        public DeterministicLayerGenerator(
            FastNoiseSampler noiseSampler,
            Vector2Int chunkSize,
            int minBeachRadius = 1,
            int maxBeachRadius = 3,
            float beachNoiseScale = 0.05f)
        {
            _noiseSampler = noiseSampler;
            _chunkSize = chunkSize;
            _minBeachRadius = minBeachRadius;
            _maxBeachRadius = maxBeachRadius;
            _beachNoiseScale = beachNoiseScale;
        }

        public int[,] GenerateLayerMap(Vector2Int chunkCoordinate, int worldSeed)
        {
            int gridW = _chunkSize.x + 2;
            int gridH = _chunkSize.y + 2;
            int padding = Mathf.Max(_maxBeachRadius + 1, 4);

            int solidW = gridW + padding * 2;
            int solidH = gridH + padding * 2;
            int[,] solidMap = new int[solidW, solidH];

            int baseGlobalX = chunkCoordinate.x * _chunkSize.x;
            int baseGlobalY = chunkCoordinate.y * _chunkSize.y;

            // 1. Gerar o mapa sólido
            for (int sx = 0; sx < solidW; sx++)
            {
                for (int sy = 0; sy < solidH; sy++)
                {
                    // Mapeia para coordenadas locais centradas no grid
                    int localX = sx - padding - 1;
                    int localY = sy - padding - 1;
                    int globalX = baseGlobalX + localX;
                    int globalY = baseGlobalY + localY;

                    solidMap[sx, sy] = CalculateSolidLayer(globalX, globalY, worldSeed);
                }
            }

            // 2. Gerar o mapa visual de transições (gridW x gridH)
            int[,] visualMap = new int[gridW, gridH];

            for (int x = 0; x < gridW; x++)
            {
                for (int y = 0; y < gridH; y++)
                {
                    int sx = x + padding;
                    int sy = y + padding;
                    int centerSolid = solidMap[sx, sy];

                    // Camada de água base (0) não transiciona para baixo
                    if (centerSolid <= 0)
                    {
                        visualMap[x, y] = centerSolid;
                        continue;
                    }

                    // Se qualquer um dos 8 vizinhos for 2 degraus abaixo, esta célula é borda de transição
                    bool isTransition = false;
                    for (int dx = -1; dx <= 1 && !isTransition; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int neighborSolid = solidMap[sx + dx, sy + dy];
                            if (neighborSolid == centerSolid - 2)
                            {
                                isTransition = true;
                                break;
                            }
                        }
                    }

                    visualMap[x, y] = isTransition ? (centerSolid - 1) : centerSolid;
                }
            }

            return visualMap;
        }

        public int CalculateSolidLayer(int globalX, int globalY, int worldSeed)
        {
            // Agrupamento em células pares para manter padrão topológico consistente
            int groupX = globalX & ~1;
            int groupY = globalY & ~1;

            if (!_noiseSampler.IsLand(groupX, groupY))
            {
                // Todas as águas do projeto pertencem à Camada 0 (Water.asset).
                // Não existem tiles de Mar Profundo (-2 ou -4) no tileset do jogo.
                return 0;
            }

            // É terra: verifica se é praia (Sand = 2) ou interior (Grass = 4)
            float beachNoise = Mathf.PerlinNoise(groupX * _beachNoiseScale + 17.3f, groupY * _beachNoiseScale + 43.7f);
            int beachWidth = Mathf.RoundToInt(Mathf.Lerp(_minBeachRadius, _maxBeachRadius, beachNoise));
            int searchRadius = Mathf.Max(1, Mathf.CeilToInt(beachWidth / 2f));

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!_noiseSampler.IsLand(groupX + dx * 2, groupY + dy * 2))
                    {
                        return 2; // Sand
                    }
                }
            }

            return 4; // Grass (Terra Firme)
        }

        /// <summary>
        /// Resolve os tiles de terreno da chunk inteira através de autotiling dual-grid de vértices.
        /// Quinas externas (Corner), quinas internas (InnerCorner), costas (Coast) e blocos (Block)
        /// emergem com 100% de precisão matemática e zero discrepância com chunks vizinhos.
        /// </summary>
        public Tile[,] GenerateTerrainTiles(Vector2Int chunkCoordinate, int worldSeed, TilesetData tileset)
        {
            Tile[,] tiles = new Tile[_chunkSize.x, _chunkSize.y];
            int baseGX = chunkCoordinate.x * _chunkSize.x;
            int baseGY = chunkCoordinate.y * _chunkSize.y;

            for (int x = 0; x < _chunkSize.x; x++)
            {
                for (int y = 0; y < _chunkSize.y; y++)
                {
                    int gx = baseGX + x;
                    int gy = baseGY + y;

                    int sw = CalculateSolidLayer(gx, gy, worldSeed);
                    int se = CalculateSolidLayer(gx + 1, gy, worldSeed);
                    int nw = CalculateSolidLayer(gx, gy + 1, worldSeed);
                    int ne = CalculateSolidLayer(gx + 1, gy + 1, worldSeed);

                    Tile tile = TileSocketResolver.ResolveFromSockets(tileset, nw, ne, sw, se);
                    if (tile == null)
                    {
                        int fallbackLayer = (sw == se && se == nw) ? sw : (sw == se ? sw : ne);
                        tile = TileSocketResolver.FindTile(tileset, fallbackLayer, Tile.TileType.Block, Tile.TileDirection.None);
                    }

                    tiles[x, y] = tile;
                }
            }

            return tiles;
        }
    }
}
