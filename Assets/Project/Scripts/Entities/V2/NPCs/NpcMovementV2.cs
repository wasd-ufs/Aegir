using UnityEngine;

namespace Aegir.Entities.V2.NPCs
{
    /// <summary>
    /// Controlador moderno de IA e movimentação para NPCs e criaturas no ecossistema V2.
    /// Herda de <see cref="NPCsMovement"/> para garantir 100% de compatibilidade retroativa
    /// com a interface de recrutamento (<see cref="RecruitmentUI"/>), dados e combate,
    /// decompondo a execução em subsistemas desacoplados (<see cref="NpcLocomotionV2"/> e <see cref="NpcBrainV2"/>).
    /// </summary>
    public class NpcMovementV2 : NPCsMovement
    {
        public static readonly int HashHorizontal = Animator.StringToHash("Horizontal");
        public static readonly int HashVertical = Animator.StringToHash("Vertical");
        public static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

        public NpcBrainV2 Brain => _brain;
        public NpcLocomotionV2 Locomotion => _locomotion;

        private NpcLocomotionV2 _locomotion;
        private NpcBrainV2 _brain;
        private float _boundaryTimer;
        private const float BoundaryInterval = 0.05f;

        protected override void Awake()
        {
            base.Awake();

            _locomotion = new NpcLocomotionV2(_rigidbody2D, transform, IsMaritime);
            _brain = new NpcBrainV2(this, transform, _spriteRenderer);
        }

        protected override void Start()
        {
            base.Start();

            _locomotion?.Initialize(_worldGenerator);
        }

        public override void Setup(GameObject player, IWorldGenerator worldGenerator)
        {
            base.Setup(player, worldGenerator);

            _locomotion?.Initialize(worldGenerator);
        }

        protected override void Update()
        {
            if (GameState.IsInBattle || !GameState.IsGameStarted) return;

            _brain?.UpdateBrain(Time.deltaTime);
        }

        protected override void FixedUpdate()
        {
            if (GameState.IsInBattle || !GameState.IsGameStarted)
            {
                _locomotion?.Stop();
                return;
            }

            if (_worldGenerator == null)
            {
                _worldGenerator = (IWorldGenerator)FindFirstObjectByType<Aegir.World.V2.Runtime.WorldGeneratorV2>()
                               ?? FindFirstObjectByType<WorldGenerator>();
                if (_worldGenerator == null) return;
            }

            _boundaryTimer += Time.fixedDeltaTime;
            if (_boundaryTimer >= BoundaryInterval)
            {
                _boundaryTimer = 0f;

                if (_locomotion != null && _locomotion.ValidateBoundaries(_worldGenerator, out Vector2 escapeDir))
                {
                    _brain?.NotifyBoundaryHit(escapeDir);
                }

                if (_locomotion != null && _locomotion.CheckChunkDespawn(_worldGenerator, gameObject))
                {
                    return;
                }
            }

            if (_locomotion != null && _brain != null)
            {
                float targetSpeed = MoveSpeed;
                if (!IsAgressive && _brain.CurrentState == NpcAiStateV2.Fleeing)
                {
                    targetSpeed *= 1.4f; // Boost de pânico na fuga
                }

                _locomotion.SteerTowards(_brain.CurrentDirection, targetSpeed, _worldGenerator);
            }

            UpdateAnimationHashes();
        }

        private void UpdateAnimationHashes()
        {
            if (_animator == null || _locomotion == null) return;

            Vector2 vel = _locomotion.Velocity;
            if (vel.sqrMagnitude >= 0.01f)
            {
                _animator.SetFloat(HashHorizontal, vel.x);
                _animator.SetFloat(HashVertical, vel.y);
            }
            _animator.SetFloat(HashMoveSpeed, vel.sqrMagnitude);
        }

        public override void MoveToBoat(Transform boatTransform)
        {
            _brain?.StartBoarding(boatTransform);
            if (_circleCollider2D != null)
            {
                _circleCollider2D.enabled = false;
            }
        }

        protected override void OnTriggerEnter2D(Collider2D collider)
        {
            if (collider == null) return;

            PlayerMovement player = collider.GetComponent<PlayerMovement>();
            if (player != null && IsAgressive && player.isOnWater)
            {
                _brain?.OnTargetDetected(collider.gameObject);
                return;
            }

            if (collider.CompareTag("PassiveCreature") && IsAgressive)
            {
                _brain?.OnTargetDetected(collider.gameObject);
            }
            else if (collider.CompareTag("AgressiveCreature") && !IsAgressive)
            {
                _brain?.OnTargetDetected(collider.gameObject);
            }
        }

        protected override void OnTriggerExit2D(Collider2D collider)
        {
            if (collider == null) return;

            if (_brain != null && _brain.CurrentPrey == collider.gameObject)
            {
                _brain.OnTargetLost();
            }
        }
    }
}
