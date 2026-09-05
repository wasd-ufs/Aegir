using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A Máquina de Estados de IA responsável pela movimentação de todos os NPCs do jogo.
/// Controla comportamentos como: Wandering (andar a esmo), Perseguição (Agro),
/// Fuga das fronteiras de colisão (Evitar ir pra terra sendo peixe e vice versa) 
/// e embarque em navios (recrutados).
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(NPCsData))]
[RequireComponent(typeof(SpriteRenderer))]
public class NPCsMovement : MonoBehaviour
{
    #region Configurações de IA
    [Header("Settings")]
    [FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float _moveSpeed = 2.8f;
    public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }
    public float moveSpeed { get => _moveSpeed; set => _moveSpeed = value; }

    [FormerlySerializedAs("timeUntilAggressive")]
    [SerializeField] private float _timeUntilAggressive = 4f;
    public float TimeUntilAggressive { get => _timeUntilAggressive; set => _timeUntilAggressive = value; }
    public float timeUntilAggressive { get => _timeUntilAggressive; set => _timeUntilAggressive = value; }

    [FormerlySerializedAs("isAgressive")]
    [SerializeField] private bool _isAgressive = true;
    public bool IsAgressive { get => _isAgressive; set => _isAgressive = value; }
    public bool isAgressive { get => _isAgressive; set => _isAgressive = value; }

    [FormerlySerializedAs("maxChaseTime")]
    [SerializeField] private float _maxChaseTime = 10f;
    public float MaxChaseTime { get => _maxChaseTime; set => _maxChaseTime = value; }
    public float maxChaseTime { get => _maxChaseTime; set => _maxChaseTime = value; }

    [FormerlySerializedAs("maxTimeUntilChangingDirection")]
    [SerializeField] private float _maxTimeUntilChangingDirection = 5f;
    public float MaxTimeUntilChangingDirection { get => _maxTimeUntilChangingDirection; set => _maxTimeUntilChangingDirection = value; }
    public float maxTimeUntilChangingDirection { get => _maxTimeUntilChangingDirection; set => _maxTimeUntilChangingDirection = value; }

    [FormerlySerializedAs("viewRadius")]
    [SerializeField] private float _viewRadius = 1f;
    public float ViewRadius { get => _viewRadius; set => _viewRadius = value; }
    public float viewRadius { get => _viewRadius; set => _viewRadius = value; }

    [FormerlySerializedAs("isMaritime")]
    [SerializeField] private bool _isMaritime = true;
    public bool IsMaritime { get => _isMaritime; set => _isMaritime = value; }
    public bool isMaritime { get => _isMaritime; set => _isMaritime = value; }
    #endregion

    #region Estado Interno e Componentes
    [Header("References")]
    private WorldGenerator _worldGenerator;

    // Estado Interno
    private GameObject _prey = null;
    private Vector2 _direction = Vector2.zero;
    private Vector3 _lastValidPosition;
    private float _aggressionTimer;
    private float _timeUntilChangeDirection;
    private float _chaseTimer;
    private float _wanderingTimer;
    private bool _shouldStopAggression = false;
    private bool _isAlert = false;
    private float _boundaryCheckTimer;
    private const float BoundaryCheckInterval = 0.05f;
    private float _borderCooldownTimer = 0f;
    private bool _hasBorderCooldown = false;
    private const float BorderChaseCooldown = 20f; // segundos até poder perseguir novamente    
    private bool _isHeadingToBoat = false;
    private Transform _boatTarget = null;

    // Componentes Acoplados
    private Rigidbody2D _rigidbody2D;
    private NPCsData _npcsData;
    private Animator _animator;
    private CircleCollider2D _circleCollider2D;
    private Vector2Int _currentChunk;
    private SpriteRenderer _spriteRenderer;
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        _animator = GetComponent<Animator>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _npcsData = GetComponent<NPCsData>();
        _circleCollider2D = GetComponent<CircleCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_isAgressive) gameObject.tag = "AgressiveCreature";
        else gameObject.tag = "PassiveCreature";
        
        _rigidbody2D.gravityScale = 0;
        _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        _circleCollider2D.radius = _viewRadius;
        _circleCollider2D.isTrigger = true;
    }

    void Start()
    {
        SetRandomDirection();
        _timeUntilChangeDirection = Random.Range(0, _maxTimeUntilChangingDirection);
    }

    public void Setup(GameObject player, WorldGenerator worldGenerator)
    {
        _worldGenerator = worldGenerator;
        _lastValidPosition = transform.position;
        _currentChunk = worldGenerator.GetChunkPosFromWorld(transform.position);
    }

    void Update()
    {
        if (GameState.IsInBattle || !GameState.IsGameStarted) return;

        // Tick do cooldown de borda (impedido de dar agro caso ele acabe de fugir de uma fronteira que não pode atravessar)
        if (_hasBorderCooldown)
        {
            _borderCooldownTimer -= Time.deltaTime;
            if (_borderCooldownTimer <= 0)
            {
                _borderCooldownTimer = 0;
                _hasBorderCooldown = false;
            }
        }

        // Hierarquia de Comportamento
        if (_isHeadingToBoat && _boatTarget != null) HandleGoingToShip();
        else if (_prey != null) HandleChasing();
        else HandleWandering();

        if (_shouldStopAggression)
        {
            _aggressionTimer -= Time.deltaTime;
            if (_aggressionTimer <= 0)
            {
                _aggressionTimer = 0;
                _shouldStopAggression = false;
            }
        }
    }

    void FixedUpdate()
    {
        if (GameState.IsInBattle || !GameState.IsGameStarted)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            return; // ← trava física completamente em combates
        }
        
        if (_worldGenerator == null) return;
        
        ApplyMovement();
        UpdateAnimations();

        if (!_isHeadingToBoat)
        {
            _boundaryCheckTimer += Time.fixedDeltaTime;
            if (_boundaryCheckTimer >= BoundaryCheckInterval)
            {
                _boundaryCheckTimer = 0;
                CheckWorldBoundaries();
                CheckDespawn();
            }
        }
    }
    
    void OnDestroy()
    {
        if (_prey != null && _prey.CompareTag("Player"))
            GameState.ChasersCount = Mathf.Max(0, GameState.ChasersCount - 1);
    }
    #endregion

    #region Comportamentos Principais
    /// <summary>
    /// Comportamento adotado assim que um RecruitableNPC é contratado, fazendo com
    /// que ele marche em direção ao barco e suma visualmente.
    /// </summary>
    public void MoveToBoat(Transform boatTransform)
    {
        _boatTarget = boatTransform;
        _isHeadingToBoat = true;
        
        _prey = null; 
        _isAlert = false;
        _shouldStopAggression = false;
        
        _circleCollider2D.enabled = false; 
    }

    /// <summary>
    /// Alias para manter compatibilidade com chamadas legadas de movimentação em direção ao barco.
    /// </summary>
    public void IrParaOBarco(Transform navio) => MoveToBoat(navio);

    private void HandleGoingToShip()
    {
        Vector2 distanceToShip = _boatTarget.position - transform.position;
        _direction = distanceToShip.normalized;

        if (distanceToShip.magnitude < 1.5f)
        {
            Color corTemp = _spriteRenderer.color;
            corTemp.a = Mathf.MoveTowards(corTemp.a, 0f, Time.fixedDeltaTime);
            _spriteRenderer.color = corTemp;
            
            if (_spriteRenderer.color.a <= 0)
            {
                gameObject.transform.SetParent(_boatTarget);
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Rotina de Perseguição à Presa. Caso fuja, a presa é esquecida baseada no maxChaseTime.
    /// </summary>
    private void HandleChasing()
    {
        if (_prey == null || _prey.Equals(null)) 
        {
            StopChasing();
            return; 
        }

        if (_isAgressive)
        {
            Vector2 distanceToPlayer = _prey.transform.position - transform.position;
            _direction = distanceToPlayer.normalized;
        }
        else
        {
            Vector2 distanceToCreature = transform.position - _prey.transform.position;
            _direction = distanceToCreature.normalized;        
        }
        _chaseTimer += Time.deltaTime;
        
        if (_chaseTimer >= _maxChaseTime) StopChasing();
    }

    private void HandleWandering()
    {
        _wanderingTimer += Time.deltaTime;
        if (_wanderingTimer >= _timeUntilChangeDirection)
        {
            _wanderingTimer = 0;
            _timeUntilChangeDirection = Random.Range(0, _maxTimeUntilChangingDirection);
            SetRandomDirection();
        }
    }

    private void StopChasing()
    {
        if (_prey != null && _prey.CompareTag("Player"))
            GameState.ChasersCount = Mathf.Max(0, GameState.ChasersCount - 1);

        _prey = null;
        _chaseTimer = 0;
        SetRandomDirection();
    }

    private void SetRandomDirection()
    {
        _direction = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
    }
    #endregion

    #region Movimento e Animação
    private void ApplyMovement()
    {
        // Criaturas passivas sendo perseguidas ganham um boost de velocidade na fuga
        float realSpeed = (!_isAgressive && _prey != null) ? _moveSpeed * 1.4f : _moveSpeed;
        
        if (!_isAlert)
        {
            _rigidbody2D.linearVelocity = Vector2.Lerp(_rigidbody2D.linearVelocity, _direction * realSpeed, Time.fixedDeltaTime);
        }
        else
        {
            // Fica parado "alertado" analisando a situação antes de iniciar perseguição
            _rigidbody2D.linearVelocity = Vector2.Lerp(_rigidbody2D.linearVelocity, Vector2.zero, Time.fixedDeltaTime);
        }
    }

    private void UpdateAnimations()
    {
        if (!_isAlert)
        {
            _animator.SetFloat("Horizontal", _rigidbody2D.linearVelocity.x);
            _animator.SetFloat("Vertical", _rigidbody2D.linearVelocity.y);
            _animator.SetFloat("MoveSpeed", _rigidbody2D.linearVelocity.sqrMagnitude);
        }
        else
        {
            _animator.SetFloat("Horizontal", _direction.x);
            _animator.SetFloat("Vertical", _direction.y);
            _animator.SetFloat("MoveSpeed", _rigidbody2D.linearVelocity.sqrMagnitude);
        }
    }
    #endregion

    #region Validação de Limites (World Boundaries)
    /// <summary>
    /// Verifica se o NPC "Marítimo" tocou num tile de "Terra" e vice versa.
    /// Se entrou em terreno proibido, força o retorno à última posição válida e desarma o Agro.
    /// </summary>
    private void CheckWorldBoundaries()
    {
        Tile actualTile = _worldGenerator.GetTileAtWorldPosition(transform.position);

        if (actualTile == null) return;

        bool isInvalidTile = _isMaritime
            ? actualTile.Metadata.Layer != 0
            : actualTile.Metadata.Layer == 0;

        if (isInvalidTile)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            transform.position = _lastValidPosition;

            if (_prey != null && _prey.CompareTag("Player"))
                GameState.ChasersCount = Mathf.Max(0, GameState.ChasersCount - 1);

            _prey = null;
            _chaseTimer = 0;
            _isAlert = false;
            _shouldStopAggression = false;
            _aggressionTimer = 0;

            if (_isAgressive)
            {
                _hasBorderCooldown = true;
                _borderCooldownTimer = BorderChaseCooldown;
            }

            _direction = GetEscapeDirection();
        }
        else
        {
            _lastValidPosition = transform.position;
        }
    }
    
    private Vector2 GetEscapeDirection()
    {
        // Testa as 8 direções e escolhe a que leva ao tile válido mais próximo do "bump"
        Vector2[] candidates = {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1,1).normalized, new Vector2(-1,1).normalized,
            new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized
        };

        float probeDistance = 0.5f;

        foreach (Vector2 dir in candidates)
        {
            Vector2 probePos = (Vector2)_lastValidPosition + dir * probeDistance;
            Tile probeTile = _worldGenerator.GetTileAtWorldPosition(probePos);

            if (probeTile == null) continue;

            bool isValid = _isMaritime
                ? probeTile.Metadata.Layer == 0
                : probeTile.Metadata.Layer != 0;

            if (isValid) return dir;
        }

        return new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
    }

    /// <summary>
    /// Exclui o NPC se a Chunk onde ele se encontra foi apagada (Despawn).
    /// </summary>
    private void CheckDespawn()
    {
        Vector2Int newChunk = _worldGenerator.GetChunkPosFromWorld(transform.position);

        if (newChunk != _currentChunk) _currentChunk = newChunk;
        if (!_worldGenerator.IsChunkActive(_currentChunk)) Destroy(gameObject);
    }
    #endregion

    #region Detecção de Triggers (Agro)
    void OnTriggerEnter2D(Collider2D collider)
    {
        PlayerMovement playerMovement = collider.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            // Inicia o estado de Alerta no Player
            if (collider.gameObject.CompareTag("Player") && _isAgressive && playerMovement.isOnWater)
            {
                _isAlert = true;
                _shouldStopAggression = false;
                _prey = null;
                _aggressionTimer = 0;
                return;
            }
        }
        else if ((collider.gameObject.CompareTag("AgressiveCreature") && !_isAgressive && _prey == null) ||
            (_npcsData.CreatureType == NPCsData.Type.Animal || _npcsData.CreatureType == NPCsData.Type.Monstro) && collider.gameObject.CompareTag("PassiveCreature") && _isAgressive && _prey == null)
        {
            // Inicia Alerta entre ecossistema (carnívoros vs passivos)
            _isAlert = true;
            _shouldStopAggression = false;
        }
    }
    
    void OnTriggerStay2D(Collider2D collider)
    {
        if (collider.gameObject.CompareTag("Player") && _isAgressive)
        {
            _aggressionTimer += Time.fixedDeltaTime;

            if (_isAlert && _prey != collider.gameObject)
            {
                Vector2 distanceToPlayer = collider.transform.position - transform.position;
                _direction = Vector2.Lerp(_direction, distanceToPlayer.normalized, Time.fixedDeltaTime);
            }
            
            // Fixa a presa caso o timer de alerta termine
            if (_aggressionTimer >= _timeUntilAggressive && _hasBorderCooldown == false)
            {
                if (_prey != collider.gameObject)
                {
                    _isAlert = false;
                    _prey = collider.gameObject;

                    if (_prey.CompareTag("Player")) 
                        GameState.ChasersCount++;
                }
            }

            return;
        }

        if ((collider.gameObject.CompareTag("PassiveCreature") && _isAgressive
        || (collider.gameObject.CompareTag("AgressiveCreature") && !_isAgressive)) && _prey == null)
        {
            _aggressionTimer += Time.fixedDeltaTime;

            if (_isAlert && _prey == null)
            {
                Vector2 distanceToPlayer = collider.transform.position - transform.position;
                _direction = Vector2.Lerp(_direction, distanceToPlayer.normalized, Time.fixedDeltaTime);
            }

            if (_aggressionTimer >= _timeUntilAggressive && _hasBorderCooldown == false)
            {
                _isAlert = false;
                _prey = collider.gameObject;
            }
        }
    }

    void OnTriggerExit2D(Collider2D collider)
    {
        if ((collider.gameObject.CompareTag("AgressiveCreature") && !_isAgressive && _prey == null) ||
            ((collider.gameObject.CompareTag("Player") || collider.gameObject.CompareTag("PassiveCreature")) && _isAgressive && _prey == null))
        {
            _shouldStopAggression = true;
            _isAlert = false;
        }
    }
    #endregion
}