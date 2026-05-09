using UnityEngine;

/// <summary>
/// Controlador principal de movimento da entidade do Jogador. 
/// Trabalha em dois estados principais geridos por "isOnWater": O movimento em água (Barco) 
/// com simulação de balanço das ondas e ventos, e o movimento em Terra firme (Capitão).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    #region Configurações de Movimento
    [Header("Speeds")]
    public float boatSpeed;
    public float captainSpeed;
    
    [Header("Física da Água")]
    public float amplitude = 0.05f; // O quanto ele sobe/desce
    public float frequencia = 2f;   // A velocidade do balanço
    public float tempoAteOVentoMudar = 10;
    #endregion

    #region Estado Interno e Física
    [Header("Internal State")]
    public bool isOnWater = true;
    private Vector2 moveInput;
    private Vector3 lastValidPosition;
    private float intervaloEntreMudancas = 0;
    private float dirVentoX = 1, dirVentoY = 1;
    #endregion

    #region Referências
    [Header("References")]
    public GameObject capitão;
    public Camera mainCamera;
    public WorldGenerator worldGenerator;

    private Rigidbody2D rb;
    private Rigidbody2D crb;
    private Animator animator;
    private Animator cAnimator;
    private PlayerInputActions inputActions;
    #endregion

    #region Ciclo de Vida (Unity)
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
        if (GameState.IsInBattle || !GameState.IsGameStarted) return; 
        
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();

        if (inputActions.Player.EnterGetOut.WasPressedThisFrame()) 
        {
            worldGenerator.TryGoOut(mainCamera);   
        }

        SimularVentos();
        AtualizarAnimacoes();
    }
    #endregion

    #region Física e Movimento (FixedUpdate)
    void FixedUpdate()
    {
        // Trava física completamente em combate ou menus
        if (GameState.IsInBattle || !GameState.IsGameStarted)
        {
            rb.linearVelocity = Vector2.zero;
            crb.linearVelocity = Vector2.zero;
            return; 
        }
        
        Vector3 currentPos = isOnWater ? transform.position : capitão.transform.position;
        Tile actualTile = worldGenerator.GetTileAtWorldPosition(currentPos);
        
        // Se o mapa ainda não carregou o tile sob o jogador, não zera a velocidade ainda
        if (actualTile == null) return; 

        // Tenta achar água caso tenha bugado por estar "marítimo" fora do mapa 
        if (actualTile.metadata.camada == 0 && lastValidPosition == null)
        {
            worldGenerator.TryFindWaterTile();
        }

        Vector2 direction = moveInput.sqrMagnitude > 1 ? moveInput.normalized : moveInput;

        if (isOnWater)
        {
            // Camada 0 é Água
            if (actualTile.metadata.camada == 0) ApplyWaterMovement(direction);
            else StopAndReset(); // Bateu em terra (Camada != 0)
        }
        else 
        {
            // Lógica do Capitão na Terra (Camada 1 ou superior)
            if (actualTile.metadata.camada != 0)
            {
                crb.linearVelocity = direction * captainSpeed;
                rb.linearVelocity = Vector2.zero; // Garante que o barco não fuja sozinho
            }
            else
            {
                crb.linearVelocity *= -1;
            }
        }
    }
    #endregion

    #region Helpers de Movimento e Simulação
    private void ApplyWaterMovement(Vector2 direction)
    {
        // Efeito de balanço das ondas cruzado com a direção do vento
        float balancox = Mathf.Sin(Time.fixedTime * frequencia) * amplitude * dirVentoX, 
              balancoy = Mathf.Cos(Time.fixedTime * frequencia) * amplitude * dirVentoY;

        Vector2 forcaBalanço = new Vector2(balancox, balancoy);
        rb.linearVelocity += forcaBalanço * Time.fixedDeltaTime;

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, direction * boatSpeed, Time.fixedDeltaTime * 1);
        lastValidPosition = transform.position;
    }

    private void SimularVentos()
    {
        intervaloEntreMudancas += Time.deltaTime;
        if(intervaloEntreMudancas >= tempoAteOVentoMudar)
        {
            intervaloEntreMudancas = 0;
            dirVentoX = Random.Range(-1f, 1f);
            dirVentoY = Random.Range(-1f, 1f);
        }
    }

    private void AtualizarAnimacoes()
    {
        if(moveInput.sqrMagnitude >= 0.01f)
        {
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

    private void StopAndReset()
    {
        rb.linearVelocity = Vector2.zero;
        transform.position = lastValidPosition;
    }
    #endregion

    #region Controles de Input
    void OnEnable()  => inputActions.Enable();
    void OnDisable() => inputActions.Disable();
    public PlayerInputActions GetInputActions() => inputActions;
    #endregion
}