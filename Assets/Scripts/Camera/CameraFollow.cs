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
    public WorldGenerator worldGenerator;

    [Header("Parâmetros de Câmera")]
    [Tooltip("Velocidade de interpolação da câmera. Valores menores resultam em um movimento mais suave/atrasado.")]
    public float smoothSpeed = 0.125f;
    
    [Tooltip("Distância base da câmera em relação ao alvo. O eixo Z deve ser negativo para visualização 2D.")]
    public Vector3 offset = new Vector3(0, 0, -10);
    
    [Tooltip("Multiplicador da força de antecipação. Define o quão longe a câmera 'olha à frente' baseado na velocidade.")]
    public float multiplicador;
    
    [Tooltip("Taxa de amortecimento (Lerp) aplicada à leitura da velocidade, para evitar solavancos na câmera.")]
    public float amortecimentoDaMecânica = 0.05f;
    #endregion

    #region Estado Interno
    private Transform alvoAtual;
    private Rigidbody2D rbAtual;
    private Vector2 velocidadeSuavizada;
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
        if (worldGenerator.player != null)
        {
            Transform playerT = worldGenerator.player;
            
            // Verifica se o jogador alvo mudou (ex: ocorreu a transição Barco <-> Capitão)
            // Atualiza o cache do alvo e seu Rigidbody2D respectivo
            if (playerT != alvoAtual)
            {
                alvoAtual = playerT;
                rbAtual = playerT.GetComponent<Rigidbody2D>();
            }

            // Suaviza a velocidade lida do jogador atual.
            // Isso evita que mudanças abruptas de direção façam a câmera pular de forma instantânea.
            velocidadeSuavizada = Vector2.Lerp(velocidadeSuavizada, rbAtual.linearVelocity, amortecimentoDaMecânica);

            // Posição Desejada = Posição Real do Jogador + Recuo Z (Offset) + Projeção de Velocidade (Antecipação)
            Vector3 desiredPosition = playerT.position + offset + new Vector3(velocidadeSuavizada.x, velocidadeSuavizada.y, 0) * multiplicador;
            
            // Interpolação linear da posição atual da câmera até a posição desejada para criar o movimento fluido
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;
        }
    }
    #endregion
}