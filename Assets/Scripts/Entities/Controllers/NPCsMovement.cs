using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(NPCsData))]
[RequireComponent(typeof(SpriteRenderer))]
public class NPCsMovement : MonoBehaviour
{
    [Header("Settings")]
    public float moveSpeed = 2.8f;
    public float timeUntilAggressive = 4f;
    public bool isAgressive = true;
    public float maxChaseTime = 10f;
    public float maxTimeUntilChangingDirection = 5f;
    public float viewRadius = 1f;
    public bool isMaritime = true;

    [Header("References")]
    private WorldGenerator worldGenerator;

    // Internal State
    private GameObject Presa = null;
    private Vector2 direction = Vector2.zero;
    private Vector3 lastValidPosition;
    private float agressionTimer;
    private float timeUntilChangeDirection;
    private float chaseTimer, wanderingTimer;
    private bool StopAgressivity = false, isAlert = false;
    private float boundaryCheckTimer;
    private const float BoundaryCheckInterval = 0.05f;
    private float borderCooldownTimer = 0f;
    private bool hasBorderCooldown = false;
    private const float BorderChaseCooldown = 20f; // segundos até poder perseguir novamente    
    private bool indoParaOBarco = false;
    private Transform alvoBarco = null;

    // Components
    private Rigidbody2D rb;
    private NPCsData nPCs;
    private Animator animator;
    private CircleCollider2D circleCollider2D;
    private Vector2Int currentChunk;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        nPCs = GetComponent<NPCsData>();
        circleCollider2D = GetComponent<CircleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (isAgressive)
        {
            gameObject.tag = "AggresiveCreature";
        }
        else
        {
            gameObject.tag = "PassiveCreature";
        }
        
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        circleCollider2D.radius = viewRadius;
        circleCollider2D.isTrigger = true;
    }

    void Start()
    {
        SetRandomDirection();
        timeUntilChangeDirection = Random.Range(0, maxTimeUntilChangingDirection);
    }

    void Update()
    {
        if (GameState.IsInBattle|| !GameState.isGameStarted) return;

        // Tick do cooldown de borda
        if (hasBorderCooldown)
        {
            borderCooldownTimer -= Time.deltaTime;
            if (borderCooldownTimer <= 0)
            {
                borderCooldownTimer = 0;
                hasBorderCooldown = false;
            }
        }

        if (indoParaOBarco && alvoBarco != null)
        {
            HandleGoingToShip();
        }
        else if (Presa != null)
        {
            HandleChasing();
        }
        else
        {
            HandleWandering();
        }

        if (StopAgressivity)
        {
            agressionTimer -= Time.deltaTime;
            if (agressionTimer <= 0)
            {
                agressionTimer = 0;
                StopAgressivity = false;
            }
        }
    }
    public void Setup(GameObject player, WorldGenerator worldGenerator)
    {
        this.worldGenerator = worldGenerator;
        lastValidPosition = transform.position;
        currentChunk = worldGenerator.GetChunkPosFromWorld(transform.position);
    }

    public void IrParaOBarco(Transform navio)
    {
        alvoBarco = navio;
        indoParaOBarco = true;
        
        Presa = null; 
        isAlert = false;
        StopAgressivity = false;
        
        circleCollider2D.enabled = false; 
    }

    private void HandleGoingToShip()
    {
        Vector2 distanceToShip = alvoBarco.position - transform.position;
        direction = distanceToShip.normalized;

        if (distanceToShip.magnitude < 1.5f)
        {
            Color corTemp = spriteRenderer.color;
            corTemp.a = Mathf.MoveTowards(corTemp.a, 0f, Time.fixedDeltaTime);
            
            spriteRenderer.color = corTemp;
            
            if (spriteRenderer.color.a <= 0)
            {
                gameObject.transform.SetParent(alvoBarco);
                gameObject.SetActive(false);
            }
        }
    }

    void FixedUpdate()
    {
        if (GameState.IsInBattle || !GameState.isGameStarted)
        {
            rb.linearVelocity = Vector2.zero;
            return; // ← trava física completamente
        }
        
        if (worldGenerator == null) return;
        ApplyMovement();
        UpdateAnimations();

        if (!indoParaOBarco)
        {
            boundaryCheckTimer += Time.fixedDeltaTime;
            if (boundaryCheckTimer >= BoundaryCheckInterval)
            {
                boundaryCheckTimer = 0;
                CheckWorldBoundaries();
                CheckDespawn();
            }
        }
    }


    private void HandleChasing()
    {
        if (Presa == null || Presa.Equals(null)) 
        {
            StopChasing();
            return; 
        }

        if(isAgressive)
        {
            Vector2 distanceToPlayer = Presa.transform.position - transform.position;
            direction = distanceToPlayer.normalized;
        }
        else
        {
            Vector2 distanceToCreature = transform.position - Presa.transform.position;
            direction = distanceToCreature.normalized;        
        }
        chaseTimer += Time.deltaTime;
        if (chaseTimer >= maxChaseTime)
        {
            StopChasing();
        }
    }

    private void HandleWandering()
    {
        wanderingTimer += Time.deltaTime;
        if (wanderingTimer >= timeUntilChangeDirection)
        {
            wanderingTimer = 0;
            timeUntilChangeDirection = Random.Range(0, maxTimeUntilChangingDirection);
            SetRandomDirection();
        }
    }

    private void StopChasing()
    {
        Presa = null;
        chaseTimer = 0;
        GameState.ChasersCount--;
        SetRandomDirection();
    }

    private void SetRandomDirection()
    {
        direction = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
    }

    private void ApplyMovement()
    {
        float realSpeed = (!isAgressive && Presa != null)? moveSpeed * 1.4f: moveSpeed;
        if (!isAlert)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, direction * realSpeed, Time.fixedDeltaTime);
        }
        else
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime);
        }
    }

    private void UpdateAnimations()
    {
        if (!isAlert)
        {
            animator.SetFloat("Horizontal", rb.linearVelocity.x);
            animator.SetFloat("Vertical", rb.linearVelocity.y);
            animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
        }
        else
        {
            animator.SetFloat("Horizontal", direction.x);
            animator.SetFloat("Vertical", direction.y);
            animator.SetFloat("MoveSpeed", rb.linearVelocity.sqrMagnitude);
        }
    }

    private void CheckWorldBoundaries()
    {
        Tile actualTile = worldGenerator.GetTileAtWorldPosition(transform.position);

        if (actualTile == null) return;

        bool isInvalidTile = isMaritime
            ? actualTile.metadata.camada != 0
            : actualTile.metadata.camada == 0;

        if (isInvalidTile)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = lastValidPosition;

            Presa = null;
            chaseTimer = 0;
            isAlert = false;
            StopAgressivity = false;
            agressionTimer = 0;

            // Aplica cooldown apenas para carnívoros/agressivos
            if (isAgressive)
            {
                hasBorderCooldown = true;
                borderCooldownTimer = BorderChaseCooldown;
            }

            direction = GetEscapeDirection();
        }
        else
        {
            lastValidPosition = transform.position;
        }
    }
    private Vector2 GetEscapeDirection()
    {
        // Testa as 8 direções e escolhe a que leva ao tile válido mais próximo
        Vector2[] candidates = {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1,1).normalized, new Vector2(-1,1).normalized,
            new Vector2(1,-1).normalized, new Vector2(-1,-1).normalized
        };

        float probeDistance = 0.5f;

        foreach (Vector2 dir in candidates)
        {
            Vector2 probePos = (Vector2)lastValidPosition + dir * probeDistance;
            Tile probeTile = worldGenerator.GetTileAtWorldPosition(probePos);

            if (probeTile == null) continue;

            bool isValid = isMaritime
                ? probeTile.metadata.camada == 0
                : probeTile.metadata.camada != 0;

            if (isValid) return dir;
        }

        // Fallback: direção aleatória
        return new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
    }
    private void CheckDespawn()
    {
        Vector2Int newChunk = worldGenerator.GetChunkPosFromWorld(transform.position);

        // Atualiza a chunk atual se o NPC se moveu
        if (newChunk != currentChunk)
        {
            currentChunk = newChunk;
        }

        // Destrói se a chunk atual não estiver mais ativa
        if (!worldGenerator.IsChunkActive(currentChunk))
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D collider)
    {
        PlayerMovement playerMovement = collider.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            if (collider.gameObject.CompareTag("Player") && isAgressive && playerMovement.isOnWater)
            {
                isAlert = true;
                StopAgressivity = false;
                Presa = null;
                agressionTimer = 0;
                return;
            }
        }

        else if ((collider.gameObject.CompareTag("AgressiveCreature" ) && !isAgressive && Presa == null) ||
            (nPCs.creatureType == NPCsData.Type.Animal || nPCs.creatureType == NPCsData.Type.Monstro) && collider.gameObject.CompareTag("PassiveCreature") && isAgressive && Presa == null)
        {
            isAlert = true;
            StopAgressivity = false;
        }
    }
    void OnTriggerStay2D(Collider2D collider)
    {
        if (collider.gameObject.CompareTag("Player") && isAgressive)
        {
            agressionTimer += Time.fixedDeltaTime;

            if (isAlert && Presa != collider.gameObject)
            {
                Vector2 distanceToPlayer = collider.transform.position - transform.position;
                direction = Vector2.Lerp(direction, distanceToPlayer.normalized, Time.fixedDeltaTime);
            }
            
            if (agressionTimer >= timeUntilAggressive && hasBorderCooldown == false)
            {
                isAlert = false;
                Presa = collider.gameObject;

                if (Presa.CompareTag("Player")) 
                    GameState.ChasersCount++;
            }

            return;
        }

        if ((collider.gameObject.CompareTag("PassiveCreature") && isAgressive
        || (collider.gameObject.CompareTag("AgressiveCreature" ) && !isAgressive)) && Presa == null)
        {
            agressionTimer += Time.fixedDeltaTime;

            if (isAlert && Presa == null)
            {
                Vector2 distanceToPlayer = collider.transform.position - transform.position;
                direction = Vector2.Lerp(direction, distanceToPlayer.normalized, Time.fixedDeltaTime);
            }

            if (agressionTimer >= timeUntilAggressive && hasBorderCooldown == false)
            {
                isAlert = false;
                Presa = collider.gameObject;
            }
        }
    }

    void OnTriggerExit2D(Collider2D collider)
    {
        if ((collider.gameObject.CompareTag("AgressiveCreature" ) && !isAgressive && Presa == null) ||
            ((collider.gameObject.CompareTag("Player") || collider.gameObject.CompareTag("PassiveCreature")) && isAgressive && Presa == null))
        {
            StopAgressivity = true;
            isAlert = false;
        }
    }

    void OnDestroy()
    {
        if (Presa != null && Presa.CompareTag("Player"))
            GameState.ChasersCount--;
    }
}