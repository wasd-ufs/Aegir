using UnityEngine;

/// <summary>
/// Controla o comportamento da câmera do jogo, fazendo-a seguir dinamicamente o jogador ativo
/// (alternando foco entre Navio e Capitão) com interpolação suave (Lerp), antecipação de movimento
/// (look-ahead) e zoom / distância variável dependendo se o jogador está na água ou em terra firme.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    #region Referências e Configurações
    [Header("Referências")]
    [Tooltip("Referência ao gerador do mundo para acessar o jogador atual (legado).")]
    [SerializeField] private WorldGenerator _worldGenerator;
    
    [Tooltip("Referência ao gerador do mundo V2.")]
    [SerializeField] private MonoBehaviour _worldGeneratorV2;

    [Tooltip("Referência direta ao controlador de movimento do jogador (detectado automaticamente se nulo).")]
    [SerializeField] private PlayerMovement _playerMovement;

    [Tooltip("Alvo customizado opcional (sobrepõe a seleção automática de foco).")]
    [SerializeField] private Transform _customTarget;

    [Header("Parâmetros de Movimento e Suavização")]
    [Tooltip("Velocidade de interpolação da posição da câmera. Valores menores geram movimento mais suave/atrasado.")]
    [SerializeField] private float _smoothSpeed = 0.08f;
    
    [Tooltip("Multiplicador da força de antecipação. Define o quão longe a câmera 'olha à frente' baseado na velocidade.")]
    [SerializeField] private float _movementLookAheadMultiplier = 0.6f;
    
    [Tooltip("Taxa de amortecimento (Lerp) aplicada à leitura da velocidade para evitar solavancos.")]
    [SerializeField] private float _movementDamping = 0.05f;

    [Header("Distância e Zoom Variável (Navio vs. Capitão)")]
    [Tooltip("Tamanho ortográfico da câmera quando o jogador está navegando no barco/navio (visão mais ampla do mar).")]
    [SerializeField] private float _boatOrthographicSize = 5f;

    [Tooltip("Tamanho ortográfico da câmera quando o capitão está a pé explorando ilhas (visão mais próxima).")]
    [SerializeField] private float _captainOrthographicSize = 3.5f;

    [Tooltip("Velocidade de transição suave do zoom ao alternar entre navio e capitão.")]
    [SerializeField] private float _zoomSmoothSpeed = 2.5f;

    [Tooltip("Offset tridimensional da câmera no modo barco/navio (eixo Z deve ser negativo em 2D).")]
    [SerializeField] private Vector3 _boatOffset = new Vector3(0, 0, -10f);

    [Tooltip("Offset tridimensional da câmera no modo capitão.")]
    [SerializeField] private Vector3 _captainOffset = new Vector3(0, 0, -10f);
    #endregion

    #region Propriedades Públicas
    public float BoatOrthographicSize { get => _boatOrthographicSize; set => _boatOrthographicSize = value; }
    public float CaptainOrthographicSize { get => _captainOrthographicSize; set => _captainOrthographicSize = value; }
    public float ZoomSmoothSpeed { get => _zoomSmoothSpeed; set => _zoomSmoothSpeed = value; }
    public float SmoothSpeed { get => _smoothSpeed; set => _smoothSpeed = value; }
    public Vector3 BoatOffset { get => _boatOffset; set => _boatOffset = value; }
    public Vector3 CaptainOffset { get => _captainOffset; set => _captainOffset = value; }
    public Transform CurrentTarget => _currentTarget;
    public bool IsFocusingCaptain => _isCaptainActive;
    public PlayerMovement PlayerMovement { get => _playerMovement; set => _playerMovement = value; }
    public Transform CustomTarget { get => _customTarget; set => _customTarget = value; }
    #endregion

    #region Estado Interno
    private Camera _camera;
    private Transform _currentTarget;
    private Rigidbody2D _currentRigidBody;
    private Vector2 _smoothedVelocity;
    private Vector3 _currentOffset = new Vector3(0, 0, -10f);
    private bool _isCaptainActive;
    #endregion

    #region Ciclo de Vida (Unity)
    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null) _camera = Camera.main;

        if (_worldGenerator == null && _worldGeneratorV2 == null)
        {
            var v2 = FindFirstObjectByType<Aegir.World.V2.Runtime.WorldGeneratorV2>();
            if (v2 != null) _worldGeneratorV2 = v2;
            else _worldGenerator = FindFirstObjectByType<WorldGenerator>();
        }

        if (_playerMovement == null)
        {
            _playerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        _currentOffset = _boatOffset;
    }

    private void Start()
    {
        Transform initialTarget = GetActivePlayerTransform();
        if (initialTarget != null)
        {
            _currentTarget = initialTarget;
            _currentRigidBody = initialTarget.GetComponent<Rigidbody2D>();
            transform.position = initialTarget.position + _currentOffset;
        }

        if (_camera != null && _camera.orthographic)
        {
            _camera.orthographicSize = _isCaptainActive ? _captainOrthographicSize : _boatOrthographicSize;
        }
    }

    public Transform GetActivePlayerTransform()
    {
        if (_customTarget != null)
        {
            _isCaptainActive = false;
            return _customTarget;
        }

        if (_playerMovement == null)
        {
            _playerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        if (_playerMovement != null)
        {
            _isCaptainActive = !_playerMovement.isOnWater || 
                               !GameState.IsOnWater || 
                               (_playerMovement.captain != null && _playerMovement.captain.activeSelf);

            if (_isCaptainActive && _playerMovement.captain != null && _playerMovement.captain.activeSelf)
            {
                return _playerMovement.captain.transform;
            }

            return _playerMovement.transform;
        }

        if (_worldGeneratorV2 is IWorldGenerator genV2 && genV2.PlayerTransform != null)
        {
            _isCaptainActive = !GameState.IsOnWater;
            return genV2.PlayerTransform;
        }

        if (_worldGenerator != null && _worldGenerator.player != null)
        {
            _isCaptainActive = !GameState.IsOnWater;
            return _worldGenerator.player;
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            _isCaptainActive = !GameState.IsOnWater;
            return playerObj.transform;
        }

        return null;
    }

    private void Update()
    {
        UpdateZoom();
    }

    private void FixedUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        Transform playerTransform = GetActivePlayerTransform();
        if (playerTransform == null) return;

        if (playerTransform != _currentTarget)
        {
            _currentTarget = playerTransform;
            _currentRigidBody = playerTransform.GetComponent<Rigidbody2D>();
            _smoothedVelocity = Vector2.zero;
        }

        Vector2 velocity = Vector2.zero;
        if (_currentRigidBody != null)
        {
            velocity = _currentRigidBody.linearVelocity;
        }

        _smoothedVelocity = Vector2.Lerp(_smoothedVelocity, velocity, _movementDamping);

        Vector3 targetOffset = _isCaptainActive ? _captainOffset : _boatOffset;
        _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, Time.fixedDeltaTime * _zoomSmoothSpeed);

        Vector3 desiredPosition = playerTransform.position + _currentOffset + new Vector3(_smoothedVelocity.x, _smoothedVelocity.y, 0) * _movementLookAheadMultiplier;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed);
        transform.position = smoothedPosition;
    }

    private void UpdateZoom()
    {
        if (_camera == null) return;

        float targetSize = _isCaptainActive ? _captainOrthographicSize : _boatOrthographicSize;

        if (_camera.orthographic)
        {
            _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, targetSize, Time.deltaTime * _zoomSmoothSpeed);
        }
        else
        {
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetSize * 10f, Time.deltaTime * _zoomSmoothSpeed);
        }
    }
    #endregion
}