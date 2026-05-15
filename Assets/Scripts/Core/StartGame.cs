using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o comportamento inicial do jogo a partir do Menu Principal,
/// atrelando botões às lógicas de transição e ativação de gameplay.
/// </summary>
public class StartGame : MonoBehaviour
{
    #region Referências Visuais e de Transição
    public Button StartBtn, OptionsBtn;
    public GameObject startScreen;
    public GameBoyTransition transition;
    #endregion

    #region Inicialização
    void Awake()
    {
        StartBtn.onClick.AddListener(() => StartG());
    }
    #endregion

    #region Ações e Eventos
    /// <summary>
    /// Inicia o jogo através de uma transição visual. 
    /// No "ponto médio" da animação (tela totalmente coberta), desativa a tela de título
    /// e libera os sistemas de jogo alterando o GameState.
    /// </summary>
    public void StartG()
    {
        if (transition != null)
        {
            transition.StartTransition(onMidpointCallback: () => 
            {
                startScreen.SetActive(false);
                GameState.isGameStarted = true;
            });
        }
    }
    #endregion
}