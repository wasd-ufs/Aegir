using UnityEngine;

/// <summary>
/// Controla o comportamento da câmera do jogo, fazendo-a seguir o jogador ativo (barco ou capitão)
/// com uma interpolação suave (Lerp) e uma antecipação de movimento visual baseada na velocidade atual.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    #region Referências e Configurações
    [Header("Referências")]
    [Tooltip("Referência ao gerador do mundo para acessar qual é o jogador atual no momento da execução.")]
    [SerializeField] private WorldGenerator _worldGenerator;

    [Header("Parâmetros de Câmera")]
    [Tooltip("Velocidade de interpolação da câmera. Valores menores resultam em um movimento mais suave/atrasado.")]
    [SerializeField] private float _smoothSpeed = 0.125f;
    
    [Tooltip("Distância base da câmera em relação ao alvo. O eixo Z deve ser negativo para visualização 2D.")]
    [SerializeField] private Vector3 _offset = new Vector3(0, 0, -10);
    
    [Tooltip("Multiplicador da força de antecipação. Define o quão longe a câmera 'olha à frente' baseado na velocidade.")]
    [SerializeField] private float _movementLookAheadMultiplier;
    
    [Tooltip("Taxa de amortecimento (Lerp) aplicada à leitura da velocidade, para evitar solavancos na câmera.")]
    [SerializeField] private float _movementDamping = 0.05f;
    #endregion

    #region Estado Interno
    private Transform _currentTarget;
    private Rigidbody2D _currentRigidBody;
    private Vector2 _smoothedVelocity;
    #endregion

    #region Ciclo de Vida (Unity)
    /// <summary>
    /// Utiliza o FixedUpdate para sincronizar a atualização da câmera com o motor de física.
    /// Como o barco e o capitão se movem através do Rigidbody2D (física), mover a câmera no FixedUpdate
    /// evita problemas de 'stuttering' (tremedeira visual).
    /// </summary>
    void FixedUpdate()
    {
        // Só tenta seguir se o jogador existir no mundo
        if (_worldGenerator.player != null)
        {
            Transform playerTransform = _worldGenerator.player;
            
            // Verifica se o jogador alvo mudou (ex: ocorreu a transição Barco <-> Capitão)
            // Atualiza o cache do alvo e seu Rigidbody2D respectivo
            if (playerTransform != _currentTarget)
            {
                _currentTarget = playerTransform;
                _currentRigidBody = playerTransform.GetComponent<Rigidbody2D>();
            }

            // Suaviza a velocidade lida do jogador atual.
            // Isso evita que mudanças abruptas de direção façam a câmera pular de forma instantânea.
            _smoothedVelocity = Vector2.Lerp(_smoothedVelocity, _currentRigidBody.linearVelocity, _movementDamping);

            // Posição Desejada = Posição Real do Jogador + Recuo Z (Offset) + Projeção de Velocidade (Antecipação)
            Vector3 desiredPosition = playerTransform.position + _offset + new Vector3(_smoothedVelocity.x, _smoothedVelocity.y, 0) * _movementLookAheadMultiplier;
            
            // Interpolação linear da posição atual da câmera até a posição desejada para criar o movimento fluido
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed);
            transform.position = smoothedPosition;
        }
    }
    #endregion
}