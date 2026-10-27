using UnityEngine;

namespace Aegir.Entities.V2.Player
{
    /// <summary>
    /// Motor de locomoção terrestre do capitão explorando ilhas a pé no mundo V2.
    /// Garante movimentação responsiva sobre ruas, avenidas, terra e praias,
    /// evitando travamento e impedindo avanço em águas profundas.
    /// </summary>
    public class CaptainMotorV2 : IPlayerMotorV2
    {
        private readonly PlayerMovementV2 _player;
        private Vector3 _lastValidLandPosition;
        private float _probeDistance = 0.35f;

        public CaptainMotorV2(PlayerMovementV2 player)
        {
            _player = player;
        }

        public void Enter()
        {
            if (_player.crb != null)
            {
                _player.crb.linearVelocity = Vector2.zero;
            }
            if (_player.rb != null)
            {
                _player.rb.linearVelocity = Vector2.zero;
            }
            if (_player.captain != null)
            {
                _lastValidLandPosition = _player.captain.transform.position;
            }
            _player.UpdateCaptainAnimations(Vector2.zero, 0f);
        }

        public void UpdateMotor(Vector2 input)
        {
            if (_player.crb != null)
            {
                _player.UpdateCaptainAnimations(input, _player.crb.linearVelocity.sqrMagnitude);
            }
        }

        public void FixedUpdateMotor(Vector2 input)
        {
            if (_player.crb == null || _player.captain == null) return;

            Vector3 currentPos = _player.captain.transform.position;
            var worldGen = _player.ActiveWorldGenerator;

            // 1. Verificação de limites em terra firme
            if (worldGen != null)
            {
                Tile currentTile = worldGen.GetTileAtWorldPosition(currentPos);
                if (currentTile != null && currentTile.Metadata.Layer == 0) // Pisou na água
                {
                    _player.crb.linearVelocity = Vector2.zero;
                    _player.captain.transform.position = _lastValidLandPosition;
                    return;
                }
                _lastValidLandPosition = currentPos;
            }

            Vector2 moveDir = input.sqrMagnitude > 1f ? input.normalized : input;

            // 2. Projeção antecipada na borda da água para evitar cair no mar
            if (worldGen != null && moveDir.sqrMagnitude > 0.01f)
            {
                Vector3 probePos = currentPos + (Vector3)(moveDir * _probeDistance);
                Tile probeTile = worldGen.GetTileAtWorldPosition(probePos);

                if (probeTile != null && probeTile.Metadata.Layer == 0)
                {
                    // Tenta deslizar ao longo da praia
                    Vector2 tangent1 = new Vector2(-moveDir.y, moveDir.x);
                    Vector2 tangent2 = new Vector2(moveDir.y, -moveDir.x);

                    Tile t1Tile = worldGen.GetTileAtWorldPosition(currentPos + (Vector3)(tangent1 * _probeDistance));
                    Tile t2Tile = worldGen.GetTileAtWorldPosition(currentPos + (Vector3)(tangent2 * _probeDistance));

                    if (t1Tile != null && t1Tile.Metadata.Layer != 0)
                    {
                        moveDir = tangent1 * 0.8f;
                    }
                    else if (t2Tile != null && t2Tile.Metadata.Layer != 0)
                    {
                        moveDir = tangent2 * 0.8f;
                    }
                    else
                    {
                        moveDir = Vector2.zero; // Não pode avançar na água
                    }
                }
            }

            // 3. Aplicação de velocidade linear ao capitão
            Vector2 targetVelocity = moveDir * _player.CaptainSpeed;
            _player.crb.linearVelocity = Vector2.Lerp(
                _player.crb.linearVelocity,
                targetVelocity,
                Time.fixedDeltaTime * 15f
            );

            // Garante que o barco continue ancorado/imóvel enquanto o capitão está a pé
            if (_player.rb != null)
            {
                _player.rb.linearVelocity = Vector2.zero;
            }

            // 4. Atualização de animação
            _player.UpdateCaptainAnimations(input, _player.crb.linearVelocity.sqrMagnitude);
        }

        public void Exit()
        {
            if (_player.crb != null)
            {
                _player.crb.linearVelocity = Vector2.zero;
            }
        }
    }
}
