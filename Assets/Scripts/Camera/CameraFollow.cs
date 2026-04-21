
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public WorldGenerator worldGenerator; // Referência para pegar o 'player' atual
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float multiplicador;
    private Transform alvoAtual;
    private Rigidbody2D rbAtual;
    private Vector2 velocidadeSuavizada;
    public float amortecimentoDaMecânica = 0.05f;

    
    void FixedUpdate()
    {
        if (worldGenerator.player != null)
        {
            Transform playerT = worldGenerator.player;
            if (playerT != alvoAtual)
            {
                alvoAtual = playerT;
                rbAtual = playerT.GetComponent<Rigidbody2D>();
            }

            velocidadeSuavizada = Vector2.Lerp(velocidadeSuavizada, rbAtual.linearVelocity, amortecimentoDaMecânica);

            Vector3 desiredPosition = playerT.position + offset + new Vector3(velocidadeSuavizada.x, velocidadeSuavizada.y, 0) * multiplicador;
            // Interpolação linear para movimento suave
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;
        }
    }
}