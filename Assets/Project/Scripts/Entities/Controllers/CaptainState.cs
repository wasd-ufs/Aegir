using UnityEngine;

public class CaptainState : PlayerMovement.IPlayerState
{
    private PlayerMovement player;

    public CaptainState(PlayerMovement player)
    {
        this.player = player;
    }

    public void Enter()
    {
        GameState.IsOnWater = false;

        player.crb.linearVelocity = Vector2.zero;
    }

    public void Update()
    {
        player.AtualizarAnimacoes();
    }

    public void FixedUpdate()
    {
        Vector3 currentPos = player.capitão.transform.position;
        Tile tile = player.worldGenerator.GetTileAtWorldPosition(currentPos);

        if (tile == null) return;

        Vector2 direction = player.moveInput.sqrMagnitude > 1
            ? player.moveInput.normalized
            : player.moveInput;

        if (tile.Metadata.Layer != 0)
        {
            player.crb.linearVelocity = direction * player.captainSpeed;
            player.rb.linearVelocity = Vector2.zero;
        }
        else
        {
            player.crb.linearVelocity *= -1;
        }
    }

    public void Exit() {}
}
