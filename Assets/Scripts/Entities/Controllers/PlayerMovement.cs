using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private Rigidbody2D crb;
    private Animator animator;
    private Animator cAnimator;
    private PlayerInputActions inputActions;
    private Vector2 moveInput;
    public float boatSpeed;
    public float captainSpeed;
    public bool isOnWater = true;
    private Vector3 lastValidPosition;
    public float amplitude = 0.05f; // O quanto ele sobe/desce
    public float frequencia = 2f;   // A velocidade do balanço
    public float tempoAteOVentoMudar = 10;
    private float intervaloEntreMudancas = 0;
    private float dirVentoX = 1, dirVentoY = 1;
    public GameObject capitão;
    public Camera mainCamera;

    public WorldGenerator worldGenerator;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        rb = GetComponent<Rigidbody2D>();
        crb = capitão.GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        cAnimator = capitão.GetComponent<Animator>();
        GameState.IsOnWater = isOnWater;
    }

    void Start()
    {
        lastValidPosition = transform.position;
    }

    void Update()
    {
        if (GameState.IsInBattle || !GameState.isGameStarted) return; 
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (inputActions.Player.EnterGetOut.WasPressedThisFrame()) 
        {
            worldGenerator.TryGoOut(mainCamera);   
        }

        intervaloEntreMudancas += Time.deltaTime;

        if(intervaloEntreMudancas >= tempoAteOVentoMudar)
        {
            intervaloEntreMudancas = 0;
            dirVentoX = Random.Range(-1f, 1f);
            dirVentoY = Random.Range(-1f, 1f);
        }

        if(moveInput.sqrMagnitude >= 0.01f)
        {
            Debug.Log("Movendo: " + moveInput);
            if (isOnWater)
            {
                animator.SetFloat("Horizontal", rb.linearVelocity.x);
                animator.SetFloat("Vertical", rb.linearVelocity.y);        
                animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
            }
            else
            {
                cAnimator.SetFloat("Horizontal", moveInput.x);
                cAnimator.SetFloat("Vertical", moveInput.y);       
            }
        }

        if (capitão.activeSelf)
            cAnimator.SetFloat("MoveSpeed", crb.linearVelocity.sqrMagnitude);
        animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
    }

    void FixedUpdate()
    {
        if (GameState.IsInBattle || !GameState.isGameStarted)
        {
            rb.linearVelocity = Vector2.zero;
            crb.linearVelocity = Vector2.zero;
            return; // trava física completamente
        }
        
        Vector3 currentPos = isOnWater ? transform.position : capitão.transform.position;
        Tile actualTile = worldGenerator.GetTileAtWorldPosition(currentPos);
        
        // Se o mapa ainda não carregou o tile sob o jogador, não zera a velocidade ainda
        if (actualTile == null) return; 

        if (actualTile.metadata.camada == 0 && lastValidPosition == null)
        {
            worldGenerator.TryFindWaterTile();
        }

        Vector2 direction = moveInput.sqrMagnitude > 1 ? moveInput.normalized : moveInput;

        if (isOnWater)
        {
            // Camada 0 é Água
            if (actualTile.metadata.camada == 0)
            {
                ApplyWaterMovement(direction);
            }
            else
            {
                // Se o barco bater em terra (camada != 0), ele para
                StopAndReset();
            }
        }
        else 
        {
            // Lógica do Capitão na Terra (Camada 1 ou superior)
            if (actualTile.metadata.camada != 0)
            {
                crb.linearVelocity = direction * captainSpeed;
                rb.linearVelocity = Vector2.zero; // Garante que o barco não fuja
            }
            else
            {
                crb.linearVelocity *= -1;
            }
        }
    }

    private void ApplyWaterMovement(Vector2 direction)
    {
        // Efeito de balanço
        float balancox = Mathf.Sin(Time.fixedTime * frequencia) * amplitude * dirVentoX, balancoy = Mathf.Cos(Time.fixedTime * frequencia) * amplitude * dirVentoY;

        Vector2 forcaBalanço = new Vector2(balancox, balancoy);
        rb.linearVelocity += forcaBalanço * Time.fixedDeltaTime;

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, direction * boatSpeed,Time.fixedDeltaTime * 1);
        lastValidPosition = transform.position;
    }

    private void StopAndReset()
    {
        rb.linearVelocity = Vector2.zero;
        transform.position = lastValidPosition;
    }

    void OnEnable()
    {
        inputActions.Enable();
    }

    void OnDisable()
    {
        inputActions.Disable();
    }

    public PlayerInputActions GetInputActions()
    {
        return inputActions;
    }
}
