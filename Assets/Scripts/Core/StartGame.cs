using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o comportamento inicial do jogo a partir do Menu Principal,
/// atrelando botões às lógicas de transição e ativação de gameplay.
/// </summary>
public class StartGame : MonoBehaviour
{
    #region Referências Visuais e de Transição
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _optionsButton;

    [SerializeField] private GameObject _startScreen;
    [SerializeField] private GameBoyTransition _transition;
    #endregion

    #region Inicialização
    void Awake()
    {
        _startButton.onClick.AddListener(() => StartGameAction());
    }
    #endregion

    #region Ações e Eventos
    /// <summary>
    /// Inicia o jogo através de uma transição visual. 
    /// No "ponto médio" da animação (tela totalmente coberta), desativa a tela de título
    /// e libera os sistemas de jogo alterando o GameState.
    /// </summary>
    public void StartGameAction()
    {
        if (_transition != null)
        {
            _transition.StartTransition(onMidpointCallback: () => 
            {
                _startScreen.SetActive(false);
                GameState.IsGameStarted = true;
            });
        }
    }
    #endregion
}