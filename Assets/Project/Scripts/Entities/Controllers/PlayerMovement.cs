using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("boatSpeed")]
    [SerializeField] private float _boatSpeed;
    public float BoatSpeed { get => _boatSpeed; set => _boatSpeed = value; }
    public float boatSpeed { get => _boatSpeed; set => _boatSpeed = value; }

    [FormerlySerializedAs("captainSpeed")]
    [SerializeField] private float _captainSpeed;
    public float CaptainSpeed { get => _captainSpeed; set => _captainSpeed = value; }
    public float captainSpeed { get => _captainSpeed; set => _captainSpeed = value; }
    
    [Header("Física da Água")]
    [FormerlySerializedAs("amplitude")]
    [SerializeField] private float _amplitude = 0.05f; // O quanto ele sobe/desce
    public float Amplitude { get => _amplitude; set => _amplitude = value; }
    public float amplitude { get => _amplitude; set => _amplitude = value; }

    [FormerlySerializedAs("frequencia")]
    [SerializeField] private float _waveFrequency = 2f;   // A velocidade do balanço
    public float WaveFrequency { get => _waveFrequency; set => _waveFrequency = value; }
    public float frequencia { get => _waveFrequency; set => _waveFrequency = value; }

    [FormerlySerializedAs("tempoAteOVentoMudar")]
    [SerializeField] private float _timeUntilWindChanges = 10f;
    public float TimeUntilWindChanges { get => _timeUntilWindChanges; set => _timeUntilWindChanges = value; }
    public float tempoAteOVentoMudar { get => _timeUntilWindChanges; set => _timeUntilWindChanges = value; }
    #endregion

    #region Estado Interno e Física
    [Header("Internal State")]
    public bool isOnWater = true;
    public Vector2 moveInput;
    private Vector3 _lastValidPosition;
    private float _windChangeInterval = 0;
    private float _windDirX = 1, _windDirY = 1;
    #endregion

    #region Referências
    [Header("References")]
    [FormerlySerializedAs("capitão")]
    public GameObject captain;
    public GameObject capitão => captain;
    public Camera mainCamera;
    public WorldGenerator worldGenerator;

    public Rigidbody2D rb;
    public Rigidbody2D crb;
    private Animator _animator;
    private Animator _cAnimator;
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
        _animator = GetComponent<Animator>();
        _cAnimator = captain.GetComponent<Animator>();
        GameState.IsOnWater = isOnWater;
    }

    void Start()
    {
        _lastValidPosition = transform.position;

        if (isOnWater)
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

        SimulateWind();
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
        if (GameState.IsInBattle || !GameState.IsGameStarted)
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
        float waveX = Mathf.Sin(Time.fixedTime * _waveFrequency) * _amplitude * _windDirX;
        float waveY = Mathf.Cos(Time.fixedTime * _waveFrequency) * _amplitude * _windDirY;

        Vector2 waveForce = new Vector2(waveX, waveY);
        rb.linearVelocity += waveForce * Time.fixedDeltaTime;

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, direction * _boatSpeed, Time.fixedDeltaTime * 1);
        _lastValidPosition = transform.position;
    }

    private void SimulateWind()
    {
        _windChangeInterval += Time.deltaTime;
        if (_windChangeInterval >= _timeUntilWindChanges)
        {
            _windChangeInterval = 0;
            _windDirX = Random.Range(-1f, 1f);
            _windDirY = Random.Range(-1f, 1f);
        }
    }

    private void SimularVentos() => SimulateWind();

    /// <summary>
    /// Atualiza as variáveis do Animator de acordo com o estado e velocidade atuais.
    /// </summary>
    public void UpdateAnimations()
    {
        if (moveInput.sqrMagnitude >= 0.01f)
        {
            if (currentState is BoatState)
            {
                _animator.SetFloat("Horizontal", rb.linearVelocity.x);
                _animator.SetFloat("Vertical", rb.linearVelocity.y);        
                _animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
            }
            else
            {
                _cAnimator.SetFloat("Horizontal", moveInput.x);
                _cAnimator.SetFloat("Vertical", moveInput.y);       
            }
        }

        if (captain.activeSelf)
            _cAnimator.SetFloat("MoveSpeed", crb.linearVelocity.sqrMagnitude);
        _animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
    }

    /// <summary>
    /// Alias para manter compatibilidade com chamadas legadas de atualização de animações.
    /// </summary>
    public void AtualizarAnimacoes() => UpdateAnimations();

    public void StopAndReset()
    {
        rb.linearVelocity = Vector2.zero;
        transform.position = _lastValidPosition;
    }
    #endregion

    #region Controles de Input
    void OnEnable()  => inputActions.Enable();
    void OnDisable() => inputActions.Disable();
    public PlayerInputActions GetInputActions() => inputActions;
    #endregion
}