using UnityEngine;

/// <summary>
/// Estado de movimentação do jogador ao navegar no barco pela água.
/// Gerencia a física de ondas, inércia marítima e colisão com terra firme.
/// </summary>
public class BoatState : PlayerMovement.IPlayerState
{
    private readonly PlayerMovement _player;

    public BoatState(PlayerMovement player)
    {
        _player = player;
    }

    public void Enter()
    {
        _player.rb.linearVelocity = Vector2.zero;
    }

    public void Update()
    {
        _player.UpdateAnimations();
    }

    public void FixedUpdate()
    {
        Vector3 currentPos = _player.transform.position;
        Tile tile = _player.worldGenerator.GetTileAtWorldPosition(currentPos);

        if (tile == null) return;

        Vector2 direction = _player.moveInput.sqrMagnitude > 1
            ? _player.moveInput.normalized
            : _player.moveInput;

        if (tile.Metadata.Layer == 0)
            _player.ApplyWaterMovement(direction);
        else
            _player.StopAndReset();
    }

    public void Exit() {}
}