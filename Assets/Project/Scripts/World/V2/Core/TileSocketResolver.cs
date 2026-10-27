using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aegir.World.V2.Core
{
    /// <summary>
    /// Motor topológico de sockets e vértices.
    /// Garante que quinas externas (Corner), quinas internas (InnerCorner), costas (Coast)
    /// e blocos sólidos (Block) tenham sockets 100% matematicamente corretos em runtime.
    /// </summary>
    public static class TileSocketResolver
    {
        /// <summary>
        /// Obtém ou gera os CornerSockets canônicos para um Tile em runtime.
        /// Resolve o problema onde tile.Metadata.Corners ficava zerado fora do Editor.
        /// </summary>
        public static Tile.CornerSockets GetSockets(Tile tile)
        {
            if (tile == null) return default;

            var meta = tile.Metadata;
            var corners = meta.Corners;

            // Se os sockets já estiverem configurados e válidos, use-os
            if (corners.NorthWest != 0 || corners.NorthEast != 0 || corners.SouthWest != 0 || corners.SouthEast != 0)
            {
                return corners;
            }

            // Para bloco da camada 0 (água), todos os 4 cantos são legitimamente 0
            if (meta.Layer == 0 && meta.Type == Tile.TileType.Block)
            {
                return new Tile.CornerSockets { NorthWest = 0, NorthEast = 0, SouthWest = 0, SouthEast = 0 };
            }

            // Gera matematicamente os sockets a partir de (Layer, Type, Direction)
            return GenerateCorners(meta.Layer, meta.Type, meta.Direction);
        }

        public static Tile.CornerSockets GenerateCorners(int baseLayer, Tile.TileType type, Tile.TileDirection direction)
        {
            if (type == Tile.TileType.Block)
            {
                return new Tile.CornerSockets
                {
                    NorthWest = baseLayer,
                    NorthEast = baseLayer,
                    SouthWest = baseLayer,
                    SouthEast = baseLayer
                };
            }

            int lowerLayer = baseLayer - 1;
            int upperLayer = baseLayer + 1;

            return (type, direction) switch
            {
                (Tile.TileType.Coast, Tile.TileDirection.North) => new Tile.CornerSockets { NorthWest = lowerLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = upperLayer },
                (Tile.TileType.Coast, Tile.TileDirection.South) => new Tile.CornerSockets { NorthWest = upperLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = lowerLayer },
                (Tile.TileType.Coast, Tile.TileDirection.East)  => new Tile.CornerSockets { NorthWest = upperLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = lowerLayer },
                (Tile.TileType.Coast, Tile.TileDirection.West)  => new Tile.CornerSockets { NorthWest = lowerLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = upperLayer },

                (Tile.TileType.Corner, Tile.TileDirection.NorthEast) => new Tile.CornerSockets { NorthWest = lowerLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = lowerLayer },
                (Tile.TileType.Corner, Tile.TileDirection.NorthWest) => new Tile.CornerSockets { NorthWest = lowerLayer, NorthEast = lowerLayer, SouthWest = lowerLayer, SouthEast = upperLayer },
                (Tile.TileType.Corner, Tile.TileDirection.SouthEast) => new Tile.CornerSockets { NorthWest = upperLayer, NorthEast = lowerLayer, SouthWest = lowerLayer, SouthEast = lowerLayer },
                (Tile.TileType.Corner, Tile.TileDirection.SouthWest) => new Tile.CornerSockets { NorthWest = lowerLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = lowerLayer },

                (Tile.TileType.InnerCorner, Tile.TileDirection.NorthEast) => new Tile.CornerSockets { NorthWest = upperLayer, NorthEast = upperLayer, SouthWest = lowerLayer, SouthEast = upperLayer },
                (Tile.TileType.InnerCorner, Tile.TileDirection.NorthWest) => new Tile.CornerSockets { NorthWest = upperLayer, NorthEast = upperLayer, SouthWest = upperLayer, SouthEast = lowerLayer },
                (Tile.TileType.InnerCorner, Tile.TileDirection.SouthEast) => new Tile.CornerSockets { NorthWest = lowerLayer, NorthEast = upperLayer, SouthWest = upperLayer, SouthEast = upperLayer },
                (Tile.TileType.InnerCorner, Tile.TileDirection.SouthWest) => new Tile.CornerSockets { NorthWest = upperLayer, NorthEast = lowerLayer, SouthWest = upperLayer, SouthEast = upperLayer },

                _ => new Tile.CornerSockets { NorthWest = baseLayer, NorthEast = baseLayer, SouthWest = baseLayer, SouthEast = baseLayer }
            };
        }

        public static bool AreSocketsCompatible(Tile.CornerSockets a, Tile.CornerSockets b, Vector2Int offsetDirection)
        {
            if (offsetDirection == Vector2Int.right)
                return a.NorthEast == b.NorthWest && a.SouthEast == b.SouthWest;

            if (offsetDirection == Vector2Int.left)
                return a.NorthWest == b.NorthEast && a.SouthWest == b.SouthEast;

            if (offsetDirection == Vector2Int.up)
                return a.NorthWest == b.SouthWest && a.NorthEast == b.SouthEast;

            if (offsetDirection == Vector2Int.down)
                return a.SouthWest == b.NorthWest && a.SouthEast == b.NorthEast;

            return false;
        }

        public static bool AreTilesCompatible(Tile a, Tile b, Vector2Int offsetDirection, RuleManager ruleManager = null)
        {
            if (a == null || b == null) return false;

            if (ruleManager != null && ruleManager.IsBlocked(a, b, offsetDirection))
            {
                return false;
            }

            Tile.CornerSockets socketsA = GetSockets(a);
            Tile.CornerSockets socketsB = GetSockets(b);

            return AreSocketsCompatible(socketsA, socketsB, offsetDirection);
        }

        /// <summary>
        /// Dado 4 sockets de vértice (NW, NE, SW, SE), resolve o tipo morfológico exato (Block, Coast, Corner, InnerCorner).
        /// </summary>
        public static bool ResolveMorphology(int nw, int ne, int sw, int se, out int layer, out Tile.TileType type, out Tile.TileDirection direction)
        {
            layer = 0;
            type = Tile.TileType.Block;
            direction = Tile.TileDirection.None;

            if (nw == ne && ne == sw && sw == se)
            {
                layer = nw;
                type = Tile.TileType.Block;
                direction = Tile.TileDirection.None;
                return true;
            }

            int min = Math.Min(Math.Min(nw, ne), Math.Min(sw, se));
            int max = Math.Max(Math.Max(nw, ne), Math.Max(sw, se));

            // Transição entre min e max (deve haver 1 nível de transição no meio: layer = min + 1)
            layer = min + 1;

            int countMax = (nw == max ? 1 : 0) + (ne == max ? 1 : 0) + (sw == max ? 1 : 0) + (se == max ? 1 : 0);

            if (countMax == 2)
            {
                // Costas
                type = Tile.TileType.Coast;
                if (nw == max && ne == max && sw == min && se == min) { direction = Tile.TileDirection.South; return true; }
                if (nw == min && ne == min && sw == max && se == max) { direction = Tile.TileDirection.North; return true; }
                if (nw == max && ne == min && sw == max && se == min) { direction = Tile.TileDirection.East; return true; }
                if (nw == min && ne == max && sw == min && se == max) { direction = Tile.TileDirection.West; return true; }

                // Diagonal saddle point resolution
                if (nw == max && se == max) { direction = Tile.TileDirection.South; return true; }
                if (ne == max && sw == max) { direction = Tile.TileDirection.North; return true; }
            }
            else if (countMax == 1)
            {
                // Quinas Externas (Corner / SL Quintet)
                type = Tile.TileType.Corner;
                if (sw == max) { direction = Tile.TileDirection.NorthEast; return true; }
                if (se == max) { direction = Tile.TileDirection.NorthWest; return true; }
                if (nw == max) { direction = Tile.TileDirection.SouthEast; return true; }
                if (ne == max) { direction = Tile.TileDirection.SouthWest; return true; }
            }
            else if (countMax == 3)
            {
                // Quinas Internas (InnerCorner / SL Intern)
                type = Tile.TileType.InnerCorner;
                if (sw == min) { direction = Tile.TileDirection.NorthEast; return true; }
                if (se == min) { direction = Tile.TileDirection.NorthWest; return true; }
                if (nw == min) { direction = Tile.TileDirection.SouthEast; return true; }
                if (ne == min) { direction = Tile.TileDirection.SouthWest; return true; }
            }

            return false;
        }

        public static Tile FindTile(TilesetData tileset, int layer, Tile.TileType type, Tile.TileDirection direction)
        {
            if (tileset == null || tileset.TilesetList == null) return null;

            return tileset.TilesetList.Find(t =>
                t != null &&
                t.Metadata.Layer == layer &&
                t.Metadata.Type == type &&
                (type == Tile.TileType.Block || t.Metadata.Direction == direction)
            );
        }

        public static Tile ResolveFromSockets(TilesetData tileset, int nw, int ne, int sw, int se)
        {
            if (ResolveMorphology(nw, ne, sw, se, out int layer, out Tile.TileType type, out Tile.TileDirection direction))
            {
                return FindTile(tileset, layer, type, direction);
            }
            return null;
        }

        public static bool HasCompatibilityError(Tile currentTile, Tile neighbor, Vector2Int dir)
        {
            if (currentTile == null || neighbor == null) return false;
            return !AreSocketsCompatible(GetSockets(currentTile), GetSockets(neighbor), dir);
        }

        public static Tile FindCompatibleTile(
            Tile currentTile,
            IEnumerable<(Vector2Int dir, Tile neighbor)> neighbors,
            TilesetData tilesetData,
            bool allowRelaxed = true)
        {
            if (tilesetData == null || tilesetData.TilesetList == null || tilesetData.TilesetList.Count == 0)
                return null;

            var neighborList = neighbors as IList<(Vector2Int dir, Tile neighbor)> ?? System.Linq.Enumerable.ToList(neighbors);

            Tile bestCandidate = null;
            float bestScore = float.MinValue;

            foreach (Tile candidate in tilesetData.TilesetList)
            {
                if (candidate == null) continue;

                bool compatible = true;
                foreach (var (dir, neighbor) in neighborList)
                {
                    if (neighbor != null && HasCompatibilityError(candidate, neighbor, dir))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible) continue;

                float score = 0f;
                if (currentTile != null)
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
                if (candidate == null) continue;

                float score = 0f;
                bool hasFatalMismatch = false;

                foreach (var (dir, neighbor) in neighborList)
                {
                    if (neighbor == null) continue;

                    var (c1, c2) = GetCandidateEdgeSockets(GetSockets(candidate), dir);
                    var (n1, n2) = GetNeighborEdgeSockets(GetSockets(neighbor), dir);

                    if (neighbor.Metadata.Layer == 0 && (candidate.Metadata.Layer >= 2 || c1 != 0 || c2 != 0))
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

                if (currentTile != null)
                {
                    int layerDiff = Math.Abs(candidate.Metadata.Layer - currentTile.Metadata.Layer);
                    score += (10 - layerDiff) * 20f;
                    if (candidate.Metadata.Type == currentTile.Metadata.Type) score += 30f;
                    if (ReferenceEquals(candidate, currentTile)) score += 50f;
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
    }
}
