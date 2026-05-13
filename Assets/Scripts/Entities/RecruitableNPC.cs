using UnityEngine;

/// <summary>
/// Define um NPC que pode ser contatado e recrutado na equipe do jogador.
/// Fica responsável por detectar a proximidade do jogador e aguardar pelo Input de Contrato.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class RecruitableNPC : MonoBehaviour
{
    #region Configurações de Interação
    [Header("Configurações")]
    public float raioDeInteração;
    public Transform barco;
    #endregion

    #region Estado Interno e Componentes
    private bool isPlayerNearby = false;
    private PlayerInputActions inputActions;
    private Rigidbody2D rb;
    private CircleCollider2D circleCollider2D;
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        inputActions = new();
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 0;
        
        circleCollider2D = GetComponent<CircleCollider2D>();
        circleCollider2D.radius = raioDeInteração;
        circleCollider2D.isTrigger = true;
    }
    
    void Update()
    {
        if (isPlayerNearby && inputActions.Player.Contatar.WasPressedThisFrame())
        {
            FindFirstObjectByType<RecruitmentUI>().OpenScreen(this, GetComponent<NPCsData>());
        }
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }
    #endregion

    #region Triggers de Colisão
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            isPlayerNearby = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
            isPlayerNearby = false;
    }
    #endregion
}