using UnityEngine;
public class BoatState : PlayerMovement.IPlayerState
{
    private PlayerMovement player;

    public BoatState(PlayerMovement player)
    {
        this.player = player;
    }

    public void Enter()
    {
        GameState.IsOnWater = true;

        player.rb.linearVelocity = Vector2.zero;
    }

    public void Update()
    {
        player.AtualizarAnimacoes();
    }

    public void FixedUpdate()
    {
        Vector3 currentPos = player.transform.position;
        Tile tile = player.worldGenerator.GetTileAtWorldPosition(currentPos);

        if (tile == null) return;

        Vector2 direction = player.moveInput.sqrMagnitude > 1
            ? player.moveInput.normalized
            : player.moveInput;

        if (tile.Metadata.Layer == 0)
            player.ApplyWaterMovement(direction);
        else
            player.StopAndReset();
    }

    public void Exit() {}
}