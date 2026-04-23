using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gerencia os elementos visuais de fundo e as transições de estado para a entrada
/// e saída do modo de Batalha (tela dedicada a combate).
/// </summary>
public class BattleData : MonoBehaviour
{
    #region Referências Visuais e de UI
    [Header("Imagens e Painéis")]
    public Image background, enemy, player;
    public Sprite pl;
    public GameObject battle, gameOverPanel;
    
    [Header("Gerenciadores Relacionados")]
    public BattleManager battleManager;
    public CrewUI playerCrewUI;
    public CrewUI enemyCrewUI;
    #endregion

    #region Estado Interno
    private GameBoyTransition transition;
    #endregion

    #region Ciclo de Vida (Unity)
    void Awake()
    {
        transition = FindFirstObjectByType<GameBoyTransition>();
        if (transition == null)
            Debug.LogWarning("[BattleData] GameBoyTransition não encontrado na cena!", this);
    }

    void Start()
    {
        battle.SetActive(false);
        GameState.IsInBattle = false;
    }
    #endregion

    #region Controle de Fluxo da Batalha
    /// <summary>
    /// Configura a UI de combate e diz ao BattleManager para iniciar os turnos.
    /// </summary>
    /// <param name="bg">Sprite do background do cenário atual.</param>
    /// <param name="en">Sprite principal representando o inimigo.</param>
    /// <param name="enemyCrew">Estrutura de dados da equipe adversária.</param>
    public void StartFight(Sprite bg, Sprite en, CrewData enemyCrew)
    {
        background.sprite = bg;
        enemy.sprite = en;
        player.sprite = pl;

        battle.SetActive(true);
        GameState.IsInBattle = true;

        playerCrewUI?.ReativarComoPlayer();
        battleManager.IniciarBatalha(enemyCrew);
    }

    /// <summary>
    /// Finaliza o embate, gerenciando a transição de volta ao mapa, o som,
    /// a cura pós-combate e a penalidade/vitória.
    /// </summary>
    /// <param name="playerVenceu">True se o inimigo foi derrotado, False se a equipe foi aniquilada.</param>
    /// <param name="playerCrew">Referência da tripulação do jogador.</param>
    /// <param name="enemyCrew">Referência da tripulação do inimigo.</param>
    /// <param name="textoLog">Texto contendo o loot gerado em caso de vitória.</param>
    public void EndFight(bool playerVenceu, CrewData playerCrew, CrewData enemyCrew, string textoLog = "")
    {
        GameState.ChasersCount = 0;
        GameState.IsInBattle = false;
        if (transition != null)
        {
            transition.StartTransition(onMidpointCallback: () =>
            {
                if (enemyCrewUI != null)
                    enemyCrewUI.gameObject.SetActive(false);
                enemyCrewUI?.LimparUI();
                battle.SetActive(false);
                battleManager.LimparBotões();
                
                GameState.IsInBattle = !playerVenceu; // Mantém travado se for Game Over
                gameOverPanel.SetActive(!playerVenceu); 
                
            }, onCompleteCallback: () =>
            {
                MusicManager.Instance.RetomarMusica();
                if (playerVenceu)
                {
                    SFXManager.Instance?.TocarVitoria();
                    battleManager.ExibirMensagem("Vitoria!!");
                    battleManager.ExibirLog(textoLog);

                    if (enemyCrew != null)
                        StartCoroutine(FadeEDestruirCrew(enemyCrew)); 
                }
                else
                {
                    SFXManager.Instance?.TocarDerrota();
                    // Revive tripulantes e inimigos
                    foreach (GameObject npc in playerCrew.crew)
                    {
                        NPCsData data = npc.GetComponent<NPCsData>();
                        data.isAlive = true;
                        data.Heal(data.vidaMáxima/2);
                        data.gameObject.SetActive(data.creatureClass != NPCsData.Class.Capitão);
                    }

                    foreach (GameObject npc in enemyCrew.crew)
                    {
                        NPCsData data = npc.GetComponent<NPCsData>();
                        data.isAlive = true;
                        data.Heal(data.vidaMáxima);
                        data.gameObject.SetActive(true);
                    }
                    enemyCrewUI?.LimparUI();
                    if (enemyCrewUI != null)
                        enemyCrewUI.gameObject.SetActive(false);
                }
            });
        }
        else
        {
            battle.SetActive(false);
            GameState.IsInBattle = false;
        }
    }

    /// <summary>
    /// Utilizado pelo botão do painel de Game Over para retornar a exploração normal.
    /// </summary>
    public void RetornarAoMundo()
    {
        transition.StartTransition(onMidpointCallback: () =>
        {
            gameOverPanel.SetActive(false);
            GameState.IsInBattle = false;

            playerCrewUI?.ReativarComoPlayer();
        });
    }
    #endregion

    #region Helpers e Animações
    /// <summary>
    /// Executa um fade-out no inimigo derrotado antes de destruí-lo de forma permanente no mapa.
    /// </summary>
    private IEnumerator FadeEDestruirCrew(CrewData crew)
    {
        List<SpriteRenderer> renderers = new();
        foreach (GameObject npc in crew.crew)
        {
            SpriteRenderer sr = npc.GetComponent<SpriteRenderer>();
            if (sr != null) renderers.Add(sr);
        }

        float elapsed = 0f;
        float duracao = 1f;

        while (elapsed < duracao)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duracao);
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
            yield return null;
        }

        Destroy(crew.gameObject);
    }
    #endregion
}