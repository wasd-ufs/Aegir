using UnityEngine;

namespace Aegir.Entities.V2.NPCs
{
    /// <summary>
    /// Cérebro de IA comportamental para NPCs e criaturas no ecossistema V2.
    /// Orquestra a tomada de decisão (Vaguear, Alerta, Perseguição, Fuga e Embarque).
    /// </summary>
    public class NpcBrainV2
    {
        private readonly NpcMovementV2 _npc;
        private readonly Transform _transform;
        private readonly SpriteRenderer _spriteRenderer;

        public NpcAiStateV2 CurrentState { get; private set; } = NpcAiStateV2.Wandering;
        public GameObject CurrentPrey => _prey;
        public Vector2 CurrentDirection => _moveDirection;

        private GameObject _prey;
        private Vector2 _moveDirection;
        private float _wanderingTimer;
        private float _timeUntilChangeDirection;
        private float _aggressionTimer;
        private float _chaseTimer;
        private float _borderCooldownTimer;
        private bool _hasBorderCooldown;
        private const float BorderCooldownDuration = 20f;

        private Transform _boatTarget;
        private bool _isBoarding;

        public NpcBrainV2(NpcMovementV2 npc, Transform transform, SpriteRenderer spriteRenderer)
        {
            _npc = npc;
            _transform = transform;
            _spriteRenderer = spriteRenderer;
            PickRandomDirection();
            _timeUntilChangeDirection = Random.Range(1f, _npc.MaxTimeUntilChangingDirection);
        }

        private GameObject _potentialTarget;

        public void UpdateBrain(float deltaTime)
        {
            if (GameState.IsInBattle || !GameState.IsGameStarted) return;

            // Cooldown de borda territorial
            if (_hasBorderCooldown)
            {
                _borderCooldownTimer -= deltaTime;
                if (_borderCooldownTimer <= 0)
                {
                    _borderCooldownTimer = 0;
                    _hasBorderCooldown = false;
                }
            }

            // Hierarquia de Estados
            if (_isBoarding && _boatTarget != null)
            {
                CurrentState = NpcAiStateV2.BoardingShip;
                ExecuteBoarding(deltaTime);
            }
            else if (_prey != null)
            {
                CurrentState = _npc.IsAgressive ? NpcAiStateV2.Chasing : NpcAiStateV2.Fleeing;
                ExecuteChasingOrFleeing(deltaTime);
            }
            else if (_potentialTarget != null)
            {
                CurrentState = NpcAiStateV2.Alert;
                ExecuteAlert(deltaTime);
            }
            else
            {
                CurrentState = NpcAiStateV2.Wandering;
                ExecuteWandering(deltaTime);
            }
        }

        private void ExecuteAlert(float deltaTime)
        {
            if (_potentialTarget == null)
            {
                StopChasing();
                return;
            }

            // Orienta-se em direção ao alvo observado mas desacelera
            Vector2 toTarget = _potentialTarget.transform.position - _transform.position;
            _moveDirection = Vector2.Lerp(_moveDirection, toTarget.normalized, deltaTime * 2f);

            _aggressionTimer += deltaTime;
            if (_aggressionTimer >= _npc.TimeUntilAggressive)
            {
                _prey = _potentialTarget;
                _potentialTarget = null;
                _aggressionTimer = 0f;
                CurrentState = _npc.IsAgressive ? NpcAiStateV2.Chasing : NpcAiStateV2.Fleeing;

                if (_prey.CompareTag("Player") && _npc.IsAgressive)
                {
                    GameState.ChasersCount++;
                }
            }
        }

        private void ExecuteWandering(float deltaTime)
        {
            _wanderingTimer += deltaTime;
            if (_wanderingTimer >= _timeUntilChangeDirection)
            {
                _wanderingTimer = 0;
                _timeUntilChangeDirection = Random.Range(1f, _npc.MaxTimeUntilChangingDirection);
                PickRandomDirection();
            }
        }

        private void ExecuteChasingOrFleeing(float deltaTime)
        {
            if (_prey == null)
            {
                StopChasing();
                return;
            }

            Vector2 offset = _prey.transform.position - _transform.position;

            if (_npc.IsAgressive)
            {
                _moveDirection = offset.normalized;
            }
            else
            {
                _moveDirection = -offset.normalized; // Fuga
            }

            _chaseTimer += deltaTime;
            if (_chaseTimer >= _npc.MaxChaseTime)
            {
                StopChasing();
            }
        }

        private void ExecuteBoarding(float deltaTime)
        {
            Vector2 toShip = _boatTarget.position - _transform.position;
            _moveDirection = toShip.normalized;

            if (toShip.magnitude < 1.5f && _spriteRenderer != null)
            {
                Color c = _spriteRenderer.color;
                c.a = Mathf.MoveTowards(c.a, 0f, deltaTime * 2f);
                _spriteRenderer.color = c;

                if (c.a <= 0f)
                {
                    _npc.transform.SetParent(_boatTarget);
                    _npc.gameObject.SetActive(false);
                }
            }
        }

        public void NotifyBoundaryHit(Vector2 escapeDir)
        {
            StopChasing();
            _hasBorderCooldown = true;
            _borderCooldownTimer = BorderCooldownDuration;
            _moveDirection = escapeDir;
        }

        public void StartBoarding(Transform boatTransform)
        {
            _boatTarget = boatTransform;
            _isBoarding = true;
            _prey = null;
            _potentialTarget = null;
        }

        public void StopChasing()
        {
            if (_prey != null && _prey.CompareTag("Player"))
            {
                GameState.ChasersCount = Mathf.Max(0, GameState.ChasersCount - 1);
            }

            _prey = null;
            _potentialTarget = null;
            _chaseTimer = 0f;
            _aggressionTimer = 0f;
            CurrentState = NpcAiStateV2.Wandering;
            PickRandomDirection();
        }

        public void OnTargetDetected(GameObject potentialTarget)
        {
            if (_hasBorderCooldown || _isBoarding) return;
            if (_prey != null) return;

            _potentialTarget = potentialTarget;
            _aggressionTimer = 0f;
        }

        public void OnTargetLost()
        {
            if (_potentialTarget != null && _prey == null)
            {
                _potentialTarget = null;
                _aggressionTimer = 0f;
                CurrentState = NpcAiStateV2.Wandering;
            }
        }

        private void PickRandomDirection()
        {
            _moveDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
        }
    }
}
