using UnityEngine;

namespace Aegir.Entities.V2.Player
{
    /// <summary>
    /// Motor de locomoção marítima do barco no mundo V2.
    /// Simula física hidrodinâmica com ondas, inércia, derivação do vento
    /// e deslizamento suave em contornos de ilhas e costas.
    /// </summary>
    public class BoatMotorV2 : IPlayerMotorV2
    {
        private readonly PlayerMovementV2 _player;
        private Vector3 _lastValidWaterPosition;
        private float _probeDistance = 0.5f;

        public BoatMotorV2(PlayerMovementV2 player)
        {
            _player = player;
        }

        public void Enter()
        {
            if (_player.rb != null)
            {
                _player.rb.linearVelocity = Vector2.zero;
            }
            _lastValidWaterPosition = _player.transform.position;
            _player.UpdateBoatAnimations(Vector2.zero, 0f);
        }

        public void UpdateMotor(Vector2 input)
        {
            // Atualiza animações em Update caso necessário
            if (_player.rb != null)
            {
                _player.UpdateBoatAnimations(_player.rb.linearVelocity, _player.rb.linearVelocity.sqrMagnitude);
            }
        }

        public void FixedUpdateMotor(Vector2 input)
        {
            if (_player.rb == null) return;

            Vector3 currentPos = _player.transform.position;
            var worldGen = _player.ActiveWorldGenerator;

            // 1. Verificação de limites imediatos no mapa
            if (worldGen != null)
            {
                Tile currentTile = worldGen.GetTileAtWorldPosition(currentPos);
                if (currentTile != null && currentTile.Metadata.Layer != 0) // Tocou terra/costa
                {
                    _player.rb.linearVelocity = Vector2.zero;
                    _player.transform.position = _lastValidWaterPosition;
                    return;
                }
                _lastValidWaterPosition = currentPos;
            }

            Vector2 moveDir = input.sqrMagnitude > 1f ? input.normalized : input;

            // 2. Simulação de Força das Ondas e Vento
            float waveX = Mathf.Sin(Time.fixedTime * _player.WaveFrequency) * _player.Amplitude * _player.WindDirX;
            float waveY = Mathf.Cos(Time.fixedTime * _player.WaveFrequency) * _player.Amplitude * _player.WindDirY;
            Vector2 waveForce = new Vector2(waveX, waveY);

            _player.rb.linearVelocity += waveForce * Time.fixedDeltaTime;

            // 3. Projeção antecipada de colisão costeira (deslizamento suave na margem)
            if (worldGen != null && moveDir.sqrMagnitude > 0.01f)
            {
                Vector3 probePos = currentPos + (Vector3)(moveDir * _probeDistance);
                Tile probeTile = worldGen.GetTileAtWorldPosition(probePos);

                if (probeTile != null && probeTile.Metadata.Layer != 0)
                {
                    // Se o movimento frontal atinge terra, testa projeções laterais para deslizar
                    Vector2 tangent1 = new Vector2(-moveDir.y, moveDir.x);
                    Vector2 tangent2 = new Vector2(moveDir.y, -moveDir.x);

                    Tile t1Tile = worldGen.GetTileAtWorldPosition(currentPos + (Vector3)(tangent1 * _probeDistance));
                    Tile t2Tile = worldGen.GetTileAtWorldPosition(currentPos + (Vector3)(tangent2 * _probeDistance));

                    if (t1Tile != null && t1Tile.Metadata.Layer == 0)
                    {
                        moveDir = tangent1 * 0.7f;
                    }
                    else if (t2Tile != null && t2Tile.Metadata.Layer == 0)
                    {
                        moveDir = tangent2 * 0.7f;
                    }
                    else
                    {
                        moveDir = Vector2.zero; // Encurralado de frente
                    }
                }
            }

            // 4. Aceleração hidrodinâmica e amortecimento de arrasto
            Vector2 targetVelocity = moveDir * _player.BoatSpeed;
            _player.rb.linearVelocity = Vector2.Lerp(
                _player.rb.linearVelocity,
                targetVelocity,
                Time.fixedDeltaTime * 2.5f
            );

            // 5. Atualização de animação
            _player.UpdateBoatAnimations(_player.rb.linearVelocity, _player.rb.linearVelocity.sqrMagnitude);
        }

        public void Exit()
        {
            if (_player.rb != null)
            {
                _player.rb.linearVelocity = Vector2.zero;
            }
        }
    }
}
