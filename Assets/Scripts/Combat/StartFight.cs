using UnityEngine;

public class StartFight : MonoBehaviour
{
    public Sprite background;
    public Sprite creature;
    private bool startingFight;
    public CrewData enemyCrew;

    private GameBoyTransition transition;

    void Awake()
    {
        transition = FindFirstObjectByType<GameBoyTransition>();
        if (transition == null)
            Debug.LogWarning("[StartFight] GameBoyTransition não encontrado na cena!", this);
    }

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
        transition.StartTransition(
            onMidpointCallback: () => battleData.StartFight(background, creature, crew)
        );
    }

    /// <summary>
    /// Se o inimigo já tem um CrewData, usa ele.
    /// Caso contrário, cria um CrewData temporário em runtime com a criatura sozinha.
    /// </summary>
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
}