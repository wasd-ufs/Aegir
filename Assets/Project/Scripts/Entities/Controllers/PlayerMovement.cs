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
    public Vector2 moveInput;
    private Vector3 lastValidPosition;
    private float intervaloEntreMudancas = 0;
    private float dirVentoX = 1, dirVentoY = 1;
    #endregion

    #region Referências
    [Header("References")]
    [UnityEngine.Serialization.FormerlySerializedAs("capitão")]
    public GameObject captain;
    public GameObject capitão => captain;
    public Camera mainCamera;
    public WorldGenerator worldGenerator;

    public Rigidbody2D rb;
    public Rigidbody2D crb;
    private Animator animator;
    private Animator cAnimator;
    public PlayerInputActions inputActions;
    #endregion

    private IPlayerState currentState;

    public void ChangeState(IPlayerState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    #region State Machine
    public interface IPlayerState
    {
        void Enter();
        void Update();
        void FixedUpdate();
        void Exit();
    }
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        inputActions = new PlayerInputActions();
        rb = GetComponent<Rigidbody2D>();
        crb = captain.GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        cAnimator = captain.GetComponent<Animator>();
        GameState.IsOnWater = isOnWater;
    }

    void Start()
    {
        lastValidPosition = transform.position;

        if(isOnWater)
            ChangeState(new BoatState(this));
        else
            ChangeState(new CaptainState(this));
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
    currentState?.Update();

    // sincroniza com GameState
    if (GameState.IsOnWater && !(currentState is BoatState))
        ChangeState(new BoatState(this));

    if (!GameState.IsOnWater && !(currentState is CaptainState))
        ChangeState(new CaptainState(this));

    }
    #endregion

    #region Física e Movimento (FixedUpdate)
    void FixedUpdate()
    {
        if(GameState.IsInBattle || !GameState.IsGameStarted)
        {
            rb.linearVelocity = Vector2.zero;
            crb.linearVelocity = Vector2.zero;
            return;
        }

        currentState?.FixedUpdate();
    }
    #endregion

    #region Helpers de Movimento e Simulação
    public void ApplyWaterMovement(Vector2 direction)
    {
        // Efeito de balanço das ondas cruzado com a direção do vento
        float waveX = Mathf.Sin(Time.fixedTime * frequencia) * amplitude * dirVentoX, 
              waveY = Mathf.Cos(Time.fixedTime * frequencia) * amplitude * dirVentoY;

        Vector2 waveForce = new Vector2(waveX, waveY);
        rb.linearVelocity += waveForce * Time.fixedDeltaTime;

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

    public void AtualizarAnimacoes()
    {
        if(moveInput.sqrMagnitude >= 0.01f)
        {
            if (currentState is BoatState)
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

        if (captain.activeSelf)
            cAnimator.SetFloat("MoveSpeed", crb.linearVelocity.sqrMagnitude);
        animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
    }

    public void StopAndReset()
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