using UnityEngine;

namespace Aegir.Entities.V2.Player
{
    /// <summary>
    /// Controlador moderno e otimizado de movimentação do jogador para o ecossistema V2.
    /// Herda de <see cref="PlayerMovement"/> para garantir 100% de compatibilidade com sistemas
    /// existentes (combate, UI, prefabs), substituindo a máquina legada por motores modulares
    /// de alta performance (<see cref="BoatMotorV2"/> e <see cref="CaptainMotorV2"/>).
    /// </summary>
    public class PlayerMovementV2 : PlayerMovement
    {
        public static readonly int HashHorizontal = Animator.StringToHash("Horizontal");
        public static readonly int HashVertical = Animator.StringToHash("Vertical");
        public static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

        public float WindDirX => _windDirX;
        public float WindDirY => _windDirY;

        private IPlayerMotorV2 _boatMotor;
        private IPlayerMotorV2 _captainMotor;
        private IPlayerMotorV2 _activeMotor;

        protected override void Awake()
        {
            base.Awake();

            _boatMotor = new BoatMotorV2(this);
            _captainMotor = new CaptainMotorV2(this);
        }

        protected override void Start()
        {
            _lastValidPosition = transform.position;

            if (isOnWater)
            {
                SwitchMotor(_boatMotor);
            }
            else
            {
                SwitchMotor(_captainMotor);
            }
        }

        protected override void Update()
        {
            if (GameState.IsInBattle || !GameState.IsGameStarted) return;

            if (inputActions != null)
            {
                moveInput = inputActions.Player.Move.ReadValue<Vector2>();

                if (inputActions.Player.EnterGetOut.WasPressedThisFrame())
                {
                    if (ActiveWorldGenerator != null)
                    {
                        ActiveWorldGenerator.TryGoOut(mainCamera);
                    }
                    else if (worldGenerator != null)
                    {
                        worldGenerator.TryGoOut(mainCamera);
                    }
                }
            }

            SimulateWind();

            // Sincronização do motor com GameState.IsOnWater
            if (GameState.IsOnWater && _activeMotor != _boatMotor)
            {
                isOnWater = true;
                SwitchMotor(_boatMotor);
            }
            else if (!GameState.IsOnWater && _activeMotor != _captainMotor)
            {
                isOnWater = false;
                SwitchMotor(_captainMotor);
            }

            _activeMotor?.UpdateMotor(moveInput);
        }

        protected override void FixedUpdate()
        {
            if (GameState.IsInBattle || !GameState.IsGameStarted)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                if (crb != null) crb.linearVelocity = Vector2.zero;
                return;
            }

            _activeMotor?.FixedUpdateMotor(moveInput);
        }

        private void SwitchMotor(IPlayerMotorV2 newMotor)
        {
            if (_activeMotor == newMotor) return;

            _activeMotor?.Exit();
            _activeMotor = newMotor;
            _activeMotor?.Enter();
        }

        public void UpdateBoatAnimations(Vector2 velocity, float sqrSpeed)
        {
            if (_animator == null) return;

            if (velocity.sqrMagnitude >= 0.01f)
            {
                _animator.SetFloat(HashHorizontal, velocity.x);
                _animator.SetFloat(HashVertical, velocity.y);
            }
            _animator.SetFloat(HashMoveSpeed, sqrSpeed);
        }

        public void UpdateCaptainAnimations(Vector2 input, float sqrSpeed)
        {
            if (_cAnimator == null) return;

            if (input.sqrMagnitude >= 0.01f)
            {
                _cAnimator.SetFloat(HashHorizontal, input.x);
                _cAnimator.SetFloat(HashVertical, input.y);
            }
            _cAnimator.SetFloat(HashMoveSpeed, sqrSpeed);
        }
    }
}
