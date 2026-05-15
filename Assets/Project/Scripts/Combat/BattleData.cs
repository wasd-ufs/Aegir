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
    [SerializeField] private Image _background;
    [SerializeField] private Image _enemy;
    [SerializeField] private Image _player;
    [SerializeField] private Sprite _playerSprite;
    [SerializeField] private GameObject _battle;
    [SerializeField] private GameObject _gameOverPanel;

    [Header("Gerenciadores Relacionados")]
    [SerializeField] private BattleManager _battleManager;
    [SerializeField] private CrewUI _playerCrewUI;
    [SerializeField] private CrewUI _enemyCrewUI;

    #endregion

    #region Estado Interno

    private GameBoyTransition _transition;

    #endregion

    #region Ciclo de Vida (Unity)

    private void Awake()
    {
        _transition = FindFirstObjectByType<GameBoyTransition>();

        if (_transition == null)
            Debug.LogWarning("[BattleData] GameBoyTransition não encontrado na cena!", this);
    }

    private void Start()
    {
        _battle.SetActive(false);
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
        _background.sprite = bg;
        _enemy.sprite = en;
        _player.sprite = _playerSprite;

        _battle.SetActive(true);
        GameState.IsInBattle = true;

        _playerCrewUI?.ReativarComoPlayer();
        _battleManager.IniciateBattle(enemyCrew);
    }

    /// <summary>
    /// Finaliza o embate, gerenciando a transição de volta ao mapa, o som,
    /// a cura pós-combate e a penalidade/vitória.
    /// </summary>
    /// <param name="playerWon">True se o inimigo foi derrotado, False se a equipe foi aniquilada.</param>
    /// <param name="playerCrew">Referência da tripulação do jogador.</param>
    /// <param name="enemyCrew">Referência da tripulação do inimigo.</param>
    /// <param name="textoLog">Texto contendo o loot gerado em caso de vitória.</param>
    public void EndFight(bool playerWon, CrewData playerCrew, CrewData enemyCrew, string textoLog = "")
    {
        GameState.ChasersCount = 0;
        GameState.IsInBattle = false;

        if (_transition != null)
        {
            _transition.StartTransition(
                onMidpointCallback: () =>
                {
                    if (_enemyCrewUI != null)
                        _enemyCrewUI.gameObject.SetActive(false);

                    _enemyCrewUI?.LimparUI();
                    _battle.SetActive(false);
                    _battleManager.ClearActionButtons();

                    GameState.IsInBattle = !playerWon;
                    _gameOverPanel.SetActive(!playerWon);
                },
                onCompleteCallback: () =>
                {
                    MusicManager.Instance.ResumeMusic();

                    if (playerWon)
                    {
                        SFXManager.Instance?.PlayVictory();
                        _battleManager.DisplayMessage("Vitoria!!");
                        _battleManager.DisplayLog(textoLog);

                        if (enemyCrew != null)
                            StartCoroutine(FadeAndDestroyCrew(enemyCrew));
                    }
                    else
                    {
                        SFXManager.Instance?.PlayDefeat();

                        foreach (GameObject npc in playerCrew.crew)
                        {
                            NPCsData data = npc.GetComponent<NPCsData>();
                            data.isAlive = true;
                            data.Heal(data.vidaMáxima / 2);
                            data.gameObject.SetActive(data.creatureClass != NPCsData.Class.Capitão);
                        }

                        foreach (GameObject npc in enemyCrew.crew)
                        {
                            NPCsData data = npc.GetComponent<NPCsData>();
                            data.isAlive = true;
                            data.Heal(data.vidaMáxima);
                            data.gameObject.SetActive(true);
                        }

                        _enemyCrewUI?.LimparUI();

                        if (_enemyCrewUI != null)
                            _enemyCrewUI.gameObject.SetActive(false);
                    }
                });
        }
        else
        {
            _battle.SetActive(false);
            GameState.IsInBattle = false;
        }
    }

    /// <summary>
    /// Utilizado pelo botão do painel de Game Over para retornar a exploração normal.
    /// </summary>
    public void RetornarAoMundo()
    {
        _transition.StartTransition(onMidpointCallback: () =>
        {
            _gameOverPanel.SetActive(false);
            GameState.IsInBattle = false;

            _playerCrewUI?.ReativarComoPlayer();
        });
    }

    #endregion

    #region Helpers e Animações

    /// <summary>
    /// Executa um fade-out no inimigo derrotado antes de destruí-lo de forma permanente no mapa.
    /// </summary>
    private IEnumerator FadeAndDestroyCrew(CrewData crew)
    {
        List<SpriteRenderer> renderers = new();

        foreach (GameObject npc in crew.crew)
        {
            SpriteRenderer sr = npc.GetComponent<SpriteRenderer>();
            if (sr != null) renderers.Add(sr);
        }

        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

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