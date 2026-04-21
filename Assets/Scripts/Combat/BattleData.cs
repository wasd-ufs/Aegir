using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BattleData : MonoBehaviour
{
    public Image background, enemy, player;
    public Sprite pl;
    public GameObject battle, gameOverPanel;
    public BattleManager battleManager;
    public CrewUI playerCrewUI;
    public CrewUI enemyCrewUI;
    private GameBoyTransition transition;

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
                GameState.IsInBattle = !playerVenceu;
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

    public void RetornarAoMundo()
    {
        transition.StartTransition(onMidpointCallback: () =>
        {
            gameOverPanel.SetActive(false);
            GameState.IsInBattle = false;

            playerCrewUI?.ReativarComoPlayer();
        });
    }

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
}