using UnityEngine;

namespace Aegir.Entities.V2.Player
{
    /// <summary>
    /// Contrato de motor de locomoção para os diferentes estados do jogador (Barco e Capitão).
    /// </summary>
    public interface IPlayerMotorV2
    {
        void Enter();
        void UpdateMotor(Vector2 input);
        void FixedUpdateMotor(Vector2 input);
        void Exit();
    }
}
