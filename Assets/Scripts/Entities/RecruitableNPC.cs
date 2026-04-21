using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class RecruitableNPC : MonoBehaviour
{
    PlayerInputActions inputActions;
    private bool isPlayerNearby = false;
    public Transform barco;
    public float raioDeInteração;
    private Rigidbody2D rb;
    private CircleCollider2D circleCollider2D;
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
            FindFirstObjectByType<RecruitmentUI>().AbrirTela(this, GetComponent<NPCsData>());
        }
    }

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

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }


}
