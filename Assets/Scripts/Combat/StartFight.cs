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
    private bool startingFight;
    private GameBoyTransition transition;
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        transition = FindFirstObjectByType<GameBoyTransition>();
        if (transition == null)
            Debug.LogWarning("[StartFight] GameBoyTransition não encontrado na cena!", this);
    }

    /// <summary>
    /// Detecta a colisão do inimigo com o jogador. Garante que o jogador está no barco
    /// e que a transição ocorra apenas uma vez.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponent<PlayerMovement>().isOnWater == false) return;
        if (startingFight) return;

        BattleData battleData = FindFirstObjectByType<BattleData>();
        if (battleData == null)
        {
            Debug.LogWarning("[StartFight] BattleData não encontrado na cena!", this);
            return;
        }

        CrewData crew = ResolverCrew();
        
        startingFight = true;
        // Inicia o efeito do GameBoy e só dispara o StartFight quando a tela estiver totalmente coberta
        transition.StartTransition(
            onMidpointCallback: () => battleData.StartFight(background, creature, crew)
        );
    }
    #endregion

    #region Resolução de Dados
    /// <summary>
    /// Se o inimigo já tem um CrewData, usa ele.
    /// Caso contrário, cria um CrewData temporário em runtime com a criatura sozinha.
    /// </summary>
    /// <returns>O CrewData a ser utilizado no combate.</returns>
    private CrewData ResolverCrew()
    {
        if (enemyCrew != null) return enemyCrew;

        // Cria um GameObject temporário para hospedar o CrewData
        GameObject tempObj = new GameObject($"[TempCrew] {gameObject.name}");
        CrewData tempCrew  = tempObj.AddComponent<CrewData>();

        // Inicializa antes do Awake rodar para evitar duplicatas na lista
        tempCrew.InicializarManualmente(gameObject);

        return tempCrew;
    }
    #endregion
}