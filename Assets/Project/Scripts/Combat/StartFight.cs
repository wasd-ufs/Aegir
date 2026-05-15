using UnityEngine;

/// <summary>
/// Gatilho posicionado em criaturas/inimigos no mapa do mundo.
/// Detecta colisões com o jogador e inicia o processo de transição para a tela de batalha.
/// </summary>
public class StartFight : MonoBehaviour
{
    #region Configurações de Encontro
    [Header("Visual da Batalha")]
    public Sprite background;
    public Sprite creature;
    
    [Header("Dados do Inimigo")]
    public CrewData enemyCrew;
    #endregion

    #region Estado e Referências
    private bool _isStartingFight;
    private GameBoyTransition _transition;
    #endregion

    #region Ciclo de Vida (Unity)
    private void Awake()
    {
        _transition = FindFirstObjectByType<GameBoyTransition>();

        if (_transition == null)
            Debug.LogWarning("[StartFight] GameBoyTransition não encontrado na cena!", this);
    }

    /// <summary>
    /// Detecta a colisão do inimigo com o jogador. Garante que o jogador está no barco
    /// e que a transição ocorra apenas uma vez.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();
        if (playerMovement == null || playerMovement.isOnWater == false) return;

        if (_isStartingFight) return;

        BattleData battleData = FindFirstObjectByType<BattleData>();
        if (battleData == null)
        {
            Debug.LogWarning("[StartFight] BattleData não encontrado na cena!", this);
            return;
        }

        CrewData crew = SolveCrew();

        _isStartingFight = true;

        // Inicia o efeito do GameBoy e só dispara o StartFight quando a tela estiver totalmente coberta
        _transition.StartTransition(
            onMidpointCallback: () => battleData.StartFight(background, creature, crew)
        );
    }
    #endregion

    #region Resolução de Dados
    /// <summary>
    /// Se o inimigo já tem um CrewData, usa ele.
    /// Caso contrário, cria um CrewData temporário em runtime com a criatura sozinha.
    /// </summary>
    private CrewData SolveCrew()
    {
        if (enemyCrew != null) return enemyCrew;

        // Cria um GameObject temporário para hospedar o CrewData
        GameObject tempObject = new GameObject($"[TempCrew] {gameObject.name}");
        CrewData tempCrew = tempObject.AddComponent<CrewData>();

        // Inicializa antes do Awake rodar para evitar duplicatas na lista
        tempCrew.InicializarManualmente(gameObject);

        return tempCrew;
    }
    #endregion
}