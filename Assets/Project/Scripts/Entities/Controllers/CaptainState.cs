using UnityEngine;

/// <summary>
/// Estado de movimentação do capitão ao explorar ilhas a pé em terra firme.
/// Controla velocidade terrestre e impede que o capitão caminhe na água sem o barco.
/// </summary>
public class CaptainState : PlayerMovement.IPlayerState
{
    private readonly PlayerMovement _player;

    public CaptainState(PlayerMovement player)
    {
        _player = player;
    }

    public void Enter()
    {
        _player.crb.linearVelocity = Vector2.zero;
    }

    public void Update()
    {
        _player.UpdateAnimations();
    }

    public void FixedUpdate()
    {
        Vector3 currentPos = _player.captain.transform.position;
        Tile tile = _player.worldGenerator.GetTileAtWorldPosition(currentPos);

        if (tile == null) return;

        Vector2 direction = _player.moveInput.sqrMagnitude > 1
            ? _player.moveInput.normalized
            : _player.moveInput;

        if (tile.Metadata.Layer != 0)
        {
            _player.crb.linearVelocity = direction * _player.CaptainSpeed;
            _player.rb.linearVelocity = Vector2.zero;
        }
        else
        {
            _player.crb.linearVelocity *= -1;
        }
    }

    public void Exit() {}
}
