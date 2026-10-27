using UnityEngine;

namespace Aegir.Entities.V2.NPCs
{
    /// <summary>
    /// Subsistema de física, locomoção e contenção territorial para entidades e NPCs no mapa V2.
    /// Impede que peixes/monstros marinhos encalhem em terra e que animais terrestres andem na água,
    /// aplicando deflexão suave ao se aproximar de fronteiras.
    /// </summary>
    public class NpcLocomotionV2
    {
        private readonly Rigidbody2D _rb;
        private readonly Transform _transform;
        private readonly bool _isMaritime;
        private Vector3 _lastValidPosition;
        private Vector2Int _currentChunk;
        private const float BoundaryProbeDistance = 0.45f;

        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public Vector3 LastValidPosition => _lastValidPosition;

        public NpcLocomotionV2(Rigidbody2D rb, Transform transform, bool isMaritime)
        {
            _rb = rb;
            _transform = transform;
            _isMaritime = isMaritime;
            _lastValidPosition = transform.position;
        }

        public void Initialize(IWorldGenerator worldGenerator)
        {
            _lastValidPosition = _transform.position;
            if (worldGenerator != null)
            {
                _currentChunk = worldGenerator.GetChunkPosFromWorld(_transform.position);
            }
        }

        public void Stop()
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Aplica aceleração suave em direção ao vetor desejado, com deflexão antecipada de bordas.
        /// </summary>
        public void SteerTowards(Vector2 desiredDirection, float targetSpeed, IWorldGenerator worldGen)
        {
            if (_rb == null) return;

            Vector2 moveDir = desiredDirection.sqrMagnitude > 1f ? desiredDirection.normalized : desiredDirection;

            // 1. Deflexão antecipada de fronteira territorial
            if (worldGen != null && moveDir.sqrMagnitude > 0.01f)
            {
                Vector3 probePos = _transform.position + (Vector3)(moveDir * BoundaryProbeDistance);
                Tile probeTile = worldGen.GetTileAtWorldPosition(probePos);

                if (probeTile != null)
                {
                    bool isForbidden = _isMaritime ? probeTile.Metadata.Layer != 0 : probeTile.Metadata.Layer == 0;
                    if (isForbidden)
                    {
                        // Testa tangentes perpendiculares para contornar a borda suavemente
                        Vector2 tangent1 = new Vector2(-moveDir.y, moveDir.x);
                        Vector2 tangent2 = new Vector2(moveDir.y, -moveDir.x);

                        Tile t1 = worldGen.GetTileAtWorldPosition(_transform.position + (Vector3)(tangent1 * BoundaryProbeDistance));
                        Tile t2 = worldGen.GetTileAtWorldPosition(_transform.position + (Vector3)(tangent2 * BoundaryProbeDistance));

                        bool t1Valid = t1 != null && (_isMaritime ? t1.Metadata.Layer == 0 : t1.Metadata.Layer != 0);
                        bool t2Valid = t2 != null && (_isMaritime ? t2.Metadata.Layer == 0 : t2.Metadata.Layer != 0);

                        if (t1Valid) moveDir = tangent1;
                        else if (t2Valid) moveDir = tangent2;
                        else moveDir = -moveDir * 0.5f; // Beco sem saída, recua
                    }
                }
            }

            Vector2 targetVelocity = moveDir * targetSpeed;
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 4f);
        }

        /// <summary>
        /// Valida se a entidade tocou um tile proibido. Se sim, restaura a última posição válida
        /// e calcula uma direção de escape.
        /// </summary>
        public bool ValidateBoundaries(IWorldGenerator worldGen, out Vector2 escapeDirection)
        {
            escapeDirection = Vector2.zero;
            if (worldGen == null) return false;

            Tile currentTile = worldGen.GetTileAtWorldPosition(_transform.position);
            if (currentTile == null) return false;

            bool isForbidden = _isMaritime
                ? currentTile.Metadata.Layer != 0
                : currentTile.Metadata.Layer == 0;

            if (isForbidden)
            {
                Stop();
                _transform.position = _lastValidPosition;
                escapeDirection = ComputeEscapeDirection(worldGen);
                return true;
            }

            _lastValidPosition = _transform.position;
            return false;
        }

        private Vector2 ComputeEscapeDirection(IWorldGenerator worldGen)
        {
            Vector2[] candidates = {
                Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
                new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized
            };

            foreach (var dir in candidates)
            {
                Vector3 probe = _lastValidPosition + (Vector3)(dir * BoundaryProbeDistance);
                Tile tile = worldGen.GetTileAtWorldPosition(probe);
                if (tile == null) continue;

                bool isValid = _isMaritime ? tile.Metadata.Layer == 0 : tile.Metadata.Layer != 0;
                if (isValid) return dir;
            }

            return new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        }

        /// <summary>
        /// Verifica se o chunk atual saiu do campo de visão ativo do mundo V2.
        /// </summary>
        public bool CheckChunkDespawn(IWorldGenerator worldGen, GameObject hostObject)
        {
            if (worldGen == null || hostObject == null) return false;

            Vector2Int newChunk = worldGen.GetChunkPosFromWorld(_transform.position);
            if (newChunk != _currentChunk)
            {
                _currentChunk = newChunk;
            }

            if (!worldGen.IsChunkActive(_currentChunk))
            {
                Object.Destroy(hostObject);
                return true;
            }

            return false;
        }
    }
}
