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
    [SerializeField] private float _interactionRadius;
    [SerializeField] private Transform _boatTransform;
    #endregion

    #region Estado Interno e Componentes
    private bool _isPlayerNearby = false;
    private PlayerInputActions _inputActions;
    private Rigidbody2D _rigidbody2D;
    private CircleCollider2D _circleCollider2D;
    #endregion

    #region Ciclo de Vida (Unity)
    private void Awake()
    {
        _inputActions = new();

        _rigidbody2D = GetComponent<Rigidbody2D>();
        _rigidbody2D.freezeRotation = true;
        _rigidbody2D.gravityScale = 0;

        _circleCollider2D = GetComponent<CircleCollider2D>();
        _circleCollider2D.radius = _interactionRadius;
        _circleCollider2D.isTrigger = true;
    }

    private void Update()
    {
        if (_isPlayerNearby && _inputActions.Player.Contatar.WasPressedThisFrame())
        {
            FindFirstObjectByType<RecruitmentUI>()
                .OpenScreen(this, GetComponent<NPCsData>());
        }
    }

    private void OnEnable()
    {
        _inputActions.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Disable();
    }
    #endregion

    #region Triggers de Colisão
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            _isPlayerNearby = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            _isPlayerNearby = false;
        }
    }
    #endregion
}